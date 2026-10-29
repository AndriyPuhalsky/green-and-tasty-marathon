namespace RestaurantAqa.Support;

/// <summary>Страви, які прогін створює у своїй локації. Ціни різні, щоб суми не збігалися випадково.</summary>
public static class Menu
{
    public static readonly IReadOnlyList<MenuDish> Dishes =
    [
        new("Garden Soup", 8.50m, "Appetizer"),
        new("Green Salad", 6.25m, "Appetizer"),
        new("Veggie Burger", 12.00m, "MainCourse"),
        new("Berry Cake", 5.75m, "Dessert"),
    ];
}

public sealed record MenuDish(string Name, decimal Price, string DishType);
