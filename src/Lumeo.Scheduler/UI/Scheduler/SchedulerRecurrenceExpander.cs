namespace Lumeo.SchedulerKernel;

/// <summary>
/// One concrete occurrence produced by <see cref="SchedulerRecurrenceExpander"/> — a single
/// (possibly recurring) source event resolved down to one real Start/End pair. Deliberately
/// carries nothing beyond the back-reference and the two timestamps: title/color/resource/etc.
/// don't vary per-occurrence, so the view layer re-joins an instance to its source
/// <see cref="SchedulerEvent"/> by <see cref="EventId"/> rather than this type duplicating those
/// fields per occurrence.
/// </summary>
/// <param name="EventId">The source <see cref="SchedulerEvent.Id"/> this occurrence belongs to.</param>
/// <param name="Start">This occurrence's concrete start (wall-clock, same <see cref="DateTime.Kind"/> semantics as <see cref="SchedulerEvent.Start"/>).</param>
/// <param name="End">This occurrence's concrete end (exclusive, per FullCalendar convention — matches <see cref="SchedulerEvent.End"/>).</param>
internal readonly record struct SchedulerEventInstance(string EventId, DateTime Start, DateTime End);

/// <summary>
/// Expands a <see cref="SchedulerEvent"/> — recurring or not — into concrete
/// <see cref="SchedulerEventInstance"/>s intersecting a given date range. Spec §2.5/§4.1/§7.1.
///
/// <para>
/// <b>One engine, two input shapes.</b> The legacy simple-recurrence model
/// (<see cref="SchedulerEvent.DaysOfWeek"/> + <see cref="SchedulerEvent.RecurrenceEnd"/>) is not a
/// parallel implementation — <see cref="ResolveRule"/> translates it into the exact
/// <see cref="SchedulerRecurrenceRule"/> it's semantically equivalent to
/// (<c>FREQ=WEEKLY;BYDAY=&lt;DaysOfWeek&gt;;UNTIL=&lt;RecurrenceEnd&gt;</c>) and everything downstream
/// runs through the identical structured-rule expansion path. See
/// <c>SchedulerRecurrenceExpanderTests.LegacyDaysOfWeek_And_EquivalentStructuredRule_Produce_ByteIdentical_Instances</c>
/// for the test that proves this rather than just asserting it.
/// </para>
///
/// <para>
/// <b>TZ/DST safety (spec §2.3).</b> Every occurrence is generated via calendar-field arithmetic
/// (<c>AddDays</c>/<c>AddMonths</c>, <c>Year</c>/<c>Month</c>/<c>Day</c>/<c>DayOfWeek</c> reads) on
/// the event's own wall-clock <see cref="DateTime"/> values — never <see cref="TimeZoneInfo"/>,
/// never elapsed-time subtraction across a day boundary. A recurring event's wall-clock
/// time-of-day is carried forward unchanged to every occurrence regardless of DST, matching spec
/// §2.3's rule that each occurrence independently resolves its own wall-clock position (no
/// special-casing for a series that happens to cross a DST boundary).
/// </para>
/// </summary>
internal static class SchedulerRecurrenceExpander
{
    /// <summary>
    /// FLOOR for the number of RRULE-generated candidate occurrences examined for a single
    /// event, independent of (and in addition to) any <see cref="SchedulerRecurrenceRule.Count"/>/
    /// <see cref="SchedulerRecurrenceRule.Until"/> the caller supplied. Mirrors the uploaded
    /// demo's <c>guard++ &lt; 500</c> (spec §2.5) — the safety net for a <c>COUNT</c>-less,
    /// <c>UNTIL</c>-less rule.
    /// </summary>
    /// <remarks>
    /// It used to be a flat ceiling, and that truncated windows longer than itself. Candidates
    /// are generated from the series' own DTSTART, not from <c>rangeStart</c>, so a resource
    /// timeline showing 24 months asks for 730 daily candidates and stopped 230 days short —
    /// the tail of an axis it had already drawn rendered as free (Codex review of PR #424).
    /// The budget now scales with the requested window via <see cref="EffectiveCandidateCap"/>,
    /// with this value as its floor and <see cref="AbsoluteCandidateCap"/> as its ceiling.
    /// </remarks>
    internal const int OccurrenceCap = 500;

