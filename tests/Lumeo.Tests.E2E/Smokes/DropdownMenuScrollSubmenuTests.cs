using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// #520: DropdownMenuContent's built-in scrollable inner viewport (max-height capped to the
/// live <c>--lumeo-dropdown-available-height</c>) for a long menu, with the submenu
/// (<c>position: fixed</c>) still fully unclipped and correctly positioned even when opened
/// from an item deep in the scrolled list. bUnit can't drive real layout/scroll/positioning
/// JS, so this drives the real "Long Menu (Built-in Scroll)" demo on
/// <c>/components/dropdown-menu</c> in a real browser. Requires the docs dev-server (see
/// project README.md).
/// </summary>
public class DropdownMenuScrollSubmenuTests : PlaywrightTestBase
{
    private async Task OpenLongMenu()
    {
        await Goto("/components/dropdown-menu");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // DropdownMenuTrigger renders a role="button" wrapper around the Button's own
        // <button> — both match an accessible-name query for "40 Items", so disambiguate
        // with .First (same pattern as PopoverAnchorContainingBlockTests).
        var trigger = Page.GetByRole(AriaRole.Button, new() { Name = "40 Items" }).First;
        await trigger.ClickAsync();

        var menu = Page.Locator("[data-slot='dropdown-menu-content']");
        await menu.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
    }

    [Fact]
    public async Task Long_Menu_Viewport_Scrolls_Instead_Of_Overflowing_The_Panel()
    {
        await OpenLongMenu();

        var viewport = Page.Locator("[data-slot='dropdown-menu-viewport']");
        await viewport.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

        var (scrollHeight, clientHeight) = await viewport.EvaluateAsync<int[]>(
            "el => [el.scrollHeight, el.clientHeight]") is { } dims
            ? (dims[0], dims[1])
            : (0, 0);

        Assert.True(scrollHeight > clientHeight,
            $"viewport should need to scroll: scrollHeight={scrollHeight}, clientHeight={clientHeight}");

        // The panel itself must not have grown to fit all 40 items — that's exactly the
        // overflow this feature caps.
        var panelBox = await Page.Locator("[data-slot='dropdown-menu-content']").BoundingBoxAsync();
        Assert.NotNull(panelBox);
        var viewportBox = await Page.EvaluateAsync<int[]>("[window.innerWidth, window.innerHeight]");
        Assert.True(panelBox!.Height <= viewportBox[1],
            $"panel height {panelBox.Height} should not exceed the browser viewport {viewportBox[1]}");
    }

    [Fact]
    public async Task Submenu_Opened_From_An_Item_Deep_In_The_Scrolled_List_Is_Fully_Visible()
    {
        await OpenLongMenu();

        // Item 35 renders as a DropdownMenuSubTrigger labelled "Item 35 (submenu)" — scroll
        // it into view inside the inner viewport first (it's below the default fold).
        var subTrigger = Page.GetByText("Item 35 (submenu)");
        await subTrigger.ScrollIntoViewIfNeededAsync();
        await subTrigger.HoverAsync();

        var subContent = Page.Locator("[data-slot='dropdown-menu-sub-content']");
        await subContent.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

        var subBox = await subContent.BoundingBoxAsync();
        Assert.NotNull(subBox);
        var viewportSize = await Page.EvaluateAsync<int[]>("[window.innerWidth, window.innerHeight]");

        // Fully visible: entirely within the browser viewport on every edge, not clipped by
        // any ancestor (the whole point of keeping the submenu position:fixed and the new
        // scroll wrapper overflow-y:auto rather than overflow-hidden).
        Assert.True(subBox!.X >= 0, $"submenu left edge {subBox.X} should be within the viewport");
        Assert.True(subBox.Y >= 0, $"submenu top edge {subBox.Y} should be within the viewport");
        Assert.True(subBox.X + subBox.Width <= viewportSize[0] + 1,
            $"submenu right edge {subBox.X + subBox.Width} should be within the viewport width {viewportSize[0]}");
        Assert.True(subBox.Y + subBox.Height <= viewportSize[1] + 1,
            $"submenu bottom edge {subBox.Y + subBox.Height} should be within the viewport height {viewportSize[1]}");

        // And its own item is actually visible/reachable.
        var subItem = Page.GetByText("Nested action");
        Assert.True(await subItem.IsVisibleAsync());
    }
}
