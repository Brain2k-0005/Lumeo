using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// v3-ONLY: dependency-arrow virtualization regression coverage (Codex round
/// 4, P2 #3). GanttArrowLayer previously drew one SVG path per dependency
/// regardless of scroll position, unlike the bars/tree rows it overlays
/// (both already virtualized — see GanttV3StickyHeaderTests' own remarks on
/// the round-2/round-3 bar/tree virtualization fixes). v2 has no arrow
/// virtualization of its own to compare against — its whole chart is one
/// non-virtualized SVG canvas (see GanttV3StickyHeaderTests' class remarks) —
/// so this is a v3-only improvement, not a rendering-equivalence gap.
///
/// <see cref="GanttParityFixtures.TallFixture"/> now carries a dependency
/// CHAIN (each of the 60 tasks depends on the one before it, 59 edges total —
/// see its own remarks) specifically for this coverage.
/// </summary>
public class GanttV3ArrowVirtualizationTests : GanttParityTestBase
{
    [Fact]
    public async Task Fewer_arrows_than_total_dependencies_render_for_the_tall_fixture()
    {
        await GotoHost("/e2e/gantt-v3?fixture=tall&viewMode=Day");

        var scrollPane = Page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        await scrollPane.WaitForAsync(new() { Timeout = 15000 });
        await Assertions.Expect(scrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });
        await scrollPane.EvaluateAsync("el => el.scrollLeft = 0");

