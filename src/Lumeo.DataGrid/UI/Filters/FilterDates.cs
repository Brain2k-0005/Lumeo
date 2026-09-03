using System.Globalization;
using System.Text.RegularExpressions;

namespace Lumeo;

/// <summary>A date rule value: a fixed day, or a day relative to today (<c>next week</c>) that
/// resolves when the filter runs.</summary>
public sealed record FilterDateValue(DateOnly? Date = null, FilterRelativeDate? Relative = null, string? Time = null)
{
    public static FilterDateValue Of(DateOnly date, string? time = null) => new(date, null, time);

    /// <summary>The day this value stands for today, or null when it holds nothing.</summary>
    public DateOnly? Resolve(DateOnly? today = null)
    {
        if (Relative is { } r) return r.Apply(today ?? DateOnly.FromDateTime(DateTime.Today));
        return Date;
    }

    public override string ToString() => Relative is { } r ? r.ToString() : Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
}

/// <summary>A day relative to today: <c>Unit</c> days, weeks, months or years ahead (positive) or back.</summary>
public sealed record FilterRelativeDate(FilterDateUnit Unit, int Offset)
{
    public static readonly FilterRelativeDate Today = new(FilterDateUnit.Day, 0);
    public static readonly FilterRelativeDate Tomorrow = new(FilterDateUnit.Day, 1);
    public static readonly FilterRelativeDate Yesterday = new(FilterDateUnit.Day, -1);

    public DateOnly Apply(DateOnly today) => Unit switch
    {
        FilterDateUnit.Day => today.AddDays(Offset),
        FilterDateUnit.Week => today.AddDays(Offset * 7),
        FilterDateUnit.Month => today.AddMonths(Offset),
        _ => today.AddYears(Offset),
    };

    public override string ToString() => $"{(Offset >= 0 ? "+" : "")}{Offset} {Unit.ToString().ToLowerInvariant()}";
}

public enum FilterDateUnit { Day, Week, Month, Year }

/// <summary>Parses and formats date rule values: "today", "next week", "3 days ago", a weekday
/// name, or an explicit date in the current culture. With labels, the phrases the labels produce
/// (<c>heute</c>, <c>in 2 Wochen</c>, <c>vor 3 Tagen</c>) parse as well.</summary>
public static class FilterDates
{
    private static readonly string[] Units = { "day", "week", "month", "year" };

