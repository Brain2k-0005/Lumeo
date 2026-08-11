using Lumeo.SchedulerKernel;
using Xunit;

namespace Lumeo.Tests.Components.SchedulerKernel;

/// <summary>
/// Pure calendar-field-arithmetic tests for <see cref="SchedulerDateMath"/> — spec §7.1's
/// "StartOfWeek for all 7 FirstDayOfWeek values, month-grid padding (42-cell grid always fully
/// covers the month...), leap-year February" matrix.
///
/// TZ/DST note: every <see cref="DateTime"/> below is a plain unspecified-kind value and none of
/// <see cref="SchedulerDateMath"/>'s methods ever call <see cref="TimeZoneInfo"/> or convert
/// between local/UTC — so these assertions hold identically whether the test runner is CI's
/// Ubuntu/UTC box or a Windows dev machine in any local timezone (spec §7.3 rule 1).
/// </summary>
public class SchedulerDateMathTests
{
    private static readonly DayOfWeek[] AllWeekStarts =
    {
        DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
        DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday
    };

    // ── StartOfWeek / EndOfWeek ─────────────────────────────────────────────

    [Fact]
    public void StartOfWeek_Returns_A_Date_With_The_Requested_DayOfWeek_For_Every_FirstDayOfWeek_Value()
    {
        // 2026-08-10 (today's anchor for this task) is a Monday.
        var monday = new DateTime(2026, 8, 10);

        foreach (var firstDayOfWeek in AllWeekStarts)
        {
            var start = SchedulerDateMath.StartOfWeek(monday, firstDayOfWeek);

            Assert.Equal(firstDayOfWeek, start.DayOfWeek);
            Assert.True(start <= monday, $"start ({start:O}) must be on/before the anchor for firstDayOfWeek={firstDayOfWeek}");
            Assert.True(start > monday.AddDays(-7), $"start ({start:O}) must be within the last 7 days for firstDayOfWeek={firstDayOfWeek}");
            Assert.Equal(TimeSpan.Zero, start.TimeOfDay);
        }
    }

    [Theory]
    [InlineData(DayOfWeek.Sunday, 2026, 8, 9)]   // US-style week: Sunday 2026-08-09 starts the week containing Monday 2026-08-10.
    [InlineData(DayOfWeek.Monday, 2026, 8, 10)]  // ISO-style week: Monday 2026-08-10 IS the start.
    [InlineData(DayOfWeek.Saturday, 2026, 8, 8)] // Middle-East-style week.
    public void StartOfWeek_Matches_Hand_Computed_Anchor_Dates(DayOfWeek firstDayOfWeek, int year, int month, int day)
    {
        var monday = new DateTime(2026, 8, 10);
        Assert.Equal(new DateTime(year, month, day), SchedulerDateMath.StartOfWeek(monday, firstDayOfWeek));
    }

    [Fact]
    public void StartOfWeek_Is_Idempotent_Across_Every_Day_In_The_Same_Week()
    {
        foreach (var firstDayOfWeek in AllWeekStarts)
        {
            var start = SchedulerDateMath.StartOfWeek(new DateTime(2026, 8, 10), firstDayOfWeek);
            for (var i = 0; i < 7; i++)
                Assert.Equal(start, SchedulerDateMath.StartOfWeek(start.AddDays(i), firstDayOfWeek));
        }
    }

    [Fact]
    public void EndOfWeek_Is_Always_Six_Days_After_StartOfWeek()
    {
        foreach (var firstDayOfWeek in AllWeekStarts)
        {
            var date = new DateTime(2026, 8, 10);
            Assert.Equal(SchedulerDateMath.StartOfWeek(date, firstDayOfWeek).AddDays(6), SchedulerDateMath.EndOfWeek(date, firstDayOfWeek));
        }
    }

    [Fact]
    public void StartOfWeek_Drops_Time_Of_Day()
    {
        var withTime = new DateTime(2026, 8, 12, 14, 37, 9);
        var start = SchedulerDateMath.StartOfWeek(withTime, DayOfWeek.Monday);
        Assert.Equal(TimeSpan.Zero, start.TimeOfDay);
    }

    // ── BuildMonthGrid: always 42 cells, always fully covers the month ─────

