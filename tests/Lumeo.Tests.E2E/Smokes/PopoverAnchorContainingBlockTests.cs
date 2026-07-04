using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Regression for the positionFixed containing-block double-fold (components.js
/// <c>compensateContainingBlock</c>) — the user-reported "Add group level menu
/// opens far to the LEFT of its button" on the SaaS demo's customers grid.
///
/// The DataGrid group panel's "Add group level" DropdownMenu is a
/// <c>position:fixed</c> overlay, and the demo wraps the page region in a
/// blur-fade (<c>will-change:transform</c>), which establishes a containing block
/// for fixed descendants. positionFixed's idempotence guard compared
/// <c>parseFloat(style.left)</c> against the in-memory folded number; the CSSOM
/// re-serialises a written length at slightly different precision, so the
/// already-folded value read back as a fresh viewport intent and got folded a
/// SECOND time within one update() — landing the menu one full containing-block
/// offset (~280px, the sidebar width) to the LEFT of its trigger. The fix keys
/// idempotence on the serialized string actually written.
///
/// This drives the real component stack on the real demo page, so reverting the
/// fix pushes the measured offset from ~0 back to the container offset and the
/// assertion fails (not tautological). Requires the docs dev-server.
/// </summary>
public class PopoverAnchorContainingBlockTests : PlaywrightTestBase
{
    [Fact]
    public async Task AddGroupLevel_Menu_Opens_Anchored_To_Its_Trigger()
    {
        await Goto("/demos/saas");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Switch to the Customers view (the grid with the group panel lives there).
        await Page.GetByRole(AriaRole.Button, new() { Name = "Customers" }).First.ClickAsync();

        // The "Add group level" trigger renders only after the Blazor WASM runtime
        // boots and the customers grid + group panel mount — give it headroom.
        var trigger = Page.Locator("[data-slot='datagrid-group-add-trigger']").First;
        await trigger.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 20000 });
        await trigger.ScrollIntoViewIfNeededAsync();
        var triggerBox = await trigger.BoundingBoxAsync();
        Assert.NotNull(triggerBox);

        await trigger.ClickAsync();

        var menu = Page.Locator("[role='menu']").Last;
        await menu.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        var menuBox = await menu.BoundingBoxAsync();
        Assert.NotNull(menuBox);

        // Align=Start ⇒ the menu's left edge sits on the trigger's left edge.
        // Fixed: ~0px. Pre-fix double-fold: one full containing-block offset (~280px).
        var horizontalOffset = System.Math.Abs(menuBox!.X - triggerBox!.X);
        Assert.True(horizontalOffset < 40,
            $"Add-group-level menu opened {horizontalOffset:F0}px off its trigger (containing-block double-fold regression)");

        // And it drops BELOW the trigger, not floating above/over it.
        Assert.True(menuBox.Y >= triggerBox.Y - 4,
            $"menu.top {menuBox.Y:F0} should be at/below trigger.top {triggerBox.Y:F0}");
    }
}
