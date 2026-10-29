using RestaurantAqa.Api;
using RestaurantAqa.Screenplay.Abilities;
using RestaurantAqa.Screenplay.Core;
using RestaurantAqa.Support;

namespace RestaurantAqa.Screenplay.Tasks;

/// <summary>Гість відкриває запрошення і приєднується до групи під своїм ім'ям.</summary>
public sealed class JoinGroup(string inviteToken) : ITask
{
    public static JoinGroup WithInvite(string inviteToken) => new(inviteToken);

    public async Task PerformAs(Actor actor)
    {
        var guest = actor.AbilityTo<JoinAsGuest>();
        var api = actor.AbilityTo<CallTheApi>();

        var invite = (await api.Get($"/guest/invites/{inviteToken}")).EnsureSuccess().As<GroupInviteResponse>();
        var session = (await api.Post("/guest/sessions", new
        {
            groupReservationId = invite.GroupReservationId,
            guestName = guest.GuestName,
        })).EnsureSuccess().As<GuestSessionCreated>();

        guest.Joined(session.SessionId, session.SessionToken);
        api.UseToken(session.SessionToken);
    }
}

public sealed class AddDishToCart(string dish, int quantity, bool mustSucceed) : ITask
{
    public static AddDishToCart Of(string dish, int quantity) => new(dish, quantity, mustSucceed: true);

    public static AddDishToCart Attempt(string dish, int quantity) => new(dish, quantity, mustSucceed: false);

    public async Task PerformAs(Actor actor)
    {
        string sessionId = actor.AbilityTo<JoinAsGuest>().RequireSessionId();
        var response = await actor.AbilityTo<CallTheApi>().Post($"/guest/sessions/{sessionId}/dishes",
            new { dishId = Stand.Current.Dish(dish).Id, quantity });
        if (mustSucceed)
        {
            response.EnsureSuccess();
        }
    }
}

public sealed class RemoveDishFromCart(string dish, int quantity) : ITask
{
    public static RemoveDishFromCart Of(string dish, int quantity) => new(dish, quantity);

    public async Task PerformAs(Actor actor)
    {
        string sessionId = actor.AbilityTo<JoinAsGuest>().RequireSessionId();
        (await actor.AbilityTo<CallTheApi>().Delete($"/guest/sessions/{sessionId}/dishes",
            new { dishId = Stand.Current.Dish(dish).Id, quantity })).EnsureSuccess();
    }
}

public sealed class SubmitCart(bool mustSucceed) : ITask
{
    public static SubmitCart Now() => new(mustSucceed: true);

    public static SubmitCart Attempt() => new(mustSucceed: false);

    public async Task PerformAs(Actor actor)
    {
        string sessionId = actor.AbilityTo<JoinAsGuest>().RequireSessionId();
        var response = await actor.AbilityTo<CallTheApi>().Post($"/guest/sessions/{sessionId}/submit");
        if (mustSucceed)
        {
            response.EnsureSuccess();
        }
    }
}
