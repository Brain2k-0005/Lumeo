using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Regression tests for the controlled-component rollback fix on Scheduler's
/// Events/EventsChanged pair. A drag/resize in the FullCalendar JS layer fires
/// JsOnEventChange, which optimistically merges the new window into the local
/// `_events` snapshot BEFORE invoking EventsChanged. When the component is used
/// in controlled mode (EventsChanged bound) and the parent vetoes the drag by
/// re-rendering with the original Events unchanged, the component must roll the
/// dragged event back to the bound window (and push the correction to JS via
/// scheduler.setEvents) rather than leaving the optimistic drag stuck in place.
///
/// Conversely, in UNCONTROLLED mode (no EventsChanged bound) a stale re-render
/// that re-supplies the SAME original Events the parent always passes must NOT
/// claw back a user's local drag — only a genuinely new Events parameter should.
///
/// Mirrors <see cref="SchedulerBehaviorTests"/> / <see cref="SchedulerEventsHashTests"/>:
/// the Scheduler's own isolated module is pre-registered in Loose mode and
/// scheduler.init is stubbed to return a non-empty instance id, which is what flips
/// _initialized = true and is required for the OnParametersSetAsync round-trip guard
/// to reach scheduler.setEvents.
/// </summary>
public class SchedulerControlledRollbackTests : IAsyncLifetime
{
    private const string ModulePath = "./_content/Lumeo.Scheduler/js/scheduler.js";
    private const string InstanceId = "sched-instance-1";

    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();

        _module = _ctx.JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;

        _module.Setup<string>("scheduler.init", _ => true).SetResult(InstanceId);
        _module.Setup<string>("scheduler.getTitle", _ => true).SetResult("June 2026");

        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static int SetEventsCount(BunitJSModuleInterop module) =>
        module.Invocations.Count(i => i.Identifier == "scheduler.setEvents");

    private static object? Prop(object jsEvent, string name) =>
        jsEvent.GetType().GetProperty(name)?.GetValue(jsEvent);

    // --- Controlled: veto rolls back ---

