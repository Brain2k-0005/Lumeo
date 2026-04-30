using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Verifies the Ctrl+K search palette opens, accepts input, and navigates.
///
/// This test covers the JS-side keyboard shortcut (docs.js) and the
/// Blazor-side search result rendering — neither of which bUnit can exercise.
///
/// Requires the docs dev-server to be running. See project README.md.
/// </summary>
public class SearchPaletteTests : PlaywrightTestBase
{
    [Fact]
    public async Task Search_palette_opens_with_ctrl_k()
    {
        await Goto("/components");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Trigger the search palette via Ctrl+K (docs.js listens for this)
        await Page.Keyboard.PressAsync("Control+k");

        // The search input has placeholder "Search components, patterns..."
        var searchInput = Page.Locator("input[placeholder*='Search']");
        await searchInput.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000,
        });

        Assert.True(await searchInput.IsVisibleAsync(),
            "Search palette did not open after Ctrl+K.");
    }

    [Fact]
    public async Task Search_palette_returns_results_for_button()
    {
        await Goto("/components");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.Keyboard.PressAsync("Control+k");

        var searchInput = Page.Locator("input[placeholder*='Search']");
        await searchInput.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000,
        });

        // Type "button" and wait for results
        await searchInput.FillAsync("button");

        // Results should appear — look for any element containing "Button"
        var firstResult = Page.Locator("text=Button").First;
        await firstResult.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000,
        });

        Assert.True(await firstResult.IsVisibleAsync(),
            "No 'Button' result appeared after typing 'button' in the search palette.");
    }

    [Fact]
    public async Task Search_palette_closes_on_escape()
    {
        await Goto("/components");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.Keyboard.PressAsync("Control+k");

        var searchInput = Page.Locator("input[placeholder*='Search']");
        await searchInput.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000,
        });

        await Page.Keyboard.PressAsync("Escape");

        await searchInput.WaitForAsync(new()
        {
            State = WaitForSelectorState.Hidden,
            Timeout = 3000,
        });

        Assert.False(await searchInput.IsVisibleAsync(),
            "Search palette did not close after pressing Escape.");
    }

    [Fact]
    public async Task Search_palette_navigates_to_component_on_click()
    {
        await Goto("/components");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.Keyboard.PressAsync("Control+k");

        var searchInput = Page.Locator("input[placeholder*='Search']");
        await searchInput.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000,
        });

        await searchInput.FillAsync("badge");

        // Wait for Badge result
        var badgeResult = Page.Locator("text=Badge").First;
        await badgeResult.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000,
        });

        await badgeResult.ClickAsync();

        // Should navigate to /components/badge
        await Page.WaitForURLAsync("**/components/badge", new() { Timeout = 5000 });
        Assert.Contains("/components/badge", Page.Url);
    }
}
