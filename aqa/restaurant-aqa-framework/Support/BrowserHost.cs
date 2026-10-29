using Microsoft.Playwright;

namespace RestaurantAqa.Support;

/// <summary>Один Playwright і один Chromium на весь прогін; кожен актор отримує свій BrowserContext.</summary>
public static class BrowserHost
{
    private static readonly SemaphoreSlim StartLock = new(1, 1);
    private static IPlaywright? _playwright;
    private static IBrowser? _browser;

    // обмежує кількість одночасно відкритих браузерних сесій
    public static SemaphoreSlim Sessions { get; } = new(TestSettings.Current.UiMaxSessions, TestSettings.Current.UiMaxSessions);

    public static async Task<IBrowser> GetBrowser()
    {
        if (_browser is not null)
        {
            return _browser;
        }

        await StartLock.WaitAsync();
        try
        {
            if (_browser is null)
            {
                var settings = TestSettings.Current;
                Assertions.SetDefaultExpectTimeout(settings.ExpectTimeoutMs);
                _playwright = await Playwright.CreateAsync();
                _playwright.Selectors.SetTestIdAttribute("data-testid");
                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = settings.Headless,
                    SlowMo = settings.SlowMoMs,
                });
            }
            return _browser;
        }
        finally
        {
            StartLock.Release();
        }
    }

    public static async Task Stop()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }
        _playwright?.Dispose();
        _playwright = null;
    }
}
