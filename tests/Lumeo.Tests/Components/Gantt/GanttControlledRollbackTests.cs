using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Gantt;

/// <summary>
/// Regression tests for the controlled-component rollback fix on Gantt's two
/// independently-bindable properties: <c>ViewMode</c>/<c>ViewModeChanged</c> (the
/// toolbar zoom) and <c>Tasks</c>/<c>TasksChanged</c> (drag/progress edits).
///
/// In CONTROLLED mode (the <c>*Changed</c> callback is bound) a parent that vetoes
/// a change by re-rendering with the value UNCHANGED from before the interaction
/// must see the component roll back to that value. Before the fix, OnParametersSetAsync
/// only re-adopted the incoming parameter when it differed from the LAST value seen
/// AS A PARAMETER (<c>_lastSeenViewMode</c> / the parent-hash gate for Tasks) — never
/// against the last value the component itself PUSHED via the callback. A veto that
/// landed back on the SAME value the parent had before the interaction was therefore
/// indistinguishable from "nothing changed", so the optimistic local mutation
/// (<c>_currentViewMode</c> / <c>_tasks</c>) never rolled back. The fix adds a
/// "last pushed" discriminator (<c>_lastPushedViewMode</c> / <c>_lastPushedTasksHash</c>)
/// set immediately before each <c>*Changed.InvokeAsync</c> call and consulted (via a
/// <c>HasDelegate</c> branch) in OnParametersSetAsync.
///
/// Mirrors <see cref="Lumeo.Tests.Components.Switch.SwitchControlledRollbackTests"/>
/// (ViewMode tests) and <see cref="GanttUncontrolledDragTests"/> /
/// <see cref="GanttTasksHashTests"/> (Tasks tests: the Gantt module is pre-registered
/// in Loose mode and <c>gantt.init</c> is stubbed to return a non-empty instance id,
/// which is required for the OnParametersSet round-trip guard to reach
/// <c>gantt.setTasks</c>).
/// </summary>
public class GanttControlledRollbackTests : IAsyncLifetime
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

        // Non-empty init result -> _initialized = true + non-null _instanceId, so the
        // OnParametersSetAsync guard can fire gantt.setTasks / gantt.changeViewMode.
        _module.Setup<string>("gantt.init", _ => true).SetResult(InstanceId);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static L.GanttTask Task1 =>
        new("t1", "Design", new DateTime(2026, 1, 1), new DateTime(2026, 1, 5), 20);

    /// <summary>The toggle button currently carrying aria-pressed="true", or null.</summary>
    private static string? PressedZoomLabel(IRenderedComponent<L.Gantt> cut) =>
        cut.FindAll("button[aria-pressed='true']")
           .Select(b => b.TextContent.Trim())
           .FirstOrDefault();

    private int SetTasksCount() =>
        _module.Invocations.Count(i => i.Identifier == "gantt.setTasks");

    // --- ViewMode: controlled veto rolls back ---

    [Fact]
    public void ViewMode_Controlled_Veto_Rolls_Back_To_Bound_Value()
    {
        // Controlled usage: ViewModeChanged is bound, but the parent never adopts
        // the pushed value into its own state (a no-op handler) — i.e. it always
        // vetoes.
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ViewModeChanged, EventCallback.Factory.Create<L.GanttViewMode>(_ctx, (_) => { }))
            .Add(c => c.Tasks, new[] { Task1 }));

        Assert.Equal("Day", PressedZoomLabel(cut));

        // User picks "Week" from the toolbar -> OnViewModeChangedAsync sets
        // _currentViewMode=Week, records _lastPushedViewMode=Week, and fires
        // ViewModeChanged (the no-op handler does not adopt it into parent state).
        var weekButton = cut.FindAll("button[aria-pressed]")
            .First(b => b.TextContent.Trim() == "Week");
        weekButton.Click();
        Assert.Equal("Week", PressedZoomLabel(cut));

        // The parent re-renders with the SAME ORIGINAL ViewMode (Day) it always had
        // — the veto. Even though this equals the value the parent had BEFORE the
        // click, it differs from _lastPushedViewMode (Week), so it must be treated
        // as authoritative and roll the toolbar back (the pre-fix bug: comparing
        // against _lastSeenViewMode, which was already Day, made this re-render
        // look "unchanged" and it was never re-adopted).
        cut.Render(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ViewModeChanged, EventCallback.Factory.Create<L.GanttViewMode>(_ctx, (_) => { }))
            .Add(c => c.Tasks, new[] { Task1 }));

        Assert.Equal("Day", PressedZoomLabel(cut));
    }

    // --- ViewMode: controlled echo of our own push is a no-op ---

    [Fact]
    public void ViewMode_Controlled_Echo_Of_Own_Push_Keeps_New_Value()
    {
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ViewModeChanged, EventCallback.Factory.Create<L.GanttViewMode>(_ctx, (_) => { }))
            .Add(c => c.Tasks, new[] { Task1 }));

        var weekButton = cut.FindAll("button[aria-pressed]")
            .First(b => b.TextContent.Trim() == "Week");
        weekButton.Click();
        Assert.Equal("Week", PressedZoomLabel(cut));

        // The parent ACCEPTS the pick and echoes back exactly the pushed value
        // (Week) — this must be a no-op (keep the toolbar selection), not get
        // mistaken for "unchanged" and silently ignored, nor for a veto.
        cut.Render(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Week)
            .Add(c => c.ViewModeChanged, EventCallback.Factory.Create<L.GanttViewMode>(_ctx, (_) => { }))
            .Add(c => c.Tasks, new[] { Task1 }));

        Assert.Equal("Week", PressedZoomLabel(cut));
    }

    // --- ViewMode: controlled programmatic reset is adopted ---

    [Fact]
    public void ViewMode_Controlled_Programmatic_Reset_Is_Adopted()
    {
        // Start at Week; parent programmatically resets to Month WITHOUT the user
        // picking from the toolbar first (simulates an external data reload).
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Week)
            .Add(c => c.ViewModeChanged, EventCallback.Factory.Create<L.GanttViewMode>(_ctx, (_) => { }))
            .Add(c => c.Tasks, new[] { Task1 }));

        Assert.Equal("Week", PressedZoomLabel(cut));

        cut.Render(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Month)
            .Add(c => c.ViewModeChanged, EventCallback.Factory.Create<L.GanttViewMode>(_ctx, (_) => { }))
            .Add(c => c.Tasks, new[] { Task1 }));

        Assert.Equal("Month", PressedZoomLabel(cut));
    }

    // --- Tasks: controlled veto rolls back ---

    [Fact]
    public async Task Tasks_Controlled_Veto_Rolls_Back_To_Bound_Value()
    {
        var original = new[] { Task1 };

        // Controlled usage: TasksChanged is bound, but the parent never adopts the
        // pushed value into its own state (a no-op handler) — i.e. it always vetoes.
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.Tasks, original)
            .Add(c => c.TasksChanged, EventCallback.Factory.Create<IEnumerable<L.GanttTask>>(_ctx, (_) => { })));

        var setTasksAfterInit = SetTasksCount();

        // The renderer reports a dragged bar: same Id, new dates. JsOnDateChange
        // folds it into the local snapshot and pushes it up via TasksChanged.
        var moved = Task1 with { Start = new DateTime(2026, 1, 3), End = new DateTime(2026, 1, 9) };
        await cut.InvokeAsync(() => cut.Instance.JsOnDateChange(moved));

        // The parent re-renders with the SAME ORIGINAL (pre-drag) Tasks it always
        // had. Because the parent never adopted the dragged value, this hash differs
        // from _lastPushedTasksHash (the post-drag hash) and must be treated as an
        // authoritative veto, rolling the bars back to it — even though it equals
        // the pre-drag PARENT hash, which is exactly the rollback bug's blind spot.
        cut.Render(p => p
            .Add(c => c.Tasks, original)
            .Add(c => c.TasksChanged, EventCallback.Factory.Create<IEnumerable<L.GanttTask>>(_ctx, (_) => { })));

        Assert.True(
            SetTasksCount() > setTasksAfterInit,
            "A controlled veto must re-push the rolled-back (pre-drag) tasks via gantt.setTasks.");

        var lastArgs = _module.Invocations.Last(i => i.Identifier == "gantt.setTasks").Arguments;
        var pushedTasks = ((IEnumerable<object>)lastArgs[1]!).ToList();
        var startProp = pushedTasks[0].GetType().GetProperty("start")!;
        var pushedStart = (string)startProp.GetValue(pushedTasks[0])!;

        // The original (pre-drag) start date, NOT the dragged 2026-01-03.
        Assert.Equal("2026-01-01", pushedStart);
    }

    // --- Tasks: echo of our own push is a no-op (no spurious re-push) ---

    [Fact]
    public async Task Tasks_Controlled_Echo_Of_Own_Push_Does_Not_Repush()
    {
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.Tasks, new[] { Task1 })
            .Add(c => c.TasksChanged, EventCallback.Factory.Create<IEnumerable<L.GanttTask>>(_ctx, (_) => { })));

        var moved = Task1 with { Start = new DateTime(2026, 1, 3), End = new DateTime(2026, 1, 9) };
        await cut.InvokeAsync(() => cut.Instance.JsOnDateChange(moved));

        var setTasksAfterDrag = SetTasksCount();

        // The parent ACCEPTS the change and echoes the exact dragged value back —
        // this must be treated as a no-op (keep the in-flight edit), not trigger a
        // redundant gantt.setTasks round-trip.
        cut.Render(p => p
            .Add(c => c.Tasks, new[] { moved })
            .Add(c => c.TasksChanged, EventCallback.Factory.Create<IEnumerable<L.GanttTask>>(_ctx, (_) => { })));

        Assert.Equal(setTasksAfterDrag, SetTasksCount());
    }
}
