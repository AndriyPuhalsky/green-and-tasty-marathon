using RestaurantAqa.Api;
using RestaurantAqa.Screenplay.Abilities;
using RestaurantAqa.Screenplay.Core;

namespace RestaurantAqa.Screenplay.Questions;

public sealed class TheLastResponse : IQuestion<ApiResponse>
{
    public static TheLastResponse Received() => new();

    public Task<ApiResponse> AnsweredBy(Actor actor)
        => Task.FromResult(actor.AbilityTo<CallTheApi>().LastResponse
                           ?? throw new InvalidOperationException($"{actor.Name} has not sent any request yet."));
}

public sealed class TheGroupSnapshot(string groupId) : IQuestion<GroupSnapshot>
{
    public static TheGroupSnapshot Of(string groupId) => new(groupId);

    public async Task<GroupSnapshot> AnsweredBy(Actor actor)
        => (await actor.AbilityTo<CallTheApi>().Get($"/guest/groups/{groupId}/sessions")).EnsureSuccess().As<GroupSnapshot>();
}

public sealed class TheGroupStatus(string groupId) : IQuestion<GroupStatusResponse>
{
    public static TheGroupStatus Of(string groupId) => new(groupId);

    public async Task<GroupStatusResponse> AnsweredBy(Actor actor)
        => (await actor.AbilityTo<CallTheApi>().Get($"/guest/groups/{groupId}/status")).EnsureSuccess().As<GroupStatusResponse>();
}

/// <summary>Замовлення бронювання, яке отримує ресторан (GET /orders/{reservationId}).</summary>
public sealed class TheRestaurantOrder(string reservationId) : IQuestion<OrderResponse>
{
    public static TheRestaurantOrder For(string reservationId) => new(reservationId);

    public async Task<OrderResponse> AnsweredBy(Actor actor)
        => (await actor.AbilityTo<CallTheApi>().Get($"/orders/{reservationId}")).EnsureSuccess().As<OrderResponse>();
}
