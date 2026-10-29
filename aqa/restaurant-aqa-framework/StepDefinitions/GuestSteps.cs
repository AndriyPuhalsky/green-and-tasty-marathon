using Reqnroll;
using RestaurantAqa.Screenplay.Core;
using RestaurantAqa.Screenplay.Questions;
using RestaurantAqa.Screenplay.Tasks;
using RestaurantAqa.Support;

namespace RestaurantAqa.StepDefinitions;

[Binding]
public sealed class GuestSteps(GroupOrderWorld world)
{
    [Given("these guests have joined the group by the invite:")]
    public async Task TheseGuestsHaveJoined(DataTable table) => await JoinGuests(world.Owner, table);

    [Given("these guests have joined the other owner's group by the invite:")]
    public async Task TheseGuestsHaveJoinedOtherGroup(DataTable table)
        => await JoinGuests(await world.ActorCalled(GroupOrderWorld.OtherOwnerRole), table);

    [Given("guest {word} has joined the group by the invite")]
    public async Task GuestHasJoined(string name)
        => await world.Guest(name).AttemptsTo(JoinGroup.WithInvite(world.Owner.Recall<string>(Remembered.InviteToken)));

    [Given("guest {word} has added {string} to the cart")]
    [When("guest {word} adds {string} to the cart")]
    public async Task GuestAddsDishes(string name, string dishes)
    {
        var guest = world.Guest(name);
        foreach (var (dish, quantity) in DishList.Parse(dishes))
        {
            await guest.AttemptsTo(AddDishToCart.Attempt(dish, quantity));
            world.LastResponse = await guest.AsksFor(TheLastResponse.Received());
            if (!world.LastResponse.IsSuccess)
            {
                return;
            }
        }
    }

    [When("guest {word} removes {string} from the cart")]
    public async Task GuestRemovesDishes(string name, string dishes)
    {
        var guest = world.Guest(name);
        foreach (var (dish, quantity) in DishList.Parse(dishes))
        {
            await guest.AttemptsTo(RemoveDishFromCart.Of(dish, quantity));
        }
    }

    [Given("guest {word} has submitted the cart")]
    [When("guest {word} submits the cart")]
    public async Task GuestSubmitsCart(string name)
    {
        var guest = world.Guest(name);
        await guest.AttemptsTo(SubmitCart.Attempt());
        world.LastResponse = await guest.AsksFor(TheLastResponse.Received());
    }

    // гості з таблиці: | guest | dishes | cart |, де cart = submitted або open
    private async Task JoinGuests(Actor owner, DataTable table)
    {
        string invite = owner.Recall<string>(Remembered.InviteToken);
        foreach (var row in table.Rows)
        {
            var guest = world.Guest(row["guest"]);
            await guest.AttemptsTo(JoinGroup.WithInvite(invite));
            foreach (var (dish, quantity) in DishList.Parse(row["dishes"]))
            {
                await guest.AttemptsTo(AddDishToCart.Of(dish, quantity));
            }
            if (row["cart"].Equals("submitted", StringComparison.OrdinalIgnoreCase))
            {
                await guest.AttemptsTo(SubmitCart.Now());
            }
        }
    }
}
