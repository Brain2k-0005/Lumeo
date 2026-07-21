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

        // Codex round 3, P2 #1: this pane is now the SHARED horizontal+vertical
        // scroll owner (previously the row-canvas had its own, isolated
        // horizontal scroll — see GanttTimeline.ScrollHost's remarks), so the
        // pre-existing (v2-parity, round 2) mount-time "scroll toward today"
        // auto-center — which loosely fires whenever TodayX > 0, even when
        // today falls OUTSIDE the fixture's rendered range (see
        // GanttTimeline.ShouldAttemptTodayScroll's remarks) — now ALSO shifts
        // THIS pane horizontally on mount, since the tall fixture is fixed to
        // March 2026 regardless of the real date this suite runs on. That
        // horizontal position is irrelevant to this spec (vertical stickiness
        // only) but would otherwise scroll the header off-screen horizontally,
        // failing the in-viewport checks below for a reason unrelated to what
        // they're testing — reset to a known, deterministic state first.
        await Assertions.Expect(scrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });
        await scrollPane.EvaluateAsync("el => el.scrollLeft = 0");

        // Sanity check the fixture is ACTUALLY tall enough to need scrolling —
        // asserting stickiness on a pane that never scrolls at all would prove
        // nothing (the same mistake the wave-1 fix's untested claim made).
        var scrollHeight = await scrollPane.EvaluateAsync<double>("el => el.scrollHeight");
        var clientHeight = await scrollPane.EvaluateAsync<double>("el => el.clientHeight");
        Assert.True(scrollHeight > clientHeight + 200,
            $"Tall fixture isn't actually tall enough to need vertical scrolling (scrollHeight={scrollHeight}, clientHeight={clientHeight}) — the regression this spec guards against can't be reproduced.");

        await Assertions.Expect(header).ToBeInViewportAsync();

        // Codex round 4, P2 #1: GanttTree's own header spacer (matches the
        // timeline header's height, so both panes' rows start at the same Y)
        // previously scrolled away with the tree rows behind it — even though
        // the TIMELINE's header (asserted above) correctly stayed pinned —
        // since only the tree's ROOT was sticky-left (horizontal only), never
        // the spacer itself (vertical). Captured BEFORE scrolling so the
        // "stays at the same Y" comparison below isn't just coincidentally
        // true.
        var treeSpacer = Page.Locator("[data-testid='gantt-v3-root'] .sticky.start-0 > div").First;
        await treeSpacer.WaitForAsync(new() { Timeout = 15000 });
        var treeSpacerBefore = await treeSpacer.BoundingBoxAsync();
        Assert.NotNull(treeSpacerBefore);

        // Scroll the OUTER pane all the way down.
        await scrollPane.EvaluateAsync("el => el.scrollTop = el.scrollHeight");

        // The wave-1 regression, reproduced without this fix: the header
        // scrolled away with the row canvas instead of staying stuck to the
        // top of THIS pane.
        await Assertions.Expect(header).ToBeInViewportAsync();

        var treeSpacerAfter = await treeSpacer.BoundingBoxAsync();
        Assert.NotNull(treeSpacerAfter);
        Assert.True(Math.Abs(treeSpacerBefore!.Y - treeSpacerAfter!.Y) < 2,
            $"expected GanttTree's own header spacer to stay pinned at the pane's top (Y unchanged) after scrolling, went from Y={treeSpacerBefore.Y} to Y={treeSpacerAfter.Y}");

        // GanttTree's new virtualization (Codex round 2, P2 #10) actually
        // engages in a real browser (unlike bUnit's headless DOM, which
        // renders every Virtualize item regardless of viewport — see
        // GanttTreeAndArrowsTests' own note): only ~34 of 60 rows are
        // materialized here, proving #10's fix works.
        var treeTaskRows = Page.Locator("[data-testid='gantt-v3-root'] [data-row-kind='task']");
        var treeCount = await treeTaskRows.CountAsync();
        Assert.True(treeCount > 0 && treeCount < 60,
            $"expected GanttTree to virtualize (some but not all 60 rows materialized), got {treeCount}");

        // FIXED (Codex round 3, P2 #1 — pulled forward from Phase 3 T1, was the
        // "KNOWN FOLLOW-UP" this spec previously documented): GanttTimeline's
        // Virtualize now correctly resolves ITS scroll ancestor to be THIS
        // outer pane (real, height-capped) instead of the row-canvas wrapper's
        // own (content-sized, never-actually-clipping) overflow-x:auto div —
        // that wrapper no longer sets overflow-x:auto at all when Gantt3
        // supplies ScrollHost (see GanttTimeline.ScrollHost's remarks), so
        // there's no longer an intervening scroll container to mis-anchor
        // against. A tall (60-row) fixture must now materialize STRICTLY
        // fewer than all 60 bars — proving virtualization actually engages,
        // not just that SOME bars render.
        var bars = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id]");
        var barCount = await bars.CountAsync();
        Assert.True(barCount > 0 && barCount < 60,
            $"expected GanttTimeline to virtualize (some but not all 60 bars materialized), got {barCount}");
    }

    [Fact]
    public async Task Header_columns_stay_aligned_with_the_row_canvas_after_a_horizontal_scroll()
    {
        // Codex round 3, P2 #1: the row-canvas div is no longer a scroll
        // container at all (overflow-x-auto moved up to Gantt3's shared outer
        // pane — see GanttTimeline.ScrollHost's remarks), so the JS transform
        // sync this spec used to assert (gantt-v3.js's registerHeaderScrollSync)
        // is no longer registered in this mode either — it would double-apply
        // the offset on top of the header's now-natural scroll movement (see
        // GanttTimeline.OnAfterRenderAsync's own remarks). The header is a
        // plain, non-transformed, non-sticky-horizontally element that lives
        // inside the SAME scrolling ancestor as the row canvas now, so
        // "columns stay aligned" is asserted directly against its on-screen
        // position shifting by exactly the scroll delta — no transform to
        // read anymore.
        await GotoHost("/e2e/gantt-v3?fixture=tall&viewMode=Day");

        var scrollPane = Page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        await scrollPane.WaitForAsync(new() { Timeout = 15000 });
        var headerInner = Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-header > div").First;
        await headerInner.WaitForAsync(new() { Timeout = 15000 });

        // Codex round 3, P2 #1: wait out the mount-time scroll-to-today
        // auto-center (see the sticky-header spec's matching remarks — this
        // pane now owns horizontal scroll too) and reset to a known scrollLeft
        // before taking the "before" measurement, so the 300px delta asserted
        // below isn't polluted by wherever that auto-center happened to land.
        await Assertions.Expect(scrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });
        await scrollPane.EvaluateAsync("el => el.scrollLeft = 0");

        var before = await headerInner.BoundingBoxAsync();
        Assert.NotNull(before);

        await scrollPane.EvaluateAsync("el => el.scrollLeft = 300");

        // A plain scrollLeft assignment reflows synchronously (no async
        // 'scroll'-event round-trip is involved anymore — see the remarks
        // above), so a direct geometry read immediately after already
        // reflects it; still allow a small tolerance for sub-pixel rounding.
        var after = await headerInner.BoundingBoxAsync();
        Assert.NotNull(after);
        var delta = before!.X - after!.X;
        Assert.True(delta is > 295 and < 305,
            $"expected the header to shift left by ~300px in lockstep with the row canvas, got a delta of {delta}");
    }
}
