using Bunit;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Regression tests for the change-detection hash that gates Scheduler's
/// .NET → JS event re-push (<c>OnParametersSetAsync</c> only calls
/// <c>scheduler.setEvents</c> when <c>ComputeEventsHash</c> changes).
///
/// The bug: ComputeEventsHash folded only <c>DaysOfWeek.Count</c> and skipped
/// <c>ExceptionDates</c>, <c>Url</c> and <c>ExtendedProps</c> entirely. So a real
/// edit to any of those fields produced an identical hash, the round-trip was
/// suppressed, and the calendar never re-rendered the change.
///
/// These tests mirror <see cref="SchedulerBehaviorTests"/>: the Scheduler's own
/// isolated module is pre-registered in Loose mode and <c>scheduler.init</c> is
/// stubbed to return a non-empty instance id, which is what gives the component a
/// non-null <c>_instanceId</c> AND sets <c>_initialized = true</c> — both required
/// for the OnParametersSet round-trip guard to even reach <c>scheduler.setEvents</c>.
///
/// Each test renders once (init fires setEvents-free), then re-renders the component
/// with a single edited field and asserts a <c>scheduler.setEvents</c> invocation
/// was dispatched — i.e. the edit was detected. Without the fix these all fail
/// because the hash is unchanged and no setEvents call is ever made.
/// </summary>
public class SchedulerEventsHashTests : IAsyncLifetime
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

        // A non-empty init result is what flips _initialized = true and captures a
        // non-null _instanceId, so the OnParametersSetAsync guard can fire setEvents.
        _module.Setup<string>("scheduler.init", _ => true).SetResult(InstanceId);
        _module.Setup<string>("scheduler.getTitle", _ => true).SetResult("June 2026");

        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static int SetEventsCount(BunitJSModuleInterop module) =>
        module.Invocations.Count(i => i.Identifier == "scheduler.setEvents");

    [Fact]
    public void Editing_DaysOfWeek_without_changing_count_repushes_events()
    {
        var start = DateTime.Today.AddHours(9);
        var end = start.AddMinutes(30);

        var before = new L.SchedulerEvent(
            Id: "rec1", Title: "Stand-up", Start: start, End: end,
            DaysOfWeek: new[] { DayOfWeek.Monday });

        var cut = _ctx.Render<L.Scheduler>(p => p.Add(c => c.Events, new[] { before }));

        // Move the recurrence from Monday to Tuesday — same Count, so the old
        // count-only hash was identical and the JS layer was never told.
        var after = before with { DaysOfWeek = new[] { DayOfWeek.Tuesday } };
        cut.Render(p => p.Add(c => c.Events, new[] { after }));

        Assert.True(
            SetEventsCount(_module) > 0,
            "Changing DaysOfWeek (same count) must re-push events via scheduler.setEvents.");
    }

    [Fact]
    public void Editing_ExceptionDates_repushes_events()
    {
        var start = DateTime.Today.AddHours(9);
        var end = start.AddMinutes(30);

        var before = new L.SchedulerEvent(
            Id: "rec1", Title: "Stand-up", Start: start, End: end,
            DaysOfWeek: new[] { DayOfWeek.Monday, DayOfWeek.Wednesday });

        var cut = _ctx.Render<L.Scheduler>(p => p.Add(c => c.Events, new[] { before }));

        // Skip next week's occurrence — adding an exception date is a real edit.
        var after = before with { ExceptionDates = new[] { DateTime.Today.AddDays(7) } };
        cut.Render(p => p.Add(c => c.Events, new[] { after }));

        Assert.True(
            SetEventsCount(_module) > 0,
            "Adding an ExceptionDate must re-push events via scheduler.setEvents.");
    }

    [Fact]
    public void Editing_Url_repushes_events()
    {
        var start = DateTime.Today.AddHours(10);
        var end = start.AddHours(1);

        var before = new L.SchedulerEvent(Id: "e1", Title: "Review", Start: start, End: end);

        var cut = _ctx.Render<L.Scheduler>(p => p.Add(c => c.Events, new[] { before }));

        var after = before with { Url = "https://example.com/meeting" };
        cut.Render(p => p.Add(c => c.Events, new[] { after }));

        Assert.True(
            SetEventsCount(_module) > 0,
            "Changing Url must re-push events via scheduler.setEvents.");
    }

    [Fact]
    public void Editing_ExtendedProps_repushes_events()
    {
        var start = DateTime.Today.AddHours(10);
        var end = start.AddHours(1);

        var before = new L.SchedulerEvent(
            Id: "e1", Title: "Review", Start: start, End: end,
            ExtendedProps: new Dictionary<string, object> { ["status"] = "tentative" });

        var cut = _ctx.Render<L.Scheduler>(p => p.Add(c => c.Events, new[] { before }));

        var after = before with
        {
            ExtendedProps = new Dictionary<string, object> { ["status"] = "confirmed" }
        };
        cut.Render(p => p.Add(c => c.Events, new[] { after }));

        Assert.True(
            SetEventsCount(_module) > 0,
            "Changing an ExtendedProps value must re-push events via scheduler.setEvents.");
    }
}
