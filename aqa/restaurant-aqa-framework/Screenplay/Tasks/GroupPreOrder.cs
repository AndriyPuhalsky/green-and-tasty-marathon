using RestaurantAqa.Api;
using RestaurantAqa.Screenplay.Abilities;
using RestaurantAqa.Screenplay.Core;
using RestaurantAqa.Support;

namespace RestaurantAqa.Screenplay.Tasks;

// Ці задачі лише надсилають запит: відповідь лишається в CallTheApi.LastResponse,
// а що з нею має статися, перевіряють кроки Then. Так одні й ті самі задачі працюють
// і для позитивних, і для негативних сценаріїв.

public sealed class RequestGroupParticipants(string groupId) : ITask
{
    public static RequestGroupParticipants Of(string groupId) => new(groupId);

    public Task PerformAs(Actor actor) => actor.AbilityTo<CallTheApi>().Get($"/guest/groups/{groupId}/sessions");
}

public sealed class SubmitGroupPreOrder(string groupId, string reservationId) : ITask
{
    public static SubmitGroupPreOrder Of(string groupId, string reservationId) => new(groupId, reservationId);

    public Task PerformAs(Actor actor)
        => actor.AbilityTo<CallTheApi>().Post($"/group-reservations/{groupId}/submit", new { reservationId });
}

public sealed class RemoveParticipant(string groupId, string sessionId) : ITask
{
    public static RemoveParticipant FromGroup(string groupId, string sessionId) => new(groupId, sessionId);

    public Task PerformAs(Actor actor)
        => actor.AbilityTo<CallTheApi>().Delete($"/guest/groups/{groupId}/sessions/{sessionId}");
}

/// <summary>GET /reservations/{id}/guest-session-id: власна сесія Owner у групі (створюється при першому виклику).</summary>
public sealed class OpenOwnSession(string reservationId) : ITask
{
    public const string OwnerSessionId = "owner session id";

    public static OpenOwnSession For(string reservationId) => new(reservationId);

    public async Task PerformAs(Actor actor)
    {
        var response = (await actor.AbilityTo<CallTheApi>().Get($"/reservations/{reservationId}/guest-session-id"))
            .EnsureSuccess().As<OwnerSessionResponse>();
        actor.Remember(OwnerSessionId, response.GuestSessionId);
    }
}

/// <summary>Owner додає страву у власну сесію групи тим самим маршрутом, що й гості.</summary>
public sealed class AddDishToOwnSession(string sessionId, string dish, int quantity) : ITask
{
    public static AddDishToOwnSession Of(string sessionId, string dish, int quantity) => new(sessionId, dish, quantity);

    public Task PerformAs(Actor actor)
        => actor.AbilityTo<CallTheApi>().Post($"/guest/sessions/{sessionId}/dishes",
            new { dishId = Stand.Current.Dish(dish).Id, quantity });
}
