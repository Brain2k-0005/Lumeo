using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Edge-data regression tests for <see cref="L.Scheduler"/> covering battle-test wave 1
/// findings #21, #22 and #67 (all cls=edge-data). Each guards a malformed-input path the
/// normal path never hits:
///
///   #21 — <c>JsOnEventChange</c> resolved the dragged event by <c>FindIndex(e =&gt; e.Id == ev.Id)</c>,
///         so a blank or DUPLICATE Id silently merged the drag onto the FIRST matching record
///         (or onto an unrelated blank-Id record). The fix only merges when exactly one record
///         matches a non-empty Id, otherwise it leaves the stored collection untouched.
///
///   #22 — <c>RebuildResourceLookup</c> used <c>ToDictionary(r =&gt; r.Id, …)</c>, which THROWS on a
///         duplicate (or treats null awkwardly) resource Id and takes the whole render down.
///         The fix builds the lookup defensively (last-wins, null Id skipped).
///
///   #67 — A recurring event whose end time-of-day is at/before its start (crosses midnight,
///         e.g. 22:00 → 02:00) emitted a literal <c>endTime</c> EARLIER than <c>startTime</c>,
///         which FullCalendar mis-renders. The fix omits endTime and emits an explicit
///         <c>duration</c> derived from <c>End - Start</c> for the cross-midnight case only;
///         same-day recurring events keep their literal endTime (normal path unchanged).
///
/// Mirrors <see cref="SchedulerBehaviorTests"/>: the Scheduler's own isolated module is
/// pre-registered in Loose mode and <c>scheduler.init</c> is stubbed to return a non-empty
/// instance id (so the component captures a non-null _instanceId and flips _initialized).
/// </summary>
public class SchedulerEdgeDataTests : IAsyncLifetime
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

    // Pull the serialized event objects out of the scheduler.init options' `events` array.
    private object[] InitEvents()
    {
        var init = Assert.Single(_module.Invocations, i => i.Identifier == "scheduler.init");
        // Init options are Dictionary<string, object?> (trim-safe — see
        // Scheduler.razor's OnAfterRenderAsync), not an anonymous type.
        var options = (System.Collections.Generic.IDictionary<string, object?>)init.Arguments[2]!;
        var serialized = options["events"];
        var array = Assert.IsAssignableFrom<System.Collections.IEnumerable>(serialized);
        return array.Cast<object>().ToArray();
    }

    // Event payload entries are Dictionary<string, object?> (trim-safe — see
    // Scheduler.razor's ToJsEvent), not anonymous types.
    private static object? Prop(object jsEvent, string name) =>
        ((System.Collections.Generic.IDictionary<string, object?>)jsEvent).TryGetValue(name, out var v) ? v : null;

    // ── Finding #21: duplicate / blank Id must not silently mutate the wrong record ──────

    [Fact]
    public async Task EventChange_with_a_duplicate_id_does_not_silently_mutate_the_first_record()
    {
        // Two distinct events share the SAME Id (a malformed but possible input). A drag
        // reports a new time window keyed only by that ambiguous Id.
        var a = new L.SchedulerEvent("dup", "First",
            DateTime.Today.AddHours(9), DateTime.Today.AddHours(10));
        var b = new L.SchedulerEvent("dup", "Second",
            DateTime.Today.AddHours(13), DateTime.Today.AddHours(14));

        IEnumerable<L.SchedulerEvent>? emitted = null;
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.Events, new[] { a, b })
            .Add(c => c.EventsChanged, EventCallback.Factory.Create<IEnumerable<L.SchedulerEvent>>(
                this, e => emitted = e)));

        // The renderer reports a drag for the ambiguous Id with a brand-new window.
        var dragged = new L.SchedulerEvent("dup", "Dragged",
            DateTime.Today.AddHours(20), DateTime.Today.AddHours(21));
        await cut.InvokeAsync(() => cut.Instance.JsOnEventChange(dragged));

        // The stored collection must be untouched: the ambiguous match is NOT merged into
        // either record (before the fix the FIRST "dup" record absorbed 20:00–21:00).
        Assert.NotNull(emitted);
        var stored = emitted!.ToList();
        Assert.Equal(2, stored.Count);
        Assert.All(stored, e =>
            Assert.False(
                e.Start == dragged.Start && e.End == dragged.End,
                "An ambiguous (duplicate-Id) drag must not be merged onto any stored record."));
        Assert.Equal(a.Start, stored[0].Start);
        Assert.Equal(b.Start, stored[1].Start);
    }

    [Fact]
    public async Task EventChange_with_a_blank_id_does_not_mutate_a_blank_id_record()
    {
        // An event with a blank Id can't be uniquely identified. A drag carrying a blank Id
        // must not be folded onto the first blank-Id record.
        var blank = new L.SchedulerEvent("", "No Id",
            DateTime.Today.AddHours(9), DateTime.Today.AddHours(10));

        IEnumerable<L.SchedulerEvent>? emitted = null;
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.Events, new[] { blank })
            .Add(c => c.EventsChanged, EventCallback.Factory.Create<IEnumerable<L.SchedulerEvent>>(
                this, e => emitted = e)));

        var dragged = new L.SchedulerEvent("", "Dragged",
            DateTime.Today.AddHours(20), DateTime.Today.AddHours(21));
        await cut.InvokeAsync(() => cut.Instance.JsOnEventChange(dragged));

        Assert.NotNull(emitted);
        var stored = Assert.Single(emitted!);
        Assert.Equal(blank.Start, stored.Start);
        Assert.Equal(blank.End, stored.End);
    }

    [Fact]
    public async Task EventChange_with_a_unique_id_still_merges_the_new_window()
    {
        // The guard must NOT regress the normal path: a unique, non-empty Id still merges
        // the dragged Start/End onto the matching record while preserving its other fields.
        var ev = new L.SchedulerEvent("only", "Meeting",
            DateTime.Today.AddHours(9), DateTime.Today.AddHours(10),
            Color: "var(--color-primary)");

        IEnumerable<L.SchedulerEvent>? emitted = null;
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.Events, new[] { ev })
            .Add(c => c.EventsChanged, EventCallback.Factory.Create<IEnumerable<L.SchedulerEvent>>(
                this, e => emitted = e)));

        var dragged = new L.SchedulerEvent("only", "ignored-title",
            DateTime.Today.AddHours(15), DateTime.Today.AddHours(16));
        await cut.InvokeAsync(() => cut.Instance.JsOnEventChange(dragged));

        Assert.NotNull(emitted);
        var stored = Assert.Single(emitted!);
        Assert.Equal(dragged.Start, stored.Start);   // window merged
        Assert.Equal(dragged.End, stored.End);
        Assert.Equal("Meeting", stored.Title);        // other fields preserved
        Assert.Equal("var(--color-primary)", stored.Color);
    }

    // ── Finding #22: duplicate resource Ids must not throw ───────────────────────────────

    [Fact]
    public void Duplicate_resource_ids_do_not_throw_during_render()
    {
        // Two resources share the same Id — ToDictionary would throw an ArgumentException
        // here and crash the whole render. The defensive build must tolerate it (last wins).
        var resources = new[]
        {
            new L.SchedulerResource("room", "Room A", "var(--color-primary)"),
            new L.SchedulerResource("room", "Room B", "var(--color-destructive)"),
        };

        var ex = Record.Exception(() => _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.Resources, resources)));

        Assert.Null(ex);
    }

    [Fact]
    public void Duplicate_resource_ids_resolve_last_wins_for_event_color()
    {
        // With duplicate Ids the last entry wins the lookup, so an event bound to that Id
        // is color-coded with the LAST resource's color (Room B / destructive), not the first.
        var resources = new[]
        {
            new L.SchedulerResource("room", "Room A", "first-color"),
            new L.SchedulerResource("room", "Room B", "last-color"),
        };
        var events = new[]
        {
            new L.SchedulerEvent("e1", "Booking",
                DateTime.Today.AddHours(10), DateTime.Today.AddHours(11), ResourceId: "room"),
        };

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.Resources, resources)
            .Add(c => c.Events, events));

        var jsEvent = Assert.Single(InitEvents());
        Assert.Equal("last-color", Prop(jsEvent, "color"));
    }

    // ── Finding #67: cross-midnight recurring events emit a duration, not a bad endTime ──

    [Fact]
    public void Cross_midnight_recurring_event_emits_duration_and_omits_bad_endTime()
    {
        // Recurring night-shift: starts 22:00, ends 02:00 the next day. Emitting the literal
        // endTime "02:00:00" hands FullCalendar an end EARLIER than the start.
        var start = DateTime.Today.AddHours(22);              // 22:00
        var end = DateTime.Today.AddDays(1).AddHours(2);      // 02:00 next day → 4h span
        var nightShift = new L.SchedulerEvent(
            Id: "night", Title: "Night Shift", Start: start, End: end,
            DaysOfWeek: new[] { DayOfWeek.Monday, DayOfWeek.Tuesday });

        var cut = _ctx.Render<L.Scheduler>(p => p.Add(c => c.Events, new[] { nightShift }));

        var jsEvent = Assert.Single(InitEvents());

        // endTime must be omitted (it would otherwise be an earlier-than-start value).
        Assert.Null(Prop(jsEvent, "endTime"));
        // A 4-hour duration must be supplied so the chip spans past midnight correctly.
        Assert.Equal("04:00:00", Prop(jsEvent, "duration"));
    }

    [Fact]
    public void Same_day_recurring_event_keeps_literal_endTime_and_no_duration()
    {
        // The normal recurring path is unchanged: a same-day window keeps its literal
        // endTime and emits no duration.
        var start = DateTime.Today.AddHours(9);               // 09:00
        var end = DateTime.Today.AddHours(9).AddMinutes(30);  // 09:30
        var standup = new L.SchedulerEvent(
            Id: "standup", Title: "Stand-up", Start: start, End: end,
            DaysOfWeek: new[] { DayOfWeek.Monday });

        var cut = _ctx.Render<L.Scheduler>(p => p.Add(c => c.Events, new[] { standup }));

        var jsEvent = Assert.Single(InitEvents());

        Assert.Equal("09:30:00", Prop(jsEvent, "endTime"));
        Assert.Null(Prop(jsEvent, "duration"));
    }
}
