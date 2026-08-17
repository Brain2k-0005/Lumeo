namespace Lumeo.SchedulerKernel;

/// <summary>
/// Pure, static calendar-field arithmetic backing every Scheduler view's grid boundaries: week
/// starts (for all 7 possible <c>FirstDayOfWeek</c> values), the always-42-cell month grid, and
/// leap-year-safe day counts. Spec §1.1/§7.1.
///
/// TZ/DST safety (spec §2.3): every method below operates purely on calendar-field reads
/// (<c>Year</c>/<c>Month</c>/<c>Day</c>/<c>DayOfWeek</c>) and calendar-field arithmetic
/// (<c>AddDays</c>) — never <see cref="TimeZoneInfo"/>, never <c>ToLocalTime</c>/<c>ToUniversalTime</c>,
/// never elapsed-time (<c>TimeSpan</c>) subtraction across a day boundary. <see cref="DateTime.Kind"/>
/// is therefore irrelevant to every result here, and nothing in this class can be perturbed by a
/// DST transition or by the difference between a CI box (Ubuntu/UTC) and a local dev machine's
/// timezone — see <c>Lumeo.GanttV3.GanttScale</c> (src/Lumeo.Gantt/UI/GanttV3/GanttScale.cs)
/// for the identical argument applied to the Gantt v3 timeline, which this class deliberately
/// mirrors (not a <c>cref</c>: Lumeo.Scheduler doesn't reference Lumeo.Gantt).
/// </summary>
internal static class SchedulerDateMath
{
    /// <summary>
    /// Every month-grid view (FullCalendar's <c>dayGridMonth</c>, and every calendar app that
    /// copies it) renders a fixed 6-week-by-7-day grid regardless of how many weeks the month
    /// itself actually spans, so the grid never visually resizes as the user navigates between
    /// months.
    /// </summary>
    internal const int MonthGridCellCount = 42;

    /// <summary>
    /// The first day of the calendar week containing <paramref name="date"/>, per
    /// <paramref name="firstDayOfWeek"/>. Time-of-day is dropped (the result is always midnight).
    /// Faithful port of <c>Scheduler.razor</c>'s existing <c>StartOfWeek</c> (int-typed
    /// <c>FirstDayOfWeek</c>, 0-6 Sun-Sat) — same formula, <see cref="DayOfWeek"/>-typed here
    /// since the kernel is a fresh implementation, not a byte-for-byte extraction of the
    /// FullCalendar-era component that has since been removed.
    /// </summary>
    internal static DateTime StartOfWeek(DateTime date, DayOfWeek firstDayOfWeek)
    {
        var diff = (7 + (date.DayOfWeek - firstDayOfWeek)) % 7;
        return date.Date.AddDays(-diff);
    }

    /// <summary>
    /// The last day (still midnight, inclusive) of the calendar week containing
    /// <paramref name="date"/>, per <paramref name="firstDayOfWeek"/>. Always exactly 6 days
    /// after <see cref="StartOfWeek"/>.
    /// </summary>
    internal static DateTime EndOfWeek(DateTime date, DayOfWeek firstDayOfWeek) =>
        StartOfWeek(date, firstDayOfWeek).AddDays(6);

    /// <summary>
    /// The first day (midnight) of the calendar month containing <paramref name="date"/>.
    /// </summary>
    internal static DateTime StartOfMonth(DateTime date) => new(date.Year, date.Month, 1);

    /// <summary>
    /// Builds the fixed <see cref="MonthGridCellCount"/>-cell (6 weeks x 7 days) grid a month
    /// view renders, anchored so the grid always fully covers every day of
    /// <paramref name="monthAnchor"/>'s month regardless of which weekday the 1st falls on or how
    /// many weeks the month itself spans (28-31 days can span 4, 5, or 6 calendar weeks
    /// depending on alignment — the grid is always exactly 6 to accommodate the worst case, with
    /// leading/trailing days from the adjacent months filling the remainder). Cell 0 is always
    /// <c>StartOfWeek(StartOfMonth(monthAnchor), firstDayOfWeek)</c>; each subsequent cell is
    /// exactly one day later — a plain <c>AddDays</c> walk, which is "AddMonths-safe" in the
    /// sense that it never itself calls <c>AddMonths</c> (the one call to <c>AddMonths</c>-adjacent
    /// logic — anchoring on day-1 of the month before doing any date walk — is what avoids the
    /// classic "Jan 31 + 1 month" overflow trap; see <see cref="StartOfMonth"/>).
    /// </summary>
    internal static IReadOnlyList<DateTime> BuildMonthGrid(DateTime monthAnchor, DayOfWeek firstDayOfWeek)
    {
        var gridStart = StartOfWeek(StartOfMonth(monthAnchor), firstDayOfWeek);
        var cells = new DateTime[MonthGridCellCount];
        for (var i = 0; i < MonthGridCellCount; i++)
            cells[i] = gridStart.AddDays(i);
        return cells;
    }

    /// <summary>
    /// Number of days in <paramref name="year"/>/<paramref name="month"/>, leap-year-aware.
    /// Thin, explicitly-named wrapper over <see cref="DateTime.DaysInMonth"/> so callers doing
    /// month-grid/recurrence math don't reach for the BCL method ad hoc — kept here as the single
    /// place this kernel's day-count logic lives.
    /// </summary>
    internal static int DaysInMonth(int year, int month) => DateTime.DaysInMonth(year, month);
}
