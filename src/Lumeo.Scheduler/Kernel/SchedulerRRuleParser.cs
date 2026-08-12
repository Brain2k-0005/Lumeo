using System.Globalization;

namespace Lumeo.SchedulerKernel;

/// <summary>
/// Parses an RFC 5545 <c>RRULE</c> string into a <see cref="SchedulerRecurrenceRule"/>.
///
/// <para>
/// ReUI parity — its event calendar accepts raw RRULE strings, which is what anyone holding
/// iCalendar/Google/Outlook data actually has. Lumeo modelled recurrence only as a structured
/// record, so such a consumer had to hand-write a parser before they could pass their own data
/// in at all.
/// </para>
///
/// <para>
/// <b>Scope is deliberately the same subset the expander already implements</b>
/// (<see cref="SchedulerRecurrenceExpander"/>): <c>FREQ</c> of DAILY/WEEKLY/MONTHLY, plus
/// <c>INTERVAL</c>, <c>COUNT</c>, <c>UNTIL</c> and <c>BYDAY</c> (with RFC 5545 ordinals such as
/// <c>2MO</c> / <c>-1FR</c>). Anything outside that — <c>FREQ=YEARLY</c>, <c>BYMONTHDAY</c>,
/// <c>BYSETPOS</c>, <c>WKST</c>, … — is <b>rejected</b>, never silently dropped. Accepting a
/// rule and then quietly ignoring half of it is the worst possible outcome here: the caller
/// gets a series that looks plausible and is wrong, on their real calendar data. A rejected
/// parse is a bug report; a silently narrowed rule is a support ticket six months later.
/// </para>
/// </summary>
public static class SchedulerRRuleParser
{
    /// <summary>
    /// Attempts to parse <paramref name="rrule"/>. Returns <c>false</c> (with
    /// <paramref name="rule"/> set to <c>null</c>) for anything malformed or outside the
    /// supported subset — see the type's own remarks for why unsupported parts are a failure
    /// rather than a silent omission.
    /// </summary>
    /// <param name="rrule">
    /// The rule text, with or without the <c>RRULE:</c> prefix an iCalendar line carries
    /// (<c>"RRULE:FREQ=WEEKLY;BYDAY=MO,WE"</c> and <c>"FREQ=WEEKLY;BYDAY=MO,WE"</c> both parse).
    /// Part names and values are treated case-insensitively, matching RFC 5545, which defines
    /// them as case-insensitive tokens.
    /// </param>
    /// <param name="rule">The parsed rule, or <c>null</c> when this returns <c>false</c>.</param>
    public static bool TryParse(string? rrule, out SchedulerRecurrenceRule? rule)
    {
        rule = null;
        if (string.IsNullOrWhiteSpace(rrule)) return false;

        var text = rrule!.Trim();
        if (text.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase))
            text = text["RRULE:".Length..];

