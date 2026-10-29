using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace RestaurantAqa.Ui.Pages;

/// <summary>Дашборд офіціанта /waiter-reservations.</summary>
public sealed class WaiterReservationsPage(IPage page)
{
    public async Task Open() => await page.GotoAsync("/waiter-reservations");

    public ILocator CardOf(string customerFullName)
        => page.GetByRole(AriaRole.Listitem).Filter(new() { HasText = $"Customer {customerFullName}" });

    public ILocator PreOrderButton(ILocator card) => card.GetByRole(AriaRole.Button, new() { Name = "Pre-order", Exact = true });
}

/// <summary>Сторінка /waiter-pre-order зі стравами замовлення бронювання.</summary>
public sealed class WaiterPreOrderPage(IPage page)
{
    public ILocator Title => page.GetByRole(AriaRole.Heading).Filter(new() { HasText = "Pre-order of" });

    // картка страви має role=button, кількість написана текстом "Quantity: N"
    public ILocator DishCards => page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Quantity:" });

    public async Task<IReadOnlyList<(string Dish, int Quantity)>> ReadItems()
    {
        var result = new List<(string, int)>();
        foreach (var card in await DishCards.AllAsync())
        {
            string name = (await card.Locator(".dish-name").InnerTextAsync()).Trim();
            string quantityText = await card.GetByText(new Regex(@"Quantity:\s*\d+")).InnerTextAsync();
            int quantity = int.Parse(Regex.Match(quantityText, @"\d+").Value);
            result.Add((name, quantity));
        }
        return result;
    }
}