    [Fact]
    public async Task Controlled_Veto_Rolls_Back_Dragged_Event_To_Bound_Window()
    {
        var start = DateTime.Today.AddHours(9);
        var end = DateTime.Today.AddHours(10);
        var original = new L.SchedulerEvent("e1", "Meeting", start, end);

        IRenderedComponent<L.Scheduler>? cut = null;

        var callback = EventCallback.Factory.Create<IEnumerable<L.SchedulerEvent>>(this, (IEnumerable<L.SchedulerEvent> _) =>
        {
            // Veto: do NOT adopt the dragged window — re-render with the original
            // Events unchanged, simulating a parent that rejected the drag.
            cut!.Render(p =>
            {
                p.Add(c => c.Events, new[] { original });
                p.Add(c => c.EventsChanged, EventCallback.Factory.Create<IEnumerable<L.SchedulerEvent>>(this, (IEnumerable<L.SchedulerEvent> _2) => { }));
            });
        });

        cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.Events, new[] { original })
            .Add(c => c.EventsChanged, callback));

        // Drag the event to a new window — JsOnEventChange optimistically merges it
        // into _events, then fires EventsChanged; the callback above vetoes it.
        var dragged = original with { Start = DateTime.Today.AddHours(15), End = DateTime.Today.AddHours(16) };
        await cut.InvokeAsync(() => cut.Instance.JsOnEventChange(dragged));

        // After the veto, the LAST scheduler.setEvents push must carry the ORIGINAL
        // window — proving the optimistic drag was rolled back, not left at 15:00-16:00.
        var lastSetEvents = _module.Invocations.Last(i => i.Identifier == "scheduler.setEvents");
        var serialized = (System.Collections.IEnumerable)lastSetEvents.Arguments[1]!;
        var pushed = Assert.Single(serialized.Cast<object>());
        Assert.Equal(start.ToString("o"), Prop(pushed, "start"));
        Assert.Equal(end.ToString("o"), Prop(pushed, "end"));
    }

    // --- Controlled: accepted drag keeps the new window, no redundant push ---

    [Fact]
    public async Task Controlled_Accepted_Drag_Keeps_New_Window_With_No_Redundant_Push()
    {
        var start = DateTime.Today.AddHours(9);
        var end = DateTime.Today.AddHours(10);
        var original = new L.SchedulerEvent("e1", "Meeting", start, end);

        IRenderedComponent<L.Scheduler>? cut = null;

        var callback = EventCallback.Factory.Create<IEnumerable<L.SchedulerEvent>>(this, (IEnumerable<L.SchedulerEvent> incoming) =>
        {
            // Accept: the parent adopts the dragged collection and re-renders with it.
            cut!.Render(p =>
            {
                p.Add(c => c.Events, incoming.ToArray());
                p.Add(c => c.EventsChanged, EventCallback.Factory.Create<IEnumerable<L.SchedulerEvent>>(this, (IEnumerable<L.SchedulerEvent> _2) => { }));
            });
        });

        cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.Events, new[] { original })
            .Add(c => c.EventsChanged, callback));

        var dragged = original with { Start = DateTime.Today.AddHours(15), End = DateTime.Today.AddHours(16) };
        await cut.InvokeAsync(() => cut.Instance.JsOnEventChange(dragged));

        // The parent's echo of our own push is a no-op — OnParametersSetAsync must
        // NOT issue a corrective scheduler.setEvents call (FullCalendar already shows
        // the dragged position natively; nothing needs correcting).
        Assert.Equal(0, SetEventsCount(_module));
    }

    // --- Controlled: a genuinely new Events collection from the parent is adopted ---

    [Fact]
    public async Task Controlled_Programmatic_Reset_Is_Adopted_Without_A_Prior_Drag()
    {
        var original = new L.SchedulerEvent("e1", "Meeting",
            DateTime.Today.AddHours(9), DateTime.Today.AddHours(10));
        var noOp = EventCallback.Factory.Create<IEnumerable<L.SchedulerEvent>>(this, (IEnumerable<L.SchedulerEvent> _) => { });

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.Events, new[] { original })
            .Add(c => c.EventsChanged, noOp));

        Assert.Equal(0, SetEventsCount(_module));

        // The parent swaps in a brand-new event list WITHOUT any prior user drag —
        // this must still be adopted and pushed to JS. EventsChanged is re-specified
        // (still bound, still controlled) on every render, as is idiomatic in bUnit.
        var replacement = new L.SchedulerEvent("e2", "Replacement",
            DateTime.Today.AddHours(13), DateTime.Today.AddHours(14));
        cut.Render(p => p
            .Add(c => c.Events, new[] { replacement })
            .Add(c => c.EventsChanged, noOp));

        Assert.True(SetEventsCount(_module) > 0,
            "A genuinely new Events collection from a controlled parent must be adopted.");
        var lastSetEvents = _module.Invocations.Last(i => i.Identifier == "scheduler.setEvents");
        var serialized = (System.Collections.IEnumerable)lastSetEvents.Arguments[1]!;
        var pushed = Assert.Single(serialized.Cast<object>());
        Assert.Equal("e2", Prop(pushed, "id"));
    }

    // --- Uncontrolled: a stale re-render must not claw back a local drag ---

    [Fact]
    public async Task Uncontrolled_Local_Drag_Survives_An_Unrelated_Rerender_With_The_Same_Events()
    {
        var start = DateTime.Today.AddHours(9);
        var end = DateTime.Today.AddHours(10);
        var original = new L.SchedulerEvent("e1", "Meeting", start, end);

        // No EventsChanged bound — Events is uncontrolled.
        var cut = _ctx.Render<L.Scheduler>(p => p.Add(c => c.Events, new[] { original }));

        var dragged = original with { Start = DateTime.Today.AddHours(15), End = DateTime.Today.AddHours(16) };
        await cut.InvokeAsync(() => cut.Instance.JsOnEventChange(dragged));

        // No EventsChanged delegate, so no callback fires and no setEvents push happens yet.
        Assert.Equal(0, SetEventsCount(_module));

        // Parent re-renders for an unrelated reason, re-supplying the SAME original
        // Events collection it always passes (it never tracked the drag).
        cut.Render(p => p.Add(c => c.Events, new[] { original }));

        // The stale re-render must NOT trigger a corrective scheduler.setEvents call
        // that would snap the calendar back to the pre-drag window.
        Assert.Equal(0, SetEventsCount(_module));
    }
}
