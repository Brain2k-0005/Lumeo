using Bunit;
using Lumeo.GanttV3;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Codex round 11 (PR #379, feat/gantt-v3) - 3 P2 findings, all in Gantt3's
/// recenter state machine: combined parameter changes double-handling the
/// live center under inconsistent geometry, sub-day ranges seeding with
/// arbitrary minutes, and a populated-to-empty task-set change never
/// requesting a recenter. See docs/superpowers/gantt-v3-cx11-report.md for
/// the full per-finding writeup. Finding #2's own AlignToUnitStart unit
/// tests live in GanttScaleTests.cs; this file covers the full Gantt3
/// integration for all three.
/// </summary>
public class GanttV3CodexRound11Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3CodexRound11Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // ── Gantt3: one capture, one recenter for combined changes (P2 #1) ──────

    [Fact]
    public void Gantt3_Recenters_Once_Using_Consistent_Geometry_When_ViewMode_And_ShowTreePane_Change_Together()
    {
        // Bug fix (Codex round 11 review, P2 #1): before this fix, a ViewMode
        // change landing in the SAME render as a ShowTreePane/direction
        // change captured the live scroll center TWICE - once in the
        // (then-separate) tree-pane/direction reconcile, using the OLD
        // offset correctly, and again in ApplyViewModeChangeAsync, but by
        // then the tracked "last seen" offset fields had already been
        // updated to the NEW values, so that SECOND read misinterpreted the
        // SAME physical scroll position under the NEW geometry instead of
        // the OLD one it was actually captured under - an order-dependent,
        // wasted-interop-round-trip bug. Now: exactly ONE live-center read.
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 5));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, true));

        var dayCfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        // Live scroll reading while ShowTreePane=true (LTR leading offset =
        // TreePaneWidth = 224).
        const double treePaneWidth = 224;
        _interop.GanttV3ScrollCenterXToReturn = treePaneWidth + 10 * dayCfg.ColumnWidth;

        var dayRangeStart = D(2026, 1, 1).AddDays(-dayCfg.PadBefore * dayCfg.Step);
        var dayRangeEnd = D(2026, 1, 5).AddDays(dayCfg.PadAfter * dayCfg.Step);
        var dayOrigin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, dayRangeStart, dayRangeEnd)[0];
        var pannedToDate = dayOrigin.Date.AddDays(10);

        Assert.Equal(0, _interop.GanttV3GetScrollCenterXCallCount);

        // BOTH ViewMode AND ShowTreePane change in the SAME render.
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Week)
            .Add(c => c.ShowTreePane, false));

        // Exactly ONE live-center read for this combined change, not two.
        Assert.Equal(1, _interop.GanttV3GetScrollCenterXCallCount);

        // Ground truth: the range rebuilds around the SAME pannedToDate using
        // the NEW mode (Week) - the NEW offset is 0 (ShowTreePane now false),
        // so it doesn't factor into the final scroll-to target at all.
        var weekCfg = GanttScale.GetConfig(L.GanttViewMode.Week);
        var newRangeStart = pannedToDate.AddDays(-weekCfg.PadBefore * weekCfg.Step);
        var newRangeEnd = pannedToDate.AddDays(weekCfg.PadAfter * weekCfg.Step);
        var newOrigin = GanttScale.BuildDateUnits(L.GanttViewMode.Week, newRangeStart, newRangeEnd)[0];
        var expectedScrollToX = GanttScale.DateToPixel(L.GanttViewMode.Week, newOrigin, pannedToDate, weekCfg.ColumnWidth);

        Assert.NotEmpty(_interop.GanttV3ScrollToXCalls);
        Assert.Equal(expectedScrollToX, _interop.GanttV3ScrollToXCalls[^1], 1);
    }

    [Fact]
    public void Gantt3_Still_Recenters_When_Only_ShowTreePane_Changes_Without_A_ViewMode_Change()
    {
        // Regression guard for the consolidation: a PURE tree-pane change
        // (no ViewMode involved at all) must NOT rebuild VisibleRange around
        // the captured center (only a genuine mode switch needs a reshaped
        // range) - only the scroll target should move. Catches the exact bug
        // an early draft of this fix introduced (see ReconcileRecenterTriggersAsync's
        // own remarks): routing this case through the same range-rebuilding
        // helper the ViewMode branch uses replaced the task-derived range
        // with a brand new one narrowly centered on the captured date.
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 5));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, true));

        var dayCfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        const double treePaneWidth = 224;
        _interop.GanttV3ScrollCenterXToReturn = treePaneWidth + 10 * dayCfg.ColumnWidth;

        var dayRangeStart = D(2026, 1, 1).AddDays(-dayCfg.PadBefore * dayCfg.Step);
        var dayRangeEnd = D(2026, 1, 5).AddDays(dayCfg.PadAfter * dayCfg.Step);
        var dayOrigin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, dayRangeStart, dayRangeEnd)[0];
        var pannedToDate = dayOrigin.Date.AddDays(10);

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day) // UNCHANGED
            .Add(c => c.ShowTreePane, false));

        // The range stays the ORIGINAL Day-mode task-derived one - the SAME
        // Origin as before the change, not a narrow window centered on the
        // panned-to date.
        var expectedScrollToX = GanttScale.DateToPixel(L.GanttViewMode.Day, dayOrigin, pannedToDate, dayCfg.ColumnWidth);
        Assert.NotEmpty(_interop.GanttV3ScrollToXCalls);
        Assert.Equal(expectedScrollToX, _interop.GanttV3ScrollToXCalls[^1], 1);
    }

    // ── Gantt3: sub-day ranges align to their unit boundary (P2 #2) ─────────

    [Fact]
    public void Gantt3_Aligns_The_New_Range_To_A_Six_Hour_Boundary_When_Switching_To_QuarterDay_With_A_Sub_Hour_Center()
    {
        // Bug fix (Codex round 11 review, P2 #2): switching to QuarterDay/
        // HalfDay after a pan supplies a continuous center carrying arbitrary
        // minutes/seconds (PixelToDateContinuous's Hour branch is a plain
        // origin.AddHours(...), no rounding) - the new range's own start (and
        // therefore every rendered Time6h header label) used to sit at that
        // same arbitrary time instead of a clean 6-/12-hour boundary.
        var task = new L.GanttTask("t1", "Task", D(2026, 3, 15), D(2026, 3, 16));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        var dayCfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        var dayRangeStart = D(2026, 3, 15).AddDays(-dayCfg.PadBefore * dayCfg.Step);
        var dayRangeEnd = D(2026, 3, 16).AddDays(dayCfg.PadAfter * dayCfg.Step);
        var dayOrigin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, dayRangeStart, dayRangeEnd)[0];

        // A live scroll reading landing on an arbitrary sub-hour time.
        var pannedToDate = D(2026, 3, 20).AddHours(14).AddMinutes(37);
        var daysFromOrigin = (pannedToDate - dayOrigin).TotalDays;
        _interop.GanttV3ScrollCenterXToReturn = daysFromOrigin * dayCfg.ColumnWidth;

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.QuarterDay)
            .Add(c => c.ShowTreePane, false));

        // Ground truth: replicate Gantt3's own pipeline (align center ->
        // ApplyPadding's Hour-unit branch -> BuildDateUnits[0] as Origin)
        // independently of the fix.
        var qCfg = GanttScale.GetConfig(L.GanttViewMode.QuarterDay);
        var alignedCenter = GanttScale.AlignToUnitStart(L.GanttViewMode.QuarterDay, pannedToDate);
        Assert.Equal(0, alignedCenter.Hour % qCfg.Step);
        Assert.Equal(0, alignedCenter.Minute);
        Assert.Equal(0, alignedCenter.Second);

        var expectedRangeStart = alignedCenter.AddDays(-Math.Ceiling(qCfg.PadBefore * qCfg.Step / 24.0));
        var expectedRangeEnd = alignedCenter.AddDays(Math.Ceiling(qCfg.PadAfter * qCfg.Step / 24.0));
        var expectedOrigin = GanttScale.BuildDateUnits(L.GanttViewMode.QuarterDay, expectedRangeStart, expectedRangeEnd)[0];

        // The range's own start (and therefore every column boundary) sits
        // on a clean 6-hour mark.
        Assert.Equal(0, expectedOrigin.Hour % qCfg.Step);
        Assert.Equal(0, expectedOrigin.Minute);

        // The recenter TARGET itself stays the RAW (unaligned) panned-to
        // date - preserving exactly what the user was looking at, not
        // rounding it to the nearest boundary.
        var expectedScrollToX = GanttScale.DateToPixel(L.GanttViewMode.QuarterDay, expectedOrigin, pannedToDate, qCfg.ColumnWidth);
        Assert.NotEmpty(_interop.GanttV3ScrollToXCalls);
        Assert.Equal(expectedScrollToX, _interop.GanttV3ScrollToXCalls[^1], 1);
    }

    // ── Gantt3: populated -> empty must recenter too (P2 #3) ────────────────

    [Fact]
    public void Gantt3_Requests_A_Recenter_When_The_Task_List_Becomes_Empty()
    {
        // Bug fix (Codex round 11 review, P2 #3): emptying the task list
        // rebuilt the range around the empty-list today-based fallback but
        // never requested a recenter - the viewport stayed wherever it was
        // under the OLD (populated) range's coordinate space, showing an
        // arbitrary region of the brand-new fallback window. Uses a
        // far-from-today task so the OLD and NEW ranges are unambiguously
        // different windows.
        var farPastTask = new L.GanttTask("t1", "Task", D(2010, 1, 1), D(2010, 1, 10));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { farPastTask })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        var scrollToCallsBefore = _interop.GanttV3ScrollToXCallCount;

        cut.Render(p => p
            .Add(c => c.Tasks, Array.Empty<L.GanttTask>())
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        Assert.True(_interop.GanttV3ScrollToXCallCount > scrollToCallsBefore);

        // Ground truth: the empty-fallback range is ALWAYS centered on
        // EffectiveToday (ComputeInitialRange's own empty-list branch), so
        // this targets Today.
        var cfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        var rangeStart = DateTime.Today.AddDays(-7).AddDays(-cfg.PadBefore * cfg.Step);
        var rangeEnd = DateTime.Today.AddDays(14).AddDays(cfg.PadAfter * cfg.Step);
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, rangeStart, rangeEnd)[0];
        var expectedScrollToX = GanttScale.DateToPixel(L.GanttViewMode.Day, origin, DateTime.Today, cfg.ColumnWidth);

        Assert.Equal(expectedScrollToX, _interop.GanttV3ScrollToXCalls[^1], 1);
    }

    [Fact]
    public void Gantt3_Does_Not_Recenter_When_A_Non_Empty_Task_List_Is_Merely_Replaced()
    {
        // Regression guard: this fix's scope is specifically the emptiness
        // TRANSITION (wasEmpty != nowEmpty) - an ordinary populated ->
        // populated task-set change (still non-empty either way) must stay
        // unaffected, matching pre-existing behavior.
        var taskA = new L.GanttTask("t1", "Task A", D(2026, 1, 1), D(2026, 1, 5));
        var taskB = new L.GanttTask("t2", "Task B", D(2026, 2, 1), D(2026, 2, 5));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { taskA })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        var scrollToCallsBefore = _interop.GanttV3ScrollToXCallCount;

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { taskB })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        Assert.Equal(scrollToCallsBefore, _interop.GanttV3ScrollToXCallCount);
    }

    [Fact]
    public void GanttTimeline_Empty_State_Message_Is_Unaffected_By_The_Recenter_Fix()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 5));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-empty"));

        cut.Render(p => p
            .Add(c => c.Tasks, Array.Empty<L.GanttTask>())
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        Assert.Single(cut.FindAll(".lumeo-gantt-v3-empty"));
    }
}
