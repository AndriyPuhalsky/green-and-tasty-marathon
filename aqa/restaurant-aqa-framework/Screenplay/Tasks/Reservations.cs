using RestaurantAqa.Api;
using RestaurantAqa.Screenplay.Abilities;
using RestaurantAqa.Screenplay.Core;
using RestaurantAqa.Support;

namespace RestaurantAqa.Screenplay.Tasks;

public static class Remembered
{
    public const string Reservation = "reservation";
    public const string Table = "table";
    public const string InviteToken = "invite token";
    public const string GroupId = "group reservation id";
    public const string OtherReservation = "other reservation";
}

/// <summary>
/// Owner бронює окремий столик у локації прогону. Свій столик на кожен сценарій означає, що
/// паралельні сценарії не перетинаються в часі. Час завжди від поточного моменту: now + N днів.
/// Після сценарію бронювання скасовується.
/// </summary>
public sealed class BookTable(int guests) : ITask
{
    private int _daysAhead = 3;
    private string _rememberAs = Remembered.Reservation;
    private string? _locationId;

    public static BookTable ForGuests(int guests) => new(guests);

    public BookTable At(string locationId)
    {
        _locationId = locationId;
        return this;
    }

    /// <summary>Друге бронювання того самого Owner зберігається під іншим ключем.</summary>
    public BookTable RememberedAs(string key)
    {
        _rememberAs = key;
        return this;
    }

    public BookTable DaysAhead(int days)
    {
        _daysAhead = days;
        return this;
    }

    public async Task PerformAs(Actor actor)
    {
        var stand = Stand.Current;
        var tracker = actor.AbilityTo<TrackTestData>();

        var table = (await CallTheApi.OfTheStand().As("stand").Post("/tables", new
        {
            locationId = _locationId ?? stand.Location.Id,
            capacity = Math.Max(guests, 2),
            tableNumber = UniqueData.NextTableNumber(),
        })).EnsureSuccess().As<TableResponse>();
        // Столики не видаляємо після сценарію: GET /reservations офіціанта падає з 500, якщо стіл
        // хоча б одного його бронювання вже видалено (див. README). Їх прибирає видалення локації
        // з force=true наприкінці прогону.

        DateTime start = RoundToQuarter(DateTime.UtcNow.AddDays(_daysAhead));
        var api = actor.AbilityTo<CallTheApi>();
        var reservation = (await api.Post("/reservations",
                new CreateReservationRequest(table.Id, start, start.AddMinutes(90), guests)))
            .EnsureSuccess().As<ReservationResponse>();
        tracker.Created($"reservation {reservation.Id}", async () => await api.Delete("/reservations", new { id = reservation.Id }));

        actor.Remember(_rememberAs == Remembered.Reservation ? Remembered.Table : _rememberAs + " table", table);
        actor.Remember(_rememberAs, reservation);
    }

    private static DateTime RoundToQuarter(DateTime value)
    {
        var rounded = new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, DateTimeKind.Utc);
        return rounded.AddMinutes(value.Minute / 15 * 15 + 15);
    }
}

/// <summary>Owner бере посилання-запрошення свого бронювання (як кнопка Invite у застосунку).</summary>
public sealed class ShareGroupInvite : ITask
{
    public static ShareGroupInvite ForTheirReservation() => new();

    public async Task PerformAs(Actor actor)
    {
        var reservation = actor.Recall<ReservationResponse>(Remembered.Reservation);
        var api = actor.AbilityTo<CallTheApi>();
        var token = (await api.Get($"/reservations/{reservation.Id}/invite-token"))
            .EnsureSuccess().As<InviteTokenResponse>().Token;
        var invite = (await api.Get($"/guest/invites/{token}")).EnsureSuccess().As<GroupInviteResponse>();

        actor.Remember(Remembered.InviteToken, token);
        actor.Remember(Remembered.GroupId, invite.GroupReservationId);
    }
}

/// <summary>Особисте замовлення Owner через POST /orders. Без нього підтвердити груповий pre-order не вийде.</summary>
public sealed class PlacePersonalPreOrder(IReadOnlyList<(string Dish, int Quantity)> items) : ITask
{
    public static PlacePersonalPreOrder With(params (string Dish, int Quantity)[] items) => new(items);

    public async Task PerformAs(Actor actor)
    {
        var reservation = actor.Recall<ReservationResponse>(Remembered.Reservation);
        var stand = Stand.Current;
        (await actor.AbilityTo<CallTheApi>().Post("/orders", new
        {
            reservationId = reservation.Id,
            orderItems = items.Select(i => new { dishId = stand.Dish(i.Dish).Id, quantity = i.Quantity }).ToArray(),
        })).EnsureSuccess();
    }
}
