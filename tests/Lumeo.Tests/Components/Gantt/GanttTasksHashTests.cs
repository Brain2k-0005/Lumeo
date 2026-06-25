using Bunit;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Gantt;

/// <summary>
/// Regression tests for the change-detection hash that gates Gantt's
/// .NET → JS task re-push (<c>OnParametersSetAsync</c> only calls
/// <c>gantt.setTasks</c> when <c>ComputeTasksHash</c> changes).
///
/// The bug (battle-test wave 1, finding #2): <c>ComputeTasksHash</c> folded only
/// the raw <c>GanttTask</c> record fields and skipped the OUTPUT of the
/// <c>BarColor</c> and <c>GroupBy</c> delegates. Those delegates are the only
/// task-visible state that lives OUTSIDE the record itself: a parent can swap the
/// <c>BarColor</c> lambda (recolour every bar) or the <c>GroupBy</c> lambda
/// (re-cluster the lanes) while leaving the task list value-identical. The hash
/// was unchanged, so the re-push was suppressed and the recolour/regroup was
/// silently dropped — the JS canvas kept the old colours/order.
///
/// These tests mirror <see cref="Lumeo.Tests.Components.Scheduler.SchedulerEventsHashTests"/>:
/// the Gantt's own isolated module is pre-registered in Loose mode and
/// <c>gantt.init</c> is stubbed to return a non-empty instance id, which is what
/// gives the component a non-null <c>_instanceId</c> AND sets
/// <c>_initialized = true</c> — both required for the OnParametersSet round-trip
/// guard to even reach <c>gantt.setTasks</c>.
///
/// Each test renders once (init fires setTasks-free), then re-renders with only the
/// BarColor / GroupBy delegate swapped and asserts a <c>gantt.setTasks</c>
/// invocation was dispatched — i.e. the recolour/regroup was detected. Without the
/// fix these fail because the hash is unchanged and no setTasks call is ever made.
/// </summary>
public class GanttTasksHashTests : IAsyncLifetime
{
    private const string ModulePath = "./_content/Lumeo.Gantt/js/gantt-v2.js";
    private const string InstanceId = "gantt-instance-1";

    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();

        _module = _ctx.JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;

        // A non-empty init result is what flips _initialized = true and captures a
        // non-null _instanceId, so the OnParametersSetAsync guard can fire setTasks.
        _module.Setup<string>("gantt.init", _ => true).SetResult(InstanceId);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static int SetTasksCount(BunitJSModuleInterop module) =>
        module.Invocations.Count(i => i.Identifier == "gantt.setTasks");

    private static L.GanttTask Task1 =>
        new("t1", "Design", new DateTime(2026, 1, 1), new DateTime(2026, 1, 5), 20);

    private static L.GanttTask Task2 =>
        new("t2", "Build", new DateTime(2026, 1, 6), new DateTime(2026, 1, 10), 0);

    [Fact]
    public void Swapping_BarColor_delegate_repushes_tasks()
    {
        // First render: every bar is the theme primary (BarColor returns null).
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.Tasks, new[] { Task1 })
            .Add(c => c.BarColor, (L.GanttTask t) => (string?)null));

        // The parent swaps in a BarColor lambda that paints t1 red — the task
        // record is byte-for-byte identical, only the per-task colour OUTPUT moved.
        // Before the fix ComputeTasksHash ignored that output, so the hash matched
        // and the recolour never reached the canvas.
        cut.Render(p => p
            .Add(c => c.Tasks, new[] { Task1 })
            .Add(c => c.BarColor, (L.GanttTask t) => t.Id == "t1" ? "var(--color-destructive)" : null));

        Assert.True(
            SetTasksCount(_module) > 0,
            "Swapping the BarColor delegate (recolouring a bar) must re-push tasks via gantt.setTasks.");
    }

    [Fact]
    public void Swapping_GroupBy_delegate_repushes_tasks()
    {
        // First render: no grouping — tasks render in their natural order.
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.Tasks, new[] { Task1, Task2 }));

        // The parent introduces a GroupBy lambda that puts t2 in group "A" and t1
        // in group "B", which re-orders the lanes (SortedTasks) and changes the
        // per-task group label OUTPUT. The task records are identical; only the
        // delegate output moved. Before the fix the hash was unchanged.
        cut.Render(p => p
            .Add(c => c.Tasks, new[] { Task1, Task2 })
            .Add(c => c.GroupBy, (L.GanttTask t) => t.Id == "t2" ? "A" : "B"));

        Assert.True(
            SetTasksCount(_module) > 0,
            "Swapping the GroupBy delegate (re-clustering the lanes) must re-push tasks via gantt.setTasks.");
    }
}
