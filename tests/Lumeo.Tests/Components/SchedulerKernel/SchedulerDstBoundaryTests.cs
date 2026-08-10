using Lumeo.SchedulerKernel;
using Xunit;

namespace Lumeo.Tests.Components.SchedulerKernel;

/// <summary>
/// Spec §2.3/§7.3: the kernel's whole date/time design is calendar-field-only arithmetic with
/// zero <see cref="TimeZoneInfo"/> calls, specifically so DST transitions (and the CI
/// Ubuntu/UTC-vs-local-Windows-timezone divergence that already caused one CI-only failure this
/// session) cannot perturb it. These tests feed the kernel real 2026 DST-transition dates — US
/// spring-forward 2026-03-08 (02:00 -&gt; 03:00, the 02:00-02:59 hour doesn't exist that day) and
/// EU fall-back 2026-10-25 (02:00 repeats) — and assert wall-clock behavior, not elapsed-time
/// behavior. None of these need the TEST RUNNER's own timezone manipulated (spec §7.3 rule 2):
/// every <see cref="DateTime"/> here is <see cref="DateTimeKind.Utc"/>-tagged defensively (the
/// kernel itself is <see cref="DateTime.Kind"/>-agnostic — see each kernel class's own TZ/DST
/// remarks — so this is a belt-and-suspenders habit, not a requirement) so a future accidental
/// <c>ToLocalTime()</c>/<c>ToUniversalTime()</c> call introduced into the kernel would visibly
/// change these results instead of silently matching by accident on a UTC-configured CI box.
/// </summary>
public class SchedulerDstBoundaryTests
{
    private static DateTime Utc(int y, int m, int d, int h = 0, int min = 0) => new(y, m, d, h, min, 0, DateTimeKind.Utc);

    // ── Spring-forward: US, 2026-03-08, 02:00 -> 03:00 (02:00-02:59 doesn't exist locally) ────

    [Fact]
    public void Weekly_Recurrence_Renders_The_Skipped_SpringForward_Hour_At_Its_Stored_WallClock_Position()
    {
        // A weekly Sunday 02:30 event. 2026-03-08 is a Sunday and IS the US spring-forward date.
        // Spec §2.3: the calendar does not try to "resolve" this — it renders the occurrence at
        // its literal stored wall-clock time like any other, exactly like Google Calendar/Outlook.
        var dtstart = Utc(2026, 2, 22, 2, 30); // a Sunday, two weeks before the transition
        Assert.Equal(DayOfWeek.Sunday, dtstart.DayOfWeek);

        var ev = new SchedulerEvent("dst-spring", "Standup", dtstart, dtstart.AddMinutes(30))
        {
            Recurrence = new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Weekly, Interval: 1, Count: 4)
        };

        var result = SchedulerRecurrenceExpander.Expand(ev, dtstart, dtstart.AddDays(30));

        var transitionDayOccurrence = Assert.Single(result, r => r.Start.Date == Utc(2026, 3, 8, 0, 0).Date);
        Assert.Equal(new TimeSpan(2, 30, 0), transitionDayOccurrence.Start.TimeOfDay);
        Assert.Equal(new TimeSpan(3, 0, 0), transitionDayOccurrence.End.TimeOfDay);
    }

    [Fact]
    public void SchedulerDateMath_Grid_For_The_SpringForward_Month_Is_An_Ordinary_42Cell_Grid()
    {
        var grid = SchedulerDateMath.BuildMonthGrid(Utc(2026, 3, 1), DayOfWeek.Monday);

        Assert.Equal(SchedulerDateMath.MonthGridCellCount, grid.Count);
        for (var d = 1; d <= 31; d++)
            Assert.Contains(Utc(2026, 3, d).Date, grid.Select(c => c.Date));
    }

    // ── Fall-back: EU, 2026-10-25, 02:00 repeats ────────────────────────────────

    [Fact]
    public void Weekly_Recurrence_Renders_The_Repeated_FallBack_Hour_Exactly_Once()
    {
        // 2026-10-25 is a Sunday and IS the EU fall-back date (local 02:00 repeats). The wall-
        // clock-only expander has no notion of "which" 02:30 this is — it emits exactly one
        // occurrence at 02:30 that day, never two, because it never reasons about elapsed local
        // time at all.
        var dtstart = Utc(2026, 10, 11, 2, 30); // a Sunday, two weeks before the transition
        Assert.Equal(DayOfWeek.Sunday, dtstart.DayOfWeek);

        var ev = new SchedulerEvent("dst-fallback", "Standup", dtstart, dtstart.AddMinutes(30))
        {
            Recurrence = new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Weekly, Interval: 1, Count: 4)
        };

        var result = SchedulerRecurrenceExpander.Expand(ev, dtstart, dtstart.AddDays(30));

        var onTransitionDay = result.Where(r => r.Start.Date == Utc(2026, 10, 25, 0, 0).Date).ToList();
        var occurrence = Assert.Single(onTransitionDay);
        Assert.Equal(new TimeSpan(2, 30, 0), occurrence.Start.TimeOfDay);
    }

    [Fact]
    public void SchedulerDateMath_Grid_For_The_FallBack_Month_Is_An_Ordinary_42Cell_Grid()
    {
        var grid = SchedulerDateMath.BuildMonthGrid(Utc(2026, 10, 1), DayOfWeek.Monday);

        Assert.Equal(SchedulerDateMath.MonthGridCellCount, grid.Count);
        for (var d = 1; d <= 31; d++)
            Assert.Contains(Utc(2026, 10, d).Date, grid.Select(c => c.Date));
    }

    // ── SchedulerTimeGridLayout: purely minute-of-day ints, structurally DST-immune ────────────

    [Fact]
    public void TimeGridLayout_Around_The_SpringForward_Hour_Is_Ordinary_Minute_Arithmetic()
    {
        // Pack operates on plain (int StartMinute, int EndMinute) — there is no DateTime, no
        // date, no timezone concept reachable from this method at all, so "the DST day" isn't
        // even an input it could special-case. Two events straddling minute 120-180 (02:00-03:00)
        // pack exactly like they would on any other day.
        var input = new (string, int, int)[]
        {
            ("a", 90, 150),  // 01:30-02:30
            ("b", 120, 180), // 02:00-03:00
        };

        var result = SchedulerTimeGridLayout.Pack(input).ToDictionary(r => r.Id, r => r.Column);

        Assert.NotEqual(result["a"], result["b"]); // they overlap (90-150 vs 120-180) -> distinct columns, same as any other pair of overlapping minutes.
    }
}
