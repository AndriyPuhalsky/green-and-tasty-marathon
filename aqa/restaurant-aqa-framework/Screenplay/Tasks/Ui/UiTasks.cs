using Microsoft.Playwright;
using RestaurantAqa.Api;
using RestaurantAqa.Screenplay.Abilities;
using RestaurantAqa.Screenplay.Core;
using RestaurantAqa.Support;
using RestaurantAqa.Ui.Pages;
using static Microsoft.Playwright.Assertions;

namespace RestaurantAqa.Screenplay.Tasks.Ui;

/// <summary>Вхід через форму Sign In так, як це робить користувач.</summary>
public sealed class LogIn : ITask
{
    public static LogIn ThroughTheLoginForm() => new();

    public async Task PerformAs(Actor actor)
    {
        var account = actor.AbilityTo<HaveAnAccount>();
        var page = await actor.AbilityTo<BrowseTheWeb>().Page();
        var login = new LoginPage(page);
        await login.Open();
        await login.SignIn(account.Email, account.Password);
    }
}

public sealed class OpenGroupPreOrder(string? reservationId, bool fromReservationsList) : ITask
{
    /// <summary>Пряме посилання на екран групи.</summary>
    public static OpenGroupPreOrder ByLink(string reservationId) => new(reservationId, false);

    /// <summary>Reservations → картка бронювання → Group Pre-Order.</summary>
    public static OpenGroupPreOrder FromReservations() => new(null, true);

    public async Task PerformAs(Actor actor)
    {
        var page = await actor.AbilityTo<BrowseTheWeb>().Page();
        var groupPage = new GroupOrderPage(page);

        if (fromReservationsList)
        {
            var reservation = actor.Recall<ReservationResponse>(Remembered.Reservation);
            var reservations = new CustomerReservationsPage(page);
            await reservations.Open();
            // у клієнта в цьому сценарії одне бронювання, тож картка з кнопкою групи одна
            var card = reservations.GroupCards;
            await Expect(card).ToHaveCountAsync(1);
            await reservations.GroupPreOrderButton(card).ClickAsync();
            await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"/reservations/{reservation.Id}/group-order"));
        }
        else
        {
            await groupPage.Open(reservationId!);
        }

        await groupPage.WaitUntilLoaded();
    }
}

public sealed class RefreshParticipants : ITask
{
    public static RefreshParticipants Now() => new();

    public async Task PerformAs(Actor actor)
    {
        var page = await actor.AbilityTo<BrowseTheWeb>().Page();
        var groupPage = new GroupOrderPage(page);
        var sessionsResponse = page.WaitForResponseAsync(r => r.Url.Contains("/sessions") && r.Request.Method == "GET");
        await groupPage.RefreshButton.ClickAsync();
        await sessionsResponse;
        await Expect(groupPage.RefreshButton).ToBeEnabledAsync();
    }
}

public sealed class SubmitPreOrderInTheApp(bool confirm) : ITask
{
    public static SubmitPreOrderInTheApp AndConfirm() => new(true);

    public static SubmitPreOrderInTheApp OpenDialogOnly() => new(false);

    public async Task PerformAs(Actor actor)
    {
        var page = await actor.AbilityTo<BrowseTheWeb>().Page();
        var groupPage = new GroupOrderPage(page);
        await groupPage.SubmitButton.ClickAsync();
        await Expect(groupPage.SubmitDialog).ToBeVisibleAsync();
        if (confirm)
        {
            await groupPage.SubmitDialog.GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true }).ClickAsync();
            // діалог закривається, коли сервер відповів
            await Expect(groupPage.SubmitDialog).ToBeHiddenAsync(new() { Timeout = TestSettings.Current.UiTimeoutMs });
        }
    }
}

public sealed class RemoveGuestInTheApp(string guestName, bool confirm) : ITask
{
    public static RemoveGuestInTheApp Named(string guestName) => new(guestName, true);

    public static RemoveGuestInTheApp ButCancel(string guestName) => new(guestName, false);

    public async Task PerformAs(Actor actor)
    {
        var page = await actor.AbilityTo<BrowseTheWeb>().Page();
        var groupPage = new GroupOrderPage(page);
        await groupPage.RemoveButtonFor(guestName).ClickAsync();
        await Expect(groupPage.RemoveDialog).ToContainTextAsync(guestName);

        string button = confirm ? "Remove Guest" : "Cancel";
        await groupPage.RemoveDialog.GetByRole(AriaRole.Button, new() { Name = button, Exact = true }).ClickAsync();
        await Expect(groupPage.RemoveDialog).ToBeHiddenAsync(new() { Timeout = TestSettings.Current.UiTimeoutMs });
    }
}

/// <summary>Офіціант відкриває свої бронювання і переходить у pre-order бронювання клієнта.</summary>
public sealed class OpenWaiterPreOrder(string customerFullName) : ITask
{
    public static OpenWaiterPreOrder OfCustomer(string customerFullName) => new(customerFullName);

    public async Task PerformAs(Actor actor)
    {
        var page = await actor.AbilityTo<BrowseTheWeb>().Page();
        var dashboard = new WaiterReservationsPage(page);
        await dashboard.Open();

        // Дашборд робить по три запити на кожне бронювання офіціанта. Якщо стенд не встиг відповісти
        // на частину з них (ліміт Lambda, див. README), картки або кнопки Pre-order немає,
        // і офіціант просто оновлює сторінку. Так само робить і тест, не більше трьох разів.
        var card = dashboard.CardOf(customerFullName);
        var preOrderButton = dashboard.PreOrderButton(card);
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await preOrderButton.WaitForAsync(new() { Timeout = 30_000 });
                break;
            }
            catch (TimeoutException) when (attempt < 3)
            {
                await page.ReloadAsync();
            }
        }
        await preOrderButton.ClickAsync();
        await Expect(new WaiterPreOrderPage(page).Title).ToBeVisibleAsync();
    }
}
