using System.Globalization;
using System.Text;

namespace Lumeo;

/// <summary>
/// The calendar system a <see cref="Calendar"/> / <see cref="DatePicker"/> lays its
/// months out in. The bound values are always plain <see cref="DateOnly"/> (Gregorian)
/// dates — only the month grid, headers, navigation and display text follow the
/// selected system.
/// </summary>
public enum CalendarSystem
{
    /// <summary>The Gregorian calendar (default).</summary>
    Gregorian,

    /// <summary>The Solar Hijri / Jalali (Persian) calendar, backed by
    /// <see cref="PersianCalendar"/>. Weeks start on Saturday unless
    /// <c>FirstDayOfWeek</c> is set explicitly.</summary>
    Jalali,
}

/// <summary>
/// Calendar-system aware date math and formatting shared by <see cref="Calendar"/>
/// and <see cref="DatePicker"/>. Gregorian goes straight through <see cref="DateOnly"/>
/// so the default path behaves exactly as before; Jalali converts through a
/// <see cref="PersianCalendar"/> (its members are read-only, so one shared instance is safe).
/// </summary>
internal static class CalendarSystemMath
{
    private static readonly PersianCalendar Persian = new();

    private static readonly DateOnly PersianMin = DateOnly.FromDateTime(Persian.MinSupportedDateTime);
    private static readonly DateOnly PersianMax = DateOnly.FromDateTime(Persian.MaxSupportedDateTime);

    private static readonly string[] JalaliMonthNamesFa =
    [
        "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند",
    ];

    private static readonly string[] JalaliMonthNamesLatin =
    [
        "Farvardin", "Ordibehesht", "Khordad", "Tir", "Mordad", "Shahrivar",
        "Mehr", "Aban", "Azar", "Dey", "Bahman", "Esfand",
    ];

    private static readonly string[] JalaliMonthNamesLatinAbbr =
    [
        "Far", "Ord", "Kho", "Tir", "Mor", "Sha",
        "Meh", "Aba", "Aza", "Dey", "Bah", "Esf",
    ];

    private static readonly int[] MonthsByNameLength = Enumerable.Range(1, 12)
        .OrderByDescending(m => Math.Max(JalaliMonthNamesFa[m - 1].Length, JalaliMonthNamesLatin[m - 1].Length))
        .ToArray();

    /// <summary>Default display/parse pattern for Jalali dates when no <c>Format</c> is given.</summary>
    public const string JalaliDefaultFormat = "yyyy/MM/dd";

    private static DateTime ToPersianDateTime(DateOnly date)
    {
        if (date < PersianMin) date = PersianMin;
        else if (date > PersianMax) date = PersianMax;
        return date.ToDateTime(TimeOnly.MinValue);
    }

    public static int GetYear(CalendarSystem system, DateOnly date) =>
        system == CalendarSystem.Jalali ? Persian.GetYear(ToPersianDateTime(date)) : date.Year;

    public static int GetMonth(CalendarSystem system, DateOnly date) =>
        system == CalendarSystem.Jalali ? Persian.GetMonth(ToPersianDateTime(date)) : date.Month;

    public static int GetDay(CalendarSystem system, DateOnly date) =>
        system == CalendarSystem.Jalali ? Persian.GetDayOfMonth(ToPersianDateTime(date)) : date.Day;

    public static int MinYear(CalendarSystem system) =>
        system == CalendarSystem.Jalali ? GetYear(system, PersianMin) : 1;

    public static int MaxYear(CalendarSystem system) =>
        system == CalendarSystem.Jalali ? GetYear(system, PersianMax) : 9999;

    /// <summary>Builds the first day of <paramref name="month"/>/<paramref name="year"/>,
    /// clamped into the range the calendar system supports.</summary>
    public static DateOnly MonthStart(CalendarSystem system, int year, int month)
    {
        if (system != CalendarSystem.Jalali)
            return new DateOnly(Math.Clamp(year, 1, 9999), Math.Clamp(month, 1, 12), 1);

        year = Math.Clamp(year, MinYear(system), MaxYear(system));
        month = Math.Clamp(month, 1, 12);
        if (year == MaxYear(system))
            month = Math.Min(month, GetMonth(system, PersianMax));
        return DateOnly.FromDateTime(Persian.ToDateTime(year, month, 1, 0, 0, 0, 0));
    }

