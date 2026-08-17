using System.Globalization;

namespace Lumeo;

/// <summary>The granularity of one column on a <see cref="SchedulerTimelineView"/>'s horizontal axis.</summary>
public enum SchedulerTimelineUnit
{
    /// <summary>One column per day.</summary>
    Day,

    /// <summary>One column per week, starting on the view's configured first day of week.</summary>
    Week,

    /// <summary>One column per calendar month.</summary>
    Month,
}

/// <summary>
/// Date-to-pixel arithmetic for the resource timeline's horizontal axis.
///
/// <para>
/// Deliberately its own implementation rather than a reference to Lumeo.Gantt, whose
/// <c>GanttScale</c> solves the same problem: that type is internal to the Gantt package, and
/// making <c>Lumeo.Scheduler</c> depend on <c>Lumeo.Gantt</c> would put a whole Gantt into every
/// project that only wanted a calendar — the opposite of why the FullCalendar dependency was
/// removed. The subset needed here is small enough that duplicating it costs less than the
/// coupling would.
/// </para>
///
/// <para>
/// Everything below is calendar-field arithmetic on wall-clock values, never
/// <see cref="TimeZoneInfo"/> and never elapsed-time subtraction across a day boundary, so a
/// DST transition cannot shift a column — the same discipline the grid views follow.
/// </para>
/// </summary>
public static class SchedulerTimelineScale
{
    /// <summary>Default column width in pixels for each unit — wide enough to read a date label.</summary>
    public static int DefaultColumnWidth(SchedulerTimelineUnit unit) => unit switch
    {
        SchedulerTimelineUnit.Day => 96,
        SchedulerTimelineUnit.Week => 120,
        SchedulerTimelineUnit.Month => 140,
        _ => 96,
    };

    /// <summary>
    /// The first instant of the column containing <paramref name="date"/>. Columns must start on
    /// a real boundary or every bar on the axis is offset by the remainder.
    /// </summary>
    public static DateTime AlignOrigin(SchedulerTimelineUnit unit, DateTime date, DayOfWeek firstDayOfWeek) => unit switch
    {
        SchedulerTimelineUnit.Day => date.Date,
        SchedulerTimelineUnit.Week => StartOfWeek(date, firstDayOfWeek),
        SchedulerTimelineUnit.Month => new DateTime(date.Year, date.Month, 1),
        _ => date.Date,
    };

    /// <summary>The column start dates covering <paramref name="count"/> units from <paramref name="origin"/>.</summary>
    public static IReadOnlyList<DateTime> BuildColumns(SchedulerTimelineUnit unit, DateTime origin, int count)
    {
        var n = Math.Max(1, count);
        var cols = new List<DateTime>(n);
        cols.Add(origin);
        for (var i = 1; i < n; i++)
        {
            // Truncate, do not saturate (Codex review round 6). Clamping each step to
            // DateTime.MaxValue produced a run of identical final columns: the same date drawn
            // over and over, headers and rules overflowing a track measured for one column.
            // An axis that reaches the end of the calendar is simply shorter.
            //
            // Stepping from the PREVIOUS column rather than recomputing from the origin is what
            // makes the saturation visible: Advance returns DateTime.MaxValue itself, which is
            // a later instant on the same final day and so compares as genuine progress.
            var next = Advance(unit, cols[^1], 1);
            if (next == DateTime.MaxValue || next <= cols[^1]) break;
            cols.Add(next);
        }
        return cols;
    }

    /// <summary>
    /// Horizontal offset in pixels of <paramref name="date"/> from <paramref name="origin"/>.
    /// Fractional within a column, so a bar starting mid-day lands mid-column rather than
    /// snapping to the boundary.
    /// </summary>
    public static double DateToPixel(SchedulerTimelineUnit unit, DateTime origin, DateTime date, int columnWidth) => unit switch
    {
        SchedulerTimelineUnit.Day => (date - origin).TotalDays * columnWidth,
        SchedulerTimelineUnit.Week => (date - origin).TotalDays / 7.0 * columnWidth,

        // Months are unequal, so a day fraction has to be taken against the LENGTH OF THE
        // MONTH the date falls in — a flat /30 would drift by up to a day inside February and
        // by half a day in every 31-day month.
        SchedulerTimelineUnit.Month => MonthsBetween(origin, date) * columnWidth
                                        + (date.Day - 1 + date.TimeOfDay.TotalDays)
                                          / DateTime.DaysInMonth(date.Year, date.Month) * columnWidth,
        _ => 0,
    };

