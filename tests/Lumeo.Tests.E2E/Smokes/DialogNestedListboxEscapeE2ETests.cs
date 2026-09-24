using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Real-browser coverage for the Escape-isolation fix (field report, present since
/// 5.10.0): a Select/DropdownMenu/Combobox opened from inside a Dialog must close only
/// that inner control on Escape, not the Dialog itself; a second Escape then closes the
/// Dialog. bUnit already covers the mechanism (see
/// Lumeo.Tests/Components/Dialog/DialogNestedListboxEscapeTests.cs) but cannot open the
/// portaled, position:fixed popovers the way a real browser does. Exercises the demo
/// added to the Dialog docs page ("Nested overlay Escape isolation").
/// Requires the docs dev-server (see project README.md).
/// </summary>
public class DialogNestedListboxEscapeE2ETests : PlaywrightTestBase
{
    private async Task<ILocator> OpenDemoDialog()
    {
        await Goto("/components/dialog");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var trigger = Page.Locator("[data-testid='dialog-nested-listbox-trigger']");
        await trigger.ClickAsync();

        var dialog = Page.Locator("[role='dialog'][aria-modal='true']");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        return dialog;
    }

    [Fact]
    public async Task Escape_On_An_Open_Select_Inside_The_Dialog_Closes_Only_The_Select()
    {
        var dialog = await OpenDemoDialog();

        var selectTrigger = Page.Locator("[data-testid='dialog-nested-select'] button[role='combobox']");
        await selectTrigger.ClickAsync();

        var listbox = Page.Locator("[role='listbox']");
        await listbox.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

        await Page.Keyboard.PressAsync("Escape");

        await listbox.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 3000 });
        Assert.True(await dialog.IsVisibleAsync(), "Escape closing the Select must not also close the Dialog.");

        // A second Escape, with nothing left open inside, closes the Dialog.
        await Page.Keyboard.PressAsync("Escape");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 3000 });
    }

    [Fact]
    public async Task Escape_On_An_Open_DropdownMenu_Inside_The_Dialog_Closes_Only_The_Menu()
    {
        var dialog = await OpenDemoDialog();

        var menuTrigger = Page.Locator("[data-testid='dialog-nested-dropdown']");
        await menuTrigger.ClickAsync();

        var menu = Page.Locator("[role='menu']");
        await menu.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

        await Page.Keyboard.PressAsync("Escape");

        await menu.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 3000 });
        Assert.True(await dialog.IsVisibleAsync(), "Escape closing the DropdownMenu must not also close the Dialog.");

        await Page.Keyboard.PressAsync("Escape");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 3000 });
    }

    [Fact]
    public async Task Escape_On_An_Open_Combobox_Inside_The_Dialog_Closes_Only_The_Combobox()
    {
        var dialog = await OpenDemoDialog();

        var comboboxInput = Page.Locator("[data-testid='dialog-nested-combobox']");
        await comboboxInput.ClickAsync();

        var listbox = Page.Locator("[role='listbox']");
        await listbox.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

        await Page.Keyboard.PressAsync("Escape");

        await listbox.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 3000 });
        Assert.True(await dialog.IsVisibleAsync(), "Escape closing the Combobox must not also close the Dialog.");

        // ComboboxInput's stopPropagation is a static "true" (unconditional), not gated on
        // whether the dropdown is open — a dynamic per-render gate looked right and passed
        // in bUnit, but a live run against this exact page is what proved it did NOT
        // reliably stop the event reaching the Dialog's own Escape handler in Blazor WASM.
        // Accepted trade-off: focus never left the input, but a second Escape here is
        // absorbed rather than bubbling to the Dialog — still resolvable via the close
        // button or backdrop click.
        await Page.Keyboard.PressAsync("Escape");
        await Page.WaitForTimeoutAsync(300);
        Assert.True(await dialog.IsVisibleAsync(), "A second Escape on the closed-but-focused Combobox input should not close the Dialog either (accepted trade-off).");
    }
}
