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

        pattern = ExpandStandardFormat(pattern);
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

    /// <summary>Maps the single-letter .NET standard date formats (<c>d D M m Y y</c>) to
    /// equivalent custom patterns for the Jalali formatter; anything else passes through.</summary>
    private static string ExpandStandardFormat(string pattern) => pattern switch
    {
        "d" => JalaliDefaultFormat,
        "D" => "dddd, d MMMM yyyy",
        "M" or "m" => "d MMMM",
        "Y" or "y" => "MMMM yyyy",
        _ => pattern,
    };

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

        pattern = ExpandStandardFormat(pattern);

        // First pass: walk the pattern token by token, so the picker's own output
        // ("yyyyMd", "yyyyMM/dd", "yyyy/MM/dd '14'", "MMMM yyyy", ...) always parses back.
        if (TryParsePositional(ToAsciiDigits(text.Trim()), pattern, out result)) return true;

        // Second pass: lenient extraction for hand-typed variations ("1403-7-12", "۱۴۰۳/۷/۱۲").
        var order = TokenOrder(pattern, out var widths, out var literals); // e.g. "yMd", widths {4,2,2}
        var ascii = ToAsciiDigits(text.Trim());
        // Quoted pattern literals ("'v2' yyyy/MM/dd") may contain digits: drop them first.
        foreach (var literal in literals)
        {
            var lit = ToAsciiDigits(literal);
            var at = lit.Length > 0 ? ascii.IndexOf(lit, StringComparison.OrdinalIgnoreCase) : -1;
            if (at >= 0) ascii = ascii.Remove(at, lit.Length).Insert(at, " ");
        }
        var runs = ExtractNumberRuns(ascii);
        var numbers = runs.Select(x => x.Value).ToList();
        var lengths = runs.Select(x => x.Length).ToList();

        // Separator-less patterns such as "yyyyMMdd" format to one digit run
        // ("14030712"): split it back by the pattern's token widths.
        if (numbers.Count <= 1 && order.Length == 3 && widths.Sum() == DigitCount(ascii))
        {
            var digits = new string(ascii.Where(c => c is >= '0' and <= '9').ToArray());
            numbers = new List<int>(3);
            lengths = new List<int>(widths);
            var pos = 0;
            foreach (var w in widths)
            {
                numbers.Add(int.Parse(digits.AsSpan(pos, w), NumberStyles.None, CultureInfo.InvariantCulture));
                pos += w;
            }
        }

        int year, month, day, yearLength;
        var textualMonth = FindMonthName(ascii);

        if (textualMonth > 0 && numbers.Count == 2)
        {
            var numericOrder = order.Replace("M", "");
            if (numericOrder.Length != 2) numericOrder = "dy";
            year = numbers[numericOrder.IndexOf('y')];
            yearLength = lengths[numericOrder.IndexOf('y')];
            day = numbers[numericOrder.IndexOf('d')];
            month = textualMonth;
        }
        else if (numbers.Count == 3)
        {
            if (order.Length != 3) order = "yMd";
            year = numbers[order.IndexOf('y')];
            yearLength = lengths[order.IndexOf('y')];
            month = numbers[order.IndexOf('M')];
            day = numbers[order.IndexOf('d')];
        }
        else
        {
            return false;
        }

        // Only a year typed with at most two digits is abbreviated: it resolves through the
        // calendar's TwoDigitYearMax window (99 → 1399, 03 → 1403). "0001" stays year 1.
        if (yearLength <= 2) year = Persian.ToFourDigitYear(year);
        return TryCreate(year, month, day, out result);
    }

    private enum PatternTokenKind { Literal, Year, Month, MonthName, Day, DayName }

    private readonly record struct PatternToken(PatternTokenKind Kind, int Width, string Text);

    private static List<PatternToken> Tokenize(string pattern)
    {
        var tokens = new List<PatternToken>();
        for (var i = 0; i < pattern.Length; i++)
        {
            var ch = pattern[i];
            if (ch is '\'' or '"')
            {
                var close = pattern.IndexOf(ch, i + 1);
                if (close < 0) close = pattern.Length;
                tokens.Add(new(PatternTokenKind.Literal, 0, pattern.Substring(i + 1, close - i - 1)));
                i = close;
                continue;
            }
            if (ch == '\\' && i + 1 < pattern.Length)
            {
                tokens.Add(new(PatternTokenKind.Literal, 0, pattern[++i].ToString()));
                continue;
            }
            if (ch is not ('y' or 'M' or 'd'))
            {
                tokens.Add(new(PatternTokenKind.Literal, 0, ch.ToString()));
                continue;
            }
            var run = 1;
            while (i + run < pattern.Length && pattern[i + run] == ch) run++;
            i += run - 1;
            tokens.Add(ch switch
            {
                'y' => new(PatternTokenKind.Year, run, ""),
                'M' => new(run >= 3 ? PatternTokenKind.MonthName : PatternTokenKind.Month, run, ""),
                _ => new(run >= 3 ? PatternTokenKind.DayName : PatternTokenKind.Day, run, ""),
            });
        }
        return tokens;
    }

    private static bool IsNumeric(PatternTokenKind kind) =>
        kind is PatternTokenKind.Year or PatternTokenKind.Month or PatternTokenKind.Day;

    /// <summary>
    /// Matches <paramref name="ascii"/> against the pattern position by position. Literals
    /// must appear where the pattern puts them; a numeric field directly followed by another
    /// numeric field takes its pattern width (1–2 digits for single-letter <c>M</c>/<c>d</c>),
    /// otherwise the whole digit run. Every valid split is tried and the parse succeeds only
    /// when exactly one distinct date results, so an ambiguous split fails instead of
    /// guessing. Fields the pattern omits default to day 1 / the current Jalali year.
    /// </summary>
    private static bool TryParsePositional(string ascii, string pattern, out DateOnly result)
    {
        result = default;
        var tokens = Tokenize(pattern);
        if (!tokens.Any(t => t.Kind is PatternTokenKind.Year or PatternTokenKind.Month
                or PatternTokenKind.MonthName or PatternTokenKind.Day))
            return false;

        var found = new HashSet<DateOnly>();
        Walk(0, 0, null, 0, null, null);
        if (found.Count != 1) return false;
        result = found.First();
        return true;

        void Walk(int ti, int pos, int? year, int yearLen, int? month, int? day)
        {
            if (found.Count > 1) return;
            if (ti == tokens.Count)
            {
                if (!ascii.AsSpan(pos).IsWhiteSpace()) return;
                var y = year ?? GetYear(CalendarSystem.Jalali, DateOnly.FromDateTime(DateTime.Today));
                if (year.HasValue && yearLen <= 2) y = Persian.ToFourDigitYear(y);
                if (TryCreate(y, month ?? 1, day ?? 1, out var d)) found.Add(d);
                return;
            }

            var t = tokens[ti];
            switch (t.Kind)
            {
                case PatternTokenKind.Literal:
                {
                    var lit = ToAsciiDigits(t.Text);
                    if (string.IsNullOrWhiteSpace(lit))
                    {
                        // Whitespace in the pattern: accept any amount, including none.
                        var p = pos;
                        while (p < ascii.Length && char.IsWhiteSpace(ascii[p])) p++;
                        Walk(ti + 1, p, year, yearLen, month, day);
                    }
                    else if (pos + lit.Length <= ascii.Length
                             && string.Compare(ascii, pos, lit, 0, lit.Length, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        Walk(ti + 1, pos + lit.Length, year, yearLen, month, day);
                    }
                    return;
                }
                case PatternTokenKind.MonthName:
                {
                    foreach (var m in MonthsByNameLength)
                    {
                        foreach (var name in new[] { JalaliMonthNamesFa[m - 1], JalaliMonthNamesLatin[m - 1], JalaliMonthNamesLatinAbbr[m - 1] })
                        {
                            if (pos + name.Length <= ascii.Length
                                && string.Compare(ascii, pos, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) == 0)
                                Walk(ti + 1, pos + name.Length, year, yearLen, m, day);
                        }
                    }
                    return;
                }
                case PatternTokenKind.DayName:
                {
                    // Weekday names carry no date information: skip the word.
                    var p = pos;
                    while (p < ascii.Length && char.IsLetter(ascii[p])) p++;
                    if (p > pos) Walk(ti + 1, p, year, yearLen, month, day);
                    return;
                }
                default:
                {
                    var runEnd = pos;
                    while (runEnd < ascii.Length && ascii[runEnd] is >= '0' and <= '9') runEnd++;
                    var available = runEnd - pos;
                    if (available == 0) return;

                    var adjacentNumeric = ti + 1 < tokens.Count && IsNumeric(tokens[ti + 1].Kind);
                    int[] widths = !adjacentNumeric
                        ? [available]
                        : t.Kind == PatternTokenKind.Year
                            ? [t.Width <= 2 ? 2 : 4]
                            : t.Width == 1 ? [1, 2] : [t.Width];

                    foreach (var w in widths)
                    {
                        if (w > available || w > 6) continue;
                        var value = int.Parse(ascii.AsSpan(pos, w), NumberStyles.None, CultureInfo.InvariantCulture);
                        switch (t.Kind)
                        {
                            case PatternTokenKind.Year:
                                Walk(ti + 1, pos + w, value, w, month, day);
                                break;
                            case PatternTokenKind.Month:
                                if (value is >= 1 and <= 12) Walk(ti + 1, pos + w, year, yearLen, value, day);
                                break;
                            default:
                                if (value is >= 1 and <= 31) Walk(ti + 1, pos + w, year, yearLen, month, value);
                                break;
                        }
                    }
                    return;
                }
            }
        }
    }

    /// <summary>Parses "Mehr 1403", "مهر ۱۴۰۳", "1403/07" or "07/1403" to the first of that Jalali month.</summary>
    public static bool TryParseJalaliMonth(string text, out DateOnly result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var ascii = ToAsciiDigits(text.Trim());
        var runs = ExtractNumberRuns(ascii);
        var numbers = runs.Select(x => x.Value).ToList();
        var textualMonth = FindMonthName(ascii);

        int year, month;
        if (textualMonth > 0 && numbers.Count == 1)
        {
            year = numbers[0];
            month = textualMonth;
        }
        else if (numbers.Count == 2)
        {
            // The year is the wider part (1403/07, 07/1403, 0001/07).
            var firstIsYear = runs[0].Length != runs[1].Length
                ? runs[0].Length > runs[1].Length
                : numbers[0] > 12;
            (year, month) = firstIsYear ? (numbers[0], numbers[1]) : (numbers[1], numbers[0]);
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
    private static string TokenOrder(string pattern, out List<int> widths, out List<string> literals)
    {
        widths = new List<int>(3);
        literals = new List<string>();
        var sb = new StringBuilder(3);
        for (var i = 0; i < pattern.Length; i++)
        {
            var ch = pattern[i];
            if (ch is '\'' or '"')
            {
                var close = pattern.IndexOf(ch, i + 1);
                if (close < 0) close = pattern.Length;
                literals.Add(pattern.Substring(i + 1, close - i - 1));
                i = close;
                continue;
            }
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

    private static List<int> ExtractNumbers(string ascii) =>
        ExtractNumberRuns(ascii).Select(x => x.Value).ToList();

    /// <summary>Each run of ASCII digits with its value and its digit count.</summary>
    private static List<(int Value, int Length)> ExtractNumberRuns(string ascii)
    {
        var result = new List<(int Value, int Length)>(3);
        var i = 0;
        while (i < ascii.Length)
        {
            if (ascii[i] is >= '0' and <= '9')
            {
                var start = i;
                while (i < ascii.Length && ascii[i] is >= '0' and <= '9') i++;
                if (i - start > 6) return [];
                result.Add((int.Parse(ascii.AsSpan(start, i - start), NumberStyles.None, CultureInfo.InvariantCulture), i - start));
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
