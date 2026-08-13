using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// A multi-day event must read as ONE bar across the days it covers.
///
/// <para>
/// Reported with a screenshot: a two-day event showed a titled chip on the first day and an
/// empty bar on the second, looking like two broken events rather than one span. The lane
/// model already dropped the rounded corners on the joining sides — the segments simply never
/// touched, because a day cell pads its content by 4px and carries a 1px right border, so
/// consecutive segments sat 9px apart.
/// </para>
///
/// <para>
/// Measured on the rendered page rather than reasoned about: 9px before, 0px after. The first
/// attempt closed only 4px, because the pill is <c>w-full</c> and a negative margin shifts a
/// 100%-wide box without widening it — which is why the width grows by the same amount it
/// bleeds.
/// </para>
/// </summary>
public class SchedulerMonthSpanTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SchedulerMonthSpanTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly DateTime Anchor = new(2026, 3, 15);

    /// <summary>Wed 11th to Fri 13th March 2026 — three days inside one week row.</summary>
    private static L.SchedulerEvent ThreeDayRun =>
        new("run", "Offsite", new DateTime(2026, 3, 11, 9, 0, 0), new DateTime(2026, 3, 13, 17, 0, 0));

    private IRenderedComponent<L.SchedulerMonthView> Render(params L.SchedulerEvent[] events) =>
        _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, Anchor)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Monday)
            .Add(c => c.Events, events));

    private static string StyleOfSegment(IRenderedComponent<L.SchedulerMonthView> cut, int index) =>
        cut.FindAll("[data-event-id='run']")[index].GetAttribute("style") ?? "";

    [Fact]
    public void The_middle_of_a_run_bleeds_on_both_sides()
    {
        var cut = Render(ThreeDayRun);

        var middle = StyleOfSegment(cut, 1);
        Assert.Contains("margin-left: -4px", middle, StringComparison.Ordinal);
        Assert.Contains("margin-right: -5px", middle, StringComparison.Ordinal);
        // Both bleeds, so the box must grow by both — a margin alone would only shift it.
        Assert.Contains("width: calc(100% + 9px)", middle, StringComparison.Ordinal);
    }

    [Fact]
    public void The_first_day_bleeds_only_to_the_right()
    {
        var cut = Render(ThreeDayRun);

        var first = StyleOfSegment(cut, 0);
        Assert.DoesNotContain("margin-left", first, StringComparison.Ordinal);
        Assert.Contains("margin-right: -5px", first, StringComparison.Ordinal);
        Assert.Contains("width: calc(100% + 5px)", first, StringComparison.Ordinal);
    }

    [Fact]
    public void The_last_day_bleeds_only_to_the_left()
    {
        var cut = Render(ThreeDayRun);

        var last = StyleOfSegment(cut, 2);
        Assert.Contains("margin-left: -4px", last, StringComparison.Ordinal);
        Assert.DoesNotContain("margin-right", last, StringComparison.Ordinal);
        Assert.Contains("width: calc(100% + 4px)", last, StringComparison.Ordinal);
    }

    [Fact]
    public void A_single_day_event_is_left_alone()
    {
        // The bleed exists to join segments. A one-day event has nothing to join to, and
        // widening it would push it over its cell's borders for no reason.
        var cut = Render(new L.SchedulerEvent("run", "Standup",
            new DateTime(2026, 3, 11, 9, 0, 0), new DateTime(2026, 3, 11, 10, 0, 0)));

        var only = StyleOfSegment(cut, 0);
        Assert.DoesNotContain("margin-left", only, StringComparison.Ordinal);
        Assert.DoesNotContain("margin-right", only, StringComparison.Ordinal);
        Assert.DoesNotContain("width: calc", only, StringComparison.Ordinal);
    }

    [Fact]
    public void Separate_occurrences_of_a_recurring_event_are_not_joined()
    {
        // A weekday-recurring event puts one instance on each day. Those are distinct
        // occurrences, not one run, so joining them into a bar would be a lie about the data.
        var cut = Render(new L.SchedulerEvent("run", "Stand-up",
            new DateTime(2026, 3, 11, 9, 0, 0), new DateTime(2026, 3, 11, 9, 15, 0),
            DaysOfWeek: new[] { DayOfWeek.Wednesday, DayOfWeek.Thursday }));

        foreach (var chip in cut.FindAll("[data-event-id='run']"))
        {
            var style = chip.GetAttribute("style") ?? "";
            Assert.DoesNotContain("margin-left", style, StringComparison.Ordinal);
            Assert.DoesNotContain("margin-right", style, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Only_the_run_start_carries_the_title()
    {
        // The continuation days are deliberately blank: with the segments now touching, one
        // title at the start labels the whole bar. Repeating it per day would print the name
        // several times inside a single bar.
        var cut = Render(ThreeDayRun);

        var titled = cut.FindAll("[data-event-id='run']")
                        .Count(c => c.TextContent.Contains("Offsite", StringComparison.Ordinal));
        Assert.Equal(1, titled);
    }
}