    /// <summary>
    /// Ceiling on candidates examined for one event, whatever the window. Reached only by a
    /// rule whose DTSTART sits far enough before the window that the run-up alone exhausts the
    /// budget — the expansion loop itself already stops at <c>rangeEnd</c>.
    /// </summary>
    internal const int AbsoluteCandidateCap = 20_000;

    /// <summary>
    /// Candidate budget for one expansion: enough to REACH the far edge of the requested
    /// window, clamped between <see cref="OccurrenceCap"/> and <see cref="AbsoluteCandidateCap"/>.
    /// Estimated per frequency, always rounding in the direction that over-counts (a month is
    /// treated as its shortest possible length), because under-counting is what truncates.
    /// </summary>
    internal static int EffectiveCandidateCap(SchedulerRecurrenceRule rule, DateTime dtstart, DateTime rangeEnd)
    {
        if (rangeEnd <= dtstart) return OccurrenceCap;

        var interval = Math.Max(1, rule.Interval);
        // Weekly collapses BYDAY to distinct weekdays — one occurrence per weekday per week.
        // Monthly does NOT: MonthlyCandidates resolves each ByDay ENTRY separately, so "first
        // through fourth Monday" is four candidates a month, not one. Counting distinct days
        // there left the budget at its floor and stopped a 366-month axis after ~125 months
        // (Codex review round 3).
        var perWeek = rule.ByDay is { Count: > 0 } ? rule.ByDay.Select(b => b.Day).Distinct().Count() : 1;
        var perMonth = rule.ByDay is { Count: > 0 } ? rule.ByDay.Count : 1;
        var daysPerCandidate = rule.Freq switch
        {
            SchedulerRecurrenceFrequency.Daily => (double)interval,
            SchedulerRecurrenceFrequency.Weekly => 7.0 * interval / Math.Max(1, perWeek),
            SchedulerRecurrenceFrequency.Monthly => 28.0 * interval / Math.Max(1, perMonth),
            _ => interval,
        };

        // The margin is a whole boundary burst, not a flat 2: weekly and monthly candidates
        // arrive in clusters, so a range ending partway through an active week or month leaves
        // an average-based estimate short by up to that cluster — enough to drop the last
        // occupied day and render it free (Codex review round 4).
        var burst = rule.Freq switch
        {
            SchedulerRecurrenceFrequency.Weekly => perWeek,
            SchedulerRecurrenceFrequency.Monthly => perMonth,
            _ => 1,
        };
        var needed = (rangeEnd - dtstart).TotalDays / Math.Max(0.5, daysPerCandidate) + burst + 2;
        return (int)Math.Clamp(needed, OccurrenceCap, AbsoluteCandidateCap);
    }

    /// <summary>
    /// Expands <paramref name="ev"/> into every occurrence whose span intersects the half-open
    /// range [<paramref name="rangeStart"/>, <paramref name="rangeEnd"/>). Handles all three
    /// cases uniformly: a plain non-recurring event (at most one instance), the legacy
    /// <see cref="SchedulerEvent.DaysOfWeek"/> model, and the structured
    /// <see cref="SchedulerEvent.Recurrence"/> rule — mirroring the uploaded demo's single shared
    /// <c>expandEvents()</c> entry point (spec §1.1) rather than requiring the caller to branch
    /// on which recurrence shape a given event uses.
    /// </summary>
    internal static IReadOnlyList<SchedulerEventInstance> Expand(SchedulerEvent ev, DateTime rangeStart, DateTime rangeEnd)
    {
        var rule = ResolveRule(ev);
        return rule is null
            ? ExpandSingle(ev, rangeStart, rangeEnd)
            : ExpandRule(ev, rule, rangeStart, rangeEnd);
    }

    /// <summary>Convenience overload: expands every event in <paramref name="events"/> and flattens the results.</summary>
    internal static IReadOnlyList<SchedulerEventInstance> ExpandAll(IEnumerable<SchedulerEvent> events, DateTime rangeStart, DateTime rangeEnd)
    {
        var result = new List<SchedulerEventInstance>();
        foreach (var ev in events)
            result.AddRange(Expand(ev, rangeStart, rangeEnd));
        return result;
    }