    /// <summary>Total width in pixels of <paramref name="count"/> columns.</summary>
    public static double TotalWidth(SchedulerTimelineUnit unit, DateTime origin, int count, int columnWidth)
    {
        var cols = BuildColumns(unit, origin, count);
        return DateToPixel(unit, origin, Advance(unit, cols[^1], 1), columnWidth);
    }

    /// <summary>
    /// Moves <paramref name="from"/> forward by <paramref name="units"/> columns, stopping at
    /// <see cref="DateTime.MaxValue"/> rather than throwing. A caller may legitimately anchor a
    /// timeline in the last representable column — <c>RangeStart</c> is public — and the
    /// exclusive end of that column does not exist (Codex review of PR #424).
    /// </summary>
    internal static DateTime Advance(SchedulerTimelineUnit unit, DateTime from, int units)
    {
        if (units <= 0) return from;

        switch (unit)
        {
            case SchedulerTimelineUnit.Week:
            {
                var days = (long)units * 7;
                return days > (DateTime.MaxValue - from).TotalDays ? DateTime.MaxValue : from.AddDays(days);
            }
            case SchedulerTimelineUnit.Month:
            {
                var monthsLeft = (DateTime.MaxValue.Year - from.Year) * 12L + (DateTime.MaxValue.Month - from.Month);
                return units > monthsLeft ? DateTime.MaxValue : from.AddMonths(units);
            }
            default:
                return units > (DateTime.MaxValue - from).TotalDays ? DateTime.MaxValue : from.AddDays(units);
        }
    }

    /// <summary>
    /// Moves <paramref name="from"/> BACKWARD by <paramref name="units"/> columns, stopping at
    /// <see cref="DateTime.MinValue"/>. The toolbar pages an axis that may already sit on the
    /// lower boundary, and a view that renders must not be crashable by its own Previous button
    /// (Codex review of PR #424).
    /// </summary>
    internal static DateTime Retreat(SchedulerTimelineUnit unit, DateTime from, int units)
    {
        if (units <= 0) return from;

        switch (unit)
        {
            case SchedulerTimelineUnit.Week:
            {
                var days = (long)units * 7;
                return days > (from - DateTime.MinValue).TotalDays ? DateTime.MinValue : from.AddDays(-days);
            }
            case SchedulerTimelineUnit.Month:
            {
                var monthsBack = (from.Year - DateTime.MinValue.Year) * 12L + (from.Month - DateTime.MinValue.Month);
                return units > monthsBack ? DateTime.MinValue : from.AddMonths(-units);
            }
            default:
                return units > (from - DateTime.MinValue).TotalDays ? DateTime.MinValue : from.AddDays(-units);
        }
    }

    /// <summary>Steps <paramref name="from"/> by <paramref name="units"/> columns in either
    /// direction, clamped to the representable range.</summary>
    internal static DateTime Step(SchedulerTimelineUnit unit, DateTime from, int units) =>
        units >= 0 ? Advance(unit, from, units) : Retreat(unit, from, -units);

    /// <summary>Header label for a column, at the granularity that column represents.</summary>
    public static string ColumnLabel(SchedulerTimelineUnit unit, DateTime column, CultureInfo culture) => unit switch
    {
        SchedulerTimelineUnit.Day => column.ToString("ddd d", culture),
        SchedulerTimelineUnit.Week => column.ToString("MMM d", culture),
        SchedulerTimelineUnit.Month => column.ToString("MMM yyyy", culture),
        _ => column.ToString("d", culture),
    };

    /// <summary>Whether <paramref name="column"/> is the column containing <paramref name="today"/>.</summary>
    public static bool IsCurrentColumn(SchedulerTimelineUnit unit, DateTime column, DateTime today) => unit switch
    {
        SchedulerTimelineUnit.Day => column.Date == today.Date,
        SchedulerTimelineUnit.Week => today.Date >= column.Date
                                      && today.Date < Advance(SchedulerTimelineUnit.Week, column.Date, 1),
        SchedulerTimelineUnit.Month => column.Year == today.Year && column.Month == today.Month,
        _ => false,
    };

    private static int MonthsBetween(DateTime origin, DateTime date) =>
        (date.Year - origin.Year) * 12 + (date.Month - origin.Month);

    private static DateTime StartOfWeek(DateTime date, DayOfWeek firstDayOfWeek)
    {
        var diff = (7 + (date.DayOfWeek - firstDayOfWeek)) % 7;
        // A caller may anchor at DateTime.MinValue, and with a week start other than the one
        // that date happens to fall on, this step goes below the representable range
        // (Codex review round 5).
        return diff > (date.Date - DateTime.MinValue).TotalDays ? DateTime.MinValue : date.Date.AddDays(-diff);
    }
}
