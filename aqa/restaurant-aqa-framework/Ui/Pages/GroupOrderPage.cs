using Microsoft.Playwright;

namespace RestaurantAqa.Ui.Pages;

/// <summary>Екран Owner /reservations/:reservationId/group-order (ДЗ6).</summary>
public sealed class GroupOrderPage(IPage page)
{
    public ILocator Heading => page.GetByRole(AriaRole.Heading, new() { Name = "Group Pre-Order", Level = 1 });
    public ILocator Loading => page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Loading group pre-order" });
    public ILocator ErrorState => page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "Couldn't load the group pre-order" });
    public ILocator NotFoundState => page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "Group reservation not found" });
    public ILocator PendingNotice => page.GetByRole(AriaRole.Status).Filter(new() { HasText = "submitted their cart yet" });
    public ILocator EmptyState => page.GetByText("Participants haven't added any dishes yet");
    public ILocator ParticipantCards => page.GetByRole(AriaRole.Article);
    public ILocator ConsolidatedOrder => page.GetByRole(AriaRole.Region, new() { Name = "Consolidated pre-order" });
    public ILocator RefreshButton => page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true });
    public ILocator SubmitButton => page.GetByRole(AriaRole.Button, new() { Name = "Submit Pre-Order" });
    public ILocator SentButton => page.GetByRole(AriaRole.Button, new() { Name = "Pre-Order Sent" });
    public ILocator SubmitDialog => page.GetByRole(AriaRole.Dialog, new() { Name = "Submit Pre-Order" });
    public ILocator RemoveDialog => page.GetByRole(AriaRole.Dialog, new() { Name = "Remove Guest" });

    // у статусу й Grand Total немає ролі чи підпису, тому тут CSS-класи компонента
    public ILocator StatusBadge => page.Locator("header .badge");
    public ILocator GrandTotal => page.Locator(".grand-total .amount");

    public ILocator Toast(string text) => page.Locator("mat-snack-bar-container").GetByText(text);

    public async Task Open(string reservationId) => await page.GotoAsync($"/reservations/{reservationId}/group-order");

    // сторінка робить кілька запитів підряд, тож чекаємо довше за таймаут одного запиту
    public async Task WaitUntilLoaded()
    {
        await Heading.WaitForAsync();
        await Loading.WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 60_000 });
    }

    public ParticipantCard Card(string title)
        => new(ParticipantCards.Filter(new() { Has = page.GetByRole(AriaRole.Heading, new() { Name = title, Exact = true }) }));

    public ILocator RemoveButtonFor(string guestName) => page.GetByRole(AriaRole.Button, new() { Name = $"Remove {guestName}", Exact = true });
}

public sealed class ParticipantCard(ILocator root)
{
    public ILocator Root => root;
    public ILocator Title => root.GetByRole(AriaRole.Heading);
    public ILocator SubmittedChip => root.GetByText("Submitted", new() { Exact = true });
    public ILocator ChoosingChip => root.GetByText("Still choosing", new() { Exact = true });
    public ILocator NoDishes => root.GetByText("No dishes yet");
    public ILocator Items => root.GetByRole(AriaRole.Listitem);
    public ILocator Subtotal => root.Locator(".subtotal span").Last;

    public async Task<IReadOnlyList<(string Dish, int Quantity)>> ReadItems()
    {
        var result = new List<(string, int)>();
        foreach (var item in await Items.AllAsync())
        {
            string name = (await item.Locator(".name").InnerTextAsync()).Trim();
            string quantity = (await item.Locator(".qty").InnerTextAsync()).Replace("×", "").Trim();
            result.Add((name, int.Parse(quantity)));
        }
        return result;
    }
}
