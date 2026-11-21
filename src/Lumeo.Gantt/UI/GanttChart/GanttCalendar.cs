using System.Globalization;

namespace Lumeo.GanttV3;

/// <summary>
/// Calendar-system aware date helpers for the Gantt time scale, layered on the core
/// <c>CalendarSystemMath</c>. Task dates stay Gregorian <see cref="DateTime"/>s; only the
/// unit boundaries (month / quarter / year starts), the columns' real day counts, the week
/// numbering and the displayed text follow the selected <see cref="CalendarSystem"/>.
/// Every method takes the Gregorian fast path unchanged when <see cref="CalendarSystem.Gregorian"/>
/// is selected, so the default behaves exactly as before.
/// </summary>
internal static class GanttCalendar
{
    private static DateOnly D(DateTime d) => DateOnly.FromDateTime(d);

    private static DateTime At(DateOnly date, DateTime like) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue) + like.TimeOfDay, like.Kind);

    internal static CultureInfo Culture(CultureInfo? culture) => culture ?? CultureInfo.CurrentCulture;

    internal static int Year(CalendarSystem s, DateTime d) => s == CalendarSystem.Jalali ? CalendarSystemMath.GetYear(s, D(d)) : d.Year;
    internal static int Month(CalendarSystem s, DateTime d) => s == CalendarSystem.Jalali ? CalendarSystemMath.GetMonth(s, D(d)) : d.Month;
    internal static int Day(CalendarSystem s, DateTime d) => s == CalendarSystem.Jalali ? CalendarSystemMath.GetDay(s, D(d)) : d.Day;

    /// <summary>Real number of days in the calendar month <paramref name="d"/> falls in (29/30/31).</summary>
    internal static int DaysInMonth(CalendarSystem s, DateTime d)
    {
        if (s != CalendarSystem.Jalali) return DateTime.DaysInMonth(d.Year, d.Month);
        return CalendarSystemMath.DaysInMonth(s, D(d));
    }

    /// <summary>First day (00:00) of <paramref name="year"/> in the calendar system.</summary>
    internal static DateTime YearStartOf(CalendarSystem s, int year, DateTimeKind kind) =>
        s == CalendarSystem.Jalali
            ? DateTime.SpecifyKind(CalendarSystemMath.MonthStart(s, year, 1).ToDateTime(TimeOnly.MinValue), kind)
            : new DateTime(year, 1, 1, 0, 0, 0, kind);

    internal static DateTime YearStart(CalendarSystem s, DateTime d) =>
        s == CalendarSystem.Jalali ? YearStartOf(s, Year(s, d), d.Kind) : new DateTime(d.Year, 1, 1, 0, 0, 0, d.Kind);

    /// <summary>Real number of days in the calendar year <paramref name="d"/> falls in.</summary>
    internal static int DaysInYear(CalendarSystem s, DateTime d)
    {
        if (s != CalendarSystem.Jalali) return DateTime.IsLeapYear(d.Year) ? 366 : 365;
        var y = Year(s, d);
        return (YearStartOf(s, y + 1, d.Kind) - YearStartOf(s, y, d.Kind)).Days;
    }

    internal static DateTime MonthStart(CalendarSystem s, DateTime d) =>
        s == CalendarSystem.Jalali
            ? DateTime.SpecifyKind(CalendarSystemMath.MonthStart(s, D(d)).ToDateTime(TimeOnly.MinValue), d.Kind)
            : new DateTime(d.Year, d.Month, 1, 0, 0, 0, d.Kind);

    /// <summary>First day of the quarter: Gregorian months 1/4/7/10; Jalali months 1/4/7/10 (the four seasons).</summary>
    internal static DateTime QuarterStart(CalendarSystem s, DateTime d)
    {
        if (s != CalendarSystem.Jalali) return new DateTime(d.Year, ((d.Month - 1) / 3) * 3 + 1, 1, 0, 0, 0, d.Kind);
        var m = Month(s, d);
        return DateTime.SpecifyKind(
            CalendarSystemMath.MonthStart(s, Year(s, d), ((m - 1) / 3) * 3 + 1).ToDateTime(TimeOnly.MinValue), d.Kind);
    }

    internal static int QuarterOf(CalendarSystem s, DateTime d) => (Month(s, d) - 1) / 3 + 1;

    internal static DateTime AddMonths(CalendarSystem s, DateTime d, int months) =>
        s == CalendarSystem.Jalali ? At(CalendarSystemMath.AddMonths(s, D(d), months), d) : d.AddMonths(months);

    internal static DateTime AddYears(CalendarSystem s, DateTime d, int years) =>
        s == CalendarSystem.Jalali ? AddMonths(s, d, years * 12) : d.AddYears(years);

    /// <summary>Whole calendar months between two dates' months (ignores the day of month).</summary>
    internal static int MonthsBetween(CalendarSystem s, DateTime start, DateTime end) =>
        (Year(s, end) - Year(s, start)) * 12 + (Month(s, end) - Month(s, start));

    /// <summary>Start of the week containing <paramref name="d"/> given the first weekday.</summary>
    internal static DateTime WeekStart(DateTime d, DayOfWeek firstDayOfWeek)
    {
        var diff = ((int)d.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
        return DateTime.SpecifyKind(d.Date.AddDays(-diff), d.Kind);
    }

    /// <summary>
    /// Jalali week-of-year of the 7-day band starting at <paramref name="bandStart"/>: week 1 is the
    /// band (starting on <paramref name="firstDayOfWeek"/>) that contains 1 Farvardin; a band belongs
    /// to the Jalali year its LAST day falls in, so a band straddling Esfand/Farvardin is week 1 of
    /// the new year.
    /// </summary>
    internal static int JalaliWeekOfYear(DateTime bandStart, DayOfWeek firstDayOfWeek)
    {
        var s = CalendarSystem.Jalali;
        var band = bandStart.Date;
        var year = Year(s, band.AddDays(6));
        var week1 = WeekStart(YearStartOf(s, year, band.Kind), firstDayOfWeek);
        return (band - week1).Days / 7 + 1;
    }

    /// <summary>Formats a date. Gregorian: plain <c>ToString</c>; Jalali: <c>CalendarSystemMath.Format</c>.</summary>
    internal static string Format(CalendarSystem s, DateTime d, string pattern, CultureInfo? culture)
    {
        var c = Culture(culture);
        return s == CalendarSystem.Jalali ? CalendarSystemMath.Format(s, D(d), pattern, c) : d.ToString(pattern, c);
    }

    internal static string Num(CalendarSystem s, int value, CultureInfo? culture, int minDigits = 1) =>
        CalendarSystemMath.FormatNumber(s, value, Culture(culture), minDigits);

    internal static string MonthName(CalendarSystem s, DateTime d, CultureInfo? culture, bool abbreviated) =>
        CalendarSystemMath.MonthName(s, Month(s, d), Culture(culture), abbreviated);

    internal static bool IsPersian(CultureInfo? culture) => Culture(culture).TwoLetterISOLanguageName == "fa";
}
