using Lumeo.SchedulerKernel;
using Xunit;

namespace Lumeo.Tests.Components.SchedulerKernel;

/// <summary>
/// Tests for <see cref="SchedulerRecurrenceExpander"/> — spec §2.5/§4.1/§7.1's recurrence matrix.
/// This is called out in the task as the highest-value test surface in the whole spec, so the
/// matrix here is intentionally broader than the floor §7.1 lists.
/// </summary>
public class SchedulerRecurrenceExpanderTests
{
    // 2026-08-10 is a Monday.
    private static readonly DateTime Monday = new(2026, 8, 10, 9, 0, 0);
    private static readonly DateTime MondayEnd = new(2026, 8, 10, 10, 0, 0); // 1-hour duration

    private static SchedulerEvent Base(
        string id = "ev",
        DateTime? start = null,
        DateTime? end = null,
        SchedulerRecurrenceRule? recurrence = null,
        IReadOnlyList<DayOfWeek>? daysOfWeek = null,
        DateTime? recurrenceEnd = null,
        IReadOnlyList<DateTime>? exceptionDates = null) =>
        // Recurrence is a record-body property (not a primary-constructor parameter — see
        // SchedulerTypes.cs's remarks on why), so it's set via object-initializer syntax here
        // rather than as a named constructor argument like the other optional parameters.
        new(id, "Title", start ?? Monday, end ?? MondayEnd,
            DaysOfWeek: daysOfWeek, RecurrenceEnd: recurrenceEnd, ExceptionDates: exceptionDates)
        {
            Recurrence = recurrence
        };

    // ── Non-recurring ────────────────────────────────────────────────────────

    [Fact]
    public void NonRecurring_Event_Intersecting_Range_Produces_One_Instance()
    {
        var ev = Base();
        var result = SchedulerRecurrenceExpander.Expand(ev, Monday.AddDays(-1), Monday.AddDays(1));

        var instance = Assert.Single(result);
        Assert.Equal(Monday, instance.Start);
        Assert.Equal(MondayEnd, instance.End);
    }

    [Fact]
    public void NonRecurring_Event_Outside_Range_Produces_No_Instances()
    {
        var ev = Base();
        var result = SchedulerRecurrenceExpander.Expand(ev, Monday.AddDays(10), Monday.AddDays(20));
        Assert.Empty(result);
    }

    // ── FREQ=DAILY ───────────────────────────────────────────────────────────

    [Fact]
    public void Daily_Interval_One_Produces_Every_Day()
    {
        var ev = Base(recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Daily, Interval: 1, Count: 5));
        var result = SchedulerRecurrenceExpander.Expand(ev, Monday, Monday.AddDays(30));

