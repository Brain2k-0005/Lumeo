using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Real-browser regression coverage for the two P0 cascade/layout bugs that
/// bUnit cannot reproduce (it has no layout engine — it sees classes, not
/// computed geometry):
///
///   #172 — a nested overlay (DropdownMenuSubContent) was positioned relative to
///          a transformed parent (the CSS `transform` used for align/flip created
///          a containing block for the `position:fixed` sub-content), so it opened
///          off its trigger / off-viewport. Fixed by making positionFixed
///          transform-free.
///   #173 — Blazicons injects an unlayered `svg[blazicon]{width:1em}` rule that, in
///          Tailwind v4, beats the size utilities in `@layer utilities`, collapsing
///          every icon to the font size. Fixed by an unlayered revert-layer reset.
///
/// Targets the deterministic <c>/e2e/p0-harness</c> page. Requires the docs
/// dev-server (see project README.md).
/// </summary>
public class P0OverlayIconTests : PlaywrightTestBase
{
    private async Task OpenMenuAndSubmenuAsync()
    {
        await Goto("/e2e/p0-harness");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.Locator("[data-testid='p0-dd-trigger']").ClickAsync();

        var subTrigger = Page.Locator("[data-testid='p0-sub-trigger']");
        await subTrigger.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

        // Submenus open on mouseenter (DropdownMenuSubTrigger.HandleMouseEnter).
        await subTrigger.HoverAsync();

        var subContent = Page.Locator("[data-testid='p0-sub-content']");
        await subContent.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
    }

    [Fact]
    public async Task Submenu_opens_within_viewport_adjacent_to_its_trigger()
    {
        await OpenMenuAndSubmenuAsync();

        var geom = await Page.EvaluateAsync<JsonElement>(@"() => {
            const sub = document.querySelector(""[data-testid='p0-sub-content']"");
            const trg = document.querySelector(""[data-testid='p0-sub-trigger']"");
            const s = sub.getBoundingClientRect();
            const t = trg.getBoundingClientRect();
            return {
                left: s.left, right: s.right, top: s.top, bottom: s.bottom, width: s.width,
                vw: window.innerWidth, vh: window.innerHeight,
                trgLeft: t.left, trgRight: t.right
            };
        }");

        double left = geom.GetProperty("left").GetDouble();
        double right = geom.GetProperty("right").GetDouble();
        double top = geom.GetProperty("top").GetDouble();
        double bottom = geom.GetProperty("bottom").GetDouble();
        double width = geom.GetProperty("width").GetDouble();
        double vw = geom.GetProperty("vw").GetDouble();
        double vh = geom.GetProperty("vh").GetDouble();
        double trgLeft = geom.GetProperty("trgLeft").GetDouble();
        double trgRight = geom.GetProperty("trgRight").GetDouble();

        Assert.True(width > 0, "Sub-content has zero width — it did not render.");

        // (a) Fully within the viewport. The #172 failure pushed it far off-screen.
        Assert.True(left >= -1 && top >= -1 && right <= vw + 1 && bottom <= vh + 1,
            $"Sub-content outside viewport: left={left} top={top} right={right} bottom={bottom} vw={vw} vh={vh}");

        // (b) Horizontally adjacent to its trigger — opening to the right (left edge
        //     ~ trigger's right edge) or flipped to the left (right edge ~ trigger's
        //     left edge). Pre-fix the transform containing block offset it by ~half
        //     the parent content's width, so neither edge lined up.
        bool opensRight = Math.Abs(left - trgRight) <= 24;
        bool opensLeft = Math.Abs(right - trgLeft) <= 24;
        Assert.True(opensRight || opensLeft,
            $"Sub-content not adjacent to its trigger: subLeft={left} subRight={right} trgLeft={trgLeft} trgRight={trgRight}");
    }

    [Theory]
    [InlineData("p0-icon-md", 16)]
    [InlineData("p0-icon-lg", 20)]
    [InlineData("p0-icon-xl", 24)]
    public async Task Icon_size_utility_wins_over_blazicon_1em(string testid, int expectedPx)
    {
        await Goto("/e2e/p0-harness");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var width = await Page.EvaluateAsync<double>(
            $@"() => {{
                const svg = document.querySelector(""[data-testid='{testid}'] svg"");
                return svg ? parseFloat(getComputedStyle(svg).width) : -1;
            }}");

        // 1.5px tolerance for sub-pixel rounding. Pre-fix every icon measured
        // ~14px (1em at text-sm) regardless of its h-/w- utility.
        Assert.True(Math.Abs(width - expectedPx) <= 1.5,
            $"Icon '{testid}' computed width {width}px, expected ~{expectedPx}px — Blazicons' 1em rule is likely still winning the cascade.");
    }
}
