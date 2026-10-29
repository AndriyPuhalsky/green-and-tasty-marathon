using System.Globalization;
using System.Text.RegularExpressions;

namespace RestaurantAqa.Support;

/// <summary>Розбирає запис "2 x Garden Soup, 1 x Green Salad" з таблиць Gherkin.</summary>
public static partial class DishList
{
    public static IReadOnlyList<(string Dish, int Quantity)> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part =>
            {
                var match = ItemPattern().Match(part);
                if (!match.Success)
                {
                    throw new FormatException($"Cannot read '{part}', expected something like '2 x Garden Soup'.");
                }
                return (match.Groups["dish"].Value.Trim(), int.Parse(match.Groups["qty"].Value));
            })
            .ToArray();
    }

    /// <summary>Однаковий запис для очікуваного й фактичного: страви за абеткою, щоб порядок не впливав.</summary>
    public static string Normalize(IEnumerable<(string Dish, int Quantity)> items)
        => string.Join(", ", items.OrderBy(i => i.Dish, StringComparer.Ordinal).Select(i => $"{i.Quantity} x {i.Dish}"));

    public static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    public static decimal ParseMoney(string text) => decimal.Parse(text.Trim().TrimStart('$'), CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^(?<qty>\d+)\s*x\s*(?<dish>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ItemPattern();
}
