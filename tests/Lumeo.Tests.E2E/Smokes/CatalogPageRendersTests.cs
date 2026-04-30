using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Sanity-checks that the /components catalog page renders all expected cards.
///
/// This is the highest-value smoke test: if the registry, RegistryService,
/// Catalog.razor, or CatalogCard.razor break, this test will catch it
/// without needing to visit each individual component page.
///
/// Requires the docs dev-server to be running. See project README.md.
/// </summary>
public class CatalogPageRendersTests : PlaywrightTestBase
{
    /// <summary>
    /// Minimum number of catalog cards that must be present.
    /// The registry has 131 entries; some may be filtered or missing docs pages,
    /// so we use 100 as the minimum sanity threshold.
    /// </summary>
    private const int MinimumExpectedCards = 100;

    [Fact]
    public async Task Catalog_page_loads_successfully()
    {
        var response = await Goto("/components");
        Assert.NotNull(response);
        Assert.True(response.Ok, $"GET /components returned HTTP {response.Status}");
    }

    [Fact]
    public async Task Catalog_page_renders_at_least_100_cards()
    {
        await Goto("/components");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // CatalogCard renders <a href="components/{slug}"> links
        // Wait for at least one card to appear before counting
        var firstCard = Page.Locator("a[href^='components/']").First;
        await firstCard.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 15_000, // Blazor WASM may take a few seconds to hydrate
        });

        var cardCount = await Page.Locator("a[href^='components/']").CountAsync();

        Assert.True(
            cardCount >= MinimumExpectedCards,
            $"Expected at least {MinimumExpectedCards} catalog cards but found {cardCount}. " +
            $"The catalog may have broken rendering or the registry may have shrunk unexpectedly.");
    }

    [Fact]
    public async Task Catalog_page_includes_button_card()
    {
        await Goto("/components");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Button is a core component — its card must always be present
        var buttonCard = Page.Locator("a[href='components/button']");
        await buttonCard.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 15_000,
        });

        Assert.True(await buttonCard.IsVisibleAsync(),
            "Button catalog card not found — registry or catalog rendering may be broken.");
    }
}
