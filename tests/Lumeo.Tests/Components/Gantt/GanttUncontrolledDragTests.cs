using Bunit;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Gantt;

/// <summary>
/// Regression for battle-test wave 1, finding #18: in UNCONTROLLED usage (the
/// parent renders a constant <c>Tasks</c> list and does NOT echo
/// <c>TasksChanged</c> back into it), a JS-side drag that moves a bar must NOT be
/// snapped back to its pre-drag position by the next ordinary parent re-render.
///
/// Mechanism of the bug: a drag fires <c>JsOnDateChange</c> → <c>ReplaceTask</c>,
/// which updated <c>_lastTasksHash</c> to the post-drag snapshot. Because
/// <c>OnParametersSetAsync</c> gated its JS re-push on that same
/// <c>_lastTasksHash</c>, the very next parent re-render — still carrying the
/// ORIGINAL (pre-drag) <c>Tasks</c> — computed a hash that differed from the
/// post-drag snapshot, so it re-pushed the stale parent tasks via
/// <c>gantt.setTasks</c> and the bar visibly jumped back. The fix tracks a
/// separate <c>_lastParentHash</c> that is advanced ONLY by a genuine parameter
/// change (never by <c>ReplaceTask</c>), so an unchanged parent payload after a
/// drag is correctly seen as "nothing new from the parent" and no re-push fires.
///
/// Mirrors <see cref="GanttTasksHashTests"/>: the Gantt's own isolated JS module is
/// pre-registered in Loose mode and <c>gantt.init</c> is stubbed to return a
/// non-empty instance id, which flips <c>_initialized = true</c> and captures a
/// non-null <c>_instanceId</c> — both required before the OnParametersSet round-trip
/// guard can reach <c>gantt.setTasks</c>.
/// </summary>
public class GanttUncontrolledDragTests : IAsyncLifetime
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

        // Non-empty init result → _initialized = true + non-null _instanceId, so the
        // OnParametersSetAsync guard can fire gantt.setTasks.
        _module.Setup<string>("gantt.init", _ => true).SetResult(InstanceId);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static int SetTasksCount(BunitJSModuleInterop module) =>
        module.Invocations.Count(i => i.Identifier == "gantt.setTasks");

    private static L.GanttTask Task1 =>
        new("t1", "Design", new DateTime(2026, 1, 1), new DateTime(2026, 1, 5), 20);

    [Fact]
    public async Task Uncontrolled_drag_is_not_snapped_back_by_a_stale_parent_rerender()
    {
        var original = new[] { Task1 };

        // Uncontrolled usage: a constant Tasks list, NO TasksChanged binding.
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.Tasks, original));

        var setTasksAfterInit = SetTasksCount(_module);

        // The renderer reports a dragged bar: same Id, new dates. ReplaceTask folds
        // it into the local snapshot; in uncontrolled usage the parent's Tasks list
        // is NOT updated.
        var moved = Task1 with { Start = new DateTime(2026, 1, 3), End = new DateTime(2026, 1, 9) };
        await cut.InvokeAsync(() => cut.Instance.JsOnDateChange(moved));

        // An ordinary parent re-render that still carries the ORIGINAL (pre-drag)
        // Tasks — e.g. any unrelated state change upstream. The reference even stays
        // the same here, but the bug reproduces regardless because the post-drag
        // snapshot hash differs from the parent's. Add an unrelated param change so
        // OnParametersSetAsync definitely runs.
        cut.Render(p => p
            .Add(c => c.Tasks, original)
            .Add(c => c.Height, "600px"));

        // Before the fix this stale re-render re-pushed the pre-drag tasks via
        // gantt.setTasks, snapping the bar back. After the fix no re-push happens
        // because the PARENT payload did not actually change.
        Assert.Equal(setTasksAfterInit, SetTasksCount(_module));
    }

    [Fact]
    public async Task A_genuine_parent_task_change_after_a_drag_still_repushes()
    {
        // The fix must not suppress a real parent-driven task update.
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.Tasks, new[] { Task1 }));

        var setTasksAfterInit = SetTasksCount(_module);

        var moved = Task1 with { Start = new DateTime(2026, 1, 3), End = new DateTime(2026, 1, 9) };
        await cut.InvokeAsync(() => cut.Instance.JsOnDateChange(moved));

        // The parent supplies a genuinely NEW task payload (different name) — this is
        // not the stale-echo case and must reach the canvas.
        var renamed = Task1 with { Name = "Design v2" };
        cut.Render(p => p
            .Add(c => c.Tasks, new[] { renamed }));

        Assert.True(
            SetTasksCount(_module) > setTasksAfterInit,
            "A genuine parent-driven task change after a drag must still re-push via gantt.setTasks.");
    }
}
