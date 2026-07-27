using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// v3-ONLY: GanttTree sticky-left regression coverage (cx3 review round on
/// Codex round 3's finding #1 restructure — GanttTree.RootClass added
/// <c>sticky left-0</c> so the tree pane stays pinned during horizontal
/// scroll, but the FIRST attempt at making that actually work
/// (GanttTimeline's flex item refusing to shrink below its own content width)
/// was insufficient by itself: the surrounding <c>display:flex</c> row is a
/// normal BLOCK-level box in normal flow, and a block box's width comes from
/// its OWN containing block, not shrink-to-fit over its children's
/// min-width/flex-basis — so the row's own measured box (what
/// position:sticky's containing block bounds resolve against) stayed
/// viewport-narrow even though the rendered content visually spilled
/// thousands of pixels wider. Reproduced live before the fix: GanttTree's
/// bounding-box sat at x=-458 after scrolling to scrollLeft=1497 (and still
/// x=-3114 after the first, insufficient fix attempt). See Gantt3.razor's
/// own remarks on the <c>width:max-content</c> fix for the full mechanism.
///
/// v2 has no tree/hierarchy pane at all (see GanttV3TreeTests' own remarks),
/// so there is no v2 counterpart here — this asserts v3's own behavior.
/// </summary>
public class GanttV3StickyLeftTests : GanttParityTestBase
{
    [Fact]
    public async Task GanttTree_Stays_Pinned_To_The_Left_Edge_After_Scrolling_Most_Of_The_Way_Across()
    {
        await GotoHost("/e2e/gantt-v3?fixture=tall&viewMode=Day");

        var scrollPane = Page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        await scrollPane.WaitForAsync(new() { Timeout = 15000 });

        // Same reasoning as the sticky-header spec's matching reset: the
        // mount-time scroll-to-today auto-center now shares this pane too
        // (Codex round 3, P2 #1), so start from a known scrollLeft=0 rather
        // than wherever that auto-center happened to land.
        await Assertions.Expect(scrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });
        await scrollPane.EvaluateAsync("el => el.scrollLeft = 0");

        var treeRow = Page.Locator("[data-testid='gantt-v3-root'] [data-row-kind]").First;
        await treeRow.WaitForAsync(new() { Timeout = 15000 });

        // Sanity check there's ACTUALLY meaningful horizontal scroll room —
        // asserting pinning on a pane that never needs to scroll horizontally
        // would prove nothing (same mistake the sticky-header spec's own
        // remarks warn about for the vertical case).
        var maxScroll = await scrollPane.EvaluateAsync<double>("el => el.scrollWidth - el.clientWidth");
        Assert.True(maxScroll > 1000,
            $"Tall fixture (Day mode) isn't actually wide enough to need horizontal scrolling (maxScroll={maxScroll}) — the regression this spec guards against can't be reproduced.");

        var before = await treeRow.BoundingBoxAsync();
        Assert.NotNull(before);

        await scrollPane.EvaluateAsync($"el => el.scrollLeft = {maxScroll * 0.7}");
        await Page.WaitForTimeoutAsync(200);

        var after = await treeRow.BoundingBoxAsync();
        Assert.NotNull(after);

        var delta = after!.X - before!.X;
        Assert.True(delta is > -2 and < 2,
            $"expected GanttTree to stay pinned to the left edge (bounding-box X within 2px of its pre-scroll position) after scrolling to 70% of maxScroll, got a delta of {delta} (before.X={before.X}, after.X={after.X})");

        // Pinned in POSITION isn't the same as pinned in VISIBILITY — assert
        // its content is actually still on-screen, not just mathematically
        // at the right X while clipped/hidden by something else.
        await Assertions.Expect(treeRow).ToBeInViewportAsync();
    }

    [Fact]
    public async Task GanttTree_Stays_Pinned_To_The_Physical_Right_Edge_Under_Rtl()
    {
        // Bug fix (Codex round 4, P2 #10): GanttTree.RootClass used a PHYSICAL
        // `left-0`, which pins to the physical left edge unconditionally — but
        // under RTL, a tree/sidebar's "leading" edge (where every OTHER
        // RTL-aware Lumeo component pins via a logical property — see
        // GanttBar's own `start-full`/`ms-2`) is the physical RIGHT. Fixed by
        // swapping to `start-0` (CSS `inset-inline-start:0`), which resolves
        // to `left:0` under LTR (unchanged — see the LTR spec above) and
        // `right:0` under RTL.
        await GotoHost("/e2e/gantt-v3?fixture=tall&viewMode=Day&rtl=1");

        var scrollPane = Page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        await scrollPane.WaitForAsync(new() { Timeout = 15000 });
        await Assertions.Expect(scrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });

        var treeRow = Page.Locator("[data-testid='gantt-v3-root'] [data-row-kind]").First;
        await treeRow.WaitForAsync(new() { Timeout = 15000 });

        async Task AssertPinnedToPhysicalRightEdge(string when)
        {
            var treeBox = await treeRow.BoundingBoxAsync();
            var paneBox = await scrollPane.BoundingBoxAsync();
            Assert.NotNull(treeBox);
            Assert.NotNull(paneBox);
            var treeRightEdge = treeBox!.X + treeBox.Width;
            var paneRightEdge = paneBox!.X + paneBox.Width;
            Assert.True(Math.Abs(treeRightEdge - paneRightEdge) < 3,
                $"expected GanttTree to be pinned to the pane's physical right edge under RTL ({when}), tree right edge={treeRightEdge}, pane right edge={paneRightEdge}");
        }

        await AssertPinnedToPhysicalRightEdge("at mount");

        // Under the 'negative' RTL scrollLeft convention (the modern
        // evergreen-browser standard — see gantt-v3.js's own remarks),
        // 0 is the RTL start and valid values run negative toward
        // -(scrollWidth - clientWidth); scroll partway to prove the pin
        // holds across a real scroll, not just coincidentally at mount.
        var maxScroll = await scrollPane.EvaluateAsync<double>("el => el.scrollWidth - el.clientWidth");
        await scrollPane.EvaluateAsync($"el => el.scrollLeft = {-maxScroll * 0.5}");
        await Page.WaitForTimeoutAsync(200);

        await AssertPinnedToPhysicalRightEdge("after scrolling to 50% of maxScroll");
    }
}