    /// <summary>Parses free text into a date value; null when nothing matches. The English phrases
    /// always work; <paramref name="labels"/> adds the ones the editor shows in its language.</summary>
    public static FilterDateValue? Parse(string text, DateOnly? today = null, CultureInfo? culture = null, FilterLabels? labels = null)
    {
        var now = today ?? DateOnly.FromDateTime(DateTime.Today);
        culture ??= CultureInfo.CurrentCulture;
        var input = Regex.Replace(text.Trim().ToLowerInvariant(), @"\s+", " ");
        if (input.Length == 0) return null;
        if (labels is not null && ParseLocalized(input, labels) is { } localized) return localized;
        if (input is "today" or "now") return new FilterDateValue(Relative: FilterRelativeDate.Today);
        if (input == "tomorrow") return new FilterDateValue(Relative: FilterRelativeDate.Tomorrow);
        if (input == "yesterday") return new FilterDateValue(Relative: FilterRelativeDate.Yesterday);

        var nextLast = Regex.Match(input, @"^(next|last|this) (day|week|month|year)$");
        if (nextLast.Success)
        {
            var direction = nextLast.Groups[1].Value switch { "next" => 1, "last" => -1, _ => 0 };
            return new FilterDateValue(Relative: new FilterRelativeDate(UnitOf(nextLast.Groups[2].Value), direction));
        }

        var counted = Regex.Match(input, @"^(?:in )?(\d+) (day|week|month|year)s?(?: ago)?$");
        if (counted.Success)
        {
            var magnitude = int.Parse(counted.Groups[1].Value, CultureInfo.InvariantCulture);
            var past = input.EndsWith("ago", StringComparison.Ordinal);
            return new FilterDateValue(Relative: new FilterRelativeDate(UnitOf(counted.Groups[2].Value), past ? -magnitude : magnitude));
        }

        var weekday = Regex.Match(input, @"^(?:(next|last|this) )?([a-z]+)$");
        if (weekday.Success)
        {
            var names = culture.DateTimeFormat.DayNames.Select(n => n.ToLowerInvariant()).ToList();
            var english = CultureInfo.InvariantCulture.DateTimeFormat.DayNames.Select(n => n.ToLowerInvariant()).ToList();
            var index = names.IndexOf(weekday.Groups[2].Value);
            if (index < 0) index = english.IndexOf(weekday.Groups[2].Value);
            if (index >= 0)
            {
                var delta = index - (int)now.DayOfWeek;
                if (weekday.Groups[1].Value == "last") { if (delta >= 0) delta -= 7; }
                else if (delta <= 0) delta += 7;
                return new FilterDateValue(Relative: new FilterRelativeDate(FilterDateUnit.Day, delta));
            }
        }

        if (DateOnly.TryParse(text.Trim(), culture, DateTimeStyles.None, out var parsed)
            || DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            return FilterDateValue.Of(parsed);
        return null;
    }

    private static FilterDateUnit UnitOf(string word) => (FilterDateUnit)Array.IndexOf(Units, word);

    private static string Normalize(string s) => Regex.Replace(s.Trim().ToLowerInvariant(), @"\s+", " ");

    /// <summary>The phrases <see cref="Format"/> produces from the labels, read back: today,
    /// tomorrow, yesterday, and the "in", "ago" and "this" formats with the labels' unit words.</summary>
    private static FilterDateValue? ParseLocalized(string input, FilterLabels labels)
    {
        if (input == Normalize(labels.DateToday)) return new FilterDateValue(Relative: FilterRelativeDate.Today);
        if (input == Normalize(labels.DateTomorrow)) return new FilterDateValue(Relative: FilterRelativeDate.Tomorrow);
        if (input == Normalize(labels.DateYesterday)) return new FilterDateValue(Relative: FilterRelativeDate.Yesterday);

        var units = new (FilterDateUnit Unit, string Word)[]
            {
                (FilterDateUnit.Day, labels.DateDay), (FilterDateUnit.Day, labels.DateDays),
                (FilterDateUnit.Week, labels.DateWeek), (FilterDateUnit.Week, labels.DateWeeks),
                (FilterDateUnit.Month, labels.DateMonth), (FilterDateUnit.Month, labels.DateMonths),
                (FilterDateUnit.Year, labels.DateYear), (FilterDateUnit.Year, labels.DateYears),
            }
            .Select(u => (u.Unit, Word: Normalize(u.Word)))
            .Where(u => u.Word.Length > 0)
            .OrderByDescending(u => u.Word.Length)
            .ToList();
        if (units.Count == 0) return null;
        var unitPattern = "(?<u>" + string.Join("|", units.Select(u => Regex.Escape(u.Word))) + ")";
        FilterDateUnit? UnitOfWord(string word) => units.Where(u => u.Word == word).Select(u => (FilterDateUnit?)u.Unit).FirstOrDefault();

        string Pattern(string format, bool counted)
        {
            var escaped = Regex.Escape(Normalize(format));
            escaped = counted
                ? escaped.Replace(Regex.Escape("{0}"), @"(?<n>\d+)").Replace(Regex.Escape("{1}"), unitPattern)
                : escaped.Replace(Regex.Escape("{0}"), unitPattern);
            return "^" + escaped + "$";
        }

        foreach (var (format, sign) in new[] { (labels.DateInFormat, 1), (labels.DateAgoFormat, -1) })
        {
            if (string.IsNullOrWhiteSpace(format) || !format.Contains("{0}") || !format.Contains("{1}")) continue;
            var m = Regex.Match(input, Pattern(format, counted: true));
            if (!m.Success || UnitOfWord(m.Groups["u"].Value) is not { } unit) continue;
            if (!int.TryParse(m.Groups["n"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) continue;
            return new FilterDateValue(Relative: new FilterRelativeDate(unit, sign * n));
        }
        if (!string.IsNullOrWhiteSpace(labels.DateThisFormat) && labels.DateThisFormat.Contains("{0}"))
        {
            var m = Regex.Match(input, Pattern(labels.DateThisFormat, counted: false));
            if (m.Success && UnitOfWord(m.Groups["u"].Value) is { } unit)
                return new FilterDateValue(Relative: new FilterRelativeDate(unit, 0));
        }
        return null;
    }

    /// <summary>The value as text: a relative phrase, or the resolved day in the culture's short pattern.</summary>
    public static string Format(FilterDateValue? value, FilterLabels labels, DateOnly? today = null, CultureInfo? culture = null)
    {
        if (value is null) return "";
        culture ??= CultureInfo.CurrentCulture;
        if (value.Relative is { } r)
        {
            if (r.Unit == FilterDateUnit.Day && r.Offset == 0) return labels.DateToday;
            if (r.Unit == FilterDateUnit.Day && r.Offset == 1) return labels.DateTomorrow;
            if (r.Unit == FilterDateUnit.Day && r.Offset == -1) return labels.DateYesterday;
            if (r.Offset > 0) return labels.DateIn(r.Offset, r.Unit);
            if (r.Offset < 0) return labels.DateAgo(-r.Offset, r.Unit);
            return labels.DateThis(r.Unit);
        }
        var resolved = value.Resolve(today);
        if (resolved is null) return "";
        var day = resolved.Value.ToString("d", culture);
        return value.Time is { Length: > 0 } t ? $"{day} {t}" : day;
    }

    /// <summary>Whatever a rule holds for a date field, as a <see cref="FilterDateValue"/>.</summary>
    public static FilterDateValue? Coerce(object? value) => value switch
    {
        null => null,
        FilterDateValue d => d,
        DateOnly d => FilterDateValue.Of(d),
        DateTime dt => FilterDateValue.Of(DateOnly.FromDateTime(dt)),
        DateTimeOffset dto => FilterDateValue.Of(DateOnly.FromDateTime(dto.DateTime)),
        string s => Parse(s),
        _ => null,
    };
}
