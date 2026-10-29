using Reqnroll;
using RestaurantAqa.Api;
using RestaurantAqa.Screenplay.Abilities;
using RestaurantAqa.Screenplay.Core;
using RestaurantAqa.Screenplay.Questions;
using RestaurantAqa.Screenplay.Tasks;
using RestaurantAqa.Support;

namespace RestaurantAqa.StepDefinitions;

[Binding]
public sealed class GroupApiSteps(GroupOrderWorld world)
{
    private const string MissingId = "00000000-0000-0000-0000-000000000000";

    // ---------- Рівень 1: список учасників ----------

    [When("the owner requests the group participants")]
    public async Task OwnerRequestsParticipants() => await Send(world.Owner, RequestGroupParticipants.Of(world.GroupId));

    [When(@"^(.+) requests the participants of the owner's group$")]
    public async Task SomeoneRequestsParticipants(string who)
        => await Send(await world.ActorCalled(who), RequestGroupParticipants.Of(world.GroupId));

    [When("the owner requests the participants of a group that does not exist")]
    public async Task OwnerRequestsMissingGroup() => await Send(world.Owner, RequestGroupParticipants.Of(MissingId));

    [Then("the participants are:")]
    public async Task ThenParticipantsAre(DataTable expected)
    {
        var snapshot = await CurrentSnapshot();
        await Eventually(async () =>
        {
            await AssertParticipants(snapshot, expected);
        }, async () => snapshot = await world.Owner.AsksFor(TheGroupSnapshot.Of(world.GroupId)));
    }

    private async Task AssertParticipants(GroupSnapshot snapshot, DataTable expected)
    {
        string ownerName = world.Owner.AbilityTo<HaveAnAccount>().FirstName;

        var expectedRows = expected.Rows.Select(r => Row(
            r["name"] == "the owner" ? ownerName : r["name"],
            r["type"],
            DishList.Normalize(DishList.Parse(r["dishes"])),
            DishList.ParseMoney(r["total"]),
            r["submitted"] == "yes"));
        var actualRows = snapshot.Participants.Select(p => Row(
            p.Name, p.Type, DishList.Normalize(p.Items.Select(i => (i.Dish.Name, i.Quantity))), p.Total, p.IsSubmitted));

        await Assert.That(Lines(actualRows)).IsEqualTo(Lines(expectedRows));
    }

    [Then("the group total is {decimal} and equals the sum of the participant totals")]
    public async Task ThenGroupTotalIs(decimal total) => await Eventually(async () =>
    {
        var snapshot = await world.Owner.AsksFor(TheGroupSnapshot.Of(world.GroupId));
        await Assert.That(snapshot.Total).IsEqualTo(total);
        await Assert.That(snapshot.Participants.Sum(p => p.Total)).IsEqualTo(snapshot.Total);
        foreach (var participant in snapshot.Participants)
        {
            await Assert.That(participant.Items.Sum(i => i.Dish.Price * i.Quantity)).IsEqualTo(participant.Total);
        }
    });

    [Then("the owner sees that {word} has a total of {decimal}")]
    public async Task ThenParticipantHasTotal(string name, decimal total) => await Eventually(async () =>
    {
        var snapshot = await world.Owner.AsksFor(TheGroupSnapshot.Of(world.GroupId));
        var participant = snapshot.Participants.SingleOrDefault(p => p.Name == name);
        await Assert.That(participant).IsNotNull();
        await Assert.That(participant!.Total).IsEqualTo(total);
    });

    [Then("the group has no participants and the total is 0")]
    public async Task ThenGroupIsEmpty()
    {
        var snapshot = await CurrentSnapshot();
        await Assert.That(snapshot.Participants.Count).IsEqualTo(0);
        await Assert.That(snapshot.Total).IsEqualTo(0m);
    }

    [Then("the participants of the other owner's group are {string}")]
    public async Task ThenOtherGroupParticipants(string names)
    {
        var other = await world.ActorCalled(GroupOrderWorld.OtherOwnerRole);
        await Eventually(async () =>
        {
            var snapshot = await other.AsksFor(TheGroupSnapshot.Of(other.Recall<string>(Remembered.GroupId)));
            await Assert.That(string.Join(", ", snapshot.Participants.Select(p => p.Name).Order())).IsEqualTo(names);
        });
    }

    [Then("guest {word} is still in the group")]
    public async Task ThenGuestIsStillInGroup(string name) => await Eventually(async () =>
    {
        var snapshot = await world.Owner.AsksFor(TheGroupSnapshot.Of(world.GroupId));
        await Assert.That(snapshot.Participants.Any(p => p.Name == name)).IsTrue();
    });