        // Sanity check the fixture is actually tall enough that not everything
        // is visible at once (same reasoning as the sticky-header/tree specs'
        // own remarks — asserting culling on a fixture that fits entirely in
        // the viewport would prove nothing).
        var bars = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id]");
        // Poll: the vertical-scroll-tracking report is async (a JS round-trip
        // after mount), so the FIRST render still shows every arrow unculled —
        // wait for culling to actually kick in.
        await Assertions.Expect(Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-arrow"))
            .Not.ToHaveCountAsync(59, new() { Timeout = 10000 });

        var arrowCount = await Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-arrow").CountAsync();
        Assert.True(arrowCount > 0 && arrowCount < 59,
            $"expected the tall fixture's 59-edge dependency chain to be culled (some but not all arrows rendered), got {arrowCount}");
    }

    [Fact]
    public async Task Arrows_for_newly_visible_rows_appear_after_scrolling_to_a_far_window()
    {
        await GotoHost("/e2e/gantt-v3?fixture=tall&viewMode=Day");

        var scrollPane = Page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        await scrollPane.WaitForAsync(new() { Timeout = 15000 });
        await Assertions.Expect(scrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });
        await scrollPane.EvaluateAsync("el => el.scrollLeft = 0");

        // The chain's LAST edge connects tall-58 -> tall-59 — neither is
        // anywhere near the top of a freshly-mounted, unscrolled pane, so
        // this specific arrow must be ABSENT until we actually scroll there.
        var lastArrow = Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-arrow[data-arrow-from='tall-58'][data-arrow-to='tall-59']");
        await Assertions.Expect(lastArrow).Not.ToBeAttachedAsync(new() { Timeout = 10000 });

        // Scroll the OUTER pane all the way down — a genuinely "far window"
        // from the initial mount position.
        await scrollPane.EvaluateAsync("el => el.scrollTop = el.scrollHeight");

        // Now that tall-58/tall-59's rows are (approximately) in view, the
        // edge connecting them must reappear — proving culling reacts to the
        // LIVE scroll position rather than a static, mount-time snapshot.
        await Assertions.Expect(lastArrow).ToBeAttachedAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task A_Long_Dependency_Spanning_The_Visible_Window_Is_Not_Culled_When_Both_Endpoints_Are_Offscreen()
    {
        // Bug fix (Codex round 5, P2 #7): GanttArrowLayer's round-4 culling
        // check tested source/target EACH individually against the visible
        // row range — but a source ABOVE the window and a target BELOW it
        // both satisfy "individually outside" even though the edge's own
        // path necessarily crosses straight THROUGH the visible rows, so it
        // was wrongly culled. GanttParityFixtures.CrossingDependencyFixture's
        // single edge (row 5 -> row 70) is sized specifically so a
        // scrolled-to-center window (plus its 10-row overscan margin)
        // excludes BOTH endpoints individually while the edge's own [5, 70]
        // span still fully brackets that window.
        await GotoHost("/e2e/gantt-v3?fixture=crossing&viewMode=Day");

        var scrollPane = Page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        await scrollPane.WaitForAsync(new() { Timeout = 15000 });
        await Assertions.Expect(scrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });
        await scrollPane.EvaluateAsync("el => el.scrollLeft = 0");

        await scrollPane.EvaluateAsync("el => el.scrollTop = (el.scrollHeight - el.clientHeight) / 2");

        var crossingArrow = Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-arrow[data-arrow-from='cross-5'][data-arrow-to='cross-70']");
        await Assertions.Expect(crossingArrow).ToBeAttachedAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task Horizontal_Only_Scroll_Does_Not_Trigger_A_Vertical_Scroll_Report()
    {
        // Bug fix (Codex round 5, P2 #8): registerVerticalScrollTracking
        // listens for the pane's native 'scroll' event, which fires
        // identically for a horizontal-only pan (browsing dates sideways) —
        // there is no separate horizontal/vertical scroll event — so every
        // sideways drag used to ALSO dispatch a full invokeMethodAsync
        // round-trip reporting an unchanged scrollTop, for no purpose (the
        // culled row range is a pure function of scrollTop/clientHeight).
        // gantt-v3.js now stamps a `data-gantt-v3-vertical-report-count`
        // attribute on an actual (post-dedup) report — a deterministic,
        // Playwright-observable proxy for the otherwise-invisible interop
        // call count, matching the existing data-gantt-v3-initial-scroll
        // latch's own reasoning.
        await GotoHost("/e2e/gantt-v3?fixture=tall&viewMode=Day");

        var scrollPane = Page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        await scrollPane.WaitForAsync(new() { Timeout = 15000 });
        await Assertions.Expect(scrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });

        // The tracker fires one report immediately on registration, but a
        // legitimate mount-time clientHeight settle (the pane finalizing its
        // own height as the horizontal scrollbar/layout land, AFTER the
        // initial-scroll latch) can fire ONE more correct report a frame or two
        // later — so the count is NOT reliably "1" the instant initial-scroll
        // lands (it may already be "2"). Hardcoding "1" here therefore raced the
        // mount settle. What this spec actually guards is that a HORIZONTAL-only
        // scroll adds NO report — so wait for the count to STABILIZE (whatever
        // it settles to), then assert the horizontal scroll leaves it unchanged.
        var baseline = await WaitForStableReportCountAsync(scrollPane);

        await scrollPane.EvaluateAsync("el => el.scrollLeft = 500"); // horizontal-only — scrollTop AND clientHeight unchanged
        await Page.WaitForTimeoutAsync(300); // comfortably outlasts a single rAF frame

        // The horizontal pan must not have added a vertical report (the JS
        // dedups on both scrollTop AND clientHeight, neither of which a sideways
        // scroll moves) — the stabilized count is unchanged.
        await Assertions.Expect(scrollPane).ToHaveAttributeAsync("data-gantt-v3-vertical-report-count", baseline);
    }

    // Polls the vertical-report count until it stops changing across two
    // consecutive samples — absorbing the one-time mount-layout settle report
    // into a stable baseline so a following assertion doesn't race it.
    private async Task<string> WaitForStableReportCountAsync(ILocator pane)
    {
        var prev = await pane.GetAttributeAsync("data-gantt-v3-vertical-report-count") ?? "0";
        for (var i = 0; i < 40; i++)
        {
            await Page.WaitForTimeoutAsync(100);
            var cur = await pane.GetAttributeAsync("data-gantt-v3-vertical-report-count") ?? "0";
            if (cur == prev) return cur;
            prev = cur;
        }
        return prev;
    }
}
