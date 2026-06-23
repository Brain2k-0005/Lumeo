using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Real-browser focus-trap + Escape behaviour for the modal overlays — the exact
/// failure modes bUnit can't reach (it fakes focus and never runs the JS focus-trap).
/// Extends the existing Dialog coverage to Drawer, Sheet and Popover.
///
/// Requires the docs dev-server. See project README.md.
/// </summary>
public class OverlayFocusTrapTests : PlaywrightTestBase
{
    private async Task<ILocator> OpenModal(string route, string triggerName)
    {
        await Goto(route);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.GetByRole(AriaRole.Button, new() { Name = triggerName }).First.ClickAsync();
        var overlay = Page.Locator("[role=dialog][aria-modal=true]").First;
        await overlay.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        return overlay;
    }

    [Theory]
    [InlineData("/components/drawer", "Open Drawer")]
    [InlineData("/components/sheet", "Open Sheet")]
    public async Task Modal_overlay_traps_focus_and_closes_on_escape(string route, string trigger)
    {
        var overlay = await OpenModal(route, trigger);

        // Tab through several elements — focus must stay inside the overlay, never
        // escape to the page behind it.
        for (var i = 0; i < 12; i++)
            await Page.Keyboard.PressAsync("Tab");

        var focusInside = await Page.EvaluateAsync<bool>(
            "() => document.querySelector('[role=dialog][aria-modal=true]')?.contains(document.activeElement) ?? false");
        Assert.True(focusInside, $"{route}: focus escaped the modal — focus trap is not working.");

        // Escape closes it.
        await Page.Keyboard.PressAsync("Escape");
        await overlay.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 3000 });
        Assert.False(await overlay.IsVisibleAsync());
    }

    [Fact]
    public async Task Popover_opens_and_closes_on_escape()
    {
        await Goto("/components/popover");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Open Popover" }).First.ClickAsync();

        var content = Page.Locator("[role=dialog]").First;
        await content.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.True(await content.IsVisibleAsync());

        await Page.Keyboard.PressAsync("Escape");
        await content.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 3000 });
        Assert.False(await content.IsVisibleAsync());
    }
}
