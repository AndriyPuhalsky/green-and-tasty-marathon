using RestaurantAqa.Screenplay.Abilities;
using RestaurantAqa.Screenplay.Core;
using RestaurantAqa.Ui.Pages;

namespace RestaurantAqa.Screenplay.Questions.Ui;

/// <summary>Сторінка групи, яку бачить актор: самі перевірки роблять кроки через Expect.</summary>
public sealed class TheGroupOrderScreen : IQuestion<GroupOrderPage>
{
    public static TheGroupOrderScreen Displayed() => new();

    public async Task<GroupOrderPage> AnsweredBy(Actor actor) => new(await actor.AbilityTo<BrowseTheWeb>().Page());
}

public sealed class TheWaiterPreOrder : IQuestion<IReadOnlyList<(string Dish, int Quantity)>>
{
    public static TheWaiterPreOrder Items() => new();

    public async Task<IReadOnlyList<(string Dish, int Quantity)>> AnsweredBy(Actor actor)
    {
        var page = new WaiterPreOrderPage(await actor.AbilityTo<BrowseTheWeb>().Page());
        await page.DishCards.First.WaitForAsync();
        return await page.ReadItems();
    }
}
