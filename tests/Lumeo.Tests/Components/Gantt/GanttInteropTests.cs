using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Gantt;

/// <summary>
/// The frappe-style SVG Gantt renderer lives in the gantt-v2.js satellite module and
/// never executes in bUnit's headless DOM, so the visual bars can't be asserted here.
/// What CAN — and must — be battle-tested is the .NET side of the JS&lt;-&gt;.NET contract:
/// the <c>[JSInvokable]</c> callbacks the renderer drives when the user clicks, drags,
/// or resizes a bar. Each is invoked exactly as the renderer would, and we assert the
/// public EventCallbacks fire and the task list is mutated in lock-step. This is the
/// real battle test for an interop-only component — previously it had host-div smoke
/// only.
/// </summary>
public class GanttInteropTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public GanttInteropTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static L.GanttTask Task1 =>
        new("t1", "Design", new DateTime(2026, 1, 1), new DateTime(2026, 1, 5), 20);

    [Fact]
    public async Task JsOnTaskClick_Fires_OnTaskClick_With_The_Clicked_Task()
    {
        L.GanttTask? clicked = null;
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.Tasks, new[] { Task1 })
            .Add(c => c.OnTaskClick, (L.GanttTask t) => { clicked = t; }));

        await cut.InvokeAsync(() => cut.Instance.JsOnTaskClick(Task1));

        Assert.NotNull(clicked);
        Assert.Equal("t1", clicked!.Id);
    }

    [Fact]
    public async Task JsOnTaskClick_Reports_The_Tasks_Current_ParentId_Not_The_Raw_JS_Payloads_Null()
    {
        // Bug fix (Codex round 2, P2 #2): gantt-v2.js's taskToJson never
        // serializes ParentId — the raw JS click payload always carries
        // ParentId == null, even for a task a GanttV3-hierarchy consumer has
        // set it on. OnTaskClick's argument must reflect the CURRENT stored
        // task's ParentId instead of forwarding the payload verbatim.
        var hierarchyTask = Task1 with { ParentId = "epic-1" };
        L.GanttTask? clicked = null;
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.Tasks, new[] { hierarchyTask })
            .Add(c => c.OnTaskClick, (L.GanttTask t) => { clicked = t; }));

        // The JS click payload never carries ParentId — mirror that exactly.
        Assert.Null(Task1.ParentId);
        await cut.InvokeAsync(() => cut.Instance.JsOnTaskClick(Task1));

        Assert.NotNull(clicked);
        Assert.Equal("epic-1", clicked!.ParentId);
    }

    [Fact]
    public async Task JsOnDateChange_Replaces_The_Task_And_Raises_OnDateChange_And_TasksChanged()
    {
        L.GanttTask? dateChanged = null;
        IEnumerable<L.GanttTask>? pushedTasks = null;
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.Tasks, new[] { Task1 })
            .Add(c => c.OnDateChange, (L.GanttTask t) => { dateChanged = t; })
            .Add(c => c.TasksChanged, (IEnumerable<L.GanttTask> ts) => { pushedTasks = ts; }));

        // The renderer reports a dragged bar: same Id, new dates.
        var moved = Task1 with { Start = new DateTime(2026, 1, 3), End = new DateTime(2026, 1, 9) };
        await cut.InvokeAsync(() => cut.Instance.JsOnDateChange(moved));

        Assert.NotNull(dateChanged);
        Assert.Equal(new DateTime(2026, 1, 9), dateChanged!.End);
        // TasksChanged carries the REPLACED task (ReplaceTask matched it by Id).
        Assert.NotNull(pushedTasks);
        Assert.Equal(new DateTime(2026, 1, 3), Assert.Single(pushedTasks!).Start);
    }

    [Fact]
    public async Task JsOnDateChange_Preserves_ParentId_Across_The_JS_Payload_Round_Trip()
    {
        // Regression (Codex review wave, feat/gantt-v3): gantt-v2.js's taskToJson
        // never serializes ParentId (v2 has no hierarchy concept), so the JS
        // payload ReplaceTask receives always has ParentId == null. A
        // GanttV3-hierarchy consumer dragging a v2-rendered bar for a task that
        // DOES set ParentId must not silently lose it.
        //
        // Round 2 (Codex review wave): the first version of this test only
        // asserted `pushedTasks` (via TasksChanged) and passed even when
        // OnDateChange's own argument still carried the UNMERGED task —
        // ReplaceTask's `t = t with {...}` reassigns a by-value parameter, so
        // returning nothing left every caller of ReplaceTask free to keep using
        // its own stale local. Asserting BOTH callback surfaces here closes
        // that gap.
        var hierarchyTask = Task1 with { ParentId = "epic-1" };
        L.GanttTask? dateChanged = null;
        IEnumerable<L.GanttTask>? pushedTasks = null;
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.Tasks, new[] { hierarchyTask })
            .Add(c => c.OnDateChange, (L.GanttTask t) => { dateChanged = t; })
            .Add(c => c.TasksChanged, (IEnumerable<L.GanttTask> ts) => { pushedTasks = ts; }));

        // The JS payload never carries ParentId — mirror that exactly (default null).
        var moved = Task1 with { Start = new DateTime(2026, 1, 3), End = new DateTime(2026, 1, 9) };
        Assert.Null(moved.ParentId);
        await cut.InvokeAsync(() => cut.Instance.JsOnDateChange(moved));

        var replaced = Assert.Single(pushedTasks!);
        Assert.Equal(new DateTime(2026, 1, 3), replaced.Start); // the edit itself still applied
        Assert.Equal("epic-1", replaced.ParentId); // but ParentId survived the round-trip

        Assert.NotNull(dateChanged);
        Assert.Equal(new DateTime(2026, 1, 3), dateChanged!.Start);
        Assert.Equal("epic-1", dateChanged.ParentId); // the OnDateChange ARGUMENT must carry it too
    }

    [Fact]
    public async Task JsOnProgressChange_Preserves_ParentId_Across_The_JS_Payload_Round_Trip()
    {
        // Mirror of the JsOnDateChange case above, for the progress-change path.
        var hierarchyTask = Task1 with { ParentId = "epic-1" };
        L.GanttTask? progressChanged = null;
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.Tasks, new[] { hierarchyTask })
            .Add(c => c.OnProgressChange, (L.GanttTask t) => { progressChanged = t; }));

        var advanced = Task1 with { Progress = 75 };
        Assert.Null(advanced.ParentId);
        await cut.InvokeAsync(() => cut.Instance.JsOnProgressChange(advanced));

        Assert.NotNull(progressChanged);
        Assert.Equal(75, progressChanged!.Progress);
        Assert.Equal("epic-1", progressChanged.ParentId); // the OnProgressChange ARGUMENT must carry it too
    }

    [Fact]
    public async Task JsOnProgressChange_Updates_Progress_And_Raises_Callbacks()
    {
        L.GanttTask? progressChanged = null;
        IEnumerable<L.GanttTask>? pushedTasks = null;
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.Tasks, new[] { Task1 })
            .Add(c => c.OnProgressChange, (L.GanttTask t) => { progressChanged = t; })
            .Add(c => c.TasksChanged, (IEnumerable<L.GanttTask> ts) => { pushedTasks = ts; }));

        var advanced = Task1 with { Progress = 80 };
        await cut.InvokeAsync(() => cut.Instance.JsOnProgressChange(advanced));

        Assert.Equal(80, progressChanged?.Progress);
        Assert.Equal(80, Assert.Single(pushedTasks!).Progress);
    }

    [Fact]
    public void Renders_The_Host_Surface_The_Js_Renderer_Mounts_Into()
    {
        var cut = _ctx.Render<L.Gantt>(p => p.Add(c => c.Tasks, new[] { Task1 }));
        Assert.Contains("lumeo-gantt-host", cut.Markup);
    }
}
