// tests/ErateWorkbench.UITests/SmokeTests.cs
//
// Happy-path UI smoke tests for ERATE Workbench.
// Verifies key surfaces are reachable and render expected structure.
//
// APP_BASE_URL environment variable overrides the default (useful for CI / port variation).
// Default: http://localhost:5000

namespace ErateWorkbench.UITests;

public class SmokeTests : IAsyncLifetime
{
    private readonly string _baseUrl = Environment.GetEnvironmentVariable("APP_BASE_URL")
                                       ?? "http://localhost:5000";

    private IPlaywright _playwright = null!;
    private IBrowser    _browser    = null!;

    // ── setup / teardown ──────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser    = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<IPage> NewPageAsync()
    {
        var page = await _browser.NewPageAsync();
        // Reasonable timeout for a local/CI app; avoids flaky long hangs.
        page.SetDefaultTimeout(10_000);
        return page;
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Health_Endpoint_Returns_Ok()
    {
        // Verifies the /health endpoint added in CC-ERATE-000019 is reachable.
        // This is the liveness gate used by both the dev script and CI startup.
        var page = await NewPageAsync();
        var response = await page.GotoAsync($"{_baseUrl}/health");

        Assert.NotNull(response);
        Assert.Equal(200, response.Status);

        var body = await response.TextAsync();
        Assert.Contains("ok", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dashboard_Loads_With_Expected_Title_And_Nav()
    {
        // Verifies the main entry point renders inside the shared layout.
        var page = await NewPageAsync();
        var response = await page.GotoAsync(_baseUrl);

        Assert.NotNull(response);
        Assert.Equal(200, response.Status);

        // Shared layout appends " — ERATE Workbench" to every page title.
        var title = await page.TitleAsync();
        Assert.Contains("ERATE Workbench", title, StringComparison.OrdinalIgnoreCase);

        // Navbar should be present (Bootstrap .navbar class).
        await page.WaitForSelectorAsync("nav.navbar");

        // Key nav links should be rendered in the shared layout.
        await page.WaitForSelectorAsync("text=Dashboard");
        await page.WaitForSelectorAsync("text=Analytics");
        await page.WaitForSelectorAsync("text=Risk Insights");
    }

    [Fact]
    public async Task Navigation_Links_Are_All_Present()
    {
        // Verifies the full nav link set is rendered on any shared-layout page.
        var page = await NewPageAsync();
        await page.GotoAsync(_baseUrl);
        await page.WaitForSelectorAsync("nav.navbar");

        var expectedLinks = new[]
        {
            "Dashboard",
            "School & Library Search",
            "Analytics",
            "Program Workflow",
            "Advisor Playbook",
            "Risk Insights",
            "Ecosystem",
            "History",
        };

        foreach (var link in expectedLinks)
        {
            var locator = page.GetByRole(AriaRole.Link, new() { Name = link, Exact = true });
            await locator.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        }
    }

    [Fact]
    public async Task Ecosystem_Page_Loads_With_Expected_Heading()
    {
        // Ecosystem is a static reference page integrated as a shared-layout Razor Page (CC-ERATE-000014).
        var page = await NewPageAsync();
        var response = await page.GotoAsync($"{_baseUrl}/Ecosystem");

        Assert.NotNull(response);
        Assert.Equal(200, response.Status);

        var title = await page.TitleAsync();
        Assert.Contains("Ecosystem", title, StringComparison.OrdinalIgnoreCase);

        // The Ecosystem page has a visible h1 with "E-Rate Ecosystem".
        var heading = page.GetByRole(AriaRole.Heading, new() { Name = "E-Rate Ecosystem", Exact = false });
        await heading.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
    }

    [Fact]
    public async Task History_Page_Loads_With_Expected_Heading()
    {
        // History is a static reference page integrated as a shared-layout Razor Page (CC-ERATE-000014).
        // Page title is "E-Rate Central — Historical Timeline — ERATE Workbench"; the word "History"
        // does not appear in the title string, so we assert the stable shared-layout suffix instead.
        var page = await NewPageAsync();
        var response = await page.GotoAsync($"{_baseUrl}/History");

        Assert.NotNull(response);
        Assert.Equal(200, response.Status);

        // All shared-layout pages end with " — ERATE Workbench"; confirms the layout rendered.
        var title = await page.TitleAsync();
        Assert.Contains("ERATE Workbench", title, StringComparison.OrdinalIgnoreCase);

        // The page renders a visible h1 with the timeline heading.
        var heading = page.GetByRole(AriaRole.Heading, new() { Name = "E-Rate Central", Exact = false });
        await heading.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
    }
}
