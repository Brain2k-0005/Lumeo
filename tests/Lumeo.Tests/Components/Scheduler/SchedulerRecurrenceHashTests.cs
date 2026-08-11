using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Regression test for the wave-1b integration item the kernel report flagged: §4.2's
/// <c>ComputeEventsHash</c> needed a <c>Recurrence</c> fold, mirroring the existing
/// <c>DaysOfWeek</c>-by-value fold this file's sibling (<see cref="SchedulerEventsHashTests"/>)
/// already regression-tests. Before the fix, editing only <see cref="L.SchedulerEvent.Recurrence"/>
/// (e.g. flipping <c>Interval</c> from 1 to 2) produced an identical hash — same reference-vs-value
/// bug class as the original DaysOfWeek report ("missed edits that swap days without changing
/// the count") — so the JS layer was never told and the calendar never re-rendered the change.
/// </summary>
public class SchedulerRecurrenceHashTests : IAsyncLifetime
{
    private const string ModulePath = "./_content/Lumeo.Scheduler/js/scheduler.js";
    private const string InstanceId = "sched-instance-recurrence";

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

    [Fact]
    public void Editing_Recurrence_Interval_repushes_events()
    {
        var start = DateTime.Today.AddHours(9);
        var end = start.AddMinutes(30);

        var before = new L.SchedulerEvent(Id: "r1", Title: "Sync", Start: start, End: end)
        {
            Recurrence = new L.SchedulerRecurrenceRule(L.SchedulerRecurrenceFrequency.Weekly, Interval: 1)
        };

        var cut = _ctx.Render<L.Scheduler>(p => p.Add(c => c.Events, new[] { before }));

        var after = before with
        {
            Recurrence = before.Recurrence! with { Interval = 2 }
        };
        cut.Render(p => p.Add(c => c.Events, new[] { after }));

        Assert.True(
            SetEventsCount(_module) > 0,
            "Changing Recurrence.Interval must re-push events via scheduler.setEvents.");
    }

    [Fact]
    public void Editing_Recurrence_ByDay_without_changing_count_repushes_events()
    {
        var start = DateTime.Today.AddHours(9);
        var end = start.AddMinutes(30);

        var before = new L.SchedulerEvent(Id: "r2", Title: "Standup", Start: start, End: end)
        {
            Recurrence = new L.SchedulerRecurrenceRule(
                L.SchedulerRecurrenceFrequency.Weekly,
                ByDay: new[] { new L.SchedulerByDayRule(DayOfWeek.Monday) })
        };

        var cut = _ctx.Render<L.Scheduler>(p => p.Add(c => c.Events, new[] { before }));

        // Same ByDay.Count (1 -> 1), different day — exactly the class of bug the
        // original DaysOfWeek report caught (count-only folding misses this).
        var after = before with
        {
            Recurrence = before.Recurrence! with { ByDay = new[] { new L.SchedulerByDayRule(DayOfWeek.Tuesday) } }
        };
        cut.Render(p => p.Add(c => c.Events, new[] { after }));

        Assert.True(
            SetEventsCount(_module) > 0,
            "Changing Recurrence.ByDay (same count) must re-push events via scheduler.setEvents.");
    }

    [Fact]
    public void Adding_Recurrence_To_A_Plain_Event_repushes_events()
    {
        var start = DateTime.Today.AddHours(9);
        var end = start.AddMinutes(30);

        var before = new L.SchedulerEvent(Id: "r3", Title: "One-off", Start: start, End: end);
        var cut = _ctx.Render<L.Scheduler>(p => p.Add(c => c.Events, new[] { before }));

        var after = before with
        {
            Recurrence = new L.SchedulerRecurrenceRule(L.SchedulerRecurrenceFrequency.Daily)
        };
        cut.Render(p => p.Add(c => c.Events, new[] { after }));

        Assert.True(
            SetEventsCount(_module) > 0,
            "Adding a Recurrence rule to a previously non-recurring event must re-push events.");
    }
}
