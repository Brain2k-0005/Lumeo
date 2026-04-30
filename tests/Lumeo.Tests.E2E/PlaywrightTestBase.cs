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
