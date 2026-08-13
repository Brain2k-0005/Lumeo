using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Edge-data regression tests for <see cref="L.Scheduler"/> covering battle-test wave 1
/// findings #21 and #22 — malformed-input paths the normal path never hits.
///
/// <para>
/// #21 — the drag commit resolved its event with a bare <c>FindIndex</c> on <c>Id</c>, so a
/// blank or DUPLICATE Id silently merged the drag onto the first matching record. The guard
/// only merges when exactly one record matches a non-empty Id.
/// </para>
/// <para>
/// #22 — <c>RebuildResourceLookup</c> used <c>ToDictionary</c>, which THROWS on a duplicate
/// resource Id and takes the whole render down. The lookup is built defensively (last wins,
/// null Id skipped).
/// </para>
///
/// <para>
/// #21's guard used to live in the FullCalendar bridge's <c>JsOnEventChange</c> only. Removing
/// the wrapper would have deleted it along with the bridge while leaving the surviving commit
/// path on the exact <c>FindIndex</c> the finding was about, so it was carried across; these
/// now drive that path through the month view's own <c>CommitDrag</c>.
/// </para>
/// <para>
/// Wave-1 finding #67 (a cross-midnight recurring event emitting an <c>endTime</c> earlier
/// than its <c>startTime</c>) is gone with the wrapper: it was a property of the FullCalendar
/// serializer, which no longer exists. Cross-midnight recurrence is covered by the RRULE
/// expander's own tests.
/// </para>
/// </summary>
public class SchedulerEdgeDataTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly DateTime Anchor = new(2026, 6, 15);
    private static readonly DateTime Day = new(2026, 6, 15);
    private static readonly DateTime Target = new(2026, 6, 22);

    private static string Iso(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private Task Drag(IRenderedComponent<L.Scheduler> cut, string eventId, DateTime to) =>
        cut.InvokeAsync(() => cut.FindComponent<L.SchedulerMonthView>().Instance.CommitDrag(eventId, Iso(to)));

    // ── Finding #21: an ambiguous Id must not mutate a stored record ─────────────────────

    [Fact]
    public async Task A_duplicate_id_drag_does_not_silently_mutate_the_first_record()
    {
        var a = new L.SchedulerEvent("dup", "First", Day.AddHours(9), Day.AddHours(10));
        var b = new L.SchedulerEvent("dup", "Second", Day.AddHours(11), Day.AddHours(12));

        IEnumerable<L.SchedulerEvent>? emitted = null;
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, new[] { a, b })
            .Add(c => c.EventsChanged, EventCallback.Factory.Create<IEnumerable<L.SchedulerEvent>>(
                this, e => emitted = e)));

        await Drag(cut, "dup", Target);

        // Neither record absorbs the drag — before the guard the FIRST "dup" did.
        Assert.NotNull(emitted);
        var stored = emitted!.ToList();
        Assert.Equal(2, stored.Count);
        Assert.Equal(a.Start, stored[0].Start);
        Assert.Equal(b.Start, stored[1].Start);
    }

    [Fact]
    public async Task A_blank_id_drag_does_not_mutate_a_blank_id_record()
    {
        // A blank Id identifies nothing, so a drag carrying one must not be folded onto
        // the first blank-Id record.
        var blank = new L.SchedulerEvent("", "No Id", Day.AddHours(9), Day.AddHours(10));

        IEnumerable<L.SchedulerEvent>? emitted = null;
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, new[] { blank })
            .Add(c => c.EventsChanged, EventCallback.Factory.Create<IEnumerable<L.SchedulerEvent>>(
                this, e => emitted = e)));

        await Drag(cut, "", Target);

        if (emitted is not null)
        {
            var stored = Assert.Single(emitted);
            Assert.Equal(blank.Start, stored.Start);
            Assert.Equal(blank.End, stored.End);
        }
    }

    [Fact]
    public async Task A_unique_id_drag_still_merges_the_new_window()
    {
        // The guard must not regress the normal path: a unique, non-empty Id still moves
        // the record while preserving every field the drag does not touch.
        var ev = new L.SchedulerEvent("only", "Meeting", Day.AddHours(9), Day.AddHours(10),
                                      Color: "var(--color-primary)");

        IEnumerable<L.SchedulerEvent>? emitted = null;
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, new[] { ev })
            .Add(c => c.EventsChanged, EventCallback.Factory.Create<IEnumerable<L.SchedulerEvent>>(
                this, e => emitted = e)));

        await Drag(cut, "only", Target);

        Assert.NotNull(emitted);
        var stored = Assert.Single(emitted!);
        Assert.Equal(Target.Date, stored.Start.Date);          // moved
        Assert.Equal(TimeSpan.FromHours(9), stored.Start.TimeOfDay);  // time of day kept
        Assert.Equal("Meeting", stored.Title);                 // other fields preserved
        Assert.Equal("var(--color-primary)", stored.Color);
    }

    // ── Finding #22: duplicate resource Ids must not throw ───────────────────────────────

    [Fact]
    public void Duplicate_resource_ids_do_not_throw_during_render()
    {
        // ToDictionary would throw an ArgumentException here and crash the whole render.
        var resources = new[]
        {
            new L.SchedulerResource("room", "Room A", "var(--color-primary)"),
            new L.SchedulerResource("room", "Room B", "var(--color-destructive)"),
        };

        var ex = Record.Exception(() => _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Resources, resources)));

        Assert.Null(ex);
    }

    [Fact]
    public void Duplicate_resource_ids_resolve_last_wins_for_event_color()
    {
        var resources = new[]
        {
            new L.SchedulerResource("room", "Room A", "first-color"),
            new L.SchedulerResource("room", "Room B", "last-color"),
        };
        var events = new[]
        {
            new L.SchedulerEvent("e1", "Booking", Day.AddHours(10), Day.AddHours(11), ResourceId: "room"),
        };

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Resources, resources)
            .Add(c => c.Events, events));

        // Read the colour off the rendered chip rather than a serialized JS payload.
        var chip = cut.FindAll("[data-event-id='e1']").First();
        Assert.Contains("last-color", chip.GetAttribute("style") ?? "", StringComparison.Ordinal);
    }
}
