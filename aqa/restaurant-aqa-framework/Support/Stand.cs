using RestaurantAqa.Api;
using RestaurantAqa.Screenplay.Abilities;
using RestaurantAqa.Screenplay.Core;
using RestaurantAqa.Screenplay.Tasks;

namespace RestaurantAqa.Support;

/// <summary>
/// Дані, спільні для всього прогону: локація, меню й офіціант. Створюються один раз перед тестами
/// і видаляються після них. Усе, що змінює стан (бронювання, групи, гості), кожен сценарій робить сам.
/// </summary>
public sealed class Stand
{
    private static Stand? _current;
    private static readonly SemaphoreSlim StrangerLock = new(1, 1);
    private Actor? _stranger;

    private Stand(LocationResponse location, LocationResponse waiterDesk, IReadOnlyDictionary<string, DishResponse> dishes, Actor waiter, Actor deskWaiter)
    {
        Location = location;
        WaiterDesk = waiterDesk;
        Dishes = dishes;
        Waiter = waiter;
        DeskWaiter = deskWaiter;
    }

    public static Stand Current => _current ?? throw new InvalidOperationException("The stand is not prepared. BeforeTestRun hook did not run.");

    public LocationResponse Location { get; }

    public IReadOnlyDictionary<string, DishResponse> Dishes { get; }

    /// <summary>Офіціант локації. Бекенд призначає його на кожне бронювання, бо інших офіціантів у локації немає.</summary>
    public Actor Waiter { get; }

    /// <summary>
    /// Окрема локація для сценаріїв, де офіціант працює в UI (тег @waiter). Дашборд офіціанта
    /// робить по три запити на кожне його бронювання, тож у нього має бути лише кілька бронювань.
    /// </summary>
    public LocationResponse WaiterDesk { get; }

    public Actor DeskWaiter { get; }

    public DishResponse Dish(string name)
        => Dishes.TryGetValue(name, out var dish)
            ? dish
            : throw new ArgumentException($"Dish '{name}' is not on the test menu. Known dishes: {string.Join(", ", Dishes.Keys)}");

    public static async Task Prepare()
    {
        var api = CallTheApi.OfTheStand().As("stand");
        await WarmUp();

        var location = await CreateLocation(api, $"AQA {UniqueData.RunId}, 1 Test Street");
        var waiterDesk = await CreateLocation(api, $"AQA {UniqueData.RunId}, 2 Waiter Street");

        var dishes = new Dictionary<string, DishResponse>();
        foreach (var dish in Menu.Dishes)
        {
            var created = (await api.Post("/dishes", new
            {
                name = dish.Name,
                description = "Test dish",
                dishType = dish.DishType,
                price = dish.Price,
                weight = "300 g",
                calories = "350 kcal",
                proteins = "10 g",
                fats = "10 g",
                carbohydrates = "40 g",
                vitamins = "A, C",
                imageBase64 = (string?)null,
                state = "Available",
                locationIds = new[] { location.Id, waiterDesk.Id },
            })).EnsureSuccess().As<DishResponse>();
            dishes[dish.Name] = created;
        }

        var waiter = await CreateWaiter("Walter", location.Id);
        var deskWaiter = await CreateWaiter("Wanda", waiterDesk.Id);

        _current = new Stand(location, waiterDesk, dishes, waiter, deskWaiter);
    }

    /// <summary>
    /// Lambda на стенді має 128 MB пам'яті, і холодний старт .NET під навантаженням буває довшим
    /// за 29 с ліміту API Gateway (тоді 504). Тому перед тестами піднімаємо кілька екземплярів
    /// паралельними легкими запитами: далі сценарії працюють уже з теплими.
    /// </summary>
    private static async Task WarmUp()
    {
        int instances = Math.Min(TestSettings.Current.Parallelism * 3, 9);
        for (int round = 0; round < 2; round++)
        {
            await Task.WhenAll(Enumerable.Range(0, instances)
                .Select(_ => CallTheApi.OfTheStand().As("warm-up").Get("/locations")));
        }
    }

    private static async Task<LocationResponse> CreateLocation(CallTheApi api, string address)
        => (await api.Post("/locations", new
        {
            address,
            description = "Location created by the automated tests, removed after the run",
            totalCapacity = 40,
            averageOccupancy = "0%",
            imageBase64 = (string?)null,
            rating = 5.0,
        })).EnsureSuccess().As<LocationResponse>();

    private static async Task<Actor> CreateWaiter(string firstName, string locationId)
    {
        var waiter = new Actor("the waiter").WhoCan(CallTheApi.OfTheStand().As("the waiter"));
        // роль Waiter потрапляє в токен лише після входу, тому спершу призначення, потім sign in
        await waiter.AttemptsTo(
            RegisterAccount.WithName(firstName, UniqueData.LastName(), "waiter"),
            AssignAsWaiter.To(locationId),
            SignIn.ThroughTheApi());
        return waiter;
    }

    /// <summary>Клієнт, який не має стосунку до бронювань сценаріїв. Лише читає, тому один на прогін.</summary>
    public async Task<Actor> Stranger()
    {
        if (_stranger is not null)
        {
            return _stranger;
        }

        await StrangerLock.WaitAsync();
        try
        {
            if (_stranger is null)
            {
                var stranger = new Actor("another customer").WhoCan(CallTheApi.OfTheStand().As("another customer"));
                await stranger.AttemptsTo(
                    RegisterAccount.WithName("Stella", UniqueData.LastName(), "stranger"),
                    SignIn.ThroughTheApi());
                _stranger = stranger;
            }
            return _stranger;
        }
        finally
        {
            StrangerLock.Release();
        }
    }

    public static async Task TearDown()
    {
        if (_current is null)
        {
            return;
        }

        var waiterApi = _current.Waiter.AbilityTo<CallTheApi>();
        foreach (var dish in _current.Dishes.Values)
        {
            await Try(() => waiterApi.Delete($"/dishes/{dish.Id}?force=true"));
        }
        // force=true видаляє і столики локації, і запис офіціанта
        await Try(() => waiterApi.Delete($"/locations/{_current.Location.Id}?force=true"));
        await Try(() => waiterApi.Delete($"/locations/{_current.WaiterDesk.Id}?force=true"));
        _current = null;
    }

    private static async Task Try(Func<Task<ApiResponse>> call)
    {
        try
        {
            var response = await call();
            if (!response.IsSuccess)
            {
                Console.WriteLine($"[cleanup] {response}");
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[cleanup] {exception.Message}");
        }
    }
}
