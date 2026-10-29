namespace RestaurantAqa.Support;

/// <summary>
/// Налаштування прогону. Джерела за пріоритетом: змінні оточення, потім файл .env у корені фреймворку.
/// </summary>
public sealed class TestSettings
{
    private static readonly Lazy<TestSettings> Instance = new(Load);

    public static TestSettings Current => Instance.Value;

    public required string BaseUrl { get; init; }
    public required string ApiUrl { get; init; }
    public required string EmailPrefix { get; init; }
    public required string EmailDomain { get; init; }
    public bool Headless { get; init; }
    public float SlowMoMs { get; init; }
    public float UiTimeoutMs { get; init; }
    public float ExpectTimeoutMs { get; init; }
    public ArtifactMode Trace { get; init; }
    public ArtifactMode Video { get; init; }
    public int UiMaxSessions { get; init; }
    public int Parallelism { get; init; }
    public required string FrameworkRoot { get; init; }

    public string ResultsDirectory => Path.Combine(FrameworkRoot, "TestResults");

    private static TestSettings Load()
    {
        string root = FindFrameworkRoot();
        var fromFile = ReadDotEnv(Path.Combine(root, ".env"));

        string Get(string key, string? fallback = null)
        {
            string? value = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                fromFile.TryGetValue(key, out value);
            }
            if (string.IsNullOrWhiteSpace(value))
            {
                value = fallback;
            }
            return value ?? throw new InvalidOperationException(
                $"Setting {key} is missing. Copy .env.example to .env or pass {key} as an environment variable.");
        }

        return new TestSettings
        {
            FrameworkRoot = root,
            BaseUrl = Get("BASE_URL").TrimEnd('/'),
            ApiUrl = Get("API_URL").TrimEnd('/'),
            EmailPrefix = Get("TEST_EMAIL_PREFIX", "aqa"),
            EmailDomain = Get("TEST_EMAIL_DOMAIN", "example.com"),
            Headless = bool.Parse(Get("HEADLESS", "true")),
            SlowMoMs = float.Parse(Get("SLOW_MO_MS", "0")),
            UiTimeoutMs = float.Parse(Get("UI_TIMEOUT_MS", "20000")),
            ExpectTimeoutMs = float.Parse(Get("EXPECT_TIMEOUT_MS", "10000")),
            Trace = ParseMode(Get("TRACE", "retain-on-failure")),
            Video = ParseMode(Get("VIDEO", "off")),
            UiMaxSessions = int.Parse(Get("UI_MAX_SESSIONS", "2")),
            Parallelism = int.Parse(Get("TEST_PARALLELISM", "3")),
        };
    }

    private static ArtifactMode ParseMode(string value) => value.Trim().ToLowerInvariant() switch
    {
        "on" => ArtifactMode.On,
        "off" => ArtifactMode.Off,
        "retain-on-failure" => ArtifactMode.RetainOnFailure,
        _ => throw new InvalidOperationException($"Unknown artifact mode '{value}', use on, off or retain-on-failure."),
    };

    private static string FindFrameworkRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "RestaurantAqa.csproj")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }

    private static Dictionary<string, string> ReadDotEnv(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
        {
            return values;
        }

        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim().Trim('"', '\'');
            values[key] = value;
        }
        return values;
    }
}

public enum ArtifactMode
{
    Off,
    On,
    RetainOnFailure,
}
