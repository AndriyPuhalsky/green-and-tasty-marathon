using System.Text;
using Allure.Net.Commons;
using Reqnroll;
using RestaurantAqa.Screenplay.Abilities;
using RestaurantAqa.Support;

namespace RestaurantAqa.StepDefinitions;

[Binding]
public sealed class Hooks(GroupOrderWorld world, ScenarioContext scenario, FeatureContext feature)
{
    [BeforeTestRun]
    public static async Task PrepareStand()
    {
        Console.WriteLine($"Run {UniqueData.RunId}: UI {TestSettings.Current.BaseUrl}, API {TestSettings.Current.ApiUrl}");
        await Stand.Prepare();
        Console.WriteLine($"Location {Stand.Current.Location.Id}, {Stand.Current.Dishes.Count} dishes, waiter ready");
    }

    [AfterTestRun]
    public static async Task TearDownStand()
    {
        Console.WriteLine($"Requests repeated because the stand was overloaded: {CallTheApi.Retries}");
        await Stand.TearDown();
        await BrowserHost.Stop();
    }

    [BeforeScenario(Order = 0)]
    public void NameArtifactsFolder()
    {
        string title = scenario.ScenarioInfo.Title;
        string args = string.Join("-", scenario.ScenarioInfo.Arguments.Values.Cast<object>());
        string slug = Slug($"{feature.FeatureInfo.Title}-{title}-{args}");
        world.ArtifactsDirectory = Path.Combine(TestSettings.Current.ResultsDirectory, "artifacts",
            $"{slug[..Math.Min(slug.Length, 90)]}-{Guid.NewGuid().ToString("N")[..6]}");
    }

    [BeforeScenario("waiter")]
    public void UseWaiterDesk() => world.AtWaiterDesk = true;

    [AfterScenario(Order = 0)]
    public async Task CollectEvidenceAndCleanUp()
    {
        bool failed = scenario.TestError is not null;

        foreach (var actor in world.Cast.Actors)
        {
            if (!actor.Has<BrowseTheWeb>())
            {
                continue;
            }

            var files = await actor.AbilityTo<BrowseTheWeb>().Close(failed);
            foreach (string file in files)
            {
                AllureApi.AddAttachment(Path.GetFileName(file), ContentType(file), file);
            }
        }

        if (failed && !world.Journal.IsEmpty)
        {
            string log = world.Journal.ToString();
            Directory.CreateDirectory(world.ArtifactsDirectory);
            await File.WriteAllTextAsync(Path.Combine(world.ArtifactsDirectory, "api-log.txt"), log);
            AllureApi.AddAttachment("api-log.txt", "text/plain", Encoding.UTF8.GetBytes(log), ".txt");
        }

        var problems = await world.Tracker.CleanUp();
        foreach (string problem in problems)
        {
            Console.WriteLine($"[cleanup] {problem}");
        }

        await world.Cast.DisposeAsync();
    }

    private static string ContentType(string file) => Path.GetExtension(file) switch
    {
        ".png" => "image/png",
        ".zip" => "application/zip",
        ".webm" => "video/webm",
        _ => "text/plain",
    };

    private static string Slug(string text)
        => string.Join("-", new string(text.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