    /// <summary>
    /// Resolves the effective <see cref="SchedulerRecurrenceRule"/> for <paramref name="ev"/>, or
    /// <c>null</c> for a non-recurring event. <see cref="SchedulerEvent.Recurrence"/> wins when
    /// both it and <see cref="SchedulerEvent.DaysOfWeek"/> are set (spec §4.1) — flagged via a
    /// debug-build assertion, not a thrown exception, since the two are redundant rather than
    /// contradictory (both ultimately resolve to a rule) and this repo's convention is to warn,
    /// not crash the render, on caller ambiguity. Exposed (not private) so tests — and the
    /// legacy/structured parity test in particular — can assert on the translation directly
    /// rather than only observing it indirectly through expanded instances.
    /// </summary>
    internal static SchedulerRecurrenceRule? ResolveRule(SchedulerEvent ev)
    {
        if (ev.Recurrence is not null)
        {
            System.Diagnostics.Debug.Assert(
                ev.DaysOfWeek is not { Count: > 0 },
                "SchedulerEvent has both Recurrence and DaysOfWeek set; Recurrence wins per spec §4.1. " +
                "This usually means the caller meant to set exactly one of the two.");
            return ev.Recurrence;
        }

        if (ev.DaysOfWeek is { Count: > 0 })
        {
            // Legacy translation (spec §4.1 point 2): DaysOfWeek + RecurrenceEnd is exactly
            // FREQ=WEEKLY;BYDAY=<DaysOfWeek>;UNTIL=<RecurrenceEnd>. No INTERVAL/COUNT
            // equivalent exists in the legacy shape, so both stay at their rule defaults
            // (Interval=1, Count=null).
            var byDay = new SchedulerByDayRule[ev.DaysOfWeek.Count];
            for (var i = 0; i < ev.DaysOfWeek.Count; i++)
                byDay[i] = new SchedulerByDayRule(ev.DaysOfWeek[i]);

            return new SchedulerRecurrenceRule(
                Freq: SchedulerRecurrenceFrequency.Weekly,
                Interval: 1,
                Count: null,
                Until: ev.RecurrenceEnd,
                ByDay: byDay);
        }

        return null;
    }

    private static IReadOnlyList<SchedulerEventInstance> ExpandSingle(SchedulerEvent ev, DateTime rangeStart, DateTime rangeEnd)
    {
        return ev.End > rangeStart && ev.Start < rangeEnd
            ? new[] { new SchedulerEventInstance(ev.Id, ev.Start, ev.End) }
            : Array.Empty<SchedulerEventInstance>();
    }

    private static IReadOnlyList<SchedulerEventInstance> ExpandRule(
        SchedulerEvent ev, SchedulerRecurrenceRule rule, DateTime rangeStart, DateTime rangeEnd)
    {
        var duration = ev.End - ev.Start;
        HashSet<DateTime>? exceptionDates = null;
        if (ev.ExceptionDates is { Count: > 0 })
        {
            exceptionDates = new HashSet<DateTime>();
            foreach (var d in ev.ExceptionDates)
                exceptionDates.Add(d.Date);
        }

        var instances = new List<SchedulerEventInstance>();
        var index = 0;
        var cap = EffectiveCandidateCap(rule, ev.Start, rangeEnd);

        foreach (var candidateStart in GenerateCandidates(rule, ev.Start))
        {
            index++;

            // Safety cap: total RRULE-generated candidates examined, independent of COUNT/UNTIL.
            if (index > cap) break;

            // UNTIL is inclusive of its own calendar date (spec §2.5).
            if (rule.Until.HasValue && candidateStart.Date > rule.Until.Value.Date) break;

            // COUNT is measured over the raw rule-generated series, before EXDATE exclusion
            // (RFC 5545 semantics) — an excluded occurrence still consumes a COUNT slot.
            if (rule.Count.HasValue && index > rule.Count.Value) break;

            // Candidates are generated in strictly ascending order, so once one lands at/after
            // rangeEnd, every later candidate would too — safe to stop scanning early. This is a
            // perf optimization only: correctness of COUNT/UNTIL never depended on rangeStart/
            // rangeEnd, since `index` and the Until/Count checks above are computed from
            // ev.Start (the series' own DTSTART), not from the requested render window.
            if (candidateStart >= rangeEnd) break;

            var excluded = exceptionDates is not null && exceptionDates.Contains(candidateStart.Date);
            if (excluded) continue;

            // Clamped rather than added blind: a positive-duration series can now be budgeted
            // far enough to reach the last representable day, where the addition itself threw.
            // The old flat cap stopped decades short of it (Codex review round 3).
            var candidateEnd = duration > DateTime.MaxValue - candidateStart
                ? DateTime.MaxValue
                : candidateStart + duration;
            if (candidateEnd > rangeStart && candidateStart < rangeEnd)
                instances.Add(new SchedulerEventInstance(ev.Id, candidateStart, candidateEnd));
        }

        return instances;
    }

