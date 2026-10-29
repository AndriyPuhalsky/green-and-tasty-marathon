using System.Net;
using System.Text.Json;

namespace RestaurantAqa.Api;

public sealed record ApiResponse(HttpMethod Method, string Path, HttpStatusCode Status, string Body)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public int StatusCode => (int)Status;

    public bool IsSuccess => StatusCode is >= 200 and < 300;

    public T As<T>()
        => JsonSerializer.Deserialize<T>(Body, Json)
           ?? throw new InvalidOperationException($"{Method} {Path} returned an empty body.");

    /// <summary>Текст помилки: бекенд віддає його в "error" або "message".</summary>
    public string ErrorMessage
    {
        get
        {
            try
            {
                using var doc = JsonDocument.Parse(Body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (string key in new[] { "error", "message" })
                    {
                        if (doc.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                        {
                            return value.GetString() ?? string.Empty;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // не JSON, повертаємо як є
            }
            return Body;
        }
    }

    public string? ErrorCode
    {
        get
        {
            try
            {
                using var doc = JsonDocument.Parse(Body);
                return doc.RootElement.ValueKind == JsonValueKind.Object
                       && doc.RootElement.TryGetProperty("errorCode", out var code)
                    ? code.GetString()
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    public EnsureSuccessResult EnsureSuccess()
    {
        if (!IsSuccess)
        {
            throw new InvalidOperationException($"{Method} {Path} failed with {StatusCode}: {Body}");
        }
        return new EnsureSuccessResult(this);
    }

    public override string ToString() => $"{Method} {Path} -> {StatusCode} {Body}";
}

public readonly record struct EnsureSuccessResult(ApiResponse Response)
{
    public T As<T>() => Response.As<T>();
}
