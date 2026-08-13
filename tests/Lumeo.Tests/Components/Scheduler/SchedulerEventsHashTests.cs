using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Field coverage of the Events change-detection hash.
///
/// <para>
/// The component decides whether a parent genuinely changed its collection by hashing it. A
/// field the hash ignores is a field whose edits are silently dropped — the parent sets it,
/// the component compares equal, and nothing is adopted. Each test below changes exactly one
/// such field and nothing else.
/// </para>
///
/// <para>
/// These used to assert that a <c>scheduler.setEvents</c> push followed. With the FullCalendar
/// bridge gone there is no push to count, so adoption is observed where it actually matters:
/// a subsequent drag is committed against the record the component is holding, so the
/// collection it emits carries the new field value if — and only if — the change was adopted.
/// </para>
/// </summary>
public class SchedulerEventsHashTests : IAsyncLifetime
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

    /// <summary>
    /// Renders <paramref name="before"/>, has the parent swap in <paramref name="after"/>, then
    /// drags — returning what the component emitted, which is built from whichever record it
    /// is actually holding.
    /// </summary>
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

    private static L.SchedulerEvent Base(string id = "rec1") =>
        new(id, "Stand-up", Day.AddHours(9), Day.AddHours(9).AddMinutes(30));

    [Fact]
    public async Task Editing_DaysOfWeek_without_changing_count_is_adopted()
    {
        // Same COUNT, different day — the old count-only hash compared equal and the edit
        // was dropped on the floor.
        var before = Base() with { DaysOfWeek = new[] { DayOfWeek.Monday } };
        var after = before with { DaysOfWeek = new[] { DayOfWeek.Tuesday } };

        var stored = await AdoptThenEdit(before, after);

        Assert.Equal(new[] { DayOfWeek.Tuesday }, stored.DaysOfWeek);
    }

    [Fact]
    public async Task Editing_ExceptionDates_is_adopted()
    {
        var before = Base() with { ExceptionDates = new[] { Day.AddDays(1) } };
        var after = before with { ExceptionDates = new[] { Day.AddDays(2) } };

        var stored = await AdoptThenEdit(before, after);

        Assert.Equal(new[] { Day.AddDays(2) }, stored.ExceptionDates);
    }

    [Fact]
    public async Task Editing_Url_is_adopted()
    {
        var before = Base() with { Url = "https://example.com/a" };
        var after = before with { Url = "https://example.com/b" };

        var stored = await AdoptThenEdit(before, after);

        Assert.Equal("https://example.com/b", stored.Url);
    }

    [Fact]
    public async Task Editing_ExtendedProps_is_adopted()
    {
        var before = Base() with { ExtendedProps = new Dictionary<string, object> { ["owner"] = "alice" } };
        var after = before with { ExtendedProps = new Dictionary<string, object> { ["owner"] = "bob" } };

        var stored = await AdoptThenEdit(before, after);

        Assert.NotNull(stored.ExtendedProps);
        Assert.Equal("bob", stored.ExtendedProps!["owner"]);
    }
}