    // ---------- Рівень 2: власна сесія Owner і submit ----------

    [Given("the owner has opened their own session in the group")]
    public async Task OwnerHasOwnSession() => await world.Owner.AttemptsTo(OpenOwnSession.For(world.Reservation.Id));

    [When("the owner adds {string} to their own session")]
    public async Task OwnerAddsToOwnSession(string dishes)
    {
        string sessionId = world.Owner.Recall<string>(OpenOwnSession.OwnerSessionId);
        foreach (var (dish, quantity) in DishList.Parse(dishes))
        {
            await Send(world.Owner, AddDishToOwnSession.Of(sessionId, dish, quantity));
        }
    }

    [Given("the owner has submitted the group pre-order")]
    public async Task OwnerHasSubmitted()
    {
        await Send(world.Owner, SubmitGroupPreOrder.Of(world.GroupId, world.Reservation.Id));
        world.LastResponse!.EnsureSuccess();
    }

    [When("the owner submits the group pre-order")]
    public async Task OwnerSubmits() => await Send(world.Owner, SubmitGroupPreOrder.Of(world.GroupId, world.Reservation.Id));

    [When(@"^(.+) submits the owner's group pre-order$")]
    public async Task SomeoneSubmits(string who)
        => await Send(await world.ActorCalled(who), SubmitGroupPreOrder.Of(world.GroupId, world.Reservation.Id));

    [When("the owner submits the group pre-order of a group that does not exist")]
    public async Task OwnerSubmitsMissingGroup() => await Send(world.Owner, SubmitGroupPreOrder.Of(MissingId, world.Reservation.Id));

    [When("the owner submits the group pre-order for a reservation that does not exist")]
    public async Task OwnerSubmitsMissingReservation() => await Send(world.Owner, SubmitGroupPreOrder.Of(world.GroupId, MissingId));

    [When("the owner submits the group pre-order for the other reservation")]
    public async Task OwnerSubmitsForOtherReservation()
    {
        var other = world.Owner.Recall<ReservationResponse>(Remembered.OtherReservation);
        await Send(world.Owner, SubmitGroupPreOrder.Of(world.GroupId, other.Id));
    }

    [Then("the submitted dishes are:")]
    public async Task ThenSubmittedDishesAre(DataTable expected)
    {
        var dishes = world.LastResponse!.EnsureSuccess().As<List<SubmittedDish>>();
        var byId = Stand.Current.Dishes.Values.ToDictionary(d => d.Id, d => d.Name);
        string actual = DishList.Normalize(dishes.Select(d => (byId.GetValueOrDefault(d.DishId, d.DishId), d.Quantity)));
        await Assert.That(actual).IsEqualTo(ExpectedDishes(expected));
    }

    [Then("the restaurant order has status {string} with dishes:")]
    public async Task ThenRestaurantOrderIs(string status, DataTable expected)
    {
        var order = await world.Owner.AsksFor(TheRestaurantOrder.For(world.Reservation.Id));
        await Assert.That(((OrderStatus)order.Status).ToString()).IsEqualTo(status);
        string actual = DishList.Normalize(order.OrderItems.Select(i => (i.Dish.Name, i.Quantity)));
        await Assert.That(actual).IsEqualTo(ExpectedDishes(expected));
    }

    [Then("the group reservation has status {string}")]
    public async Task ThenGroupStatusIs(string status)
    {
        var groupStatus = await world.Owner.AsksFor(TheGroupStatus.Of(world.GroupId));
        await Assert.That(groupStatus.Status).IsEqualTo(status);
    }

    // ---------- Рівень 3: видалення учасника ----------

    [When("the owner removes guest {word} from the group")]
    public async Task OwnerRemovesGuest(string name)
        => await Send(world.Owner, RemoveParticipant.FromGroup(world.GroupId, SessionOf(name)));

    [When(@"^(.+) removes guest (\w+) from the owner's group$")]
    public async Task SomeoneRemovesGuest(string who, string name)
        => await Send(await world.ActorCalled(who), RemoveParticipant.FromGroup(world.GroupId, SessionOf(name)));

    [When("the owner removes a participant that does not exist")]
    public async Task OwnerRemovesMissingParticipant()
        => await Send(world.Owner, RemoveParticipant.FromGroup(world.GroupId, MissingId));

