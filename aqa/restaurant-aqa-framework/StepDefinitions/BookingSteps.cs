using Reqnroll;
using RestaurantAqa.Screenplay.Tasks;
using RestaurantAqa.Support;

namespace RestaurantAqa.StepDefinitions;

[Binding]
public sealed class BookingSteps(GroupOrderWorld world)
{
    [Given("the owner has booked a table for {int} guests")]
    public async Task OwnerHasBookedTable(int guests)
    {
        var owner = await world.NewCustomer(GroupOrderWorld.OwnerRole);
        await owner.AttemptsTo(BookTable.ForGuests(guests).At(world.BookingLocationId));
    }

    [Given("the owner has shared the group invite")]
    public async Task OwnerHasSharedInvite() => await world.Owner.AttemptsTo(ShareGroupInvite.ForTheirReservation());

    [Given("the owner has a personal pre-order with {string}")]
    public async Task OwnerHasPersonalPreOrder(string dishes)
        => await world.Owner.AttemptsTo(PlacePersonalPreOrder.With(DishList.Parse(dishes).ToArray()));

    [Given("the owner has booked another table for {int} guests")]
    public async Task OwnerHasBookedAnotherTable(int guests)
        => await world.Owner.AttemptsTo(BookTable.ForGuests(guests).RememberedAs(Remembered.OtherReservation));

    [Given("another owner has booked a table for {int} guests and shared the invite")]
    public async Task AnotherOwnerHasGroup(int guests)
    {
        var other = await world.NewCustomer(GroupOrderWorld.OtherOwnerRole);
        await other.AttemptsTo(BookTable.ForGuests(guests), ShareGroupInvite.ForTheirReservation());
    }
}
