namespace Lumeo;

/// <summary>
/// The <c>FREQ</c> component of the wave-1 RRULE subset (spec §2.5). <c>YEARLY</c> and every
/// other RFC 5545 frequency are explicitly deferred — not silently unsupported, just out of
/// scope for wave 1.
/// </summary>
public enum SchedulerRecurrenceFrequency
{
    /// <summary>Repeats every <c>Interval</c> days.</summary>
    Daily,

    /// <summary>Repeats every <c>Interval</c> weeks, on the day(s) in <see cref="SchedulerRecurrenceRule.ByDay"/> (or the start date's own weekday when <see cref="SchedulerRecurrenceRule.ByDay"/> is unset).</summary>
    Weekly,

    /// <summary>Repeats every <c>Interval</c> months, on the day(s)/ordinal-weekday(s) in <see cref="SchedulerRecurrenceRule.ByDay"/> (or the start date's own day-of-month when <see cref="SchedulerRecurrenceRule.ByDay"/> is unset).</summary>
    Monthly
}

/// <summary>
/// A single <c>BYDAY</c> term: a weekday, optionally qualified with an ordinal ("2nd Monday",
/// "last Friday") for <see cref="SchedulerRecurrenceFrequency.Monthly"/> rules.
/// </summary>
/// <param name="Day">The day of the week.</param>
/// <param name="Ordinal">
/// Only meaningful for <see cref="SchedulerRecurrenceFrequency.Monthly"/> rules. A positive value
/// counts from the start of the month (<c>1</c> = first occurrence of <see cref="Day"/> in the
/// month, <c>2</c> = second, ...); a negative value counts from the end of the month (<c>-1</c> =
/// last occurrence, <c>-2</c> = second-to-last, ...), matching RFC 5545's <c>BYDAY</c> ordinal
/// syntax (e.g. <c>"2MO"</c> = second Monday, <c>"-1FR"</c> = last Friday — the "last Friday of
/// the month standup" case called out in the spec). <c>null</c> (unset) is treated as <c>1</c>
/// (the first occurrence) — RFC 5545's OTHER reading of a bare, unqualified <c>BYDAY</c> inside a
/// <c>MONTHLY</c> rule ("every occurrence of that weekday in the month", i.e. potentially more
/// than one date) is out of scope for wave 1. Ignored entirely for
/// <see cref="SchedulerRecurrenceFrequency.Weekly"/> rules, where a <see cref="SchedulerByDayRule"/>
/// only ever selects a day-of-week within each recurring week.
/// </param>
public sealed record SchedulerByDayRule(DayOfWeek Day, int? Ordinal = null);

/// <summary>
/// The wave-1 RRULE subset (spec §2.5): <c>FREQ=DAILY|WEEKLY|MONTHLY</c>, <c>INTERVAL</c>,
/// <c>COUNT</c>, <c>UNTIL</c>, and <c>BYDAY</c> (weekly day-of-week set, or monthly
/// ordinal-weekday). <c>EXDATE</c> is deliberately NOT part of this record — it continues to be
/// expressed via <see cref="SchedulerEvent.ExceptionDates"/>, the existing shipped property,
/// rather than duplicating it here.
/// </summary>
/// <param name="Freq">The repeat frequency.</param>
/// <param name="Interval">
/// Repeat every <see cref="Interval"/>-th occurrence of <see cref="Freq"/>'s unit (e.g.
/// <see cref="Freq"/> = <see cref="SchedulerRecurrenceFrequency.Weekly"/> and
/// <see cref="Interval"/> = 2 means "every 2 weeks"). Must be 1 or greater; values below 1 are
/// treated as 1 by <c>SchedulerRecurrenceExpander</c> rather than throwing, matching this repo's
/// general "warn, don't crash the render" posture for malformed input.
/// </param>
/// <param name="Count">
/// When set, bounds the series to exactly this many occurrences, counted from
/// <see cref="SchedulerEvent.Start"/> (the series' own DTSTART) — regardless of which date range
/// the expander is later asked to render, so a windowed render always agrees with a full-series
/// render about which occurrence index is "the 5th one". Per RFC 5545, this count is taken over
/// the raw rule-generated series BEFORE <see cref="SchedulerEvent.ExceptionDates"/> exclusions are
/// applied — an excluded occurrence still consumes one of the <see cref="Count"/> slots.
/// </param>
/// <param name="Until">
/// When set, the inclusive end date of the series (spec §2.5: "UNTIL — inclusive end date”). Only
/// the date part is significant; an occurrence landing on <see cref="Until"/>'s own calendar date
/// is still included.
/// </param>
/// <param name="ByDay">
/// For <see cref="SchedulerRecurrenceFrequency.Weekly"/>: the set of weekdays the event repeats
/// on within each recurring week (<see cref="SchedulerByDayRule.Ordinal"/> is ignored). For
/// <see cref="SchedulerRecurrenceFrequency.Monthly"/>: one or more ordinal-weekday terms (e.g.
/// "2nd Monday", "last Friday" — see <see cref="SchedulerByDayRule.Ordinal"/>). <c>null</c>
/// (unset) falls back to <see cref="SchedulerEvent.Start"/>'s own day-of-week (Weekly) or
/// day-of-month (Monthly). Not applicable to <see cref="SchedulerRecurrenceFrequency.Daily"/>.
/// </param>
public sealed record SchedulerRecurrenceRule(
    SchedulerRecurrenceFrequency Freq,
    int Interval = 1,
    int? Count = null,
    DateTime? Until = null,
    IReadOnlyList<SchedulerByDayRule>? ByDay = null);
