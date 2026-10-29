using Microsoft.Playwright;
using Reqnroll;
using RestaurantAqa.Screenplay.Abilities;
using RestaurantAqa.Screenplay.Questions.Ui;
using RestaurantAqa.Screenplay.Tasks.Ui;
using RestaurantAqa.Support;
using RestaurantAqa.Ui.Pages;
using static Microsoft.Playwright.Assertions;

namespace RestaurantAqa.StepDefinitions;

[Binding]
public sealed class GroupUiSteps(GroupOrderWorld world)
{
    private const string MissingId = "00000000-0000-0000-0000-000000000000";

    // Під час першого відкриття картка "My Pre-Order" може з'явитися лише після фонового
    // оновлення (раз на 15 с), див. "Знайдені дефекти" в README. Тому картки чекаємо довше.
    private static readonly LocatorAssertionsToHaveCountOptions CardsCount = new() { Timeout = 25_000 };
    private static readonly LocatorAssertionsToBeVisibleOptions CardVisible = new() { Timeout = 25_000 };

    private Task<GroupOrderPage> Screen() => world.Owner.AsksFor(TheGroupOrderScreen.Displayed());

    // ---------- вхід і навігація ----------

    [Given(@"^(the owner|another customer|the waiter) (?:is|has) signed in to the app$")]
    public async Task SignedIn(string who) => await (await world.ActorCalled(who)).AttemptsTo(LogIn.ThroughTheLoginForm());

    [Given("the owner has opened the group pre-order page")]
    [When("the owner opens the group pre-order page")]
    public async Task OwnerOpensPage()
    {
        if (!world.Owner.AbilityTo<BrowseTheWeb>().IsOpen)
        {
            await world.Owner.AttemptsTo(LogIn.ThroughTheLoginForm());
        }
        await world.Owner.AttemptsTo(OpenGroupPreOrder.ByLink(world.Reservation.Id));
    }

    [When("the owner opens the group pre-order from the reservations page")]
    public async Task OwnerOpensFromReservations() => await world.Owner.AttemptsTo(OpenGroupPreOrder.FromReservations());

    [When("the owner opens the group pre-order of a reservation that does not exist")]
    public async Task OwnerOpensMissing() => await world.Owner.AttemptsTo(OpenGroupPreOrder.ByLink(MissingId));

    [When("another customer opens the owner's group pre-order by link")]
    public async Task StrangerOpensOwnersPage()
        => await (await world.AnotherCustomer()).AttemptsTo(OpenGroupPreOrder.ByLink(world.Reservation.Id));

    [When("the owner refreshes the participants")]
    public async Task OwnerRefreshes() => await world.Owner.AttemptsTo(RefreshParticipants.Now());

    // ---------- що бачить Owner ----------

    [Then("the owner sees these participant cards:")]
    public async Task ThenOwnerSeesCards(DataTable expected)
    {
        var screen = await Screen();
        await Expect(screen.ParticipantCards).ToHaveCountAsync(expected.RowCount, CardsCount);

        foreach (var row in expected.Rows)
        {
            var card = screen.Card(row["card"]);
            await Expect(card.Root).ToBeVisibleAsync();
            await Expect(row["status"] == "Submitted" ? card.SubmittedChip : card.ChoosingChip).ToBeVisibleAsync();
            await Expect(card.Subtotal).ToHaveTextAsync(row["subtotal"]);

            var expectedItems = DishList.Parse(row["dishes"]);
            if (expectedItems.Count == 0)
            {
                await Expect(card.NoDishes).ToBeVisibleAsync();
            }
            else
            {
                await Expect(card.Items).ToHaveCountAsync(expectedItems.Count);
                await Assert.That(DishList.Normalize(await card.ReadItems())).IsEqualTo(DishList.Normalize(expectedItems));
            }
        }
    }

    [Then("the card {string} shows status {string} and subtotal {string}")]
    public async Task ThenCardShows(string title, string status, string subtotal)
    {
        var card = (await Screen()).Card(title);
        await Expect(status == "Submitted" ? card.SubmittedChip : card.ChoosingChip).ToBeVisibleAsync();
        await Expect(card.Subtotal).ToHaveTextAsync(subtotal);
    }

    [Then("the consolidated pre-order shows:")]
    public async Task ThenConsolidatedShows(DataTable expected)
    {
        var region = (await Screen()).ConsolidatedOrder;
        var rows = region.GetByRole(AriaRole.Listitem);
        await Expect(rows).ToHaveCountAsync(expected.RowCount);
        foreach (var row in expected.Rows)
        {
            await Expect(rows.Filter(new() { HasText = $"{row["dish"]} × {row["quantity"]}" })).ToHaveCountAsync(1);
        }
    }

    [Then("the consolidated pre-order says {string}")]
    public async Task ThenConsolidatedSays(string text) => await Expect((await Screen()).ConsolidatedOrder).ToContainTextAsync(text);

    [Given("the grand total shown is {string}")]
    [Then("the grand total shown is {string}")]
    public async Task ThenGrandTotalIs(string amount) => await Expect((await Screen()).GrandTotal).ToHaveTextAsync(amount);

    [Then("the page warns that some participants haven't submitted their cart yet")]
    public async Task ThenPendingNoticeShown() => await Expect((await Screen()).PendingNotice).ToBeVisibleAsync();