    /// <summary>
    /// Lazily yields raw RRULE candidate start timestamps for <paramref name="rule"/>, in
    /// strictly ascending order, starting at (and never before) <paramref name="dtstart"/>
    /// (<see cref="SchedulerEvent.Start"/>). Unbounded — callers apply COUNT/UNTIL/the occurrence
    /// cap themselves (see <see cref="ExpandRule"/>).
    /// </summary>
    private static IEnumerable<DateTime> GenerateCandidates(SchedulerRecurrenceRule rule, DateTime dtstart) => rule.Freq switch
    {
        SchedulerRecurrenceFrequency.Daily => DailyCandidates(rule, dtstart),
        SchedulerRecurrenceFrequency.Weekly => WeeklyCandidates(rule, dtstart),
        SchedulerRecurrenceFrequency.Monthly => MonthlyCandidates(rule, dtstart),
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule.Freq, "Unsupported SchedulerRecurrenceFrequency."),
    };

    private static IEnumerable<DateTime> DailyCandidates(SchedulerRecurrenceRule rule, DateTime dtstart)
    {
        var interval = Math.Max(1, rule.Interval);
        var current = dtstart;
        while (true)
        {
            yield return current;
            // The step count is no longer bounded by a flat 500, so the advance has to guard
            // its own overflow rather than borrow that bound (Codex review of PR #424).
            if ((DateTime.MaxValue - current).TotalDays < interval) yield break;
            current = current.AddDays(interval);
        }
    }

    // RFC 5545's WKST (week-start-for-INTERVAL-stepping) defaults to Monday; the wave-1 subset
    // doesn't expose WKST as a knob (not in the spec's supported-component table), so Monday is
    // used unconditionally here. Deliberately independent of the Scheduler component's own
    // FirstDayOfWeek (SchedulerDateMath's concern, which is purely about which weekday a
    // month/week GRID visually starts on) — conflating the two would make a rule's occurrence
    // dates shift if a consumer merely changed their calendar's display setting, which would be
    // a real, surprising bug.
    private static IEnumerable<DateTime> WeeklyCandidates(SchedulerRecurrenceRule rule, DateTime dtstart)
    {
        var interval = Math.Max(1, rule.Interval);
        var days = rule.ByDay is { Count: > 0 }
            ? rule.ByDay.Select(b => b.Day).Distinct().OrderBy(IsoWeekdayIndex).ToArray()
            : new[] { dtstart.DayOfWeek };

        var anchorWeekStart = dtstart.Date.AddDays(-IsoWeekdayIndex(dtstart.DayOfWeek));
        var weekOffset = 0;
        while (true)
        {
            var offsetDays = weekOffset * 7L * interval;
            if (offsetDays > (DateTime.MaxValue - anchorWeekStart).TotalDays) yield break;
            var weekStart = anchorWeekStart.AddDays(offsetDays);
            foreach (var day in days)
            {
                // The day offset and the time-of-day are each an advance of their own, and the
                // last week before DateTime.MaxValue overflows on one of them rather than on the
                // week step guarded above.
                var dayOffset = IsoWeekdayIndex(day);
                if (dayOffset > (DateTime.MaxValue - weekStart).TotalDays) yield break;
                var atMidnight = weekStart.AddDays(dayOffset);
                if (dtstart.TimeOfDay > DateTime.MaxValue - atMidnight) yield break;

                var candidate = atMidnight + dtstart.TimeOfDay;
                if (candidate >= dtstart)
                    yield return candidate;
            }
            weekOffset++;
        }
    }

    private static IEnumerable<DateTime> MonthlyCandidates(SchedulerRecurrenceRule rule, DateTime dtstart)
    {
        var interval = Math.Max(1, rule.Interval);
        var monthOffset = 0;
        while (true)
        {
            // AddMonths on a day-1 anchor sidesteps the "Jan 31 + 1 month" overflow trap —
            // SchedulerDateMath.StartOfMonth documents the identical concern for the month grid.
            var anchor = new DateTime(dtstart.Year, dtstart.Month, 1);
            var months = (long)monthOffset * interval;
            // Whole years alone under-counts by up to eleven months: a series starting in
            // January 9999 has eleven representable steps left, and comparing years only
            // treated zero as its maximum, so it yielded January and stopped (Codex review
            // round 3).
            var monthsLeft = (DateTime.MaxValue.Year - anchor.Year) * 12L + (DateTime.MaxValue.Month - anchor.Month);
            if (months > monthsLeft) yield break;
            var monthAnchor = anchor.AddMonths((int)months);
            var year = monthAnchor.Year;
            var month = monthAnchor.Month;

            IEnumerable<DateTime> datesThisMonth;
            if (rule.ByDay is { Count: > 0 })
            {
                var resolved = new List<DateTime>();
                foreach (var byDay in rule.ByDay)
                {
                    var d = ResolveOrdinalWeekday(year, month, byDay.Day, byDay.Ordinal ?? 1);
                    if (d.HasValue) resolved.Add(d.Value);
                }
                resolved.Sort();
                datesThisMonth = resolved;
            }
            else
            {
                // No BYDAY: RFC 5545 default for a bare FREQ=MONTHLY is "same day-of-month as
                // DTSTART". Clamped to the target month's own day count (rather than skipped)
                // so a 31st-anchored monthly rule still produces a February occurrence — a
                // deliberate, documented wave-1 choice; BYMONTHDAY itself (explicit day-number
                // input) is out of scope (spec §2.5's deferred list).
                var day = Math.Min(dtstart.Day, DateTime.DaysInMonth(year, month));
                datesThisMonth = new[] { new DateTime(year, month, day) };
            }

            foreach (var date in datesThisMonth)
            {
                // Same shape as the weekly generator: the time-of-day is its own advance, and
                // the month anchor can already sit on the last representable day.
                if (dtstart.TimeOfDay > DateTime.MaxValue - date.Date) yield break;

                var candidate = date.Date + dtstart.TimeOfDay;
                if (candidate >= dtstart)
                    yield return candidate;
            }

            monthOffset++;
        }
    }

    /// <summary>
    /// The <paramref name="ordinal"/>-th occurrence of <paramref name="day"/> in
    /// <paramref name="year"/>/<paramref name="month"/>. Positive counts from the 1st (1 = first
    /// occurrence); negative counts from the end of the month (-1 = last occurrence). Returns
    /// <c>null</c> when the month doesn't have that many occurrences of the weekday (e.g. a
    /// hypothetical "5th Monday" in a month that only has four).
    /// </summary>
    private static DateTime? ResolveOrdinalWeekday(int year, int month, DayOfWeek day, int ordinal)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        if (ordinal > 0)
        {
            var count = 0;
            for (var d = 1; d <= daysInMonth; d++)
            {
                if (new DateTime(year, month, d).DayOfWeek != day) continue;
                count++;
                if (count == ordinal) return new DateTime(year, month, d);
            }
            return null;
        }

        if (ordinal < 0)
        {
            var count = 0;
            for (var d = daysInMonth; d >= 1; d--)
            {
                if (new DateTime(year, month, d).DayOfWeek != day) continue;
                count++;
                if (count == -ordinal) return new DateTime(year, month, d);
            }
            return null;
        }

        // Ordinal 0 isn't a valid RFC 5545 ordinal; treat as "no match" rather than throwing —
        // same "warn, don't crash" posture used elsewhere in this class.
        return null;
    }

    /// <summary>Monday=0 .. Sunday=6 — the ISO-8601 weekday ordering used to walk a week's candidate days in ascending date order.</summary>
    private static int IsoWeekdayIndex(DayOfWeek d) => d == DayOfWeek.Sunday ? 6 : (int)d - 1;
}