        SchedulerRecurrenceFrequency? freq = null;
        var interval = 1;
        int? count = null;
        DateTime? until = null;
        List<SchedulerByDayRule>? byDay = null;

        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) return false; // "FREQ" with no value, or a stray "=WEEKLY"

            var name = part[..eq].Trim();
            var value = part[(eq + 1)..].Trim();
            if (value.Length == 0) return false;

            switch (name.ToUpperInvariant())
            {
                case "FREQ":
                    if (freq is not null) return false; // duplicate part — ambiguous, not a merge
                    freq = value.ToUpperInvariant() switch
                    {
                        "DAILY" => SchedulerRecurrenceFrequency.Daily,
                        "WEEKLY" => SchedulerRecurrenceFrequency.Weekly,
                        "MONTHLY" => SchedulerRecurrenceFrequency.Monthly,
                        _ => null, // YEARLY / HOURLY / … — outside the expander's own scope
                    };
                    if (freq is null) return false;
                    break;

                case "INTERVAL":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out interval) || interval < 1)
                        return false; // RFC 5545: INTERVAL is a positive integer
                    break;

                case "COUNT":
                    if (count is not null || until is not null) return false; // COUNT and UNTIL are mutually exclusive
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedCount) || parsedCount < 1)
                        return false;
                    count = parsedCount;
                    break;

                case "UNTIL":
                    if (until is not null || count is not null) return false;
                    if (!TryParseUntil(value, out var parsedUntil)) return false;
                    until = parsedUntil;
                    break;

                case "BYDAY":
                    if (byDay is not null) return false;
                    if (!TryParseByDay(value, out byDay)) return false;
                    break;

                default:
                    // Deliberately strict — see the type's own remarks.
                    return false;
            }
        }

        if (freq is null) return false; // FREQ is the one REQUIRED part (RFC 5545 §3.3.10)

        rule = new SchedulerRecurrenceRule(freq.Value, interval, count, until, byDay);
        return true;
    }

    /// <summary>
    /// Parses <paramref name="rrule"/>, or throws <see cref="FormatException"/> when it is
    /// malformed or outside the supported subset. Prefer <see cref="TryParse"/> when the input
    /// comes from user data.
    /// </summary>
    public static SchedulerRecurrenceRule Parse(string? rrule) =>
        TryParse(rrule, out var rule) && rule is not null
            ? rule
            : throw new FormatException(
                $"Not a supported RRULE: '{rrule}'. Supported: FREQ=DAILY|WEEKLY|MONTHLY with " +
                "optional INTERVAL, COUNT or UNTIL, and BYDAY (ordinals allowed, e.g. 2MO or -1FR).");

    /// <summary>
    /// RFC 5545 writes UNTIL as a DATE (<c>yyyyMMdd</c>) or a DATE-TIME (<c>yyyyMMddTHHmmss</c>,
    /// optionally UTC-suffixed with <c>Z</c>).
    /// <para>
    /// The <c>Z</c> form is accepted but NOT converted to local time, deliberately: this
    /// scheduler's kernel is documented as wall-clock throughout — it never calls
    /// <c>ToLocalTime</c>/<c>ToUniversalTime</c> — so shifting only this one value would make
    /// UNTIL disagree with every other date in the same rule. <see cref="SchedulerRecurrenceRule.Until"/>
    /// is also compared by DATE only, which makes the distinction moot for all but a series
    /// ending within hours of midnight.
    /// </para>
    /// </summary>
    private static bool TryParseUntil(string value, out DateTime result)
    {
        var text = value.EndsWith("Z", StringComparison.OrdinalIgnoreCase) ? value[..^1] : value;
        return DateTime.TryParseExact(
            text,
            ["yyyyMMdd", "yyyyMMdd'T'HHmmss"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out result);
    }

    private static bool TryParseByDay(string value, out List<SchedulerByDayRule>? terms)
    {
        terms = null;
        var parsed = new List<SchedulerByDayRule>();

        foreach (var raw in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var term = raw.Trim();
            if (term.Length < 2) return false;

            // Trailing two characters are the weekday; anything before them is the ordinal.
            var dayToken = term[^2..].ToUpperInvariant();
            var ordinalToken = term[..^2];

            var day = dayToken switch
            {
                "SU" => DayOfWeek.Sunday,
                "MO" => DayOfWeek.Monday,
                "TU" => DayOfWeek.Tuesday,
                "WE" => DayOfWeek.Wednesday,
                "TH" => DayOfWeek.Thursday,
                "FR" => DayOfWeek.Friday,
                "SA" => DayOfWeek.Saturday,
                _ => (DayOfWeek?)null,
            };
            if (day is null) return false;

            int? ordinal = null;
            if (ordinalToken.Length > 0)
            {
                if (!int.TryParse(ordinalToken, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsedOrdinal))
                    return false;
                // RFC 5545 allows +/-1..53; 0 is explicitly invalid, and this rule set treats a
                // missing ordinal as "first", so a literal 0 must not silently become that.
                if (parsedOrdinal == 0) return false;
                ordinal = parsedOrdinal;
            }

            parsed.Add(new SchedulerByDayRule(day.Value, ordinal));
        }

        if (parsed.Count == 0) return false;
        terms = parsed;
        return true;
    }
}
