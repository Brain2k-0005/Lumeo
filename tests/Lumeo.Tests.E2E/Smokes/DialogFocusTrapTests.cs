using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Verifies Dialog focus-trap behaviour using real browser interactions.
///
/// These tests cover failure modes that bUnit cannot reach:
/// - The actual JS <c>focus-trap</c> setup via <c>ComponentInteropService</c>
/// - Tab key cycling staying inside the dialog
/// - Escape key closing the dialog
///
/// Requires the docs dev-server to be running. See project README.md.
/// </summary>
public class DialogFocusTrapTests : PlaywrightTestBase
{
    [Fact]
    public async Task Dialog_opens_when_trigger_clicked()
    {
        await Goto("/components/dialog");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The first demo trigger is "Open Dialog"
        var trigger = Page.Locator("button", new() { HasText = "Open Dialog" }).First;
        await trigger.ClickAsync();

        var dialog = Page.Locator("[role='dialog']");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.True(await dialog.IsVisibleAsync());
    }

    [Fact]
    public async Task Dialog_closes_on_escape_key()
    {
        await Goto("/components/dialog");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var trigger = Page.Locator("button", new() { HasText = "Open Dialog" }).First;
        await trigger.ClickAsync();

        var dialog = Page.Locator("[role='dialog']");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

        await Page.Keyboard.PressAsync("Escape");

        // Give the close animation time to complete
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 3000 });
        Assert.False(await dialog.IsVisibleAsync());
    }

    [Fact]
    public async Task Dialog_focus_stays_inside_when_tabbing()
    {
        await Goto("/components/dialog");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var trigger = Page.Locator("button", new() { HasText = "Open Dialog" }).First;
        await trigger.ClickAsync();

        var dialog = Page.Locator("[role='dialog']");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

        // Tab through all focusable elements — after enough tabs the focus should cycle
        // back inside the dialog rather than escape to the page.
        for (var i = 0; i < 10; i++)
        {
            await Page.Keyboard.PressAsync("Tab");
        }

        // The currently focused element should be inside the dialog
        var focusedIsInsideDialog = await Page.EvaluateAsync<bool>(
            "() => document.querySelector('[role=\"dialog\"]').contains(document.activeElement)");

        Assert.True(focusedIsInsideDialog,
            "Focus escaped the dialog after tabbing — focus trap is not working.");
    }
}
