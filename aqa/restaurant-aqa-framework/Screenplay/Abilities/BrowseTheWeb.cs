using Microsoft.Playwright;
using RestaurantAqa.Screenplay.Core;
using RestaurantAqa.Support;

namespace RestaurantAqa.Screenplay.Abilities;

/// <summary>Власний браузерний контекст актора: окремі cookies і localStorage, свій trace і відео.</summary>
public sealed class BrowseTheWeb : IDisposableAbility
{
    private readonly string _actorName;
    private readonly string _artifactsDir;
    private IBrowserContext? _context;
    private IPage? _page;
    private bool _holdsSession;

    private BrowseTheWeb(string actorName, string artifactsDir)
    {
        _actorName = actorName;
        _artifactsDir = artifactsDir;
    }

    public static BrowseTheWeb For(string actorName, string artifactsDir) => new(actorName, artifactsDir);

    public string BaseUrl => TestSettings.Current.BaseUrl;

    public async Task<IPage> Page()
    {
        if (_page is not null)
        {
            return _page;
        }

        var settings = TestSettings.Current;
        await BrowserHost.Sessions.WaitAsync();
        _holdsSession = true;

        var browser = await BrowserHost.GetBrowser();
        var options = new BrowserNewContextOptions
        {
            BaseURL = settings.BaseUrl,
            Locale = "en-US",
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
        };
        if (settings.Video != ArtifactMode.Off)
        {
            options.RecordVideoDir = Path.Combine(_artifactsDir, "video-tmp");
            options.RecordVideoSize = new RecordVideoSize { Width = 1280, Height = 800 };
        }

        _context = await browser.NewContextAsync(options);
        _context.SetDefaultTimeout(settings.UiTimeoutMs);
        _context.SetDefaultNavigationTimeout(settings.UiTimeoutMs * 2);

        if (settings.Trace != ArtifactMode.Off)
        {
            await _context.Tracing.StartAsync(new TracingStartOptions
            {
                Title = _actorName,
                Screenshots = true,
                Snapshots = true,
                Sources = false,
            });
        }

        _page = await _context.NewPageAsync();
        return _page;
    }

    public bool IsOpen => _page is not null;

    /// <summary>Закриває браузер і повертає файли, які варто додати у звіт.</summary>
    public async Task<IReadOnlyList<string>> Close(bool scenarioFailed)
    {
        var artifacts = new List<string>();
        if (_context is null)
        {
            return artifacts;
        }

        var settings = TestSettings.Current;
        bool keepAnything = scenarioFailed || settings.Trace == ArtifactMode.On || settings.Video == ArtifactMode.On;
        if (keepAnything)
        {
            Directory.CreateDirectory(_artifactsDir);
        }
        string prefix = Path.Combine(_artifactsDir, Slug(_actorName));

        try
        {
            if (scenarioFailed && _page is not null && !_page.IsClosed)
            {
                string screenshot = prefix + "-failure.png";
                await _page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshot, FullPage = true });
                artifacts.Add(screenshot);
            }

            if (settings.Trace != ArtifactMode.Off)
            {
                bool keepTrace = settings.Trace == ArtifactMode.On || scenarioFailed;
                string? tracePath = keepTrace ? prefix + "-trace.zip" : null;
                await _context.Tracing.StopAsync(new TracingStopOptions { Path = tracePath });
                if (tracePath is not null)
                {
                    artifacts.Add(tracePath);
                }
            }

            var video = _page?.Video;
            await _context.CloseAsync();

            if (video is not null)
            {
                bool keepVideo = settings.Video == ArtifactMode.On || scenarioFailed;
                string recorded = await video.PathAsync();
                if (keepVideo)
                {
                    string target = prefix + "-video.webm";
                    File.Move(recorded, target, overwrite: true);
                    artifacts.Add(target);
                }
                else
                {
                    File.Delete(recorded);
                }
                DeleteIfEmpty(Path.GetDirectoryName(recorded));
                DeleteIfEmpty(_artifactsDir);
            }
        }
        finally
        {
            _context = null;
            _page = null;
            ReleaseSession();
        }

        return artifacts;
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null)
        {
            await Close(scenarioFailed: false);
        }
        ReleaseSession();
    }

    private void ReleaseSession()
    {
        if (_holdsSession)
        {
            _holdsSession = false;
            BrowserHost.Sessions.Release();
        }
    }

    private static void DeleteIfEmpty(string? directory)
    {
        if (directory is not null && Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }
    }

    private static string Slug(string text)
        => new string(text.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray()).Trim('-');
}
