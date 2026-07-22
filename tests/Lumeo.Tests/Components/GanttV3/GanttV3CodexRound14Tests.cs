using Bunit;
using Lumeo.GanttV3;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Codex round 14 (PR #379, feat/gantt-v3) — the viewport-state consolidation.
/// Rounds 11-13 all landed in Gantt3's recenter machinery, each fix adding
/// tracked state that spawned the next round's finding. This round replaces the
/// accreted per-parameter tracking with ONE snapshot owner
/// (<see cref="GanttViewportReconciler"/>) and fixes four corollary findings:
///
///  #1 — the stored last-seen column width was the OUTGOING mode's default
///       (computed before SetViewMode), so a mode switch's controlled echo saw
///       a spurious width change and re-fired the recenter.
///  #2 — emptiness was classified on RAW task counts, not the filtered/
///       renderable set — a list of only invalid-duration tasks read as
///       "populated".
///  #3 — a preserved-center scroll target persisted, so a LATER browser-"today"
///       change (midnight) scrolled to the stale center instead of today.
///  #4 — on Blazor Server the capture await inside the reconcile exposed a
///       half-reconciled frame (new tasks committed, recenter not yet landed).
///
/// The whole cx9-cx13 behavior matrix stays green UNCHANGED (those tests are the
/// spec); this file covers the reconciler's own snapshot-diff decision table and
/// the four findings. See docs/superpowers/gantt-v3-cx14-report.md.
/// </summary>
public class GanttV3CodexRound14Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3CodexRound14Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // ── The reconciler's own snapshot-diff decision table (pure) ────────────

    private static GanttViewportSnapshot Snap(
        int tasksVersion = 1,
        bool renderableEmpty = false,
        L.GanttViewMode mode = L.GanttViewMode.Day,
        int columnWidth = 38,
        bool showTreePane = false,
        LayoutDirection direction = LayoutDirection.Ltr) =>
        new(tasksVersion, renderableEmpty, mode, columnWidth, showTreePane, direction);

    [Fact]
    public void Decide_No_Change_Is_A_Noop()
    {
        var s = Snap();
        var d = GanttViewportReconciler.Decide(s, s, taskRangeDisjoint: false);
        Assert.Equal(new GanttViewportDecision(false, GanttRangeSource.Keep, GanttScrollTarget.None), d);
    }

    [Theory]
    [InlineData(38, 120, true, false, LayoutDirection.Ltr, LayoutDirection.Ltr)]   // column width alone
    [InlineData(38, 38, false, true, LayoutDirection.Ltr, LayoutDirection.Ltr)]    // tree pane alone
    [InlineData(38, 38, false, false, LayoutDirection.Ltr, LayoutDirection.Rtl)]   // direction alone
    public void Decide_Geometry_Only_Change_Preserves_Center_Without_Rebuilding_The_Range(
        int prevW, int nextW, bool prevTree, bool nextTree, LayoutDirection prevDir, LayoutDirection nextDir)
    {
        var prev = Snap(columnWidth: prevW, showTreePane: prevTree, direction: prevDir);
        var next = Snap(columnWidth: nextW, showTreePane: nextTree, direction: nextDir);
        var d = GanttViewportReconciler.Decide(prev, next, taskRangeDisjoint: false);
        Assert.Equal(new GanttViewportDecision(true, GanttRangeSource.Keep, GanttScrollTarget.CapturedCenter), d);
    }

    [Fact]
    public void Decide_ViewMode_Only_Change_Self_Centers_On_The_Captured_Center()
    {
        var prev = Snap(mode: L.GanttViewMode.Day, columnWidth: 38);
        var next = Snap(mode: L.GanttViewMode.Week, columnWidth: 140);
        var d = GanttViewportReconciler.Decide(prev, next, taskRangeDisjoint: false);
        Assert.Equal(new GanttViewportDecision(true, GanttRangeSource.SelfCenteredOnCapture, GanttScrollTarget.CapturedCenter), d);
    }

    [Fact]
    public void Decide_Tasks_Change_Without_Emptiness_Or_Geometry_Rebuilds_Range_But_Does_Not_Recenter()
    {
        var prev = Snap(tasksVersion: 1, renderableEmpty: false);
        var next = Snap(tasksVersion: 2, renderableEmpty: false);
        var d = GanttViewportReconciler.Decide(prev, next, taskRangeDisjoint: false);
        Assert.Equal(new GanttViewportDecision(false, GanttRangeSource.TaskDerived, GanttScrollTarget.None), d);
    }

    [Theory]
    [InlineData(false, true)]   // populated -> empty
    [InlineData(true, false)]   // empty -> populated
    public void Decide_Emptiness_Transition_Targets_Today_Or_Midpoint_From_The_Task_Derived_Range(bool prevEmpty, bool nextEmpty)
    {
        var prev = Snap(tasksVersion: 1, renderableEmpty: prevEmpty);
        var next = Snap(tasksVersion: 2, renderableEmpty: nextEmpty);
        var d = GanttViewportReconciler.Decide(prev, next, taskRangeDisjoint: false);
        Assert.Equal(new GanttViewportDecision(false, GanttRangeSource.TaskDerived, GanttScrollTarget.TodayOrMidpoint), d);
    }

    [Fact]
    public void Decide_Tasks_And_Mode_Change_Without_Emptiness_Keeps_Task_Range_But_Preserves_The_Center()
    {
        var prev = Snap(tasksVersion: 1, renderableEmpty: false, mode: L.GanttViewMode.Day, columnWidth: 38);
        var next = Snap(tasksVersion: 2, renderableEmpty: false, mode: L.GanttViewMode.Week, columnWidth: 140);
        var d = GanttViewportReconciler.Decide(prev, next, taskRangeDisjoint: false);
        Assert.Equal(new GanttViewportDecision(true, GanttRangeSource.TaskDerived, GanttScrollTarget.CapturedCenter), d);
    }

    [Fact]
    public void Decide_Tasks_And_Mode_Change_With_Emptiness_Transition_Targets_Today_Or_Midpoint()
    {
        var prev = Snap(tasksVersion: 1, renderableEmpty: true, mode: L.GanttViewMode.Day);
        var next = Snap(tasksVersion: 2, renderableEmpty: false, mode: L.GanttViewMode.Week, columnWidth: 140);
        var d = GanttViewportReconciler.Decide(prev, next, taskRangeDisjoint: false);
        Assert.Equal(new GanttViewportDecision(false, GanttRangeSource.TaskDerived, GanttScrollTarget.TodayOrMidpoint), d);
    }

    // ── Finding #1: no spurious recenter on a mode switch's controlled echo ──

    [Fact]
    public void Finding1_A_ViewMode_Switch_With_No_ColumnWidth_Override_Does_Not_Re_Recenter_On_The_Echo()
    {
        // Rounds 11-13's own bug: the last-seen column width was captured from
        // the OUTGOING mode (BEFORE SetViewMode), so after a Day->Month switch
        // with NO explicit ColumnWidth, the stored width was Day's default (38)
        // while the live effective width was now Month's (120) — the controlled
        // ViewModeChanged echo (a second parameter pass with the same Month)
        // then saw a "column width change" and fired a SECOND, wrong recenter
        // that clobbered the first. The snapshot now records the width against
        // the mode it belongs to, so the echo is a clean no-op.
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 5));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ViewModeChanged, EventCallback.Factory.Create<L.GanttViewMode>(this, _ => { }))
            .Add(c => c.ShowTreePane, false)); // NO ColumnWidth override

        _interop.GanttV3ScrollCenterXToReturn = 5 * GanttScale.GetConfig(L.GanttViewMode.Day).ColumnWidth;

        var beforeSwitch = _interop.GanttV3ScrollToXCallCount;
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Month)
            .Add(c => c.ViewModeChanged, EventCallback.Factory.Create<L.GanttViewMode>(this, _ => { }))
            .Add(c => c.ShowTreePane, false));

        var afterSwitch = _interop.GanttV3ScrollToXCallCount;
        Assert.True(afterSwitch > beforeSwitch, "the mode switch itself should recenter once");

        // The controlled echo: the same Month re-pushed as a parameter. Under
        // the old bug this fired a spurious column-width-triggered recenter.
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Month)
            .Add(c => c.ViewModeChanged, EventCallback.Factory.Create<L.GanttViewMode>(this, _ => { }))
            .Add(c => c.ShowTreePane, false));

        Assert.Equal(afterSwitch, _interop.GanttV3ScrollToXCallCount);
    }

    // ── Finding #2: emptiness is classified on the RENDERABLE (filtered) set ─

    [Fact]
    public void Finding2_Replacing_Valid_Tasks_With_Only_Invalid_Ones_Is_An_Emptiness_Transition()
    {
        // A non-milestone task whose End precedes its Start is dropped by
        // GanttRowModel.FilterValidDurationTasks — it renders no row. A raw
        // count would call [invalid] "populated" and skip the recenter; the
        // renderable count correctly sees it as empty, so this populated->empty
        // transition recenters on Today (the empty-fallback range's own center).
        var valid = new L.GanttTask("t1", "Valid", D(2010, 1, 1), D(2010, 1, 10)); // far from today
        var invalidOnly = new L.GanttTask("bad", "Invalid", D(2026, 6, 10), D(2026, 6, 1)); // End < Start, non-milestone

        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { valid })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        // Sanity: the valid task renders; then it becomes renderable-empty.
        Assert.Single(cut.FindAll("[data-task-id='t1']"));
        var before = _interop.GanttV3ScrollToXCallCount;

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { invalidOnly })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        // The invalid task renders NOTHING, and the empty-state message shows —
        // proving the renderable set is empty.
        Assert.Empty(cut.FindAll("[data-task-id='bad']"));
        Assert.Single(cut.FindAll(".lumeo-gantt-v3-empty"));

        // And the emptiness transition requested a recenter onto Today (the
        // empty-fallback range is centered on Today by construction).
        Assert.True(_interop.GanttV3ScrollToXCallCount > before,
            "a valid->only-invalid transition must be treated as populated->empty and recenter");

        var cfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        var rangeStart = DateTime.Today.AddDays(-7).AddDays(-cfg.PadBefore * cfg.Step);
        var rangeEnd = DateTime.Today.AddDays(14).AddDays(cfg.PadAfter * cfg.Step);
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, rangeStart, rangeEnd)[0];
        var expected = GanttScale.DateToPixel(L.GanttViewMode.Day, origin, DateTime.Today, cfg.ColumnWidth);
        Assert.Equal(expected, _interop.GanttV3ScrollToXCalls[^1], 1);
    }

    // ── Finding #3: the scroll intent is one-shot; a later Today change targets Today ─

    [Fact]
    public void Finding3_A_Consumed_Preserve_Center_Intent_Expires_So_A_Later_Today_Change_Targets_Today()
    {
        // Exercises GanttTimeline's consumption directly (standalone, no Gantt3):
        // an explicit ScrollTargetDate is honored ONCE — on the render its
        // ScrollToTodayRequestId bump arrives — and then expires. A subsequent
        // browser-"today" change (same request id, ScrollTargetDate parameter
        // still lingering) must scroll to the NEW today, not the stale target.
        var tasks = new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 20)) };
        var rangeStart = D(2025, 12, 1);
        var rangeEnd = D(2026, 3, 1);
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, rangeStart, rangeEnd)[0];
        var colWidth = GanttScale.GetConfig(L.GanttViewMode.Day).ColumnWidth;
        var today = D(2026, 1, 15);
        var preserved = D(2026, 2, 1);

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, rangeStart)
            .Add(c => c.RangeEnd, rangeEnd)
            .Add(c => c.Today, today)
            .Add(c => c.ScrollToTodayRequestId, 0));

        // firstRender centered on Today (default intent, no target).
        // Now issue an explicit preserve-center intent (a recenter): request id 1.
        cut.Render(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, rangeStart)
            .Add(c => c.RangeEnd, rangeEnd)
            .Add(c => c.Today, today)
            .Add(c => c.ScrollTargetDate, preserved)
            .Add(c => c.ScrollToTodayRequestId, 1));

        var expectedPreservedX = GanttScale.DateToPixel(L.GanttViewMode.Day, origin, preserved, colWidth);
        Assert.Equal(expectedPreservedX, _interop.GanttV3ScrollToXCalls[^1], 1);

        // Midnight crossing: Today advances, SAME request id, ScrollTargetDate
        // parameter still present. The intent is already consumed, so this must
        // target the NEW today — not the stale preserved center.
        var newToday = D(2026, 1, 16);
        cut.Render(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, rangeStart)
            .Add(c => c.RangeEnd, rangeEnd)
            .Add(c => c.Today, newToday)
            .Add(c => c.ScrollTargetDate, preserved)
            .Add(c => c.ScrollToTodayRequestId, 1));

        var expectedTodayX = GanttScale.DateToPixel(L.GanttViewMode.Day, origin, newToday, colWidth);
        Assert.Equal(expectedTodayX, _interop.GanttV3ScrollToXCalls[^1], 1);
        Assert.NotEqual(expectedPreservedX, _interop.GanttV3ScrollToXCalls[^1], 0);
    }

    // ── Finding #4: no half-reconciled frame while the capture is in flight ──

    [Fact]
    public async Task Finding4_Tasks_Are_Not_Committed_Until_After_The_Live_Center_Capture_Resolves()
    {
        // On Blazor Server the capture await inside the reconcile lets an
        // intermediate render interleave. The old code committed the new tasks
        // BEFORE that await, so that render showed the new tasks against the
        // still-un-moved scroll — a visible half-reconciled frame. The capture
        // now runs BEFORE any commit: while it is suspended, the chart still
        // shows a fully OLD, coherent frame.
        var taskA = new L.GanttTask("t1", "A", D(2026, 1, 1), D(2026, 1, 10));
        var taskB = new L.GanttTask("t2", "B", D(2010, 6, 1), D(2010, 6, 10)); // a different, non-empty window
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { taskA })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        Assert.Single(cut.FindAll("[data-task-id='t1']"));

        // Suspend the reconcile inside the live-center capture.
        var gate = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = gate;
        var capturesBefore = _interop.GanttV3GetScrollCenterXCallCount;
        var scrollsBefore = _interop.GanttV3ScrollToXCalls.Count;

        // Fire a combined tasks + mode change (needs a capture) WITHOUT awaiting
        // its completion — block-bodied lambda so bUnit's InvokeAsync doesn't
        // await the (now-suspended) reconcile internally (see
        // DataGridAwaitedCommitRaceTests for the same idiom).
        Task reconcile = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            reconcile = cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(L.Gantt3.Tasks)] = new List<L.GanttTask> { taskB },
                [nameof(L.Gantt3.ViewMode)] = L.GanttViewMode.Week,
            }));
        });

        Assert.False(reconcile.IsCompleted, "the reconcile should still be awaiting the live-center capture");
        Assert.True(_interop.GanttV3GetScrollCenterXCallCount > capturesBefore, "the capture must have been requested");

        // Blazor renders once when the async lifecycle yields at the gate — that
        // frame must still be the OLD, coherent one: taskA present, taskB NOT
        // yet committed (the old code committed the new tasks before the await).
        // Deliberately NOT cut.Render() — that re-applies bUnit's own tracked
        // (taskA) parameters and would fight the manual SetParametersAsync above.
        Assert.Single(cut.FindAll("[data-task-id='t1']"));
        Assert.Empty(cut.FindAll("[data-task-id='t2']"));
        Assert.Equal(0, _interop.GanttV3ScrollToXCalls.Count - scrollsBefore); // no recenter emitted yet either

        // Resume — the commit now lands as one coherent frame.
        gate.SetResult(0);
        await reconcile;

        Assert.Empty(cut.FindAll("[data-task-id='t1']"));
        Assert.Single(cut.FindAll("[data-task-id='t2']"));
    }
}
