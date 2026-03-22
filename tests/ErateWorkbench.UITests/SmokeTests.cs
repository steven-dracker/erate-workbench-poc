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
        // Increased modestly for CI: dropdown interaction requires JS execution after click.
        page.SetDefaultTimeout(15_000);
        return page;
    }

    /// <summary>
    /// Waits for a dropdown item link to become visible after its parent dropdown has been
    /// opened. On failure, dumps the page HTML to the console to aid CI debugging.
    /// </summary>
    private static async Task VerifyDropdownLinkAsync(IPage page, string linkName)
    {
        // Scope to the navbar to avoid ambiguity with any matching text in page content.
        var locator = page.Locator("nav.navbar")
                          .GetByRole(AriaRole.Link, new() { Name = linkName, Exact = false });

        try
        {
            await locator.WaitForAsync(new LocatorWaitForOptions
            {
                State   = WaitForSelectorState.Visible,
                Timeout = 5_000,
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SMOKE] Dropdown link '{linkName}' not visible. {ex.Message}");
            Console.WriteLine(await page.ContentAsync());
            throw;
        }
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
        // Verifies the main entry point renders inside the shared layout with the
        // grouped navigation introduced in CC-ERATE-000037.
        var page = await NewPageAsync();
        var response = await page.GotoAsync(_baseUrl);

        Assert.NotNull(response);
        Assert.Equal(200, response.Status);

        // Shared layout appends " — ERATE Workbench" to every page title.
        var title = await page.TitleAsync();
        Assert.Contains("ERATE Workbench", title, StringComparison.OrdinalIgnoreCase);

        // Navbar container must be present.
        await page.WaitForSelectorAsync("nav.navbar");

        // Dashboard is a standalone top-level link (not inside a dropdown).
        await page.GetByRole(AriaRole.Link, new() { Name = "Dashboard", Exact = true })
                  .WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });

        // The four dropdown group toggles are rendered as <a role="button"> elements.
        // They are always visible without interaction — verify each is present.
        foreach (var toggle in new[] { "Explore", "Insights", "Reference", "Help" })
        {
            await page.GetByRole(AriaRole.Button, new() { Name = toggle, Exact = true })
                      .WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        }
    }

    [Fact]
    public async Task Navigation_Links_Are_All_Present()
    {
        // Verifies the full nav link set across all grouped dropdowns (CC-ERATE-000037).
        // Step 1 checks top-level visible items; Step 2 opens each dropdown and checks its links.
        var page = await NewPageAsync();
        await page.GotoAsync(_baseUrl);
        await page.WaitForSelectorAsync("nav.navbar");

        // ── Step 1 — top-level nav items (always visible) ────────────────────

        await page.GetByRole(AriaRole.Link, new() { Name = "Dashboard", Exact = true })
                  .WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });

        foreach (var toggle in new[] { "Explore", "Insights", "Reference", "Help" })
        {
            await page.GetByRole(AriaRole.Button, new() { Name = toggle, Exact = true })
                      .WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        }

        // ── Step 2 — Explore dropdown ─────────────────────────────────────────

        await page.GetByRole(AriaRole.Button, new() { Name = "Explore", Exact = true })
                  .ClickAsync();

        await VerifyDropdownLinkAsync(page, "School & Library Search");
        await VerifyDropdownLinkAsync(page, "Analytics");
        await VerifyDropdownLinkAsync(page, "Filing Window");

        // ── Step 3 — Insights dropdown ────────────────────────────────────────

        // Clicking a new toggle closes the previous dropdown (Bootstrap behaviour).
        await page.GetByRole(AriaRole.Button, new() { Name = "Insights", Exact = true })
                  .ClickAsync();

        await VerifyDropdownLinkAsync(page, "Risk Insights");
        await VerifyDropdownLinkAsync(page, "Advisor Playbook");

        // ── Step 4 — Reference dropdown ───────────────────────────────────────

        await page.GetByRole(AriaRole.Button, new() { Name = "Reference", Exact = true })
                  .ClickAsync();

        await VerifyDropdownLinkAsync(page, "Program Workflow");
        await VerifyDropdownLinkAsync(page, "Ecosystem");
        await VerifyDropdownLinkAsync(page, "History");

        // ── Step 5 — Help dropdown ────────────────────────────────────────────

        await page.GetByRole(AriaRole.Button, new() { Name = "Help", Exact = true })
                  .ClickAsync();

        await VerifyDropdownLinkAsync(page, "About");
        await VerifyDropdownLinkAsync(page, "Release Notes");
        // "Swagger UI ↗" — matched with Exact = false to avoid arrow-character sensitivity.
        await VerifyDropdownLinkAsync(page, "Swagger UI");
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
