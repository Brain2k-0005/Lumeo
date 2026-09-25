using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// LU-20 (regression from #521 / 5.11.0, SQL Analyst field report against 5.11.1): a full-bleed
/// <c>-mx-1</c> separator inside a zero-padding <c>overflow-y-auto</c> scroll viewport forced a
/// horizontal scrollbar in EVERY DropdownMenuContent/ContextMenuContent/MenubarContent that had a
/// separator (scrollWidth 252 vs clientWidth 248 in the field report — the -mx-1 overhang plus
/// overflow-y:auto implying overflow-x:auto once overflow-x is left visible). bUnit can't measure
/// real scrollWidth/clientWidth/scrollbar presence, so this drives the real docs demos (each has at
/// least one separator) in a real browser. Requires the docs dev-server — see project README.md.
/// </summary>
public class MenuSeparatorScrollbarE2ETests : PlaywrightTestBase
{
    private static async Task AssertNoHorizontalOverflow(ILocator viewport)
    {
        await viewport.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        var (scrollWidth, clientWidth) = await viewport.EvaluateAsync<int[]>(
            "el => [el.scrollWidth, el.clientWidth]") is { } dims
            ? (dims[0], dims[1])
            : (0, 0);

        Assert.True(scrollWidth <= clientWidth,
            $"scroll viewport should not need horizontal scroll: scrollWidth={scrollWidth}, clientWidth={clientWidth}");

        var overflowX = await viewport.EvaluateAsync<string>("el => getComputedStyle(el).overflowX");
        Assert.Equal("hidden", overflowX);
    }

    [Fact]
    public async Task DropdownMenu_With_A_Separator_Has_No_Horizontal_Scrollbar()
    {
        await Goto("/components/dropdown-menu");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.Locator("[data-testid='dropdown-open-trigger']").ClickAsync();

        var viewport = Page.Locator("[data-slot='dropdown-menu-viewport']").First;
        await AssertNoHorizontalOverflow(viewport);
    }

    [Fact]
    public async Task ContextMenu_With_A_Separator_Has_No_Horizontal_Scrollbar()
    {
        await Goto("/components/context-menu");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByText("Right click here").First.ClickAsync(new() { Button = MouseButton.Right });

        var viewport = Page.Locator("[data-slot='context-menu-viewport']").First;
        await AssertNoHorizontalOverflow(viewport);
    }

    [Fact]
    public async Task Menubar_With_A_Separator_Has_No_Horizontal_Scrollbar()
    {
        await Goto("/components/menubar");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByText("File", new() { Exact = true }).First.ClickAsync();

        var viewport = Page.Locator("[data-slot='menubar-viewport']").First;
        await AssertNoHorizontalOverflow(viewport);
    }
}
