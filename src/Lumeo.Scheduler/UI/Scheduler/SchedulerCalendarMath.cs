using System.Globalization;
using System.Text;

namespace Lumeo.SchedulerKernel;

/// <summary>
/// <see cref="DateTime"/>-typed adapter over the core library's <c>CalendarSystemMath</c> for the
/// Scheduler's grid boundaries, titles and labels. Event data is always Gregorian
/// <see cref="DateTime"/>; only the month/day structure and the displayed text follow the
/// <see cref="CalendarSystem"/>. Every Gregorian branch reproduces the pre-existing
/// <see cref="DateTime"/> expression exactly, so the default path is unchanged.
/// </summary>
internal static class SchedulerCalendarMath
{
    private static DateOnly D(DateTime dt) => DateOnly.FromDateTime(dt);

    /// <summary>First day (midnight) of the calendar-system month containing <paramref name="date"/>.</summary>
    internal static DateTime StartOfMonth(CalendarSystem system, DateTime date) =>
        system == CalendarSystem.Gregorian
            ? new DateTime(date.Year, date.Month, 1)
            : CalendarSystemMath.MonthStart(system, D(date)).ToDateTime(TimeOnly.MinValue);

    /// <summary>Adds whole calendar-system months (day of month clamped).</summary>
    internal static DateTime AddMonths(CalendarSystem system, DateTime date, int months)
    {
        if (system == CalendarSystem.Gregorian) return date.AddMonths(months);
        return CalendarSystemMath.AddMonths(system, D(date), months).ToDateTime(TimeOnly.FromTimeSpan(date.TimeOfDay));
    }

    internal static bool SameMonth(CalendarSystem system, DateTime a, DateTime b) =>
        system == CalendarSystem.Gregorian
            ? a.Month == b.Month && a.Year == b.Year
            : CalendarSystemMath.SameMonth(system, D(a), D(b));

    internal static int DayOfMonth(CalendarSystem system, DateTime date) =>
        system == CalendarSystem.Gregorian ? date.Day : CalendarSystemMath.GetDay(system, D(date));

    internal static int DaysInMonth(CalendarSystem system, DateTime date) =>
        system == CalendarSystem.Gregorian
            ? DateTime.DaysInMonth(date.Year, date.Month)
            : CalendarSystemMath.MonthEnd(system, D(date)).DayNumber - CalendarSystemMath.MonthStart(system, D(date)).DayNumber + 1;

    /// <summary>Whole calendar-system months from <paramref name="origin"/>'s month to <paramref name="date"/>'s.</summary>
    internal static int MonthsBetween(CalendarSystem system, DateTime origin, DateTime date) =>
        system == CalendarSystem.Gregorian
            ? (date.Year - origin.Year) * 12 + (date.Month - origin.Month)
            : (CalendarSystemMath.GetYear(system, D(date)) - CalendarSystemMath.GetYear(system, D(origin))) * 12
              + (CalendarSystemMath.GetMonth(system, D(date)) - CalendarSystemMath.GetMonth(system, D(origin)));

    /// <summary>The effective first day of week: an explicit value wins, otherwise Saturday for Jalali.</summary>
    internal static int ResolveFirstDayOfWeek(CalendarSystem system, int configured, bool explicitlySet, CultureInfo culture) =>
        system == CalendarSystem.Jalali && !explicitlySet
            ? (int)CalendarSystemMath.DefaultFirstDayOfWeek(system, culture)
            : configured;

    /// <summary>Digits localized for the calendar system and culture (Persian digits for Jalali under <c>fa</c>).</summary>
    internal static string Digits(CalendarSystem system, string text, CultureInfo culture)
    {
        if (system != CalendarSystem.Jalali || string.IsNullOrEmpty(text)) return text;
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
            sb.Append(ch is >= '0' and <= '9' ? CalendarSystemMath.FormatNumber(system, ch - '0', culture) : ch.ToString());
        return sb.ToString();
    }

    /// <summary>
    /// The culture a time-of-day is rendered with. Gregorian keeps the ambient
    /// <see cref="CultureInfo.CurrentCulture"/> the pre-existing <c>{value:t}</c> interpolations read;
    /// Jalali uses the UI culture like every other label in the calendar-system path.
    /// </summary>
    internal static CultureInfo TimeCulture(CalendarSystem system) =>
        system == CalendarSystem.Gregorian ? CultureInfo.CurrentCulture : CultureInfo.CurrentUICulture;

    /// <summary>A clock time using the culture's own time pattern and AM/PM designators.</summary>
    internal static string Time(CalendarSystem system, DateTime value, CultureInfo culture, string format = "t") =>
        Digits(system, value.ToString(format, culture), culture);

    /// <summary>Full date, e.g. "Saturday, 12 Mehr 1403" / the culture's "D" pattern.</summary>
    internal static string LongDate(CalendarSystem system, DateTime value, CultureInfo culture) =>
        system == CalendarSystem.Gregorian ? value.ToString("D", culture) : CalendarSystemMath.LongDate(system, D(value), culture);

    internal static string Format(CalendarSystem system, DateTime value, string pattern, CultureInfo culture) =>
        system == CalendarSystem.Gregorian ? value.ToString(pattern, culture) : CalendarSystemMath.Format(system, D(value), pattern, culture);

    internal static string MonthTitle(CalendarSystem system, DateTime value, CultureInfo culture) =>
        system == CalendarSystem.Gregorian ? value.ToString("MMMM yyyy", culture) : CalendarSystemMath.MonthTitle(system, D(value), culture);

    /// <summary>Abbreviated month + day, optionally with the year. Gregorian keeps the culture-ordered output.</summary>
    internal static string MonthDay(CalendarSystem system, DateTime value, CultureInfo culture, bool includeYear) =>
        system == CalendarSystem.Gregorian
            ? SchedulerTitleFormatter.FormatMonthDay(value, culture, includeYear)
            : CalendarSystemMath.Format(system, D(value), includeYear ? "d MMM yyyy" : "d MMM", culture);

    internal static string WeekdayMonthDay(CalendarSystem system, DateTime value, CultureInfo culture) =>
        system == CalendarSystem.Gregorian
            ? SchedulerTitleFormatter.FormatWeekdayMonthDay(value, culture)
            : CalendarSystemMath.Format(system, D(value), "dddd, d MMM yyyy", culture);
}
