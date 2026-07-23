using System.Linq;
using Bunit;
using Lumeo.GanttV3;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Codex round 10 (PR #379, feat/gantt-v3) - 4 P2 findings: virtualized rows
/// contributing no in-flow height, a parent-driven ViewMode parameter change
/// not recentering (only the toolbar path did), ShowTreePane's default
/// deriving from unfiltered tasks, and Month/Year's Today-recenter preserving
/// a raw TimeSpan instead of the rendered column count. See
/// docs/superpowers/gantt-v3-cx10-report.md for the full per-finding writeup.
/// </summary>
public class GanttV3CodexRound10Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3CodexRound10Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // ── GanttTimeline: virtualized items need an in-flow height (P2 #1) ─────

    [Fact]
    public void GanttTimeline_Virtualized_Row_Items_Carry_An_In_Flow_Height_Style()
    {
        // Bug fix (Codex round 10 review, P2 #1): every Virtualize item
        // template rendered ONLY absolutely-positioned content (GanttBar's
        // own wrapper, the group-header stripe) - neither establishes an
        // in-flow box, so each materialized item contributed 0px to normal
        // document flow, which Virtualize's own placeholder/spacer sizing
        // depends on. Wraps each item in a plain (non-positioned) div with an
        // explicit height:RowHeight style - visual geometry is unaffected
        // (verified by the unchanged bar/arrow positions the OTHER Gantt
        // tests already assert, and by the visual snapshot suite staying
        // byte-identical - see this round's own report).
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 5));
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));

        var itemWrapper = cut.Find(".lumeo-gantt-v3-row-item");
        Assert.Contains($"height:{GanttScale.RowHeight}px", itemWrapper.GetAttribute("style"));
    }

    // ── Gantt3: parent-driven ViewMode change must recenter too (P2 #2) ─────

    [Fact]
    public void Gantt3_Recenters_Preserving_The_Live_Center_When_The_ViewMode_Parameter_Changes_On_A_Mounted_Chart()
    {
        // Bug fix (Codex round 10 review, P2 #2): a PARENT-driven ViewMode
        // parameter change (a two-way-bound consumer setting it directly,
        // not a click through GanttNav's own toolbar) used to fall into the
        // SAME branch as first-mount range seeding - recomputing the range
        // from task min/max and never bumping the recenter token, silently
        // discarding whatever the user had actually scrolled to. Now routes
        // through the EXACT SAME ApplyViewModeChangeAsync the toolbar path
        // uses.
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 5));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        var dayCfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        // A live scroll reading 20 Day-columns from Origin - nowhere near
        // the task's own min/max window's center, so a fallback-to-task-
        // min/max recompute would be trivially distinguishable from a
        // center-preserving one.
        _interop.GanttV3ScrollCenterXToReturn = 20 * dayCfg.ColumnWidth;

        var dayRangeStart = D(2026, 1, 1).AddDays(-dayCfg.PadBefore * dayCfg.Step);
        var dayRangeEnd = D(2026, 1, 5).AddDays(dayCfg.PadAfter * dayCfg.Step);
        var dayOrigin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, dayRangeStart, dayRangeEnd)[0];
        var pannedToDate = dayOrigin.Date.AddDays(20);

        // Parent-driven change: re-render with a DIFFERENT ViewMode
        // parameter value, NOT via GanttNav's own toolbar click.
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Week)
            .Add(c => c.ShowTreePane, false));

        // Ground truth: replicate ApplyViewModeChangeAsync's own math
        // independently - Week mode's own Origin/ColumnWidth, centered on
        // the SAME pannedToDate (padded around itself, matching
        // ApplyPadding's Day-unit branch which Week also uses).
        var weekCfg = GanttScale.GetConfig(L.GanttViewMode.Week);
        var newRangeStart = pannedToDate.AddDays(-weekCfg.PadBefore * weekCfg.Step);
        var newRangeEnd = pannedToDate.AddDays(weekCfg.PadAfter * weekCfg.Step);
        var newOrigin = GanttScale.BuildDateUnits(L.GanttViewMode.Week, newRangeStart, newRangeEnd)[0];
        var expectedScrollToX = GanttScale.DateToPixel(L.GanttViewMode.Week, newOrigin, pannedToDate, weekCfg.ColumnWidth);

        Assert.NotEmpty(_interop.GanttV3ScrollToXCalls);
        Assert.Equal(expectedScrollToX, _interop.GanttV3ScrollToXCalls[^1], 1);
    }

    // ── Gantt3: ShowTreePane default must use the filtered task set (P2 #3) ─

    [Fact]
    public void Gantt3_Does_Not_Show_The_Tree_Pane_When_The_Only_ParentId_Task_Is_Filtered_Out_As_Invalid()
    {
        // Bug fix (Codex round 10 review, P2 #3): DefaultShowTreePane used to
        // check the RAW, unfiltered task list for a ParentId - an invalid-
        // duration task (End<Start, non-milestone) carrying the ONLY
        // ParentId in the list showed the tree pane for what is, in the
        // actually-rendered Rows, a flat chart (that task never reaches
        // Rows at all).
        var validFlat = new L.GanttTask("t1", "Valid", D(2026, 1, 1), D(2026, 1, 5)); // no ParentId
        var invalidChild = new L.GanttTask("t2", "Invalid", D(2026, 1, 10), D(2026, 1, 5)) { ParentId = "t1" }; // End before Start

        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { validFlat, invalidChild })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        // The valid task still renders (proves this isn't just "everything
        // got filtered out") - GanttTree renders NOTHING at all when
        // EffectiveShowTreePane is false (the @if block in Gantt3's markup
        // skips it entirely, not just its rows).
        Assert.Single(cut.FindAll("[data-task-id='t1']"));
        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-tree-indent"));
    }

    // ── Gantt3.GoToTodayAsync: Month/Year recenter preserves column count (P2 #4) ─

    [Fact]
    public void GoToTodayAsync_Preserves_The_Rendered_Column_Count_In_Month_Mode_Across_A_February_Containing_Window()
    {
        // Bug fix (Codex round 10 review, P2 #4): re-deriving the recentered
        // end as `start + originalWidth` (a raw TimeSpan) is only correct
        // for Day/Week/Hour modes, where a "day"/"hour" is a fixed duration.
        // Month/Year units are NOT fixed-duration (variable month/year
        // lengths - Feb has 28/29 days, others 30/31), so the SAME tick span
        // re-applied from a DIFFERENT calendar start does not reliably
        // reproduce the SAME number of rendered columns. This task's own
        // Month-mode window (12-month padding both sides of Jan 2026) spans
        // Jan 2025 -> Jan 2027 inclusive - 25 columns, crossing Feb 2025 AND
        // Feb 2026 - and the column count must survive a Today click
        // regardless of which real calendar day the suite happens to run on.
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 15), D(2026, 1, 20));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Month));

        var columnsBefore = cut.FindAll("div.shrink-0.text-center.text-xs.text-muted-foreground").Count;
        Assert.True(columnsBefore > 0);

        var todayButton = cut.FindAll("button").First(b => b.TextContent.Trim() == "Today");
        todayButton.Click();

        var columnsAfter = cut.FindAll("div.shrink-0.text-center.text-xs.text-muted-foreground").Count;
        Assert.Equal(columnsBefore, columnsAfter);
    }

    [Fact]
    public void GoToTodayAsync_Preserves_The_Rendered_Column_Count_In_Year_Mode()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 15), D(2026, 1, 20));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Year));

        var columnsBefore = cut.FindAll("div.shrink-0.text-center.text-xs.text-muted-foreground").Count;
        Assert.True(columnsBefore > 0);

        var todayButton = cut.FindAll("button").First(b => b.TextContent.Trim() == "Today");
        todayButton.Click();

        var columnsAfter = cut.FindAll("div.shrink-0.text-center.text-xs.text-muted-foreground").Count;
        Assert.Equal(columnsBefore, columnsAfter);
    }
}