    [Fact]
    public void BuildMonthGrid_Is_Always_42_Cells_Contiguous_And_Fully_Covers_The_Month_Every_Month_Every_WeekStart()
    {
        // Sweep two full years (24 months) x all 7 FirstDayOfWeek values = 168 grids. Includes
        // both a leap (2028) and non-leap (2026) year so February's day count varies too.
        foreach (var year in new[] { 2026, 2028 })
        {
            for (var month = 1; month <= 12; month++)
            {
                foreach (var firstDayOfWeek in AllWeekStarts)
                {
                    var anchor = new DateTime(year, month, 1);
                    var grid = SchedulerDateMath.BuildMonthGrid(anchor, firstDayOfWeek);

                    Assert.Equal(SchedulerDateMath.MonthGridCellCount, grid.Count);
                    Assert.Equal(firstDayOfWeek, grid[0].DayOfWeek);

                    // Contiguous: every cell is exactly one day after the previous one.
                    for (var i = 1; i < grid.Count; i++)
                        Assert.Equal(grid[i - 1].AddDays(1), grid[i]);

                    var firstOfMonth = new DateTime(year, month, 1);
                    var daysInMonth = DateTime.DaysInMonth(year, month);
                    var lastOfMonth = new DateTime(year, month, daysInMonth);

                    Assert.True(grid[0] <= firstOfMonth, $"{year}-{month:D2} firstDayOfWeek={firstDayOfWeek}: grid must start on/before the 1st.");
                    Assert.True(grid[^1] >= lastOfMonth, $"{year}-{month:D2} firstDayOfWeek={firstDayOfWeek}: grid must end on/after the last day.");

                    // Every day of the target month appears in the grid exactly once.
                    for (var d = 1; d <= daysInMonth; d++)
                        Assert.Contains(new DateTime(year, month, d), grid);
                }
            }
        }
    }

    [Fact]
    public void BuildMonthGrid_First_Cell_Is_Always_A_Valid_Week_Start_For_The_Requested_FirstDayOfWeek()
    {
        // Regression for the specific bug shape this method must avoid: anchoring the grid via a
        // naive AddMonths/AddDays walk from an un-normalized date instead of from day-1 of the
        // month can misalign the grid by a week in edge months. Every month/weekstart pair's
        // grid[0] must equal SchedulerDateMath.StartOfWeek applied to day-1 of that month.
        foreach (var month in Enumerable.Range(1, 12))
        foreach (var firstDayOfWeek in AllWeekStarts)
        {
            var anchor = new DateTime(2026, month, 15); // mid-month anchor, not day-1
            var expected = SchedulerDateMath.StartOfWeek(new DateTime(2026, month, 1), firstDayOfWeek);
            Assert.Equal(expected, SchedulerDateMath.BuildMonthGrid(anchor, firstDayOfWeek)[0]);
        }
    }

    // ── Leap years ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(2024, true)]  // divisible by 4, not by 100 -> leap
    [InlineData(2026, false)] // not divisible by 4 -> not leap
    [InlineData(2028, true)]  // divisible by 4, not by 100 -> leap
    [InlineData(1900, false)] // divisible by 100, not by 400 -> NOT leap
    [InlineData(2000, true)]  // divisible by 400 -> leap
    public void DaysInMonth_February_Reflects_Leap_Year_Rules(int year, bool isLeap)
    {
        Assert.Equal(isLeap ? 29 : 28, SchedulerDateMath.DaysInMonth(year, 2));
    }

    [Fact]
    public void BuildMonthGrid_For_February_Of_A_Leap_Year_Includes_The_29th()
    {
        var grid = SchedulerDateMath.BuildMonthGrid(new DateTime(2028, 2, 1), DayOfWeek.Monday);
        Assert.Contains(new DateTime(2028, 2, 29), grid);
    }

    [Fact]
    public void BuildMonthGrid_For_February_Of_A_Non_Leap_Year_Does_Not_Include_The_29th()
    {
        // 2027-02-29 doesn't exist as a constructible DateTime at all (2027 is not a leap year),
        // so there is nothing to assert "not contained" — the meaningful assertion is that every
        // REAL day of the month (1-28) is covered, with no gaps and no need for a 29th.
        var grid = SchedulerDateMath.BuildMonthGrid(new DateTime(2027, 2, 1), DayOfWeek.Monday);
        for (var d = 1; d <= 28; d++)
            Assert.Contains(new DateTime(2027, 2, d), grid);
    }

    // ── StartOfMonth ─────────────────────────────────────────────────────────

    [Fact]
    public void StartOfMonth_Anchors_On_Day_One_Without_Overflowing_A_Short_Month()
    {
        // The classic "Jan 31 + 1 month" trap: StartOfMonth must anchor on day-1 BEFORE any
        // AddMonths call happens downstream (BuildMonthGrid), never add a month to day-31 directly.
        var jan31 = new DateTime(2026, 1, 31);
        Assert.Equal(new DateTime(2026, 1, 1), SchedulerDateMath.StartOfMonth(jan31));
        Assert.Equal(new DateTime(2026, 2, 1), SchedulerDateMath.StartOfMonth(jan31).AddMonths(1));
    }
}
