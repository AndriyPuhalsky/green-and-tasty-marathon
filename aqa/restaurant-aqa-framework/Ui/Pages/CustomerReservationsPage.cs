using Microsoft.Playwright;

namespace RestaurantAqa.Ui.Pages;

/// <summary>"Reservations" клієнта: картки бронювань з кнопкою Group Pre-Order.</summary>
public sealed class CustomerReservationsPage(IPage page)
{
    public async Task Open() => await page.GotoAsync("/reservations");

    /// <summary>Картка бронювання, з якої можна відкрити груповий pre-order.</summary>
    public ILocator GroupCards => page.GetByRole(AriaRole.Listitem).Filter(new()
    {
        Has = page.GetByRole(AriaRole.Button, new() { Name = "Group Pre-Order" }),
    });

    public ILocator GroupPreOrderButton(ILocator card) => card.GetByRole(AriaRole.Button, new() { Name = "Group Pre-Order" });
}