    /// <summary>The first day of the month <paramref name="date"/> falls in.</summary>
    public static DateOnly MonthStart(CalendarSystem system, DateOnly date) =>
        system == CalendarSystem.Jalali
            ? MonthStart(system, GetYear(system, date), GetMonth(system, date))
            : new DateOnly(date.Year, date.Month, 1);

    /// <summary>The last day of the month <paramref name="date"/> falls in.</summary>
    public static DateOnly MonthEnd(CalendarSystem system, DateOnly date)
    {
        if (system != CalendarSystem.Jalali)
            return new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

        var start = MonthStart(system, date);
        var days = Persian.GetDaysInMonth(GetYear(system, start), GetMonth(system, start));
        var end = start.AddDays(days - 1);
        return end > PersianMax ? PersianMax : end;
    }

    /// <summary>Adds whole months in the calendar system, clamping the day of month
    /// (e.g. 31 Shahrivar + 1 month → 30 Mehr) like <see cref="DateOnly.AddMonths"/>.</summary>
    public static DateOnly AddMonths(CalendarSystem system, DateOnly date, int months)
    {
        if (system != CalendarSystem.Jalali)
            return date.AddMonths(months);

        try
        {
            return DateOnly.FromDateTime(Persian.AddMonths(ToPersianDateTime(date), months));
        }
        catch (ArgumentException)
        {
            return months < 0 ? PersianMin : PersianMax;
        }
    }

    public static bool SameMonth(CalendarSystem system, DateOnly a, DateOnly b) =>
        system == CalendarSystem.Jalali
            ? GetYear(system, a) == GetYear(system, b) && GetMonth(system, a) == GetMonth(system, b)
            : a.Year == b.Year && a.Month == b.Month;

    /// <summary>Jalali weeks conventionally start on Saturday; Gregorian follows the culture.</summary>
    public static DayOfWeek DefaultFirstDayOfWeek(CalendarSystem system, CultureInfo culture) =>
        system == CalendarSystem.Jalali ? DayOfWeek.Saturday : culture.DateTimeFormat.FirstDayOfWeek;

