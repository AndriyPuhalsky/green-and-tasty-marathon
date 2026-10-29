using RestaurantAqa.Api;
using RestaurantAqa.Screenplay.Abilities;
using RestaurantAqa.Screenplay.Core;
using RestaurantAqa.Screenplay.Tasks;

namespace RestaurantAqa.Support;

/// <summary>
/// Стан одного сценарію: актори, журнал запитів і список прибирання.
/// Reqnroll створює новий екземпляр на кожен сценарій і передає його в кроки через конструктор.
/// </summary>
public sealed class GroupOrderWorld
{
    public const string OwnerRole = "the owner";
    public const string OtherOwnerRole = "the other owner";

    private static readonly string[] OwnerFirstNames = ["Olivia", "Oscar", "Sofia", "Mark", "Irene", "Taras", "Marta", "Victor"];

    public GroupOrderWorld()
    {
        ArtifactsDirectory = Path.Combine(TestSettings.Current.ResultsDirectory, "artifacts", Guid.NewGuid().ToString("N")[..10]);
    }

    public Cast Cast { get; } = new();

    public ApiJournal Journal { get; } = new();

    public TrackTestData Tracker { get; } = new();

    public string ArtifactsDirectory { get; set; }

    /// <summary>Сценарій з тегом @waiter бронює столик в окремій локації зі своїм офіціантом.</summary>
    public bool AtWaiterDesk { get; set; }

    public string BookingLocationId => AtWaiterDesk ? Stand.Current.WaiterDesk.Id : Stand.Current.Location.Id;

    /// <summary>Відповідь на останню дію з кроку When, її перевіряють кроки Then.</summary>
    public ApiResponse? LastResponse { get; set; }

    public Actor Owner => Cast.TryGet(OwnerRole, out var owner)
        ? owner
        : throw new InvalidOperationException("The owner has not booked a table in this scenario yet.");

    public string GroupId => Owner.Recall<string>(Remembered.GroupId);

    public ReservationResponse Reservation => Owner.Recall<ReservationResponse>(Remembered.Reservation);

    public async Task<Actor> NewCustomer(string role)
    {
        string firstName = OwnerFirstNames[Random.Shared.Next(OwnerFirstNames.Length)];
        var actor = Cast.ActorNamed(role, a => a.WhoCan(CallTheApi.OfTheStand(Journal).As(role), Tracker, Browser(role)));
        if (!actor.Has<HaveAnAccount>())
        {
            await actor.AttemptsTo(RegisterAccount.WithName(firstName, UniqueData.LastName(), "owner"), SignIn.ThroughTheApi());
        }
        return actor;
    }

    public Actor Guest(string name)
        => Cast.ActorNamed($"guest {name}", a => a.WhoCan(
            CallTheApi.OfTheStand(Journal).As($"guest {name}"),
            new JoinAsGuest(name),
            Tracker));

    /// <summary>Офіціант прогону зі своїм браузером у межах сценарію.</summary>
    public Actor Waiter() => FromSharedAccount("the waiter", AtWaiterDesk ? Stand.Current.DeskWaiter : Stand.Current.Waiter);

    public async Task<Actor> AnotherCustomer() => FromSharedAccount("another customer", await Stand.Current.Stranger());

    public Actor Anonymous()
        => Cast.ActorNamed("an anonymous visitor", a => a.WhoCan(CallTheApi.OfTheStand(Journal).As("an anonymous visitor")));

    /// <summary>Актор за фразою з Gherkin: "the owner", "the waiter", "guest Anna", "another customer"...</summary>
    public async Task<Actor> ActorCalled(string phrase)
    {
        phrase = phrase.Trim();
        if (phrase.StartsWith("guest ", StringComparison.OrdinalIgnoreCase))
        {
            return Guest(phrase["guest ".Length..].Trim('"', ' '));
        }

        return phrase.ToLowerInvariant() switch
        {
            OwnerRole => Owner,
            OtherOwnerRole => Cast.TryGet(OtherOwnerRole, out var other) ? other : throw new InvalidOperationException("There is no other owner in this scenario."),
            "the waiter" => Waiter(),
            "another customer" => await AnotherCustomer(),
            "an anonymous visitor" => Anonymous(),
            _ => throw new ArgumentException($"Unknown actor '{phrase}'."),
        };
    }

    private Actor FromSharedAccount(string role, Actor shared)
        => Cast.ActorNamed(role, a =>
        {
            var api = CallTheApi.OfTheStand(Journal).As(role);
            api.UseToken(shared.AbilityTo<CallTheApi>().BearerToken);
            return a.WhoCan(api, shared.AbilityTo<HaveAnAccount>(), Tracker, Browser(role));
        });

    private BrowseTheWeb Browser(string role) => BrowseTheWeb.For(role, ArtifactsDirectory);
}
