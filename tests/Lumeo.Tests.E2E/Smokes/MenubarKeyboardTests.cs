using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Real-browser menu behaviour for Menubar: clicking a top-level trigger opens its
/// menu (role=menu) and Escape closes it. bUnit can't run the menu's JS positioning /
/// outside-click / focus wiring, so the open→close round-trip is verified here.
///
/// Requires the docs dev-server. See project README.md.
/// </summary>
public class MenubarKeyboardTests : PlaywrightTestBase
{
    [Fact]
    public async Task Menubar_opens_a_menu_on_click_and_closes_on_escape()
    {
        await Goto("/components/menubar");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Open the "File" menu.
        await Page.GetByText("File", new() { Exact = true }).First.ClickAsync();

        var menu = Page.Locator("[role=menu]").First;
        await menu.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.True(await menu.IsVisibleAsync());

        // Escape closes it.
        await Page.Keyboard.PressAsync("Escape");
        await menu.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 3000 });
        Assert.False(await menu.IsVisibleAsync());
    }
}
