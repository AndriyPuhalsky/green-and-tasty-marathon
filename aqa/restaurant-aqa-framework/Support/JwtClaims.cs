using System.Text;
using System.Text.Json;

namespace RestaurantAqa.Support;

public static class JwtClaims
{
    public static string? Subject(string jwt)
    {
        string[] parts = jwt.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        string payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        return doc.RootElement.TryGetProperty("sub", out var sub) ? sub.GetString() : null;
    }
}
