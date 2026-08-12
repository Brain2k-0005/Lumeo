using System.Globalization;
using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// ReUI parity — its event calendar exposes a "week numbers" display toggle
/// (https://reui.io/components/event-calendar, "Display Controls"). Lumeo's
/// first-party month grid had no way to show them at all, so this is a
/// capability gap rather than a styling difference.
///
/// The interesting part is WHICH week number a display row gets. The label is
/// always the ISO-8601 week, read off the row's own Thursday — ISO's defining
/// day, and the one day-of-week every 7-day row contains exactly once
/// regardless of <c>FirstDayOfWeek</c>. Reading the row's FIRST cell instead
/// looks equivalent and is not: in a Sunday-start grid the leading Sunday
/// belongs to the PREVIOUS ISO week.
/// </summary>
public class SchedulerMonthViewWeekNumbersTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SchedulerMonthViewWeekNumbersTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Off_By_Default_The_Grid_Keeps_Its_Exact_Seven_Column_Markup()
    {
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 1, 15)));

        // Predicted-wrong result if the column were rendered unconditionally:
        // rowheader cells present, and the grid class switched away from
        // grid-cols-7 for every existing consumer.
        Assert.Empty(cut.FindAll("[role='rowheader']"));
        Assert.Contains("grid-cols-7", cut.Find("[role='grid']").GetAttribute("class"));
    }

    [Fact]
    public void On_Every_Row_Carries_Its_ISO_Week_And_The_Grid_Gains_One_Column()
    {
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 1, 15))
            .Add(c => c.ShowWeekNumbers, true));

        var headers = cut.FindAll("[role='rowheader']");
        Assert.Equal(6, headers.Count); // the grid is always 6 rows

        Assert.DoesNotContain("grid-cols-7", cut.Find("[role='grid']").GetAttribute("class"));
    }

    [Fact]
    public void The_Week_Number_Matches_ISOWeek_For_Each_Row_S_Own_Thursday()
    {
        // Independent oracle: recompute from the rendered day cells themselves
        // rather than restating the expected numbers as literals, so the test
        // cannot drift with the anchor month.
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 1, 15))
            .Add(c => c.ShowWeekNumbers, true));

        var dayCells = cut.FindAll("[data-cell-date]");
        Assert.Equal(42, dayCells.Count);

        var headers = cut.FindAll("[role='rowheader']");
        for (var row = 0; row < 6; row++)
        {
            var thursday = Enumerable.Range(0, 7)
                .Select(col => DateTime.ParseExact(
                    dayCells[row * 7 + col].GetAttribute("data-cell-date")!,
                    "yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Single(d => d.DayOfWeek == DayOfWeek.Thursday);

            Assert.Equal(ISOWeek.GetWeekOfYear(thursday).ToString(CultureInfo.InvariantCulture),
                headers[row].TextContent.Trim());
        }
    }

    [Fact]
    public void A_Sunday_Start_Grid_Labels_The_Same_Weeks_As_A_Monday_Start_One()
    {
        // The regression this pins: reading the row's FIRST cell would make the
        // Sunday-start grid report one week lower than the Monday-start grid for
        // rows whose leading Sunday belongs to the previous ISO week. Thursday
        // is in the same ISO week under both start days, so the labels must
        // agree for every row that covers the same Thursday.
        var monday = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 3, 15))
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Monday)
            .Add(c => c.ShowWeekNumbers, true));
        var sunday = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 3, 15))
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.ShowWeekNumbers, true));

        var mondayByThursday = WeekByThursday(monday);
        var sundayByThursday = WeekByThursday(sunday);

        foreach (var (thursday, week) in sundayByThursday)
        {
            if (mondayByThursday.TryGetValue(thursday, out var expected))
                Assert.Equal(expected, week);
        }
        // Guard against the assertion loop being vacuous.
        Assert.NotEmpty(sundayByThursday.Keys.Intersect(mondayByThursday.Keys));
    }

    private static Dictionary<DateTime, string> WeekByThursday(IRenderedComponent<L.SchedulerMonthView> cut)
    {
        var dayCells = cut.FindAll("[data-cell-date]");
        var headers = cut.FindAll("[role='rowheader']");
        var map = new Dictionary<DateTime, string>();
        for (var row = 0; row < 6; row++)
        {
            var thursday = Enumerable.Range(0, 7)
                .Select(col => DateTime.ParseExact(
                    dayCells[row * 7 + col].GetAttribute("data-cell-date")!,
                    "yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Single(d => d.DayOfWeek == DayOfWeek.Thursday);
            map[thursday] = headers[row].TextContent.Trim();
        }
        return map;
    }

    [Fact]
    public void The_Week_Column_Is_Not_A_Gridcell_So_Keyboard_Navigation_Is_Untouched()
    {
        // The month grid's arrow-key navigation walks day cells by index. A
        // week-number cell rendered as role="gridcell" would inject a
        // non-focusable stop into that sequence.
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 1, 15))
            .Add(c => c.ShowWeekNumbers, true));

        Assert.Equal(42, cut.FindAll("[role='gridcell']").Count);
        Assert.All(cut.FindAll("[role='rowheader']"),
            h => Assert.Null(h.GetAttribute("tabindex")));
    }
}
