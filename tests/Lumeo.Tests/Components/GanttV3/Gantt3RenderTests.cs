using System.Globalization;
using System.Text.RegularExpressions;
using Bunit;
using Lumeo.GanttV3;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Read-only-markup regression tests for the GanttV3 render tree (design spec
/// Phase 2, T2): <see cref="L.GanttBar"/> (bar/milestone geometry + colour +
/// BarTemplate), <see cref="L.GanttTimeline"/> (header runs, grid, today
/// marker, row virtualization), and <see cref="L.Gantt3"/> (root
/// composition + Class/AdditionalAttributes splat). No drag/JS-interop is
/// exercised here — everything asserted is static markup produced from
/// <see cref="GanttScale"/> + plain parameters.
/// </summary>
public class Gantt3RenderTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Gantt3RenderTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // ── GanttBar ─────────────────────────────────────────────────────────────

    [Fact]
    public void GanttBar_Renders_Expected_CssCustomProperties_For_A_Regular_Bar()
    {
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 4));
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 38d)
            .Add(c => c.Width, 114d)
            .Add(c => c.RowIndex, 2)
            .Add(c => c.BarHeight, GanttScale.DefaultBarHeight));

        var wrapper = cut.Find("[data-task-id='t1']");
        var style = wrapper.GetAttribute("style") ?? "";
        Assert.Contains("--lumeo-gantt-bar-x:38px", style);
        Assert.Contains("--lumeo-gantt-bar-w:114px", style);
        Assert.Contains("--lumeo-gantt-bar-row:2", style);
        Assert.Contains("left:var(--lumeo-gantt-bar-x)", style);
        Assert.Contains("width:var(--lumeo-gantt-bar-w)", style);

        // top = row*36 + (36-22)/2 = 72 + 7 = 79
        var expectedTop = (2 * GanttScale.RowHeight) + (GanttScale.RowHeight - GanttScale.DefaultBarHeight) / 2.0;
        Assert.Contains($"top:{expectedTop.ToString(CultureInfo.InvariantCulture)}px", style);
    }

    [Fact]
    public void GanttBar_Milestone_Renders_With_The_Milestone_Diamond_Class()
    {
        var milestone = new L.GanttTask("m1", "Kickoff", D(2026, 1, 5), D(2026, 1, 5), IsMilestone: true);
        var regular = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 4));

        var milestoneCut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, milestone)
            .Add(c => c.X, 160d)
            .Add(c => c.Width, 22d)
            .Add(c => c.RowIndex, 0));
        Assert.Contains("lumeo-gantt-v3-milestone", milestoneCut.Markup);
        Assert.Equal("true", milestoneCut.Find("[data-task-id='m1']").GetAttribute("data-milestone"));

        // Regression (Codex review wave): the un-rotated square must be sized to
        // BarHeight/sqrt(2) so the ROTATED bounding box comes out to exactly
        // BarHeight (v2 parity) — a plain BarHeight-side square rotated 45deg
        // would bounding-box at BarHeight*sqrt(2), ~41% too large.
        var expectedSide = GanttScale.DefaultBarHeight / Math.Sqrt(2);
        var diamondStyle = milestoneCut.Find(".lumeo-gantt-v3-milestone").GetAttribute("style") ?? "";
        Assert.Contains($"width:{expectedSide.ToString(CultureInfo.InvariantCulture)}px", diamondStyle);
        Assert.Contains($"height:{expectedSide.ToString(CultureInfo.InvariantCulture)}px", diamondStyle);

        var regularCut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, regular)
            .Add(c => c.X, 38d)
            .Add(c => c.Width, 114d)
            .Add(c => c.RowIndex, 0));
        Assert.DoesNotContain("lumeo-gantt-v3-milestone", regularCut.Markup);
        Assert.Null(regularCut.Find("[data-task-id='t1']").GetAttribute("data-milestone"));
    }

    [Fact]
    public void GanttBar_Progress_Fill_Width_Reflects_Task_Progress()
    {
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 4), Progress: 40);
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 100d)
            .Add(c => c.RowIndex, 0));

        var progressFill = cut.Find(".lumeo-gantt-v3-bar-progress");
        Assert.Contains("width:40%", progressFill.GetAttribute("style"));
    }

    [Fact]
    public void GanttBar_BarTemplate_Override_Wins_Over_The_Default_Label()
    {
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 4));

        var withoutTemplate = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task).Add(c => c.X, 0d).Add(c => c.Width, 100d).Add(c => c.RowIndex, 0));
        Assert.Contains("lumeo-gantt-v3-bar-label", withoutTemplate.Markup);
        Assert.Contains("Design", withoutTemplate.Markup);

        var withTemplate = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task).Add(c => c.X, 0d).Add(c => c.Width, 100d).Add(c => c.RowIndex, 0)
            .Add(c => c.BarTemplate, (RenderFragment<L.GanttTask>)(t => b =>
            {
                b.OpenElement(0, "span");
                b.AddAttribute(1, "class", "custom-bar-content");
                b.AddContent(2, $"Custom: {t.Name}");
                b.CloseElement();
            })));
        Assert.DoesNotContain("lumeo-gantt-v3-bar-label", withTemplate.Markup);
        Assert.Contains("custom-bar-content", withTemplate.Markup);
        Assert.Contains("Custom: Design", withTemplate.Markup);
    }

    [Fact]
    public void GanttBar_Class_And_AdditionalAttributes_Splat_Onto_The_Root()
    {
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 4));
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task).Add(c => c.X, 0d).Add(c => c.Width, 100d).Add(c => c.RowIndex, 0)
            .Add(c => c.Class, "my-bar")
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "bar-1" }));

        Assert.Contains("my-bar", cut.Markup);
        Assert.Contains("data-testid=\"bar-1\"", cut.Markup);
    }

    // ── GanttTimeline ────────────────────────────────────────────────────────

    [Fact]
    public void GanttTimeline_Renders_A_Bar_Per_Leaf_Task_With_CssVars_Computed_From_GanttScale()
    {
        var rangeStart = D(2026, 1, 1);
        var rangeEnd = D(2026, 1, 10);
        var barTask = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 4));
        var milestoneTask = new L.GanttTask("m1", "Kickoff", D(2026, 1, 5), D(2026, 1, 5), IsMilestone: true);

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { barTask, milestoneTask })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, rangeStart)
            .Add(c => c.RangeEnd, rangeEnd));

        // Two rows in the DOM — one per task, in order.
        var bars = cut.FindAll("[data-task-id]");
        Assert.Equal(2, bars.Count);

        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, rangeStart, rangeEnd)[0];
        var colW = GanttScale.GetConfig(L.GanttViewMode.Day).ColumnWidth;

        // Bar task: x1 = dateToX(start), x2 = dateToX(end + 1 day), w = max(8, x2-x1).
        var x1 = GanttScale.DateToPixel(L.GanttViewMode.Day, origin, barTask.Start);
        var x2 = GanttScale.DateToPixel(L.GanttViewMode.Day, origin, barTask.End.AddDays(1));
        var expectedBarWidth = Math.Max(8, x2 - x1);
        var barStyle = cut.Find("[data-task-id='t1']").GetAttribute("style") ?? "";
        Assert.Contains($"--lumeo-gantt-bar-x:{x1.ToString(CultureInfo.InvariantCulture)}px", barStyle);
        Assert.Contains($"--lumeo-gantt-bar-w:{expectedBarWidth.ToString(CultureInfo.InvariantCulture)}px", barStyle);
        Assert.Contains("--lumeo-gantt-bar-row:0", barStyle);

        // Milestone: bounding box centered on its start column.
        var center = GanttScale.DateToPixel(L.GanttViewMode.Day, origin, milestoneTask.Start) + colW / 2.0;
        var half = GanttScale.DefaultBarHeight / 2.0;
        var msStyle = cut.Find("[data-task-id='m1']").GetAttribute("style") ?? "";
        Assert.Contains($"--lumeo-gantt-bar-x:{(center - half).ToString(CultureInfo.InvariantCulture)}px", msStyle);
        Assert.Contains($"--lumeo-gantt-bar-w:{((double)GanttScale.DefaultBarHeight).ToString(CultureInfo.InvariantCulture)}px", msStyle);
        Assert.Contains("--lumeo-gantt-bar-row:1", msStyle);
    }

    [Fact]
    public void GanttTimeline_BarColor_Delegate_Colors_The_Bars_Background_Not_A_Literal_String()
    {
        // Regression (feat/gantt-v3 T4 parity harness): GanttTimeline's
        // <GanttBar ... Color="row.Color" .../> was missing the "@" prefix —
        // since GanttBar.Color is string-typed, Razor treated the unprefixed
        // value as the LITERAL text "row.Color" instead of an expression, so
        // EVERY bar rendered "background-color:row.Color" (invalid CSS,
        // silently ignored by the browser) and BarColor never had any visible
        // effect. A Playwright v2/v3 parity check caught the literal string in
        // the rendered style attribute; this pins the fix in the fast suite.
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 4));
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.BarColor, (Func<L.GanttTask, string?>)(_ => "#f59e0b")));

        var bg = cut.Find("[data-task-id='t1'] .lumeo-gantt-v3-bar-bg");
        Assert.Contains("background-color:#f59e0b", bg.GetAttribute("style"));
        Assert.DoesNotContain("row.Color", cut.Markup);
    }

    [Fact]
    public void GanttTimeline_Bar_X_And_Today_Marker_Scale_With_A_ColumnWidth_Override()
    {
        // Regression (Codex review wave): GanttTimeline's header/grid already
        // scaled correctly with a ColumnWidth override (they read
        // EffectiveColumnWidth directly), but bar positions and the today
        // marker went through DateToPixel/BarGeometry, which silently ignored
        // the override and stayed on the mode's default 38px — visible
        // misalignment against the correctly-rescaled grid.
        var today = DateTime.Today;
        var task = new L.GanttTask("t1", "Design", today.AddDays(2), today.AddDays(4));
        var rangeStart = today.AddDays(-3);
        var rangeEnd = today.AddDays(10);

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, rangeStart)
            .Add(c => c.RangeEnd, rangeEnd)
            .Add(c => c.ColumnWidth, 76)
            .Add(c => c.TodayHighlight, true));

        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, rangeStart, rangeEnd)[0];
        var expectedX = GanttScale.DateToPixel(L.GanttViewMode.Day, origin, task.Start, 76);
        var expectedTodayX = GanttScale.DateToPixel(L.GanttViewMode.Day, origin, today, 76);

        var barStyle = cut.Find("[data-task-id='t1']").GetAttribute("style") ?? "";
        Assert.Contains($"--lumeo-gantt-bar-x:{expectedX.ToString(CultureInfo.InvariantCulture)}px", barStyle);

        var todayStyle = cut.Find(".lumeo-gantt-v3-today-line").GetAttribute("style") ?? "";
        Assert.Contains($"left:{expectedTodayX.ToString(CultureInfo.InvariantCulture)}px", todayStyle);
    }

    [Fact]
    public void GanttTimeline_Header_Shows_Expected_Upper_And_Lower_Runs_For_Day_Mode_Crossing_A_Month_Boundary()
    {
        var rangeStart = D(2026, 1, 28);
        var rangeEnd = D(2026, 2, 3);

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, rangeStart)
            .Add(c => c.RangeEnd, rangeEnd));

        var units = GanttScale.BuildDateUnits(L.GanttViewMode.Day, rangeStart, rangeEnd);
        var upperRuns = GanttScale.UpperRuns(L.GanttViewMode.Day, units);
        var lowerLabels = GanttScale.LowerLabels(L.GanttViewMode.Day, units);

        Assert.Equal(2, upperRuns.Count); // January run + February run
        foreach (var run in upperRuns)
            Assert.Contains(run.Label, cut.Markup);
        foreach (var label in lowerLabels)
            Assert.Contains(label, cut.Markup);
    }

    [Fact]
    public void GanttTimeline_Header_Shows_Expected_Upper_And_Lower_Runs_For_Month_Mode()
    {
        var rangeStart = D(2026, 1, 1);
        var rangeEnd = D(2026, 4, 1);

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Month)
            .Add(c => c.RangeStart, rangeStart)
            .Add(c => c.RangeEnd, rangeEnd));

        var units = GanttScale.BuildDateUnits(L.GanttViewMode.Month, rangeStart, rangeEnd);
        var upperRuns = GanttScale.UpperRuns(L.GanttViewMode.Month, units);
        var lowerLabels = GanttScale.LowerLabels(L.GanttViewMode.Month, units);

        Assert.Single(upperRuns); // one calendar year -> single "2026" run
        Assert.Equal("2026", upperRuns[0].Label);
        Assert.Contains("2026", cut.Markup);
        foreach (var label in lowerLabels)
            Assert.Contains(label, cut.Markup); // "Jan","Feb","Mar","Apr"
    }

    [Fact]
    public void GanttTimeline_Today_Marker_Present_When_TodayHighlight_And_Today_In_Range()
    {
        var today = DateTime.Today;
        var rangeStart = today.AddDays(-3);
        var rangeEnd = today.AddDays(3);
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, rangeStart)
            .Add(c => c.RangeEnd, rangeEnd)
            .Add(c => c.TodayHighlight, true));

        Assert.Contains("lumeo-gantt-v3-today-line", cut.Markup);

        // Rigor parity with the bar/milestone tests: assert the marker's actual
        // left px, not just its presence — a wrong Origin/off-by-one in TodayX
        // would otherwise go uncaught.
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, rangeStart, rangeEnd)[0];
        var expectedTodayX = GanttScale.DateToPixel(L.GanttViewMode.Day, origin, today);
        var marker = cut.Find(".lumeo-gantt-v3-today-line");
        Assert.Contains($"left:{expectedTodayX.ToString(CultureInfo.InvariantCulture)}px", marker.GetAttribute("style"));
    }

    [Fact]
    public void GanttTimeline_Today_Marker_Absent_When_TodayHighlight_Is_False()
    {
        var today = DateTime.Today;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, today.AddDays(-3))
            .Add(c => c.RangeEnd, today.AddDays(3))
            .Add(c => c.TodayHighlight, false));

        Assert.DoesNotContain("lumeo-gantt-v3-today-line", cut.Markup);
    }

    [Fact]
    public void GanttTimeline_Today_Marker_Absent_When_Today_Is_Outside_The_Visible_Range()
    {
        var farPast = DateTime.Today.AddYears(-5);
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, farPast.AddDays(-10))
            .Add(c => c.RangeEnd, farPast.AddDays(10))
            .Add(c => c.TodayHighlight, true));

        Assert.DoesNotContain("lumeo-gantt-v3-today-line", cut.Markup);
    }

    [Fact]
    public void GanttTimeline_Class_And_AdditionalAttributes_Splat_Onto_The_Root()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Class, "my-timeline")
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "timeline-1" }));

        Assert.Contains("my-timeline", cut.Markup);
        Assert.Contains("data-testid=\"timeline-1\"", cut.Markup);
    }

    // ── Gantt3 (root) ────────────────────────────────────────────────────────

    [Fact]
    public void Gantt3_Renders_Nav_And_Timeline_Without_Throwing_For_Default_And_Task_Backed_Fixtures()
    {
        var exception = Record.Exception(() => _ctx.Render<L.Gantt3>());
        Assert.Null(exception);

        var tasks = new List<L.GanttTask>
        {
            new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6)),
            new("m1", "Kickoff", D(2026, 1, 2), D(2026, 1, 2), IsMilestone: true),
        };
        var cut = _ctx.Render<L.Gantt3>(p => p.Add(c => c.Tasks, tasks));
        Assert.Contains("Design", cut.Markup);
        Assert.Contains("Kickoff", cut.Markup);
    }

    [Fact]
    public void Gantt3_Recomputes_VisibleRange_When_An_Entirely_New_Task_Set_Arrives()
    {
        var initialTasks = new List<L.GanttTask> { new("t1", "Old", D(2026, 1, 1), D(2026, 1, 5)) };
        var cut = _ctx.Render<L.Gantt3>(p => p.Add(c => c.Tasks, initialTasks));

        // Replace with a task set entirely outside the range materialized for
        // initialTasks (two years later) — regression (Codex review wave):
        // VisibleRange previously stayed pinned to the OLD range, so the new
        // task's bar rendered many thousands of columns off-canvas.
        var laterTasks = new List<L.GanttTask> { new("t2", "New", D(2028, 1, 1), D(2028, 1, 5)) };
        cut.Render(p => p.Add(c => c.Tasks, laterTasks));

        var barStyle = cut.Find("[data-task-id='t2']").GetAttribute("style") ?? "";
        var xMatch = Regex.Match(barStyle, @"--lumeo-gantt-bar-x:(-?[\d.]+)px");
        Assert.True(xMatch.Success, $"bar style missing --lumeo-gantt-bar-x: {barStyle}");
        var x = double.Parse(xMatch.Groups[1].Value, CultureInfo.InvariantCulture);

        // In-canvas: Day mode pads 60 columns (38px each) before the earliest
        // task, so a correctly recomputed range places the bar within a few
        // thousand pixels of the origin — nowhere near the tens of thousands
        // of pixels off it would be if the window were still centered on 2026.
        Assert.InRange(x, 0, 10_000);
    }

    [Fact]
    public void Gantt3_Class_And_AdditionalAttributes_Splat_Onto_The_Root()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Class, "my-gantt3")
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "gantt3-root" }));

        Assert.Contains("my-gantt3", cut.Markup);
        Assert.Contains("data-testid=\"gantt3-root\"", cut.Markup);
    }

    [Fact]
    public void Gantt3_Renders_The_ZoomLevel_Toolbar_Buttons()
    {
        var cut = _ctx.Render<L.Gantt3>();
        Assert.Contains("Day", cut.Markup);
        Assert.Contains("Week", cut.Markup);
        Assert.Contains("Month", cut.Markup);
        Assert.Contains("Year", cut.Markup);
    }
}
