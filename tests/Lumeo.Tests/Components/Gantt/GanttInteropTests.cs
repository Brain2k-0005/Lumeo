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
