using Bunit;
using Lumeo.GanttV3;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Gantt v3 Phase 2, T2 — progress drag (<c>CommitProgress</c>), click-vs-drag
/// (<c>NotifyTaskClick</c>), and CanDrop live validation (<c>ValidateDrop</c>).
/// Same headless-DOM limitation as T1's <c>GanttV3DragTests</c>: gantt-v3.js's
/// pointer/ghost geometry itself (progress-handle hit-testing, the CanDrop
/// snapped-position cache) never executes in bUnit — that is T4's Playwright
/// coverage. This file exercises the .NET-side seams: the JSInvokable methods
/// gantt-v3.js calls, the options bag it reads (<c>hasCanDrop</c>), and the
/// GanttBar markup gantt-v3.js hit-tests against.
/// </summary>
public class GanttV3ProgressClickCanDropTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3ProgressClickCanDropTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);
    private static L.GanttTask Task1(int progress = 40) => new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6), progress);
    private static L.GanttTask Milestone1 => new("m1", "Kickoff", D(2026, 1, 2), D(2026, 1, 2), IsMilestone: true);

    // ── GanttBar progress-handle markup ──────────────────────────────────────

    [Fact]
    public void GanttBar_Renders_Progress_Handle_When_Interactive_And_Not_Milestone()
    {
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, Task1()).Add(c => c.X, 0d).Add(c => c.Width, 100d).Add(c => c.RowIndex, 0)
            .Add(c => c.Readonly, false));

        Assert.NotEmpty(cut.FindAll("[data-gantt-progress-handle]"));
    }

    [Fact]
    public void GanttBar_Omits_Progress_Handle_When_Readonly()
    {
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, Task1()).Add(c => c.X, 0d).Add(c => c.Width, 100d).Add(c => c.RowIndex, 0)
            .Add(c => c.Readonly, true));

        Assert.Empty(cut.FindAll("[data-gantt-progress-handle]"));
    }

    [Fact]
    public void GanttBar_Omits_Progress_Handle_For_Milestone_Even_When_Interactive()
    {
        // v2 parity: gantt-v2.js's milestone branch returns before ever creating
        // a progress handle (gantt-v2.js:504) — milestones have no progress.
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, Milestone1).Add(c => c.X, 0d).Add(c => c.Width, 22d).Add(c => c.RowIndex, 0)
            .Add(c => c.Readonly, false));

        Assert.Empty(cut.FindAll("[data-gantt-progress-handle]"));
    }

    [Fact]
    public void GanttBar_Renders_DataTaskProgress_Attribute_Clamped()
    {
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, Task1(progress: 140)).Add(c => c.X, 0d).Add(c => c.Width, 100d).Add(c => c.RowIndex, 0));

        Assert.Equal("100", cut.Find("[data-task-id='t1']").GetAttribute("data-task-progress"));
    }

    // ── GanttTimeline.CommitProgress (JSInvokable) ───────────────────────────

    [Fact]
    public async Task CommitProgress_Fires_OnTaskUpdate_With_Progress_Source()
    {
        GanttTaskUpdate? received = null;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1() })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => { received = u; }));

        await cut.InvokeAsync(() => cut.Instance.CommitProgress("t1", 75));

        Assert.NotNull(received);
        Assert.Equal(GanttTaskUpdateSource.Progress, received!.Source);
        Assert.Equal(75, received.Task.Progress);
    }

    [Fact]
    public async Task CommitProgress_Clamps_Out_Of_Range_Values()
    {
        GanttTaskUpdate? received = null;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1() })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => { received = u; }));

        await cut.InvokeAsync(() => cut.Instance.CommitProgress("t1", 250));
        Assert.Equal(100, received!.Task.Progress);

        await cut.InvokeAsync(() => cut.Instance.CommitProgress("t1", -50));
        Assert.Equal(0, received!.Task.Progress);
    }

    [Fact]
    public async Task CommitProgress_Unknown_TaskId_Fires_Nothing()
    {
        var fired = false;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1() })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate _) => { fired = true; }));

        await cut.InvokeAsync(() => cut.Instance.CommitProgress("nope", 50));

        Assert.False(fired);
    }

    // ── GanttTimeline.NotifyTaskClick (JSInvokable) ──────────────────────────

    [Fact]
    public async Task NotifyTaskClick_Fires_OnTaskClick_With_The_Task()
    {
        L.GanttTask? clicked = null;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1() })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.OnTaskClick, (L.GanttTask t) => { clicked = t; }));

        await cut.InvokeAsync(() => cut.Instance.NotifyTaskClick("t1"));

        Assert.NotNull(clicked);
        Assert.Equal("t1", clicked!.Id);
    }

    [Fact]
    public async Task NotifyTaskClick_Unknown_TaskId_Fires_Nothing()
    {
        var fired = false;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1() })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.OnTaskClick, (L.GanttTask _) => { fired = true; }));

        await cut.InvokeAsync(() => cut.Instance.NotifyTaskClick("nope"));

        Assert.False(fired);
    }

    // ── GanttTimeline.ValidateDrop (JSInvokable) + hasCanDrop options flag ───

    [Fact]
    public void BuildDragOptions_HasCanDrop_False_When_CanDrop_Unset()
    {
        _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1() })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));

        var options = Assert.IsType<Dictionary<string, object?>>(_interop.LastGanttV3DragOptions);
        Assert.Equal(false, options["hasCanDrop"]);
    }

    [Fact]
    public void BuildDragOptions_HasCanDrop_True_When_CanDrop_Set()
    {
        _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1() })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.CanDrop, (L.GanttTask _, GanttScheduleDropContext _) => true));

        var options = Assert.IsType<Dictionary<string, object?>>(_interop.LastGanttV3DragOptions);
        Assert.Equal(true, options["hasCanDrop"]);
    }

    [Fact]
    public void ValidateDrop_Returns_True_When_CanDrop_Is_Null()
    {
        // Defensive default — JS shouldn't be calling this at all when
        // hasCanDrop is false, but the .NET side stays permissive if it does.
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1() })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));

        Assert.True(cut.Instance.ValidateDrop("t1", "move", "2026-01-05", "2026-01-09"));
    }

    [Fact]
    public void ValidateDrop_Invokes_CanDrop_With_Task_And_ProposedContext()
    {
        L.GanttTask? seenTask = null;
        GanttScheduleDropContext? seenContext = null;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1() })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.CanDrop, (L.GanttTask t, GanttScheduleDropContext ctx) =>
            {
                seenTask = t;
                seenContext = ctx;
                return false;
            }));

        var result = cut.Instance.ValidateDrop("t1", "resize-end", "2026-01-02", "2026-01-08");

        Assert.False(result);
        Assert.NotNull(seenTask);
        Assert.Equal("t1", seenTask!.Id);
        Assert.NotNull(seenContext);
        Assert.Equal(D(2026, 1, 2), seenContext!.ProposedStart);
        Assert.Equal(D(2026, 1, 8), seenContext.ProposedEnd);
        Assert.Equal(GanttTaskUpdateSource.ResizeEnd, seenContext.Source);
    }

    [Fact]
    public void ValidateDrop_Returns_CanDrop_Result_When_True()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1() })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.CanDrop, (L.GanttTask _, GanttScheduleDropContext _) => true));

        Assert.True(cut.Instance.ValidateDrop("t1", "move", "2026-01-05", "2026-01-09"));
    }

    [Fact]
    public void ValidateDrop_Unknown_TaskId_Returns_True_Defensively()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1() })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.CanDrop, (L.GanttTask _, GanttScheduleDropContext _) => false));

        Assert.True(cut.Instance.ValidateDrop("nope", "move", "2026-01-05", "2026-01-09"));
    }

    [Fact]
    public void ValidateDrop_Invalid_Date_String_Returns_False()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1() })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.CanDrop, (L.GanttTask _, GanttScheduleDropContext _) => true));

        Assert.False(cut.Instance.ValidateDrop("t1", "move", "not-a-date", "2026-01-09"));
    }

    // ── Gantt3 end-to-end (bubbled through the nested GanttTimeline) ─────────

    [Fact]
    public async Task Gantt3_Progress_Commit_Fires_OnProgressChange_Not_OnDateChange()
    {
        L.GanttTask? progressChanged = null;
        var dateChangedFired = false;
        GanttTaskUpdate? taskUpdate = null;

        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1() })
            .Add(c => c.OnProgressChange, (L.GanttTask t) => { progressChanged = t; })
            .Add(c => c.OnDateChange, (L.GanttTask _) => { dateChangedFired = true; })
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => { taskUpdate = u; }));

        var timeline = cut.FindComponent<L.GanttTimeline>();
        await cut.InvokeAsync(() => timeline.Instance.CommitProgress("t1", 90));

        Assert.NotNull(progressChanged);
        Assert.Equal(90, progressChanged!.Progress);
        Assert.False(dateChangedFired);

        Assert.NotNull(taskUpdate);
        Assert.Equal(GanttTaskUpdateSource.Progress, taskUpdate!.Source);

        Assert.Equal("90", cut.Find("[data-task-id='t1']").GetAttribute("data-task-progress"));
    }

    [Fact]
    public async Task Gantt3_Drag_Commit_Still_Fires_OnDateChange_Not_OnProgressChange()
    {
        // Regression guard for the Source-branch added alongside OnProgressChange:
        // a move/resize commit must still take the OnDateChange path.
        var progressChangedFired = false;
        L.GanttTask? dateChanged = null;

        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1() })
            .Add(c => c.OnProgressChange, (L.GanttTask _) => { progressChangedFired = true; })
            .Add(c => c.OnDateChange, (L.GanttTask t) => { dateChanged = t; }));

        var timeline = cut.FindComponent<L.GanttTimeline>();
        await cut.InvokeAsync(() => timeline.Instance.CommitDrag("t1", "move", "2026-01-05", "2026-01-09"));

        Assert.NotNull(dateChanged);
        Assert.False(progressChangedFired);
    }

    [Fact]
    public async Task Gantt3_OnTaskClick_Passthrough_Fires_With_The_Task()
    {
        L.GanttTask? clicked = null;
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1() })
            .Add(c => c.OnTaskClick, (L.GanttTask t) => { clicked = t; }));

        var timeline = cut.FindComponent<L.GanttTimeline>();
        await cut.InvokeAsync(() => timeline.Instance.NotifyTaskClick("t1"));

        Assert.NotNull(clicked);
        Assert.Equal("t1", clicked!.Id);
    }

    [Fact]
    public void Gantt3_CanDrop_Passthrough_Reaches_The_Nested_Timeline()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1() })
            .Add(c => c.CanDrop, (L.GanttTask _, GanttScheduleDropContext _) => false));

        var timeline = cut.FindComponent<L.GanttTimeline>();
        Assert.False(timeline.Instance.ValidateDrop("t1", "move", "2026-01-05", "2026-01-09"));
    }
}
