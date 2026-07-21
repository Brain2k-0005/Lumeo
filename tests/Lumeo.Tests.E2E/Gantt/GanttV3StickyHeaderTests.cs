using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// v3-ONLY: sticky-header regression coverage (Codex round 2, P1 #3 — "sticky
/// header STILL broken", the wave-1 "overflow-y-visible" fix was a no-op).
/// MANDATORY per the fix's own review gate: a tall-enough fixture that
/// genuinely NEEDS vertical scrolling, asserted both before AND after
/// scrolling — a fixture that never actually scrolls would trivially "pass"
/// a stays-visible assertion without proving anything.
///
/// v2 has NO sticky header at all: gantt-v2.js's <c>render()</c> draws the
/// header as plain <c>&lt;rect&gt;</c>/<c>&lt;text&gt;</c> elements INSIDE the
/// same single scrollable <c>&lt;svg&gt;</c> canvas as the bars — no
/// <c>position: sticky</c> anywhere in that file or in lumeo-gantt.css
/// (checked). Scrolling v2's chart down scrolls its header away with
/// everything else. This is therefore a v3-only IMPROVEMENT, not a rendering-
/// equivalence gap — there is no v2 counterpart to assert here, and no
/// parity baseline to pin.
/// </summary>
public class GanttV3StickyHeaderTests : GanttParityTestBase
{
    [Fact]
    public async Task Sticky_header_stays_visible_while_scrolling_a_tall_task_list()
    {
        await GotoHost("/e2e/gantt-v3?fixture=tall&viewMode=Day");

        var header = Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-header");
        await header.WaitForAsync(new() { Timeout = 15000 });

        // Gantt3's own shared vertical-scroll pane (the "rounded-md border ...
        // overflow:auto" wrapper around the tree+timeline flex row) — not
        // GanttTimeline's own root (which no longer establishes any scroll
        // container itself post-restructure; see GanttTimeline.razor's remarks).
        var scrollPane = Page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        await scrollPane.WaitForAsync(new() { Timeout = 15000 });

        // Sanity check the fixture is ACTUALLY tall enough to need scrolling —
        // asserting stickiness on a pane that never scrolls at all would prove
        // nothing (the same mistake the wave-1 fix's untested claim made).
        var scrollHeight = await scrollPane.EvaluateAsync<double>("el => el.scrollHeight");
        var clientHeight = await scrollPane.EvaluateAsync<double>("el => el.clientHeight");
        Assert.True(scrollHeight > clientHeight + 200,
            $"Tall fixture isn't actually tall enough to need vertical scrolling (scrollHeight={scrollHeight}, clientHeight={clientHeight}) — the regression this spec guards against can't be reproduced.");

        await Assertions.Expect(header).ToBeInViewportAsync();

        // Scroll the OUTER pane all the way down.
        await scrollPane.EvaluateAsync("el => el.scrollTop = el.scrollHeight");

        // The wave-1 regression, reproduced without this fix: the header
        // scrolled away with the row canvas instead of staying stuck to the
        // top of THIS pane.
        await Assertions.Expect(header).ToBeInViewportAsync();

        // GanttTree's new virtualization (Codex round 2, P2 #10) actually
        // engages in a real browser (unlike bUnit's headless DOM, which
        // renders every Virtualize item regardless of viewport — see
        // GanttTreeAndArrowsTests' own note): only ~34 of 60 rows are
        // materialized here, proving #10's fix works.
        var treeTaskRows = Page.Locator("[data-testid='gantt-v3-root'] [data-row-kind='task']");
        var treeCount = await treeTaskRows.CountAsync();
        Assert.True(treeCount > 0 && treeCount < 60,
            $"expected GanttTree to virtualize (some but not all 60 rows materialized), got {treeCount}");

        // KNOWN FOLLOW-UP (discovered by this spec, not one of the original 10
        // findings): GanttTimeline's OWN pre-existing Virtualize (added before
        // this task, T2's read-only-markup pass) does NOT actually virtualize
        // in a real browser — it renders all 60 bars regardless of viewport.
        // Root cause appears to be the SAME CSS-overflow-promotion quirk this
        // whole finding is about: GanttTimeline's row-canvas wrapper
        // (.lumeo-gantt-v3-canvas-scroll, overflow-x:auto) auto-promotes to
        // overflow-y:auto too, and Blazor's <Virtualize> picks THAT unconstrained
        // (content-sized, never-actually-clipping) element as its nearest
        // scrolling ancestor instead of continuing up to Gantt3's true outer
        // pane — the identical mechanism that broke position:sticky, just for
        // Virtualize's own ancestor walk instead. This predates this task (the
        // pre-fix RootClass had the exact same overflow-y:auto promotion), so
        // it is not a regression THIS fix introduced, but it wasn't previously
        // visible since nothing exercised GanttTimeline virtualizing at real
        // browser scale until this tall fixture existed. Left unfixed here —
        // flagged as a follow-up in the round-2 report — rather than another
        // Global structural change on top of the sticky-header restructure.
        var bars = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id]");
        var barCount = await bars.CountAsync();
        Assert.Equal(60, barCount);
    }

    [Fact]
    public async Task Header_columns_stay_aligned_with_the_row_canvas_after_a_horizontal_scroll()
    {
        // The header can no longer physically BE the row-canvas's own
        // scrolling element post-restructure (gantt-v3.js's
        // registerHeaderScrollSync mirrors scrollLeft onto it via transform
        // instead) — this pins that the mirror actually keeps the two in
        // sync, not just that the header stays vertically visible.
        await GotoHost("/e2e/gantt-v3?fixture=tall&viewMode=Day");

        var canvasScroll = Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-canvas-scroll");
        await canvasScroll.WaitForAsync(new() { Timeout = 15000 });
        var headerInner = Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-header > div").First;

        await canvasScroll.EvaluateAsync("el => el.scrollLeft = 300");
        // Native 'scroll' events dispatch asynchronously — poll via Playwright's
        // auto-retrying assertion instead of a fixed timeout.
        await Assertions.Expect(headerInner).ToHaveCSSAsync("transform", "matrix(1, 0, 0, 1, -300, 0)");
    }
}
