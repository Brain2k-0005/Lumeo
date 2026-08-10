using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// bUnit coverage for <see cref="L.SchedulerAgendaView"/> — the first-party renderer behind
/// the existing <c>SchedulerView.List</c> value (spec §0's decision to keep the enum member,
/// change only the internal rendering).
/// </summary>
public class SchedulerAgendaViewTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d, int h = 0, int mi = 0) => new(y, m, d, h, mi, 0);

    [Fact]
    public void Empty_Events_Shows_NoEvents_Message()
    {
        var cut = _ctx.Render<L.SchedulerAgendaView>(p => p.Add(c => c.AnchorDate, D(2026, 3, 11)));

        Assert.NotEmpty(cut.FindAll("[role='list']"));
        Assert.Empty(cut.FindAll("[role='listitem']"));
    }

    [Fact]
    public void Groups_Events_By_Day_As_ListItems()
    {
        var events = new[]
        {
            new L.SchedulerEvent("e1", "Standup", D(2026, 3, 11, 9, 0), D(2026, 3, 11, 9, 30)),
            new L.SchedulerEvent("e2", "Review", D(2026, 3, 12, 14, 0), D(2026, 3, 12, 15, 0)),
        };
        var cut = _ctx.Render<L.SchedulerAgendaView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.DaysToShow, 7)
            .Add(c => c.Events, events));

        var items = cut.FindAll("[role='listitem']");
        Assert.Equal(2, items.Count);
        Assert.Contains("Standup", cut.Markup);
        Assert.Contains("Review", cut.Markup);
    }

    [Fact]
    public void Clicking_A_Row_Fires_OnEventClick()
    {
        L.SchedulerEvent? clicked = null;
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 11, 9, 0), D(2026, 3, 11, 9, 30)) };
        var cut = _ctx.Render<L.SchedulerAgendaView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.Events, events)
            .Add(c => c.OnEventClick, (L.SchedulerEvent e) => clicked = e));

        cut.Find("[role='listitem']").Click();

        Assert.NotNull(clicked);
        Assert.Equal("e1", clicked!.Id);
    }

    [Fact]
    public void Events_Outside_The_Window_Are_Excluded()
    {
        var events = new[]
        {
            new L.SchedulerEvent("in", "In window", D(2026, 3, 12), D(2026, 3, 12, 1, 0)),
            new L.SchedulerEvent("out", "Out of window", D(2026, 4, 20), D(2026, 4, 20, 1, 0)),
        };
        var cut = _ctx.Render<L.SchedulerAgendaView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.DaysToShow, 7)
            .Add(c => c.Events, events));

        Assert.Contains("In window", cut.Markup);
        Assert.DoesNotContain("Out of window", cut.Markup);
    }

    [Fact]
    public void Recurring_Event_Expands_Into_Multiple_ListItems()
    {
        var events = new[]
        {
            new L.SchedulerEvent(
                "rec1", "Daily standup", D(2026, 3, 9, 9, 0), D(2026, 3, 9, 9, 15),
                DaysOfWeek: new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday },
                RecurrenceEnd: D(2026, 3, 31)),
        };
        var cut = _ctx.Render<L.SchedulerAgendaView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 9))
            .Add(c => c.DaysToShow, 7)
            .Add(c => c.Events, events));

        // Mon 3/9, Tue 3/10, Wed 3/11 fall inside the 7-day window.
        Assert.Equal(3, cut.FindAll("[role='listitem']").Count);
    }
}
