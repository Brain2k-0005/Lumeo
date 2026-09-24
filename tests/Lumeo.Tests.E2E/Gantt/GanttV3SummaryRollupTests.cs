using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// Gantt v3 Phase 3, T3 — summary rollups: envelope strips + overridable math
/// (design spec: "E2E: strip renders, % label value, updates after drag").
/// v3-ONLY (summary rollups have no v2 equivalent — v2 has neither hierarchy
/// nor a rollup concept at all), driven against
/// <c>/e2e/gantt-v3-tree?fixture=rollup</c> (<c>GanttParityFixtures.RollupTasks</c>
/// — see its own remarks for the exact duration-weighted math this suite's
/// assertions are derived from).
/// </summary>
public class GanttV3SummaryRollupTests : GanttParityTestBase
{
    private const string Root = "[data-testid='gantt-v3-tree-root']";

    // Issue #506: the drag engine's opt-in journal (gantt-v3.js diagLog: registerDrag, the
    // pointerdown's hit target, pointerup, the CommitDrag dispatch, next to the #385 scroll
    // writes) is on for this class, so a "drag never committed" failure carries the sequence
    // that produced it instead of only the unchanged attribute.
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await Page.AddInitScriptAsync("window.__lumeoGanttDiag = true;");
    }

    private async Task GotoRollupFixtureAsync()
    {
        await GotoHost("/e2e/gantt-v3-tree?fixture=rollup&infiniteScroll=0");
        await Page.Locator($"{Root} [data-task-id]").First
            .WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 15000 });

        // Issue #506: a bar in the DOM is not a draggable bar. The first render lands before two
        // things the drag specs depend on, each with its own stamp on the scroll host:
        //   - GanttTimeline registers the JS drag engine from OnAfterRenderAsync; until that
        //     round trip returns there is no pointerdown listener, and a drag is simply ignored.
        //     data-gantt-v3-drag-origin is rendered only once registerDrag has returned.
        //   - gantt-v3.js centerOn's initial scroll-to-today runs in a later animation frame; if
        //     it lands after CenterAsync's ScrollIntoView + BoundingBox, the measured point is
        //     suddenly ~1600px off-screen and the pointer presses on nothing.
        //     data-gantt-v3-initial-scroll="done" is set in the same call as that scroll.
        // Replaying this spec with 150ms of emulated network latency hit both windows (2 of 6
        // runs: a pointerdown journaled 98ms BEFORE registerDrag, and one over no element at all
        // after the late centering scroll) — rc3 then keeps 2026-03-15, the CI failure exactly.
        await Assertions.Expect(Page.Locator($"{Root} [data-gantt-v3-drag-origin]"))
            .ToHaveCountAsync(1, new() { Timeout = 15000 });
        await Assertions.Expect(Page.Locator($"{Root} [data-gantt-v3-initial-scroll='done']").First)
            .ToBeAttachedAsync(new() { Timeout = 15000 });
    }

    private async Task<(float X, float Y)> CenterAsync(ILocator locator)
    {
        await locator.ScrollIntoViewIfNeededAsync();
        var box = await locator.BoundingBoxAsync();
        Assert.NotNull(box);
        return ((float)(box!.X + box.Width / 2), (float)(box.Y + box.Height / 2));
    }

    private async Task DragAsync((float X, float Y) from, (float X, float Y) to)
    {
        // Marks the test's own gesture start in the journal the engine writes to, with what the
        // pointer is over at that instant.
        await Page.EvaluateAsync(@"([x, y]) => {
            if (window.__lumeoGanttDiag !== true) return;
            const el = document.elementFromPoint(x, y);
            const bar = el && el.closest('[data-task-id]');
            (window.__lumeoGanttScrollLog || (window.__lumeoGanttScrollLog = [])).push({
                ev: 'test-mousedown', x, y,
                over: bar ? bar.getAttribute('data-task-id') : (el ? el.tagName + ':' + (el.getAttribute('class') || '').split(' ')[0] : null),
                t: performance.now() });
        }", new object[] { (double)from.X, (double)from.Y });
        await Page.Mouse.MoveAsync(from.X, from.Y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(to.X, to.Y);
        await Page.Mouse.UpAsync();
    }

    // Failure-only dump: the engine journal plus the readiness stamps and scroll position, so the
    // next CI failure says whether the pointerdown happened before registerDrag, missed the bar,
    // or committed and was lost afterwards.
    private async Task ExpectOrDumpAsync(Func<Task> expectation, (float X, float Y) pointer)
    {
        try
        {
            await expectation();
        }
        catch (PlaywrightException ex)
        {
            var dump = await Page.EvaluateAsync<string>(@"([root, x, y]) => {
                const host = document.querySelector(root + ' [data-gantt-v3-drag-origin]');
                const el = document.elementFromPoint(x, y);
                return JSON.stringify({
                    dragOrigin: host ? host.getAttribute('data-gantt-v3-drag-origin') : null,
                    initialScroll: host ? host.getAttribute('data-gantt-v3-initial-scroll') : null,
                    scrollLeft: host ? host.scrollLeft : null,
                    rc3: [...document.querySelectorAll(root + "" [data-task-id='rc3']"")].map(b => ({
                        start: b.getAttribute('data-task-start'), progress: b.getAttribute('data-task-progress'),
                        x: b.getBoundingClientRect().x, y: b.getBoundingClientRect().y })),
                    overPointerNow: el ? el.tagName + ':' + (el.getAttribute('class') || '').split(' ')[0] : null,
                    journal: window.__lumeoGanttScrollLog || [],
                });
            }", new object[] { Root, (double)pointer.X, (double)pointer.Y });
            throw new Xunit.Sdk.XunitException(ex.Message + "\n--- gantt-v3 drag diagnostics (issue #506) ---\n" + dump);
        }
    }

    [Fact]
    public async Task Summary_strip_renders_on_the_hierarchy_parent_row_with_the_correct_duration_weighted_percent_label()
    {
        await GotoRollupFixtureAsync();

        // Exactly one strip — the "Program" parent row (rp1); none of its 3
        // leaf children render one.
        var strips = Page.Locator($"{Root} .lumeo-gantt-v3-summary-bar");
        await Assertions.Expect(strips).ToHaveCountAsync(1);

        // (4*100 + 10*50 + 4*0) / 18 = 900/18 = 50% exactly (see the fixture's
        // own remarks for the duration weights).
        var label = Page.Locator($"{Root} .lumeo-gantt-v3-summary-label");
        await Assertions.Expect(label).ToHaveTextAsync("50%");
    }

    [Fact]
    public async Task Summary_strip_is_absent_when_ShowSummaryBars_is_off()
    {
        // The default (untouched) tree fixture never opts in — Phase-3-ledger-
        // pinned v2/REUI look-delta (ShowSummaryBars defaults false).
        await GotoHost("/e2e/gantt-v3-tree?infiniteScroll=0");
        await Page.Locator($"{Root} [data-task-id]").First
            .WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 15000 });

        Assert.Equal(0, await Page.Locator($"{Root} .lumeo-gantt-v3-summary-bar").CountAsync());
    }

    [Fact]
    public async Task A_progress_drag_on_a_child_task_recomputes_the_parents_summary_percent_label()
    {
        await GotoRollupFixtureAsync();

        var label = Page.Locator($"{Root} .lumeo-gantt-v3-summary-label");
        await Assertions.Expect(label).ToHaveTextAsync("50%");

        // rc3 ("Docs") starts at Progress=0 — its progress handle therefore
        // starts at the bar's own LEFT edge (same starting-position convention
        // GanttDragParityTests' own progress-drag spec relies on). Its bar is
        // Start=3/15, End=3/19 — 4 calendar days by End-Start (the weight
        // RollupTasks' own remarks use for the duration-weighted math), but
        // GanttScale.BarGeometry RENDERS a bar's width via end.AddDays(1) (the
        // same inclusive-end convention every Gantt v3 bar uses, so a 1-day
        // task still gets a visible, non-zero-width bar) — 5 calendar days
        // (3/15..3/19 inclusive) wide, i.e. 5*38=190px in Day mode. Dragging
        // the handle that FULL RENDERED width lands it at exactly 100% (an
        // E2E-round-trip-only distinction from the rollup weight's own 4-day
        // figure — bUnit's CommitProgress-direct spec, which never goes
        // through this pixel math at all, is unaffected by it).
        var handle = Page.Locator($"{Root} [data-task-id='rc3'] [data-gantt-progress-handle]");
        var from = await CenterAsync(handle);
        const int dx = 5 * 38;
        await DragAsync(from, (from.X + dx, from.Y));

        // (4*100 + 10*50 + 4*100) / 18 = 1300/18 ≈ 72.2% -> rounds to 72%.
        // Playwright's own auto-retrying assertion is what proves this is a
        // genuine RECOMPUTE (the strip settling on a NEW value), not merely
        // "a strip still exists" (which a stale-cache bug would still satisfy).
        await ExpectOrDumpAsync(() => Assertions.Expect(label).ToHaveTextAsync("72%", new() { Timeout = 10000 }), from);

        // The child's own bar reflects the committed progress too — proves the
        // drag genuinely committed (not just a lucky race on the label text).
        await Assertions.Expect(Page.Locator($"{Root} [data-task-id='rc3']"))
            .ToHaveAttributeAsync("data-task-progress", "100");
    }

    [Fact]
    public async Task A_date_drag_on_a_child_task_widens_the_parents_summary_strip()
    {
        await GotoRollupFixtureAsync();

        var strip = Page.Locator($"{Root} .lumeo-gantt-v3-summary-bar");
        var beforeBox = await strip.BoundingBoxAsync();
        Assert.NotNull(beforeBox);

        // Move rc3 ("Docs", the rightmost/latest child) 5 days further out —
        // widens the envelope's own End, hence its rendered width.
        var bar = Page.Locator($"{Root} [data-task-id='rc3']");
        var barCenter = await CenterAsync(bar);
        const int dx = 5 * 38; // 5 days @ 38px/day (Day mode)
        await DragAsync(barCenter, (barCenter.X + dx, barCenter.Y));

        await ExpectOrDumpAsync(() => Assertions.Expect(Page.Locator($"{Root} [data-task-id='rc3']"))
            .ToHaveAttributeAsync("data-task-start", "2026-03-20", new() { Timeout = 10000 }), barCenter);

        var afterBox = await strip.BoundingBoxAsync();
        Assert.NotNull(afterBox);
        Assert.True(afterBox!.Width > beforeBox!.Width + 100,
            $"expected the summary strip to widen by ~{dx}px after rc3 moved 5 days later, before={beforeBox.Width} after={afterBox.Width}");
    }
}