    private static bool IsPersianCulture(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName is "fa";

    public static string MonthName(CalendarSystem system, int month, CultureInfo culture, bool abbreviated = false)
    {
        if (system != CalendarSystem.Jalali)
        {
            return abbreviated
                ? culture.DateTimeFormat.AbbreviatedMonthNames[month - 1]
                : culture.DateTimeFormat.MonthNames[month - 1];
        }

        if (IsPersianCulture(culture)) return JalaliMonthNamesFa[month - 1];
        return abbreviated ? JalaliMonthNamesLatinAbbr[month - 1] : JalaliMonthNamesLatin[month - 1];
    }

    /// <summary>Formats a number for display. Jalali dates under a Persian (<c>fa</c>)
    /// culture use Persian digits (۰–۹); everything else uses ASCII digits.</summary>
    public static string FormatNumber(CalendarSystem system, int value, CultureInfo culture, int minDigits = 1)
    {
        var text = value.ToString("D" + minDigits, CultureInfo.InvariantCulture);
        if (system != CalendarSystem.Jalali || !IsPersianCulture(culture)) return text;

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
            sb.Append(ch is >= '0' and <= '9' ? (char)('۰' + (ch - '0')) : ch);
        return sb.ToString();
    }

    /// <summary>"Mehr 1403" / "مهر ۱۴۰۳" style month heading.</summary>
    public static string MonthTitle(CalendarSystem system, DateOnly date, CultureInfo culture)
    {
        if (system != CalendarSystem.Jalali)
            return date.ToString("MMMM yyyy", culture);

        return $"{MonthName(system, GetMonth(system, date), culture)} {FormatNumber(system, GetYear(system, date), culture)}";
    }

    /// <summary>Full, screen-reader friendly date ("Saturday, 12 Mehr 1403").</summary>
    public static string LongDate(CalendarSystem system, DateOnly date, CultureInfo culture)
    {
        if (system != CalendarSystem.Jalali)
            return date.ToDateTime(TimeOnly.MinValue).ToString("D", culture);

        return Format(system, date, "dddd, d MMMM yyyy", culture);
    }

    /// <summary>
    /// Formats <paramref name="date"/> with a .NET-style custom date pattern. For Gregorian
    /// this is plain <see cref="DateOnly.ToString(string, IFormatProvider)"/>. For Jalali the
    /// day/month/year tokens (<c>d dd ddd dddd M MM MMM MMMM y yy yyyy</c>) are resolved
    /// against the Persian calendar; quoted text and every other character is kept as-is.
    /// </summary>
    public static string Format(CalendarSystem system, DateOnly date, string pattern, CultureInfo culture)
    {
        if (system != CalendarSystem.Jalali)
            return date.ToString(pattern, culture);

        var year = GetYear(system, date);
        var month = GetMonth(system, date);
        var day = GetDay(system, date);
        var sb = new StringBuilder();

        for (var i = 0; i < pattern.Length; i++)
        {
            var ch = pattern[i];
            if (ch is '\'' or '"')
            {
                var close = pattern.IndexOf(ch, i + 1);
                if (close < 0) close = pattern.Length;
                sb.Append(pattern, i + 1, close - i - 1);
                i = close;
                continue;
            }
            if (ch == '\\' && i + 1 < pattern.Length)
            {
                sb.Append(pattern[++i]);
                continue;
            }
            if (ch is not ('y' or 'M' or 'd'))
            {
                sb.Append(ch);
                continue;
            }

            var run = 1;
            while (i + run < pattern.Length && pattern[i + run] == ch) run++;
            i += run - 1;

            switch (ch)
            {
                case 'y':
                    sb.Append(run <= 2
                        ? FormatNumber(system, year % 100, culture, run)
                        : FormatNumber(system, year, culture, run));
                    break;
                case 'M':
                    sb.Append(run switch
                    {
                        1 => FormatNumber(system, month, culture),
                        2 => FormatNumber(system, month, culture, 2),
                        3 => MonthName(system, month, culture, abbreviated: true),
                        _ => MonthName(system, month, culture),
                    });
                    break;
                default: // 'd'
                    var dow = (int)date.DayOfWeek;
                    sb.Append(run switch
                    {
                        1 => FormatNumber(system, day, culture),
                        2 => FormatNumber(system, day, culture, 2),
                        3 => culture.DateTimeFormat.AbbreviatedDayNames[dow],
                        _ => culture.DateTimeFormat.DayNames[dow],
                    });
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Parses user-typed Jalali text against <paramref name="pattern"/>. Accepts ASCII,
    /// Persian and Arabic-Indic digits and any separators; the order of the numeric
    /// year/month/day parts follows the pattern (default <c>yyyy/MM/dd</c>). A textual
    /// month (Persian or Latin transliteration) is recognised when the pattern uses
    /// <c>MMM</c>/<c>MMMM</c>.
    /// </summary>
    public static bool TryParseJalali(string text, string pattern, out DateOnly result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var order = TokenOrder(pattern, out var widths); // e.g. "yMd", widths {4,2,2}
        var ascii = ToAsciiDigits(text.Trim());
        var numbers = ExtractNumbers(ascii);

        // Separator-less patterns such as "yyyyMMdd" format to one digit run
        // ("14030712"): split it back by the pattern's token widths.
        if (numbers.Count <= 1 && order.Length == 3 && widths.Sum() == DigitCount(ascii))
        {
            var digits = new string(ascii.Where(c => c is >= '0' and <= '9').ToArray());
            numbers = new List<int>(3);
            var pos = 0;
            foreach (var w in widths)
            {
                numbers.Add(int.Parse(digits.AsSpan(pos, w), NumberStyles.None, CultureInfo.InvariantCulture));
                pos += w;
            }
        }

        int year, month, day;
        var textualMonth = FindMonthName(ascii);

        if (textualMonth > 0 && numbers.Count == 2)
        {
            var numericOrder = order.Replace("M", "");
            if (numericOrder.Length != 2) numericOrder = "dy";
            year = numbers[numericOrder.IndexOf('y')];
            day = numbers[numericOrder.IndexOf('d')];
            month = textualMonth;
        }
        else if (numbers.Count == 3)
        {
            if (order.Length != 3) order = "yMd";
            year = numbers[order.IndexOf('y')];
            month = numbers[order.IndexOf('M')];
            day = numbers[order.IndexOf('d')];
        }
        else
        {
            return false;
        }

        // Two-digit years resolve through the calendar's TwoDigitYearMax window
        // (99 → 1399, 03 → 1403) so a "yy" value round-trips unchanged.
        if (year is >= 0 and < 100) year = Persian.ToFourDigitYear(year);
        return TryCreate(year, month, day, out result);
    }

    /// <summary>Parses "Mehr 1403", "مهر ۱۴۰۳", "1403/07" or "07/1403" to the first of that Jalali month.</summary>
    public static bool TryParseJalaliMonth(string text, out DateOnly result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var ascii = ToAsciiDigits(text.Trim());
        var numbers = ExtractNumbers(ascii);
        var textualMonth = FindMonthName(ascii);

        int year, month;
        if (textualMonth > 0 && numbers.Count == 1)
        {
            year = numbers[0];
            month = textualMonth;
        }
        else if (numbers.Count == 2)
        {
            // The year is the wider part (1403/07 or 07/1403).
            (year, month) = numbers[0] > 12 ? (numbers[0], numbers[1]) : (numbers[1], numbers[0]);
        }
        else
        {
            return false;
        }

        return TryCreate(year, month, 1, out result);
    }

    /// <summary>Parses a Jalali year number to the first day of that year.</summary>
    public static bool TryParseJalaliYear(string text, out DateOnly result)
    {
        result = default;
        var numbers = ExtractNumbers(ToAsciiDigits(text.Trim()));
        return numbers.Count == 1 && TryCreate(numbers[0], 1, 1, out result);
    }

    private static bool TryCreate(int year, int month, int day, out DateOnly result)
    {
        result = default;
        if (year < 1 || month is < 1 or > 12 || day < 1) return false;
        try
        {
            if (day > Persian.GetDaysInMonth(year, month)) return false;
            result = DateOnly.FromDateTime(Persian.ToDateTime(year, month, day, 0, 0, 0, 0));
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static int DigitCount(string text) => text.Count(c => c is >= '0' and <= '9');

    /// <summary>The order of the numeric y/M/d tokens in <paramref name="pattern"/> and the
    /// digit width each takes when the parts are written without separators.</summary>
    private static string TokenOrder(string pattern, out List<int> widths)
    {
        widths = new List<int>(3);
        var sb = new StringBuilder(3);
        var inQuote = '\0';
        for (var i = 0; i < pattern.Length; i++)
        {
            var ch = pattern[i];
            if (inQuote != '\0') { if (ch == inQuote) inQuote = '\0'; continue; }
            if (ch is '\'' or '"') { inQuote = ch; continue; }
            if (ch == '\\') { i++; continue; }
            if (ch is 'y' or 'M' or 'd')
            {
                var run = 1;
                while (i + run < pattern.Length && pattern[i + run] == ch) run++;
                i += run - 1;
                // ddd/dddd are weekday names, not the day number.
                if (ch == 'd' && run >= 3) continue;
                if (sb.ToString().IndexOf(ch) < 0)
                {
                    sb.Append(ch);
                    widths.Add(ch == 'y' ? (run <= 2 ? 2 : 4) : 2);
                }
            }
        }
        return sb.ToString();
    }

    private static int FindMonthName(string text)
    {
        // Longest names first: "دی" (Dey) is a substring of "اردیبهشت" (Ordibehesht).
        foreach (var m in MonthsByNameLength)
        {
            if (text.Contains(JalaliMonthNamesFa[m - 1], StringComparison.Ordinal)
                || text.Contains(JalaliMonthNamesLatin[m - 1], StringComparison.OrdinalIgnoreCase))
                return m;
        }
        // Abbreviations last so "Tir"/"Dey" don't shadow longer names.
        for (var m = 1; m <= 12; m++)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(text, $@"\b{JalaliMonthNamesLatinAbbr[m - 1]}\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return m;
        }
        return 0;
    }

    private static List<int> ExtractNumbers(string ascii)
    {
        var result = new List<int>(3);
        var i = 0;
        while (i < ascii.Length)
        {
            if (ascii[i] is >= '0' and <= '9')
            {
                var start = i;
                while (i < ascii.Length && ascii[i] is >= '0' and <= '9') i++;
                if (i - start > 6) return [];
                result.Add(int.Parse(ascii.AsSpan(start, i - start), NumberStyles.None, CultureInfo.InvariantCulture));
            }
            else
            {
                i++;
            }
        }
        return result;
    }

    /// <summary>Maps Persian (۰–۹), Arabic-Indic (٠–٩) and any other Unicode decimal digits to ASCII.</summary>
    internal static string ToAsciiDigits(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            var value = CharUnicodeInfo.GetDecimalDigitValue(ch);
            sb.Append(value >= 0 ? (char)('0' + value) : ch);
        }
        return sb.ToString();
    }
}
