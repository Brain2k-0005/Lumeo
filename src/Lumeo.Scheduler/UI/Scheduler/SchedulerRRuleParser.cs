using System.Globalization;
using System.Linq;

// Deliberately alongside SchedulerRecurrenceRule.cs in UI/Scheduler rather than
// in Kernel/ (Codex review of this PR, P2): RegistryGen builds a component's
// vendored `files` list from its own UI/<Name>/ folder, so nothing under Kernel/
// is shipped by `lumeo add scheduler` — this parser would have been public API
// that source-vendored consumers simply did not receive. It also belongs next to
// the type it produces.
namespace Lumeo;

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
/// (<see cref="Lumeo.SchedulerKernel.SchedulerRecurrenceExpander"/>): <c>FREQ</c> of DAILY/WEEKLY/MONTHLY, plus
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
        var intervalSeen = false;
        int? count = null;
        DateTime? until = null;
        List<SchedulerByDayRule>? byDay = null;

        // NOT RemoveEmptyEntries (Codex review of this PR, P2): that silently
        // swallowed ";FREQ=DAILY", "FREQ=DAILY;" and "FREQ=DAILY;;COUNT=2",
        // which is exactly the truncated/mis-assembled calendar line this
        // parser's strictness exists to surface.
        var parts = text.Split(';');
        if (parts.Length == 0) return false;
        foreach (var part in parts)
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
                    // Duplicate-rejected like every other part (Codex review of
                    // this PR, P2): letting a second INTERVAL overwrite the
                    // first silently picks a winner from an ambiguous rule.
                    if (intervalSeen) return false;
                    intervalSeen = true;
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
        if (!ByDayIsHonouredBy(freq.Value, byDay)) return false;

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
    /// Whether the expander will actually HONOUR <paramref name="byDay"/> under
    /// <paramref name="freq"/> (Codex review of this PR, P1). Rejecting mismatches is the whole
    /// point of this parser's strictness — each case below otherwise parses cleanly and then
    /// produces a series that silently means something else:
    /// <list type="bullet">
    ///   <item><c>FREQ=DAILY;BYDAY=MO</c> — daily expansion ignores ByDay entirely, so this
    ///   would expand to EVERY day rather than Mondays.</item>
    ///   <item><c>FREQ=WEEKLY;BYDAY=2MO</c> — weekly expansion ignores the ordinal, so
    ///   "second Monday" would expand to every Monday.</item>
    ///   <item><c>FREQ=MONTHLY;BYDAY=MO</c> — RFC 5545 reads an unqualified monthly term as
    ///   EVERY Monday in the month; this rule model treats a missing ordinal as the FIRST one
    ///   (see <see cref="SchedulerByDayRule.Ordinal"/>, which records that divergence as
    ///   out of scope), so accepting it would quietly drop every occurrence but the first.</item>
    /// </list>
    /// </summary>
    private static bool ByDayIsHonouredBy(SchedulerRecurrenceFrequency freq, List<SchedulerByDayRule>? byDay)
    {
        if (byDay is null) return true; // no BYDAY at all is always fine

        return freq switch
        {
            SchedulerRecurrenceFrequency.Daily => false,
            SchedulerRecurrenceFrequency.Weekly => byDay.All(d => d.Ordinal is null),
            SchedulerRecurrenceFrequency.Monthly => byDay.All(d => d.Ordinal is not null),
            _ => false,
        };
    }

    /// <summary>
    /// RFC 5545 writes UNTIL as a DATE (<c>yyyyMMdd</c>) or a DATE-TIME (<c>yyyyMMddTHHmmss</c>,
    /// optionally UTC-suffixed with <c>Z</c>).
    /// <para>
    /// A DATE-TIME is accepted ONLY when its time-of-day is <c>23:59:59</c> — the end-of-day
    /// idiom real calendars emit. Correction to an earlier claim of mine that the distinction was
    /// "moot" (Codex review of this PR, P2): it is not.
    /// <see cref="SchedulerRecurrenceRule.Until"/> is bounded by DATE, so a cutoff of
    /// <c>20260812T080000</c> would still include that day's 09:00 occurrence — later than the
    /// caller asked for. End-of-day is the one time-of-day where date-only bounding is exactly
    /// equivalent; every other value is rejected rather than silently widened to the full day.
    /// </para>
    /// <para>
    /// The <c>Z</c> suffix is accepted but NOT converted to local time: this scheduler's kernel is
    /// wall-clock throughout — it never calls <c>ToLocalTime</c>/<c>ToUniversalTime</c> — so
    /// shifting only this one value would make UNTIL disagree with every other date in the same
    /// rule.
    /// </para>
    /// </summary>
    private static bool TryParseUntil(string value, out DateTime result)
    {
        // The Z designator belongs to RFC 5545's DATE-TIME form only, so a
        // date-only value carrying one is malformed (Codex review of this PR,
        // P2) — stripping it unconditionally made "20261231Z" parse.
        var hasUtcSuffix = value.EndsWith("Z", StringComparison.OrdinalIgnoreCase);
        var text = hasUtcSuffix ? value[..^1] : value;

        if (DateTime.TryParseExact(text, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return !hasUtcSuffix;

        if (!DateTime.TryParseExact(text, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return false;

        // See the remarks above: only the end-of-day idiom survives date-only bounding intact.
        if (result.TimeOfDay == new TimeSpan(23, 59, 59)) return true;

        result = default;
        return false;
    }

    private static bool TryParseByDay(string value, out List<SchedulerByDayRule>? terms)
    {
        terms = null;
        var parsed = new List<SchedulerByDayRule>();

        // Empty entries preserved for the same reason the outer split preserves
        // them (Codex review of this PR, P2): "BYDAY=MO,", ",MO" and "MO,,WE"
        // are malformed, and dropping the blanks made them parse.
        foreach (var raw in value.Split(','))
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
                // Bounded to +/-1..5, not RFC 5545's +/-1..53 (Codex review of
                // this PR, P2): ordinals are only honoured for MONTHLY here (see
                // ByDayIsHonouredBy), and no month can contain a sixth
                // occurrence of a weekday. "6MO" would otherwise resolve to
                // nothing every month, so the expander spins to its safety cap
                // and yields a silently EMPTY series instead of reporting the
                // bad rule. The wider RFC range exists for YEARLY, which this
                // subset does not implement.
                //
                // Compared without Math.Abs on purpose: Math.Abs(int.MinValue)
                // THROWS, which would make the documented never-throwing
                // TryParse throw for "BYDAY=-2147483648MO".
                if (parsedOrdinal is 0 or > 5 or < -5) return false;
                ordinal = parsedOrdinal;
            }

            // Duplicate terms are rejected, not collapsed (Codex review of this
            // PR, P2): "BYDAY=1MO,1MO" reaches monthly expansion as two rules,
            // emitting two instances at the SAME timestamp and burning the whole
            // COUNT on one date. Rejecting matches how duplicate PARTS are
            // handled — an ambiguous rule is not silently resolved.
            if (parsed.Any(t => t.Day == day.Value && t.Ordinal == ordinal)) return false;
            // ...and reject SEMANTIC overlap too (Codex review of this PR, P2):
            // "1MO,-5MO" are distinct terms that resolve to the SAME date in any
            // month with five Mondays, emitting a duplicate instance and burning
            // COUNT on one date — the same failure the exact-duplicate check
            // above prevents, just spelled differently. Any mix of a
            // counts-from-start and a counts-from-end ordinal for one weekday
            // can collide depending on month length, so the whole combination is
            // refused rather than accepted for some months and not others.
            if (ordinal is { } o && parsed.Any(t => t.Day == day.Value && t.Ordinal is { } prev && Math.Sign(prev) != Math.Sign(o)))
                return false;
            parsed.Add(new SchedulerByDayRule(day.Value, ordinal));
        }

        if (parsed.Count == 0) return false;
        terms = parsed;
        return true;
    }
}
