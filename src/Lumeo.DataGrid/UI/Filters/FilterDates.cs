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
/// name, or an explicit date in the current culture.</summary>
public static class FilterDates
{
    private static readonly string[] Units = { "day", "week", "month", "year" };

    /// <summary>Parses free text into a date value; null when nothing matches.</summary>
    public static FilterDateValue? Parse(string text, DateOnly? today = null, CultureInfo? culture = null)
    {
        var now = today ?? DateOnly.FromDateTime(DateTime.Today);
        culture ??= CultureInfo.CurrentCulture;
        var input = Regex.Replace(text.Trim().ToLowerInvariant(), @"\s+", " ");
        if (input.Length == 0) return null;
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