        Assert.Equal(5, result.Count);
        for (var i = 0; i < 5; i++)
            Assert.Equal(Monday.AddDays(i), result[i].Start);
    }

    [Fact]
    public void Daily_Interval_Three_Skips_Two_Days_Each_Time()
    {
        var ev = Base(recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Daily, Interval: 3, Count: 4));
        var result = SchedulerRecurrenceExpander.Expand(ev, Monday, Monday.AddDays(30));

        Assert.Equal(new[] { 0, 3, 6, 9 }.Select(d => Monday.AddDays(d)), result.Select(r => r.Start));
    }

    // ── FREQ=WEEKLY ──────────────────────────────────────────────────────────

    [Fact]
    public void Weekly_With_No_ByDay_Repeats_On_Dtstarts_Own_Weekday()
    {
        var ev = Base(recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Weekly, Interval: 1, Count: 3));
        var result = SchedulerRecurrenceExpander.Expand(ev, Monday, Monday.AddDays(30));

        Assert.Equal(new[] { Monday, Monday.AddDays(7), Monday.AddDays(14) }, result.Select(r => r.Start));
    }

    [Fact]
    public void Weekly_ByDay_MonWed_Produces_Both_Days_Each_Week_In_Ascending_Order()
    {
        var byDay = new[] { new SchedulerByDayRule(DayOfWeek.Monday), new SchedulerByDayRule(DayOfWeek.Wednesday) };
        var ev = Base(recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Weekly, Interval: 1, Count: 4, ByDay: byDay));
        var result = SchedulerRecurrenceExpander.Expand(ev, Monday, Monday.AddDays(30));

        Assert.Equal(
            new[] { Monday, Monday.AddDays(2), Monday.AddDays(7), Monday.AddDays(9) },
            result.Select(r => r.Start));
    }

    [Fact]
    public void Weekly_Interval_Two_Skips_The_Alternate_Week()
    {
        var byDay = new[] { new SchedulerByDayRule(DayOfWeek.Monday) };
        var ev = Base(recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Weekly, Interval: 2, Count: 3, ByDay: byDay));
        var result = SchedulerRecurrenceExpander.Expand(ev, Monday, Monday.AddDays(60));

        Assert.Equal(new[] { Monday, Monday.AddDays(14), Monday.AddDays(28) }, result.Select(r => r.Start));
    }

    [Fact]
    public void Weekly_Never_Emits_An_Occurrence_Before_Dtstart_Even_When_ByDay_Includes_An_Earlier_Weekday()
    {
        // DTSTART is Wednesday 2026-08-12; BYDAY includes Monday, which — within the SAME
        // Monday-anchored calendar week as DTSTART — falls chronologically BEFORE it (Mon
        // 2026-08-10 < Wed 2026-08-12). That first Monday must be skipped: an RRULE series never
        // produces an occurrence earlier than its own DTSTART, even when BYDAY would otherwise
        // generate one for the week DTSTART itself falls in.
        var wednesday = new DateTime(2026, 8, 12, 9, 0, 0);
        var byDay = new[] { new SchedulerByDayRule(DayOfWeek.Monday), new SchedulerByDayRule(DayOfWeek.Wednesday) };
        var ev = Base(start: wednesday, end: wednesday.AddHours(1),
            recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Weekly, Interval: 1, Count: 3, ByDay: byDay));
        var result = SchedulerRecurrenceExpander.Expand(ev, wednesday.AddDays(-7), wednesday.AddDays(30));

        // First occurrence is DTSTART itself (Wed 08-12) — NOT the preceding Monday (08-10),
        // which would otherwise have been the very first BYDAY candidate generated.
        Assert.Equal(
            new[] { wednesday, wednesday.AddDays(5) /* Mon 08-17 */, wednesday.AddDays(7) /* Wed 08-19 */ },
            result.Select(r => r.Start));
    }

    // ── FREQ=MONTHLY ─────────────────────────────────────────────────────────

    [Fact]
    public void Monthly_With_No_ByDay_Repeats_On_Dtstarts_Own_Day_Of_Month()
    {
        // DTSTART on the 10th.
        var ev = Base(recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Monthly, Interval: 1, Count: 3));
        var result = SchedulerRecurrenceExpander.Expand(ev, Monday, Monday.AddMonths(6));

        Assert.Equal(
            new[] { new DateTime(2026, 8, 10, 9, 0, 0), new DateTime(2026, 9, 10, 9, 0, 0), new DateTime(2026, 10, 10, 9, 0, 0) },
            result.Select(r => r.Start));
    }

    [Fact]
    public void Monthly_With_No_ByDay_Clamps_A_31st_Anchor_To_The_Shorter_Months_Last_Day()
    {
        var jan31 = new DateTime(2026, 1, 31, 8, 0, 0);
        var ev = Base(start: jan31, end: jan31.AddHours(1),
            recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Monthly, Interval: 1, Count: 4));
        var result = SchedulerRecurrenceExpander.Expand(ev, jan31, jan31.AddMonths(6));

        Assert.Equal(
            new[]
            {
                new DateTime(2026, 1, 31, 8, 0, 0),
                new DateTime(2026, 2, 28, 8, 0, 0), // 2026 is not a leap year — clamped to the 28th.
                new DateTime(2026, 3, 31, 8, 0, 0), // March has 31 days again — no permanent drift.
                new DateTime(2026, 4, 30, 8, 0, 0), // April clamps to the 30th.
            },
            result.Select(r => r.Start));
    }

    [Fact]
    public void Monthly_ByDay_With_Positive_Ordinal_Resolves_The_Nth_Weekday_Of_Each_Month()
    {
        // "2nd Monday" of September/October 2026, hand-derived independently of the SUT via plain
        // LINQ over the month's days (not reusing SchedulerRecurrenceExpander's own algorithm).
        DateTime NthWeekday(int year, int month, DayOfWeek day, int n) =>
            Enumerable.Range(1, DateTime.DaysInMonth(year, month))
                .Select(d => new DateTime(year, month, d))
                .Where(d => d.DayOfWeek == day)
                .Skip(n - 1)
                .First();

        var secondMondaySep = NthWeekday(2026, 9, DayOfWeek.Monday, 2);
        var secondMondayOct = NthWeekday(2026, 10, DayOfWeek.Monday, 2);

        var start = new DateTime(2026, 9, 1, 9, 0, 0);
        var byDay = new[] { new SchedulerByDayRule(DayOfWeek.Monday, Ordinal: 2) };
        var ev = Base(start: start, end: start.AddHours(1),
            recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Monthly, Interval: 1, Count: 2, ByDay: byDay));
        var result = SchedulerRecurrenceExpander.Expand(ev, start, start.AddMonths(3));

        Assert.Equal(
            new[] { secondMondaySep.Add(new TimeSpan(9, 0, 0)), secondMondayOct.Add(new TimeSpan(9, 0, 0)) },
            result.Select(r => r.Start));
    }

    [Fact]
    public void Monthly_ByDay_With_Negative_Ordinal_Resolves_The_Last_Weekday_Of_Each_Month()
    {
        // The "last Friday of the month standup" case the spec calls out explicitly (§2.5/§6).
        DateTime LastWeekday(int year, int month, DayOfWeek day) =>
            Enumerable.Range(1, DateTime.DaysInMonth(year, month))
                .Select(d => new DateTime(year, month, d))
                .Where(d => d.DayOfWeek == day)
                .Last();

        var lastFridayAug = LastWeekday(2026, 8, DayOfWeek.Friday);
        var lastFridaySep = LastWeekday(2026, 9, DayOfWeek.Friday);

        var start = lastFridayAug.Add(new TimeSpan(16, 0, 0));
        var byDay = new[] { new SchedulerByDayRule(DayOfWeek.Friday, Ordinal: -1) };
        var ev = Base(start: start, end: start.AddMinutes(30),
            recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Monthly, Interval: 1, Count: 2, ByDay: byDay));
        var result = SchedulerRecurrenceExpander.Expand(ev, start, start.AddMonths(3));

        Assert.Equal(
            new[] { lastFridayAug.Add(new TimeSpan(16, 0, 0)), lastFridaySep.Add(new TimeSpan(16, 0, 0)) },
            result.Select(r => r.Start));
    }

    [Fact]
    public void Monthly_ByDay_Ordinal_That_Does_Not_Exist_In_A_Given_Month_Is_Skipped_Not_Thrown()
    {
        // "5th Monday" doesn't exist in every month. February 2026 has only 4 Mondays.
        var start = new DateTime(2026, 2, 2, 9, 0, 0); // first Monday of Feb 2026
        var byDay = new[] { new SchedulerByDayRule(DayOfWeek.Monday, Ordinal: 5) };
        var ev = Base(start: start, end: start.AddHours(1),
            recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Monthly, Interval: 1, Count: 2, ByDay: byDay));

        var exception = Record.Exception(() => SchedulerRecurrenceExpander.Expand(ev, start, start.AddMonths(12)));
        Assert.Null(exception);
    }

    // ── COUNT / UNTIL / occurrence cap ───────────────────────────────────────

    [Fact]
    public void Count_Bounds_The_Series_From_Dtstart_Regardless_Of_How_Wide_The_Requested_Range_Is()
    {
        var ev = Base(recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Daily, Interval: 1, Count: 5));
        // Range extends 1000 days out — far more than 5 days — yet only 5 instances come back.
        var result = SchedulerRecurrenceExpander.Expand(ev, Monday, Monday.AddDays(1000));

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void Until_Is_Inclusive_Of_Its_Own_Calendar_Date()
    {
        var until = Monday.AddDays(4); // Friday
        var ev = Base(recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Daily, Interval: 1, Until: until));
        var result = SchedulerRecurrenceExpander.Expand(ev, Monday, Monday.AddDays(30));

        Assert.Equal(5, result.Count); // Mon..Fri inclusive = 5 days
        Assert.Equal(until.Date, result[^1].Start.Date);
    }

    [Fact]
    public void A_Window_Longer_Than_The_Floor_Is_Filled_Completely()
    {
        // The cap used to be a flat 500 ceiling, which truncated any window longer than itself.
        // Candidates are counted from the series' own start, so a resource timeline drawing 24
        // months asked for 730 daily occurrences and got 500 — the last eight months of an axis
        // it had already drawn rendered as free (Codex review of PR #424).
        var ev = Base(recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Daily, Interval: 1));

        var result = SchedulerRecurrenceExpander.Expand(ev, Monday, Monday.AddDays(2_000));

        Assert.Equal(2_000, result.Count);
    }

    [Fact]
    public void A_Runaway_Rule_Still_Stops_At_The_Absolute_Ceiling()
    {
        // The budget scales with the window, so the safety net moved rather than disappeared:
        // a COUNT-less, UNTIL-less rule against an absurd range stops at the ceiling.
        var ev = Base(recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Daily, Interval: 1));

        var result = SchedulerRecurrenceExpander.Expand(ev, Monday, Monday.AddDays(100_000));

        Assert.Equal(SchedulerRecurrenceExpander.AbsoluteCandidateCap, result.Count);
    }

    [Theory]
    [InlineData(SchedulerRecurrenceFrequency.Daily)]
    [InlineData(SchedulerRecurrenceFrequency.Weekly)]
    [InlineData(SchedulerRecurrenceFrequency.Monthly)]
    public void A_Series_Near_The_End_Of_Time_Advances_Without_Overflowing(SchedulerRecurrenceFrequency freq)
    {
        // The generators used to lean on the flat 500 to stay inside DateTime's range. With the
        // budget able to run far longer, each advance has to guard its own overflow instead.
        var start = DateTime.MaxValue.AddDays(-40);
        var ev = Base(start: start, end: start.AddHours(1),
                      recurrence: new SchedulerRecurrenceRule(freq, Interval: 1));

        var result = SchedulerRecurrenceExpander.Expand(ev, start, DateTime.MaxValue);

        Assert.NotEmpty(result);
    }

    [Fact]
    public void The_Budget_Never_Drops_Below_Its_Floor_For_A_Tiny_Window()
    {
        var rule = new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Daily, Interval: 1);

        Assert.Equal(SchedulerRecurrenceExpander.OccurrenceCap,
                     SchedulerRecurrenceExpander.EffectiveCandidateCap(rule, Monday, Monday.AddDays(3)));
    }

    // ── EXDATE / ExceptionDates ──────────────────────────────────────────────

    [Fact]
    public void ExceptionDates_Skips_The_Matching_Occurrence_But_Still_Consumes_A_Count_Slot()
    {
        var byDay = new[] { new SchedulerByDayRule(DayOfWeek.Monday) };
        var ev = Base(
            recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Weekly, Interval: 1, Count: 3, ByDay: byDay),
            exceptionDates: new[] { Monday.AddDays(7) }); // the 2nd of 3 counted occurrences is excluded
        var result = SchedulerRecurrenceExpander.Expand(ev, Monday, Monday.AddDays(60));

        // Only 2 instances come back (Mon week 1, Mon week 3) — the excluded week-2 Monday still
        // consumed COUNT's 2nd slot, so there is no 4th occurrence pulled in to "make up" for it.
        Assert.Equal(new[] { Monday, Monday.AddDays(14) }, result.Select(r => r.Start));
    }

    // ── Instance shape ───────────────────────────────────────────────────────

    [Fact]
    public void Instances_Preserve_The_Source_Events_TimeOfDay_And_Duration()
    {
        var start = new DateTime(2026, 8, 10, 14, 30, 0);
        var end = new DateTime(2026, 8, 10, 15, 45, 0); // 75-minute duration
        var ev = Base(start: start, end: end,
            recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Daily, Interval: 1, Count: 3));
        var result = SchedulerRecurrenceExpander.Expand(ev, start, start.AddDays(10));

        foreach (var instance in result)
        {
            Assert.Equal(new TimeSpan(14, 30, 0), instance.Start.TimeOfDay);
            Assert.Equal(TimeSpan.FromMinutes(75), instance.End - instance.Start);
        }
    }

    [Fact]
    public void Every_Instance_Carries_The_Source_Events_Id()
    {
        var ev = Base(id: "abc123", recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Daily, Interval: 1, Count: 3));
        var result = SchedulerRecurrenceExpander.Expand(ev, Monday, Monday.AddDays(10));

        Assert.All(result, r => Assert.Equal("abc123", r.EventId));
    }

    // ── ResolveRule / legacy translation ─────────────────────────────────────

    [Fact]
    public void ResolveRule_Translates_Legacy_DaysOfWeek_And_RecurrenceEnd_Into_Weekly_ByDay_Until()
    {
        var recurrenceEnd = Monday.AddDays(30);
        var ev = Base(daysOfWeek: new[] { DayOfWeek.Monday, DayOfWeek.Wednesday }, recurrenceEnd: recurrenceEnd);

        var rule = SchedulerRecurrenceExpander.ResolveRule(ev);

        Assert.NotNull(rule);
        Assert.Equal(SchedulerRecurrenceFrequency.Weekly, rule!.Freq);
        Assert.Equal(1, rule.Interval);
        Assert.Null(rule.Count);
        Assert.Equal(recurrenceEnd, rule.Until);
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Wednesday }, rule.ByDay!.Select(b => b.Day));
    }

    [Fact]
    public void ResolveRule_Prefers_Structured_Recurrence_Over_Legacy_DaysOfWeek_When_Both_Are_Set()
    {
        var structured = new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Daily, Interval: 2);
        var ev = Base(recurrence: structured, daysOfWeek: new[] { DayOfWeek.Friday });

        // ResolveRule deliberately fires a Debug.Assert for this ambiguous (both set) input
        // (spec §4.1: "flagged via a debug-build assertion — not a thrown exception"). In a
        // Debug-configuration test run, the test host's own trace listener turns a failed
        // Debug.Assert into a throwing exception so it can't pop a blocking dialog mid test-run
        // — that interception is a debug-build/test-host detail, not part of ResolveRule's
        // actual contract ("warn, don't crash"), and this repo has no existing precedent for
        // asserting on Debug.Assert firing directly. Swap in an empty trace-listener set for the
        // scope of this one call so the diagnostic's SIDE EFFECT doesn't interfere with testing
        // its MAIN return value; in a Release build (no DEBUG symbol — e.g. the actual CI/gate
        // build) Debug.Assert compiles away entirely and this listener swap is simply inert.
        var originalListeners = new System.Diagnostics.TraceListener[System.Diagnostics.Trace.Listeners.Count];
        System.Diagnostics.Trace.Listeners.CopyTo(originalListeners, 0);
        System.Diagnostics.Trace.Listeners.Clear();
        try
        {
            var rule = SchedulerRecurrenceExpander.ResolveRule(ev);
            Assert.Same(structured, rule);
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Clear();
            System.Diagnostics.Trace.Listeners.AddRange(originalListeners);
        }
    }

    [Fact]
    public void ResolveRule_Returns_Null_For_A_Plain_NonRecurring_Event()
    {
        Assert.Null(SchedulerRecurrenceExpander.ResolveRule(Base()));
    }

    [Fact]
    public void ResolveRule_Returns_Null_When_DaysOfWeek_Is_An_Empty_List()
    {
        Assert.Null(SchedulerRecurrenceExpander.ResolveRule(Base(daysOfWeek: Array.Empty<DayOfWeek>())));
    }

    // ── The parity test: one engine, two input shapes ────────────────────────

    /// <summary>
    /// Spec §4.1/§7.1's explicit requirement: prove — not just assert in prose — that the legacy
    /// <see cref="SchedulerEvent.DaysOfWeek"/>/<see cref="SchedulerEvent.RecurrenceEnd"/> input
    /// shape and the equivalent structured <see cref="SchedulerEvent.Recurrence"/> rule are
    /// expanded by the exact same underlying engine, by asserting the two produce byte-identical
    /// (Start, End) instance sequences over a real multi-month range.
    /// </summary>
    [Fact]
    public void LegacyDaysOfWeek_And_EquivalentStructuredRule_Produce_ByteIdentical_Instances()
    {
        var recurrenceEnd = Monday.AddDays(90);

        var legacy = Base(
            id: "legacy",
            daysOfWeek: new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday },
            recurrenceEnd: recurrenceEnd);

        var structuredByDay = new[]
        {
            new SchedulerByDayRule(DayOfWeek.Monday),
            new SchedulerByDayRule(DayOfWeek.Wednesday),
            new SchedulerByDayRule(DayOfWeek.Friday),
        };
        var structured = Base(
            id: "structured",
            recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Weekly, Interval: 1, Until: recurrenceEnd, ByDay: structuredByDay));

        var rangeStart = Monday.AddDays(-14);
        var rangeEnd = Monday.AddDays(120);

        var legacyInstances = SchedulerRecurrenceExpander.Expand(legacy, rangeStart, rangeEnd);
        var structuredInstances = SchedulerRecurrenceExpander.Expand(structured, rangeStart, rangeEnd);

        Assert.NotEmpty(legacyInstances);
        Assert.Equal(legacyInstances.Count, structuredInstances.Count);

        for (var i = 0; i < legacyInstances.Count; i++)
        {
            // "Byte-identical" modulo the deliberately-different EventId each source event
            // carries — Start/End must match exactly.
            Assert.Equal(legacyInstances[i].Start, structuredInstances[i].Start);
            Assert.Equal(legacyInstances[i].End, structuredInstances[i].End);
        }
    }

    /// <summary>Same parity property, but with EXDATE/ExceptionDates involved on both sides.</summary>
    [Fact]
    public void LegacyDaysOfWeek_And_EquivalentStructuredRule_Produce_ByteIdentical_Instances_With_Exceptions()
    {
        var recurrenceEnd = Monday.AddDays(60);
        var excluded = new[] { Monday.AddDays(7), Monday.AddDays(16) }; // 2nd Monday, 3rd Wednesday

        var legacy = Base(
            id: "legacy",
            daysOfWeek: new[] { DayOfWeek.Monday, DayOfWeek.Wednesday },
            recurrenceEnd: recurrenceEnd,
            exceptionDates: excluded);

        var structured = Base(
            id: "structured",
            recurrence: new SchedulerRecurrenceRule(
                SchedulerRecurrenceFrequency.Weekly, Interval: 1, Until: recurrenceEnd,
                ByDay: new[] { new SchedulerByDayRule(DayOfWeek.Monday), new SchedulerByDayRule(DayOfWeek.Wednesday) }),
            exceptionDates: excluded);

        var legacyInstances = SchedulerRecurrenceExpander.Expand(legacy, Monday, Monday.AddDays(90));
        var structuredInstances = SchedulerRecurrenceExpander.Expand(structured, Monday, Monday.AddDays(90));

        Assert.NotEmpty(legacyInstances);
        Assert.Equal(
            legacyInstances.Select(i => (i.Start, i.End)),
            structuredInstances.Select(i => (i.Start, i.End)));
    }

    [Fact]
    public void A_Monthly_Rule_With_Several_Ordinals_Of_One_Weekday_Fills_A_Long_Axis()
    {
        // MonthlyCandidates resolves each ByDay ENTRY, so first-through-fourth Monday is four
        // occurrences a month. Budgeting by DISTINCT weekday counted one, left the cap at its
        // floor, and stopped a long axis a third of the way in (Codex review of PR #424).
        var byDay = new[]
        {
            new SchedulerByDayRule(DayOfWeek.Monday, 1),
            new SchedulerByDayRule(DayOfWeek.Monday, 2),
            new SchedulerByDayRule(DayOfWeek.Monday, 3),
            new SchedulerByDayRule(DayOfWeek.Monday, 4),
        };
        var ev = Base(recurrence: new SchedulerRecurrenceRule(
            SchedulerRecurrenceFrequency.Monthly, Interval: 1, ByDay: byDay));

        var result = SchedulerRecurrenceExpander.Expand(ev, Monday, Monday.AddMonths(300));

        Assert.True(result.Count > 1_000,
            $"300 months x 4 Mondays should be ~1200 occurrences, got {result.Count}.");
    }

    [Fact]
    public void A_Monthly_Series_Starting_In_The_Final_Year_Still_Advances()
    {
        // The overflow bound counted whole years only, so every month inside 9999 read as
        // "no room left" and the series yielded its first occurrence alone.
        var start = new DateTime(9999, 1, 15, 9, 0, 0);
        var ev = Base(start: start, end: start.AddHours(1),
                      recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Monthly, Interval: 1));

        var result = SchedulerRecurrenceExpander.Expand(ev, start, new DateTime(9999, 4, 1));

        Assert.Equal(3, result.Count);   // January, February, March
    }

    [Fact]
    public void An_Occurrence_At_The_End_Of_Time_Clamps_Its_End_Instead_Of_Throwing()
    {
        // With the budget able to reach the last representable day, candidateStart + duration
        // overflowed. The old flat cap stopped decades short of it.
        var start = DateTime.MaxValue.AddDays(-3).AddHours(-2);
        var ev = Base(start: start, end: start.AddHours(4),
                      recurrence: new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Daily, Interval: 1));

        var result = SchedulerRecurrenceExpander.Expand(ev, start, DateTime.MaxValue);

        Assert.NotEmpty(result);
        Assert.All(result, r => Assert.True(r.End <= DateTime.MaxValue));
    }
}
