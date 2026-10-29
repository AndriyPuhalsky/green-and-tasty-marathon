using System.Collections.Concurrent;
using System.Text;

namespace RestaurantAqa.Api;

/// <summary>Журнал запитів сценарію. Якщо сценарій упав, журнал іде у звіт як вкладення.</summary>
public sealed class ApiJournal
{
    private readonly ConcurrentQueue<string> _entries = new();

    public void Record(string actor, HttpMethod method, string path, string? requestBody, ApiResponse response, TimeSpan elapsed)
    {
        var entry = new StringBuilder()
            .Append($"[{DateTime.UtcNow:HH:mm:ss.fff}] {actor}: {method} {path} -> {response.StatusCode} ({elapsed.TotalMilliseconds:F0} ms)");
        if (!string.IsNullOrEmpty(requestBody))
        {
            entry.Append("\n  request:  ").Append(Shorten(requestBody));
        }
        entry.Append("\n  response: ").Append(Shorten(response.Body));
        _entries.Enqueue(entry.ToString());
    }

    public bool IsEmpty => _entries.IsEmpty;

    public override string ToString() => string.Join("\n\n", _entries);

    private static string Shorten(string text) => text.Length > 2000 ? text[..2000] + "…" : text;
}
