using Bunit;
using Lumeo.GanttV3;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Codex round 12 (PR #379, feat/gantt-v3) - the single remaining P1: the
/// tasksChanged x viewModeChanged matrix wasn't exhaustive. A ViewMode change
/// landing in the SAME render as a task-set change previously took the
/// mode-only path outright (self-centered the range around a captured LIVE
/// SCROLL position with zero awareness the task set also changed) - the
/// common async-load shape (render once empty/placeholder, then populate
/// real tasks, possibly alongside a caller ALSO switching modes) could land
/// the real tasks entirely outside the resulting range, with no recenter
/// ever bringing them into view. See docs/superpowers/gantt-v3-cx12-report.md
/// for the full writeup and the explicit 4-case matrix (also documented as a
/// comment table directly in Gantt3.razor's own OnParametersSetAsync).
/// </summary>
public class GanttV3CodexRound12Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3CodexRound12Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // ── Case 1: neither tasksChanged nor viewModeChanged ────────────────────

    [Fact]
    public void Case1_Neither_Tasks_Nor_ViewMode_Change_Requests_No_Recenter()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 5));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        var scrollToCallsBefore = _interop.GanttV3ScrollToXCallCount;

        // Re-render with an EQUAL (structurally, so GanttState.SetTasks no-ops
        // and tasksChanged stays false) task list and the SAME ViewMode.
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        Assert.Equal(scrollToCallsBefore, _interop.GanttV3ScrollToXCallCount);
    }

    // ── Case 2: viewModeChanged alone (tasksChanged false) ──────────────────
    // Already covered thoroughly by round 10/11's own tests; a light
    // confirmation here completes the explicit 4-case matrix in one place.

    [Fact]
    public void Case2_ViewMode_Change_Alone_Recenters_On_The_Preserved_Live_Center()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 5));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        var dayCfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        var dayRangeStart = D(2026, 1, 1).AddDays(-dayCfg.PadBefore * dayCfg.Step);
        var dayRangeEnd = D(2026, 1, 5).AddDays(dayCfg.PadAfter * dayCfg.Step);
        var dayOrigin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, dayRangeStart, dayRangeEnd)[0];
        var pannedToDate = dayOrigin.Date.AddDays(10);
        _interop.GanttV3ScrollCenterXToReturn = 10 * dayCfg.ColumnWidth;

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task }) // SAME reference content, tasksChanged stays false
            .Add(c => c.ViewMode, L.GanttViewMode.Week)
            .Add(c => c.ShowTreePane, false));

        var weekCfg = GanttScale.GetConfig(L.GanttViewMode.Week);
        var newRangeStart = pannedToDate.AddDays(-weekCfg.PadBefore * weekCfg.Step);
        var newRangeEnd = pannedToDate.AddDays(weekCfg.PadAfter * weekCfg.Step);
        var newOrigin = GanttScale.BuildDateUnits(L.GanttViewMode.Week, newRangeStart, newRangeEnd)[0];
        var expectedScrollToX = GanttScale.DateToPixel(L.GanttViewMode.Week, newOrigin, pannedToDate, weekCfg.ColumnWidth);

        Assert.NotEmpty(_interop.GanttV3ScrollToXCalls);
        Assert.Equal(expectedScrollToX, _interop.GanttV3ScrollToXCalls[^1], 1);
    }

    // ── Case 3: tasksChanged alone (viewModeChanged false) ──────────────────
    // Already covered thoroughly by round 9/11's own tests; a light
    // confirmation here completes the matrix.

    [Fact]
    public void Case3_Tasks_Change_Alone_Recenters_On_The_Tasks_When_It_Is_An_Emptiness_Transition()
    {
        var farPastTask = new L.GanttTask("t1", "Task", D(2010, 1, 1), D(2010, 1, 10));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, Array.Empty<L.GanttTask>())
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        var scrollToCallsBefore = _interop.GanttV3ScrollToXCallCount;

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { farPastTask })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)); // UNCHANGED mode

        Assert.True(_interop.GanttV3ScrollToXCallCount > scrollToCallsBefore);

        var cfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        var rangeStart = D(2010, 1, 1).AddDays(-cfg.PadBefore * cfg.Step);
        var rangeEnd = D(2010, 1, 10).AddDays(cfg.PadAfter * cfg.Step);
        var midpoint = rangeStart + new TimeSpan((rangeEnd - rangeStart).Ticks / 2);
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, rangeStart, rangeEnd)[0];
        var expectedScrollToX = GanttScale.DateToPixel(L.GanttViewMode.Day, origin, midpoint, cfg.ColumnWidth);

        Assert.Equal(expectedScrollToX, _interop.GanttV3ScrollToXCalls[^1], 1);
    }

    // ── Case 4: BOTH tasksChanged AND viewModeChanged (this round's fix) ────

    [Fact]
    public void Case4_Empty_To_Populated_With_A_Mode_Change_In_One_Render_Centers_On_The_New_Tasks()
    {
        // The dispatch's own headline scenario: empty -> populated (far from
        // today) AND a mode change in ONE SetParametersAsync call. Before
        // this fix, the mode-only path won outright and self-centered the
        // range around a captured LIVE SCROLL position that had nothing to
        // do with these brand-new, far-past tasks - they landed entirely
        // outside the resulting range.
        var farPastTask = new L.GanttTask("t1", "Task", D(2010, 1, 1), D(2010, 1, 10));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, Array.Empty<L.GanttTask>())
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        var scrollToCallsBefore = _interop.GanttV3ScrollToXCallCount;

        // BOTH change in the SAME render: Tasks empty -> populated, AND
        // ViewMode Day -> Week.
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { farPastTask })
            .Add(c => c.ViewMode, L.GanttViewMode.Week));

        Assert.True(_interop.GanttV3ScrollToXCallCount > scrollToCallsBefore);

        // Ground truth: the range must be TASK-derived under the NEW mode
        // (Week) - NOT self-centered around whatever the old, empty Day-mode
        // chart's own scroll position was. This is an emptiness transition
        // (wasEmpty=true, nowEmpty=false), and Today (~2026) falls nowhere
        // near this 2010 task, so the recenter target is the new range's
        // own midpoint.
        var weekCfg = GanttScale.GetConfig(L.GanttViewMode.Week);
        var rangeStart = D(2010, 1, 1).AddDays(-weekCfg.PadBefore * weekCfg.Step);
        var rangeEnd = D(2010, 1, 10).AddDays(weekCfg.PadAfter * weekCfg.Step);
        var midpoint = rangeStart + new TimeSpan((rangeEnd - rangeStart).Ticks / 2);
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Week, rangeStart, rangeEnd)[0];
        var expectedScrollToX = GanttScale.DateToPixel(L.GanttViewMode.Week, origin, midpoint, weekCfg.ColumnWidth);

        Assert.Equal(expectedScrollToX, _interop.GanttV3ScrollToXCalls[^1], 1);

        // Independent confirmation that the range genuinely contains the
        // task: its own bar must actually render (Virtualize wouldn't
        // materialize it at all if the range/columns didn't cover its dates).
        Assert.Single(cut.FindAll("[data-task-id='t1']"));
    }

    [Fact]
    public void Case4_Populated_To_Empty_With_A_Mode_Change_In_One_Render_Still_Targets_Today()
    {
        // The reverse emptiness direction, combined with a mode change.
        var task = new L.GanttTask("t1", "Task", D(2010, 1, 1), D(2010, 1, 10));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        var scrollToCallsBefore = _interop.GanttV3ScrollToXCallCount;

        cut.Render(p => p
            .Add(c => c.Tasks, Array.Empty<L.GanttTask>())
            .Add(c => c.ViewMode, L.GanttViewMode.Week));

        Assert.True(_interop.GanttV3ScrollToXCallCount > scrollToCallsBefore);

        // The empty-list fallback range is ALWAYS centered on Today by
        // construction, so todayInRange is always true here regardless of
        // mode - this targets Today (ScrollTargetDate cleared to null).
        var weekCfg = GanttScale.GetConfig(L.GanttViewMode.Week);
        var rangeStart = DateTime.Today.AddDays(-7).AddDays(-weekCfg.PadBefore * weekCfg.Step);
        var rangeEnd = DateTime.Today.AddDays(14).AddDays(weekCfg.PadAfter * weekCfg.Step);
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Week, rangeStart, rangeEnd)[0];
        var expectedScrollToX = GanttScale.DateToPixel(L.GanttViewMode.Week, origin, DateTime.Today, weekCfg.ColumnWidth);

        Assert.Equal(expectedScrollToX, _interop.GanttV3ScrollToXCalls[^1], 1);
    }

    [Fact]
    public void Case4_Non_Empty_Task_Set_Replaced_With_A_Mode_Change_Preserves_The_Live_Center()
    {
        // Tasks change to a DIFFERENT non-empty set (not an emptiness
        // transition) in the SAME render as a mode change - the recenter
        // target must be the PRESERVED live-scroll center (captured under
        // the OLD mode/range), not Today and not the new tasks' own midpoint.
        var taskA = new L.GanttTask("t1", "Task A", D(2026, 1, 1), D(2026, 1, 5));
        var taskB = new L.GanttTask("t2", "Task B", D(2010, 6, 1), D(2010, 6, 10)); // a totally different window
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { taskA })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        var dayCfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        var dayRangeStart = D(2026, 1, 1).AddDays(-dayCfg.PadBefore * dayCfg.Step);
        var dayRangeEnd = D(2026, 1, 5).AddDays(dayCfg.PadAfter * dayCfg.Step);
        var dayOrigin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, dayRangeStart, dayRangeEnd)[0];
        var pannedToDate = dayOrigin.Date.AddDays(10);
        _interop.GanttV3ScrollCenterXToReturn = 10 * dayCfg.ColumnWidth;

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { taskB })
            .Add(c => c.ViewMode, L.GanttViewMode.Week)
            .Add(c => c.ShowTreePane, false));

        // Ground truth: the RANGE is task-derived (taskB's own window, Week
        // mode), but the RECENTER TARGET is the preserved pannedToDate
        // captured under the OLD (Day-mode) geometry - independent of where
        // taskB actually is.
        var weekCfg = GanttScale.GetConfig(L.GanttViewMode.Week);
        var rangeStart = D(2010, 6, 1).AddDays(-weekCfg.PadBefore * weekCfg.Step);
        var rangeEnd = D(2010, 6, 10).AddDays(weekCfg.PadAfter * weekCfg.Step);
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Week, rangeStart, rangeEnd)[0];
        var expectedScrollToX = GanttScale.DateToPixel(L.GanttViewMode.Week, origin, pannedToDate, weekCfg.ColumnWidth);

        Assert.NotEmpty(_interop.GanttV3ScrollToXCalls);
        Assert.Equal(expectedScrollToX, _interop.GanttV3ScrollToXCalls[^1], 1);
    }
}
