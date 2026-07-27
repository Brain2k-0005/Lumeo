using System.Linq;
using Bunit;
using Lumeo.GanttV3;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Codex round 8 (PR #379, feat/gantt-v3) - 5 P2 findings: 2 real v2-
/// normalization parity gaps (date-only truncation for sub-day modes,
/// End&lt;Start filtering incl. dependency behavior) + 3 reconcile follow-ups
/// on earlier rounds' own fixes (tree-pane-offset recenter, direction-flip
/// preserving the LIVE center instead of Today, header-sync latched on
/// firstRender). See docs/superpowers/gantt-v3-cx8-report.md for the full
/// per-finding writeup. Finding #2's E2E angle (pan away, flip direction,
/// previously-centered date stays in viewport) lives in GanttV3RtlTests.cs.
/// </summary>
public class GanttV3CodexRound8Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3CodexRound8Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // ── Gantt3: recenter when ScrollHostLeadingOffset changes (P2 #1/#2) ────

    [Fact]
    public void Gantt3_Recenters_Using_The_Live_Scroll_Center_Captured_With_The_Old_Leading_Offset_When_ShowTreePane_Changes()
    {
        // Bug fix (Codex round 8 review, P2 #1): EffectiveShowTreePane
        // flipping (ShowTreePane toggled here; GroupBy added/removed or the
        // first ParentId appearing would trip the SAME check) shifts
        // ScrollHostLeadingOffset by TreePaneWidth, but nothing previously
        // told GanttTimeline to re-request its DOM scrollLeft against the new
        // layout.
        //
        // Bug fix (Codex round 8 review, P2 #2): the recenter must preserve
        // WHATEVER was actually on screen (a live scroll-center read), not
        // fall through to ScrollTargetDate ?? Today - this test's own
        // ground-truth math is what actually proves that: it reads the live
        // center using the OLD offset (TreePaneWidth, since ShowTreePane
        // starts true) and asserts the FINAL re-center target lands there
        // (converted through the NEW offset of 0), not on Today.
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 5));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, true));

        // TreePaneWidth (224) + 10 Day columns (38px each) - a live scroll
        // reading taken while the tree pane is still shown (LTR leading
        // offset = TreePaneWidth).
        const double treePaneWidth = 224;
        var cfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        _interop.GanttV3ScrollCenterXToReturn = treePaneWidth + 10 * cfg.ColumnWidth;

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        // Ground truth: replicate Gantt3's own ComputeInitialRange/Origin math
        // independently of the fix (Day mode: PadBefore/PadAfter*Step day
        // padding around the single task's own Start/End).
        var rangeStart = D(2026, 1, 1).AddDays(-cfg.PadBefore * cfg.Step);
        var rangeEnd = D(2026, 1, 5).AddDays(cfg.PadAfter * cfg.Step);
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, rangeStart, rangeEnd)[0];
        var expectedCenterDate = origin.Date.AddDays(10); // ((224+380) - 224) / 38 = 10
        var expectedScrollToX = GanttScale.DateToPixel(L.GanttViewMode.Day, origin, expectedCenterDate, cfg.ColumnWidth);

        Assert.NotEmpty(_interop.GanttV3ScrollToXCalls);
        Assert.Equal(expectedScrollToX, _interop.GanttV3ScrollToXCalls[^1], 3);
    }

    [Fact]
    public void Gantt3_Recenters_On_The_Live_Scroll_Center_Not_Today_When_Direction_Flips_After_A_Manual_Pan()
    {
        // Bug fix (Codex round 8 review, P2 #2): before this fix, a direction
        // flip's recenter fell through to GanttTimeline's own ScrollTargetX
        // (ScrollTargetDate ?? Today) - correct right after a fresh mount or a
        // view-mode switch (both DO set ScrollTargetDate), but WRONG the
        // moment a user has manually panned with neither trigger involved: it
        // silently snapped back to Today instead of preserving what the user
        // was actually looking at. No tree pane here (offset stays 0 on both
        // sides of the flip, LTR AND RTL) - isolates that the RECENTER
        // ITSELF (not just the offset math finding #1 covers) now uses the
        // live reading. Wraps Gantt3 in a real DirectionProvider (mirrors
        // GanttV3CodexRound5Tests' own RTL setup) and re-renders THAT with a
        // flipped Direction to exercise the exact cascading-parameter path a
        // real app's direction toggle would take, on the SAME mounted
        // Gantt3 instance.
        var farFromToday = D(2026, 6, 15); // deliberately far from DateTime.Today
        var task = new L.GanttTask("t1", "Task", farFromToday, farFromToday.AddDays(4));
        var cut = _ctx.Render<L.DirectionProvider>(p => p
            .Add(d => d.Direction, Lumeo.Services.LayoutDirection.Ltr)
            .AddChildContent<L.Gantt3>(g => g
                .Add(c => c.Tasks, new List<L.GanttTask> { task })
                .Add(c => c.ViewMode, L.GanttViewMode.Day)
                .Add(c => c.ShowTreePane, false)));

        var cfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        var rangeStart = farFromToday.AddDays(-cfg.PadBefore * cfg.Step);
        var rangeEnd = farFromToday.AddDays(4).AddDays(cfg.PadAfter * cfg.Step);
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, rangeStart, rangeEnd)[0];

        // Simulate the user having manually panned to a live center 20 columns
        // from Origin - nowhere near Today, and never assigned to
        // ScrollTargetDate (no view-mode switch, no Today click happened).
        var pannedToDate = origin.Date.AddDays(20);
        _interop.GanttV3ScrollCenterXToReturn = 20 * cfg.ColumnWidth;

        // The flip itself - LTR -> RTL on the same mounted Gantt3.
        cut.Render(p => p
            .Add(d => d.Direction, Lumeo.Services.LayoutDirection.Rtl)
            .AddChildContent<L.Gantt3>(g => g
                .Add(c => c.Tasks, new List<L.GanttTask> { task })
                .Add(c => c.ViewMode, L.GanttViewMode.Day)
                .Add(c => c.ShowTreePane, false)));

        var expectedScrollToX = GanttScale.DateToPixel(L.GanttViewMode.Day, origin, pannedToDate, cfg.ColumnWidth);

        Assert.NotEmpty(_interop.GanttV3ScrollToXCalls);
        Assert.Equal(expectedScrollToX, _interop.GanttV3ScrollToXCalls[^1], 3);
    }

    // ── GanttTimeline: header-sync reconcile on host-identity change (P2 #3) ─

    [Fact]
    public void GanttTimeline_Configures_Header_Sync_When_A_Standalone_Timeline_Loses_Its_Host()
    {
        // Bug fix (Codex round 8 review, P2 #3): header-scroll-sync
        // registration used to be decided ONLY on firstRender - a shared-pane
        // timeline (ScrollHost supplied at mount, header sync correctly
        // SKIPPED then) later losing its host (ScrollHost reverting to null,
        // reverting to standalone) never registered it, leaving the header
        // un-synced. Shares the SAME scrollHostChanged trigger the cx7
        // vertical-scroll-tracking rebind already established rather than a
        // second detection mechanism.
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.ScrollHost, new Microsoft.AspNetCore.Components.ElementReference("host-a")));

        Assert.Equal(0, _interop.GanttV3RegisterHeaderScrollSyncCallCount);

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.ScrollHost, (Microsoft.AspNetCore.Components.ElementReference?)null));

        Assert.Equal(1, _interop.GanttV3RegisterHeaderScrollSyncCallCount);
    }

    [Fact]
    public void GanttTimeline_Unregisters_Header_Sync_When_A_Standalone_Timeline_Later_Receives_A_Host()
    {
        // Symmetric case: standalone at mount (header sync registered), then
        // a parent supplies a host (shared-pane mode) - the sync must be torn
        // down, since GanttTimeline.RootStyle's remarks document it would
        // otherwise double-apply the horizontal offset (once via natural
        // scroll, once via the now-unnecessary transform).
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));

        Assert.Equal(1, _interop.GanttV3RegisterHeaderScrollSyncCallCount);
        Assert.Equal(0, _interop.GanttV3UnregisterHeaderScrollSyncCallCount);

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.ScrollHost, new Microsoft.AspNetCore.Components.ElementReference("host-a")));

        Assert.Equal(1, _interop.GanttV3UnregisterHeaderScrollSyncCallCount);
    }

    // ── GanttScale.BarGeometry: v2 date-only normalization parity (P2 #4) ───

    [Theory]
    [InlineData(L.GanttViewMode.QuarterDay)]
    [InlineData(L.GanttViewMode.HalfDay)]
    [InlineData(L.GanttViewMode.Day)]
    [InlineData(L.GanttViewMode.Week)]
    public void BarGeometry_Truncates_A_Tasks_Time_Of_Day_The_Same_As_v2s_ParseDate(L.GanttViewMode mode)
    {
        // Bug fix (Codex round 8 review, P2 #4): v2's normalizeTasks/parseDate
        // (gantt-v2.js) truncate Start/End to a plain calendar date BEFORE the
        // renderer ever sees them, unconditionally on view mode - v3 had no
        // equivalent, so a task carrying a real time-of-day rendered at that
        // exact fractional pixel offset instead. A task with a 14:37 start/end
        // time must render IDENTICALLY to its own midnight-truncated version
        // in every mode, sub-day ones (QuarterDay/HalfDay, where this is most
        // visible) included.
        var origin = D(2026, 3, 1);
        var cfg = GanttScale.GetConfig(mode);

        var timed = new L.GanttTask("t1", "Task", D(2026, 3, 4).AddHours(14).AddMinutes(37), D(2026, 3, 6).AddHours(9).AddMinutes(15));
        var midnight = new L.GanttTask("t1", "Task", D(2026, 3, 4), D(2026, 3, 6));

        var (timedX, timedWidth) = GanttScale.BarGeometry(timed, mode, origin, cfg.ColumnWidth, GanttScale.DefaultBarHeight);
        var (midnightX, midnightWidth) = GanttScale.BarGeometry(midnight, mode, origin, cfg.ColumnWidth, GanttScale.DefaultBarHeight);

        Assert.Equal(midnightX, timedX, 6);
        Assert.Equal(midnightWidth, timedWidth, 6);
    }

    [Fact]
    public void BarGeometry_Truncates_A_Milestones_Time_Of_Day_Too()
    {
        var origin = D(2026, 3, 1);
        var cfg = GanttScale.GetConfig(L.GanttViewMode.QuarterDay);

        var timed = new L.GanttTask("m1", "Kickoff", D(2026, 3, 4).AddHours(14).AddMinutes(37), D(2026, 3, 4).AddHours(14).AddMinutes(37), IsMilestone: true);
        var midnight = new L.GanttTask("m1", "Kickoff", D(2026, 3, 4), D(2026, 3, 4), IsMilestone: true);

        var (timedX, timedWidth) = GanttScale.BarGeometry(timed, L.GanttViewMode.QuarterDay, origin, cfg.ColumnWidth, GanttScale.DefaultBarHeight);
        var (midnightX, midnightWidth) = GanttScale.BarGeometry(midnight, L.GanttViewMode.QuarterDay, origin, cfg.ColumnWidth, GanttScale.DefaultBarHeight);

        Assert.Equal(midnightX, timedX, 6);
        Assert.Equal(midnightWidth, timedWidth, 6);
    }

    // ── GanttRowModel: v2 End<Start filtering parity (P2 #5) ────────────────

    [Fact]
    public void BuildVisibleRows_Drops_A_Non_Milestone_Task_Whose_End_Is_Before_Its_Start()
    {
        // Bug fix (Codex round 8 review, P2 #5): v2's normalizeTasks drops any
        // non-milestone task with End < Start (`.filter(t => t.end >=
        // t.start)`) before the renderer ever sees it - no bar, no row. v3
        // rendered an 8px sliver bar (BarGeometry's own Math.Max(8, ...)
        // width clamp) instead.
        var valid = new L.GanttTask("t1", "Valid", D(2026, 1, 1), D(2026, 1, 5));
        var invalid = new L.GanttTask("t2", "Invalid", D(2026, 1, 10), D(2026, 1, 5)); // End before Start

        var rows = GanttRowModel.BuildVisibleRows(new[] { valid, invalid }, new HashSet<string>());

        var row = Assert.Single(rows);
        Assert.Equal("t1", row.Task!.Id);
    }

    [Fact]
    public void BuildVisibleRows_Never_Filters_A_Milestone_Regardless_Of_Its_Own_Start_End_Relationship()
    {
        // v2 never actually filters a milestone on this rule at all - it
        // forces `end = start` for every milestone BEFORE the End<Start check
        // runs (gantt-v2.js line 90), so the rule can never trip for one.
        // Mirrored the same way: a milestone's End/Start relationship is
        // never even compared.
        var milestone = new L.GanttTask("m1", "Kickoff", D(2026, 1, 5), D(2026, 1, 5), IsMilestone: true);

        var rows = GanttRowModel.BuildVisibleRows(new[] { milestone }, new HashSet<string>());

        Assert.Single(rows);
    }

    [Fact]
    public void GanttArrowLayer_Drops_An_Arrow_Whose_Dependency_Points_At_A_Filtered_Out_Task()
    {
        // Bug fix (Codex round 8 review, P2 #5): mirrors v2's own arrow loop
        // (`const source = taskById.get(depId); if (!source) continue;` -
        // gantt-v2.js line 653), which silently skips an arrow whose source
        // task was itself dropped by normalizeTasks' End<Start filter. Since
        // BuildVisibleRows now drops the invalid task from Rows entirely,
        // GanttArrowLayer's own geometryByTaskId lookup naturally can't find
        // it - no separate dependency-cleanup code needed.
        var invalidUpstream = new L.GanttTask("t1", "Invalid", D(2026, 1, 10), D(2026, 1, 5)); // End before Start
        var downstream = new L.GanttTask("t2", "Build", D(2026, 1, 6), D(2026, 1, 8), Dependencies: new[] { "t1" });
        var rows = GanttRowModel.BuildVisibleRows(new[] { invalidUpstream, downstream }, new HashSet<string>());

        var cut = _ctx.Render<L.GanttArrowLayer>(p => p
            .Add(c => c.Rows, rows)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.Origin, D(2026, 1, 1))
            .Add(c => c.ColumnWidth, GanttScale.GetConfig(L.GanttViewMode.Day).ColumnWidth)
            .Add(c => c.BarHeight, GanttScale.DefaultBarHeight)
            .Add(c => c.Width, 2000d)
            .Add(c => c.Height, 200d));

        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-arrow"));
    }
}
