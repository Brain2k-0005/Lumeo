using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Verifies DropdownMenu keyboard navigation using real browser events.
///
/// bUnit cannot test:
/// - Arrow key navigation between menu items
/// - Enter activating the focused item
/// - Escape dismissing the menu without activation
///
/// Requires the docs dev-server to be running. See project README.md.
/// </summary>
public class DropdownKeyboardTests : PlaywrightTestBase
{
    [Fact]
    public async Task DropdownMenu_opens_on_trigger_click()
    {
        await Goto("/components/dropdown-menu");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var trigger = Page.Locator("[data-testid='dropdown-open-trigger']").First;
        await trigger.ClickAsync();

        // The dropdown menu content has role='menu'
        var menu = Page.Locator("[role='menu']").First;
        await menu.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.True(await menu.IsVisibleAsync());
    }

    [Fact]
    public async Task DropdownMenu_closes_on_escape()
    {
        await Goto("/components/dropdown-menu");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var trigger = Page.Locator("[data-testid='dropdown-open-trigger']").First;
        await trigger.ClickAsync();

        var menu = Page.Locator("[role='menu']").First;
        await menu.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

        await Page.Keyboard.PressAsync("Escape");

        await menu.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 3000 });
        Assert.False(await menu.IsVisibleAsync());
    }

    [Fact]
    public async Task DropdownMenu_arrow_keys_move_focus()
    {
        await Goto("/components/dropdown-menu");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var trigger = Page.Locator("[data-testid='dropdown-open-trigger']").First;
        await trigger.ClickAsync();

        var menu = Page.Locator("[role='menu']").First;
        await menu.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

        // Arrow down should move focus to the first item
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("ArrowDown");

        // After two arrow-downs, some item inside the menu should be focused
        var focusedIsInsideMenu = await Page.EvaluateAsync<bool>(
            "() => document.querySelector('[role=\"menu\"]').contains(document.activeElement)");

        Assert.True(focusedIsInsideMenu,
            "Arrow key navigation did not move focus inside the dropdown menu.");
    }
}