    [Then("the page says {string}")]
    public async Task ThenPageSays(string text)
    {
        var page = await world.Owner.AbilityTo<BrowseTheWeb>().Page();
        await Expect(page.GetByText(text, new() { Exact = false }).First).ToBeVisibleAsync();
    }

    [Then("another customer sees the message {string} and no participant cards")]
    public async Task ThenStrangerSeesMessage(string text)
    {
        var page = await (await world.AnotherCustomer()).AbilityTo<BrowseTheWeb>().Page();
        var screen = new GroupOrderPage(page);
        await Expect(page.GetByRole(AriaRole.Alert).Filter(new() { HasText = text })).ToBeVisibleAsync();
        await Expect(screen.ParticipantCards).ToHaveCountAsync(0);
    }

    [Then("the owner sees only their own card {string}")]
    public async Task ThenOnlyOwnCard(string title)
    {
        var screen = await Screen();
        await Expect(screen.Card(title).Root).ToBeVisibleAsync(CardVisible);
        await Expect(screen.ParticipantCards).ToHaveCountAsync(1);
    }

    [Then("the owner sees the cards of {string}")]
    public async Task ThenOwnerSeesCardsOf(string guests)
    {
        var screen = await Screen();
        foreach (string name in guests.Split(',', StringSplitOptions.TrimEntries))
        {
            await Expect(screen.Card($"Guest: {name}").Root).ToBeVisibleAsync();
        }
    }

    [Then("the card {string} is not shown")]
    public async Task ThenCardNotShown(string title) => await Expect((await Screen()).Card(title).Root).ToHaveCountAsync(0);

    [Then("the {string} button is disabled")]
    public async Task ThenButtonDisabled(string name)
    {
        var page = await world.Owner.AbilityTo<BrowseTheWeb>().Page();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true })).ToBeDisabledAsync();
    }

    [Then("there is no {string} button for the owner's own card")]
    public async Task ThenNoRemoveOnOwnCard(string name)
    {
        var card = (await Screen()).Card("My Pre-Order");
        await Expect(card.Root).ToBeVisibleAsync(CardVisible);
        await Expect(card.Root.GetByRole(AriaRole.Button, new() { Name = name })).ToHaveCountAsync(0);
    }

    [Then("no {string} buttons are shown")]
    public async Task ThenNoButtons(string name)
    {
        var page = await world.Owner.AbilityTo<BrowseTheWeb>().Page();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = name })).ToHaveCountAsync(0);
    }

    [Then("the pre-order status shown is {string}")]
    public async Task ThenStatusShown(string status) => await Expect((await Screen()).StatusBadge).ToHaveTextAsync(status);

    // ---------- submit ----------

    [When("the owner opens the submit dialog")]
    public async Task OwnerOpensSubmitDialog() => await world.Owner.AttemptsTo(SubmitPreOrderInTheApp.OpenDialogOnly());

    [Then("the dialog says {string}")]
    public async Task ThenDialogSays(string text) => await Expect((await Screen()).SubmitDialog).ToContainTextAsync(text);

    [When("the owner confirms the submit")]
    public async Task OwnerConfirmsSubmit()
    {
        var screen = await Screen();
        await screen.SubmitDialog.GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true }).ClickAsync();
        await Expect(screen.SubmitDialog).ToBeHiddenAsync();
    }

    [Given("the owner has submitted the group pre-order in the app")]
    [When("the owner submits the group pre-order in the app")]
    public async Task OwnerSubmitsInApp()
    {
        if (!world.Owner.AbilityTo<BrowseTheWeb>().IsOpen)
        {
            await OwnerOpensPage();
        }
        await world.Owner.AttemptsTo(SubmitPreOrderInTheApp.AndConfirm());
    }

    [Then("the app confirms {string}")]
    [Then("the app shows the error {string}")]
    public async Task ThenToastShown(string text)
    {
        string expected = text.Replace("{reservation id}", world.Reservation.Id);
        await Expect((await Screen()).Toast(expected)).ToBeVisibleAsync();
    }

    // ---------- видалення гостя ----------

    [When("the owner removes guest {word} in the app")]
    public async Task OwnerRemovesGuestInApp(string name) => await world.Owner.AttemptsTo(RemoveGuestInTheApp.Named(name));

    [When("the owner starts removing guest {word} but cancels")]
    public async Task OwnerCancelsRemoval(string name) => await world.Owner.AttemptsTo(RemoveGuestInTheApp.ButCancel(name));

    // ---------- офіціант ----------

    [When("the waiter opens the pre-order of the owner's reservation")]
    public async Task WaiterOpensPreOrder()
    {
        var waiter = world.Waiter();
        await waiter.AttemptsTo(
            LogIn.ThroughTheLoginForm(),
            OpenWaiterPreOrder.OfCustomer(world.Owner.AbilityTo<HaveAnAccount>().FullName));
    }

    [Then("the waiter sees these dishes:")]
    public async Task ThenWaiterSeesDishes(DataTable expected)
    {
        var items = await world.Waiter().AsksFor(TheWaiterPreOrder.Items());
        string expectedItems = DishList.Normalize(expected.Rows.Select(r => (r["dish"], int.Parse(r["quantity"]))));
        await Assert.That(DishList.Normalize(items)).IsEqualTo(expectedItems);
    }
}
