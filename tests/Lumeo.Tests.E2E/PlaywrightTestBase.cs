using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E;

/// <summary>
/// Abstract base class for all Lumeo Playwright tests.
///
/// Lifecycle: creates one Chromium browser + page per test class instance
/// (xUnit creates a new class instance per [Fact]/[Theory], so each test gets
/// a fresh page). Override <see cref="InitializeAsync"/> to add test-specific
/// setup after calling base.
///
/// Base URL: defaults to <c>http://localhost:5287</c> (the Lumeo.Docs dev-server
/// default port). Override with the <c>LUMEO_E2E_BASE_URL</c> environment variable
/// so CI can point at a different host or port.
/// </summary>
public abstract class PlaywrightTestBase : IAsyncLifetime
{
    protected IPlaywright Playwright = default!;
    protected IBrowser Browser = default!;
    protected IPage Page = default!;

    protected string BaseUrl { get; } =
        Environment.GetEnvironmentVariable("LUMEO_E2E_BASE_URL")
        ?? "http://localhost:5287";

    public virtual async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });
        Page = await Browser.NewPageAsync();

        // Pre-seed the consent state so the GDPR banner doesn't render and steal
        // role="dialog" / Escape key focus from the components under test. The
        // ConsentService treats any non-empty dictionary as "user has decided",
        // so a simple { analytics: false } entry is enough to suppress the banner.
        // Also seeds a deterministic theme so visual snapshots are stable.
        await Page.AddInitScriptAsync(@"
            try {
                localStorage.setItem('lumeo:consent:v1', JSON.stringify({
                    analytics: false,
                    marketing: false,
                }));
                localStorage.setItem('theme-mode', 'light');
            } catch (e) { /* localStorage may be blocked in some contexts */ }
        ");
    }

    public virtual async Task DisposeAsync()
    {
        await Page.CloseAsync();
        await Browser.CloseAsync();
        Playwright.Dispose();
    }

    /// <summary>
    /// Navigates to <paramref name="path"/> relative to <see cref="BaseUrl"/> and
    /// returns the Playwright response.
    /// </summary>
    protected Task<IResponse?> Goto(string path)
        => Page.GotoAsync($"{BaseUrl}{path}");
}