    [When("the owner removes their own session from the group")]
    public async Task OwnerRemovesOwnSession()
        => await Send(world.Owner, RemoveParticipant.FromGroup(world.GroupId, world.Owner.Recall<string>(OpenOwnSession.OwnerSessionId)));

    [When("the owner removes guest {word} of the other owner's group from their own group")]
    public async Task OwnerRemovesForeignGuest(string name)
        => await Send(world.Owner, RemoveParticipant.FromGroup(world.GroupId, SessionOf(name)));

    [Then("the response lists the participants {string} with a group total of {decimal}")]
    public async Task ThenResponseListsParticipants(string names, decimal total)
    {
        var snapshot = world.LastResponse!.EnsureSuccess().As<GroupSnapshot>();
        await Assert.That(string.Join(", ", snapshot.Participants.Select(p => p.Name).Order())).IsEqualTo(names);
        await Assert.That(snapshot.Total).IsEqualTo(total);
        await Assert.That(snapshot.Participants.Sum(p => p.Total)).IsEqualTo(total);
    }

    [Then("the response has no participants and a group total of 0")]
    public async Task ThenResponseIsEmpty()
    {
        var snapshot = world.LastResponse!.EnsureSuccess().As<GroupSnapshot>();
        await Assert.That(snapshot.Participants.Count).IsEqualTo(0);
        await Assert.That(snapshot.Total).IsEqualTo(0m);
    }

    // ---------- Загальні перевірки відповіді ----------

    [Then("the request succeeds")]
    public async Task ThenRequestSucceeds()
    {
        var response = world.LastResponse ?? throw new InvalidOperationException("No request has been sent in this scenario.");
        await Assert.That(response.StatusCode).IsEqualTo(200).Because(response.ToString());
    }

    [Then("the request is rejected with status {int}")]
    public async Task ThenRejectedWithStatus(int status)
    {
        var response = world.LastResponse ?? throw new InvalidOperationException("No request has been sent in this scenario.");
        await Assert.That(response.StatusCode).IsEqualTo(status).Because(response.ToString());
    }

    [Then("the request is rejected with status {int} and message {string}")]
    public async Task ThenRejectedWithStatusAndMessage(int status, string message)
    {
        await ThenRejectedWithStatus(status);
        string expected = message.Replace("{reservation id}", world.Reservation.Id).Replace("{missing id}", MissingId);
        await Assert.That(world.LastResponse!.ErrorMessage).IsEqualTo(expected);
    }

    [Then("the error code is {string}")]
    public async Task ThenErrorCodeIs(string code) => await Assert.That(world.LastResponse!.ErrorCode).IsEqualTo(code);

    /// <summary>
    /// Список учасників бекенд читає з GSI DynamoDB, а він оновлюється з невеликою затримкою.
    /// Перевірки стану через GET тому повторюються кілька секунд, як auto-wait у Playwright.
    /// Відповідь самого DELETE перевіряється без повторів: її бекенд зобов'язаний віддати вже оновленою.
    /// </summary>
    private static async Task Eventually(Func<Task> assertion, Func<Task>? refresh = null)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await assertion();
                return;
            }
            catch (TUnit.Assertions.Exceptions.AssertionException) when (attempt < 6)
            {
                await Task.Delay(1000);
                if (refresh is not null)
                {
                    await refresh();
                }
            }
        }
    }

    private async Task Send(Actor actor, ITask task)
    {
        await actor.AttemptsTo(task);
        world.LastResponse = await actor.AsksFor(TheLastResponse.Received());
    }

    private async Task<GroupSnapshot> CurrentSnapshot()
    {
        var response = world.LastResponse;
        if (response is { Method.Method: "GET" } && response.Path.EndsWith("/sessions") && response.IsSuccess)
        {
            return response.As<GroupSnapshot>();
        }
        return await world.Owner.AsksFor(TheGroupSnapshot.Of(world.GroupId));
    }

    private string SessionOf(string guestName) => world.Guest(guestName).AbilityTo<JoinAsGuest>().RequireSessionId();

    private static string ExpectedDishes(DataTable table)
        => DishList.Normalize(table.Rows.Select(r => (r["dish"], int.Parse(r["quantity"]))));

    private static string Row(string name, string type, string dishes, decimal total, bool submitted)
        => $"{name} | {type} | {dishes} | {DishList.Money(total)} | {(submitted ? "submitted" : "open")}";

    private static string Lines(IEnumerable<string> rows) => string.Join("\n", rows.Order(StringComparer.Ordinal));
}
