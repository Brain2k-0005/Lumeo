using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Recurrence-rule coverage of the Events change-detection hash — the companion to
/// <see cref="SchedulerEventsHashTests"/>, which explains the shape of these.
///
/// <para>
/// A rule field the hash ignores is a rule edit the component silently drops. These change
/// exactly one field and check that a later edit is committed against the NEW rule, which is
/// only true if the change was adopted.
/// </para>
/// </summary>
public class SchedulerRecurrenceHashTests : IAsyncLifetime
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

    private async Task<L.SchedulerEvent> AdoptThenEdit(L.SchedulerEvent before, L.SchedulerEvent after)
    {
        IEnumerable<L.SchedulerEvent>? emitted = null;
        var sink = EventCallback.Factory.Create<IEnumerable<L.SchedulerEvent>>(this, (IEnumerable<L.SchedulerEvent> e) => emitted = e);

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, new[] { before })
            .Add(c => c.EventsChanged, sink));

        cut.Render(p => p
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, new[] { after })
            .Add(c => c.EventsChanged, sink));

        await cut.InvokeAsync(() => cut.FindComponent<L.SchedulerMonthView>().Instance
            .CommitDrag(after.Id, Target.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

        Assert.NotNull(emitted);
        return Assert.Single(emitted!);
    }

    private static L.SchedulerEvent Sync =>
        new("r1", "Sync", Day.AddHours(9), Day.AddHours(9).AddMinutes(30));

    [Fact]
    public async Task Editing_Recurrence_Interval_is_adopted()
    {
        var before = Sync with { Recurrence = new L.SchedulerRecurrenceRule(L.SchedulerRecurrenceFrequency.Weekly, Interval: 1) };
        var after = before with { Recurrence = before.Recurrence! with { Interval = 2 } };

        var stored = await AdoptThenEdit(before, after);

        Assert.Equal(2, stored.Recurrence!.Interval);
    }

    [Fact]
    public async Task Editing_Recurrence_ByDay_without_changing_count_is_adopted()
    {
        // Same count, different day — the trap a count-only hash falls into.
        var before = Sync with
        {
            Recurrence = new L.SchedulerRecurrenceRule(L.SchedulerRecurrenceFrequency.Weekly,
                ByDay: new[] { new L.SchedulerByDayRule(DayOfWeek.Monday) }),
        };
        var after = before with { Recurrence = before.Recurrence! with { ByDay = new[] { new L.SchedulerByDayRule(DayOfWeek.Tuesday) } } };

        var stored = await AdoptThenEdit(before, after);

        Assert.Equal(DayOfWeek.Tuesday, Assert.Single(stored.Recurrence!.ByDay!).Day);
    }

    [Fact]
    public async Task Adding_Recurrence_To_A_Plain_Event_is_adopted()
    {
        var before = Sync;
        var after = before with { Recurrence = new L.SchedulerRecurrenceRule(L.SchedulerRecurrenceFrequency.Daily) };

        var stored = await AdoptThenEdit(before, after);

        Assert.NotNull(stored.Recurrence);
        Assert.Equal(L.SchedulerRecurrenceFrequency.Daily, stored.Recurrence!.Freq);
    }
}
