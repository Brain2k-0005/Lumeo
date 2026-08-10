using Lumeo.SchedulerKernel;
using Xunit;

namespace Lumeo.Tests.Components.SchedulerKernel;

/// <summary>
/// Pure-data tests for <see cref="SchedulerMonthLayout.PackRow"/>/<see cref="SchedulerMonthLayout.HiddenCounts"/>
/// — spec §1.4's exact test matrix: mid-row start, cross-row-boundary independence, per-day
/// hidden count, lane reuse after an event ends.
/// </summary>
public class SchedulerMonthLayoutTests
{
    // A Monday-start week: Mon 2026-08-10 .. Sun 2026-08-16.
    private static readonly DateTime WeekStart = new(2026, 8, 10);

    private static DateTime Day(int offset) => WeekStart.AddDays(offset);

    [Fact]
    public void Empty_Input_Produces_Empty_Lanes()
    {
        var lanes = SchedulerMonthLayout.PackRow(WeekStart, Array.Empty<(string, DateTime, DateTime, bool)>());
        Assert.Empty(lanes);
    }

    [Fact]
    public void A_Three_Day_Event_Starting_Mid_Row_Gets_A_Single_Lane_For_Its_Whole_Span()
    {
        // Wed(2)-Fri(4), exclusive end = Sat(5).
        var events = new (string, DateTime, DateTime, bool)[]
        {
            ("mid", Day(2), Day(5), true),
        };

        var lanes = SchedulerMonthLayout.PackRow(WeekStart, events);

        Assert.Equal(0, lanes["mid"]);
    }

    [Fact]
    public void Two_Non_Overlapping_MultiDay_Events_Can_Share_Lane_Zero()
    {
        // Mon-Tue (excl end Wed) and Thu-Fri (excl end Sat): disjoint day-cells, no conflict.
        var events = new (string, DateTime, DateTime, bool)[]
        {
            ("first", Day(0), Day(2), true),
            ("second", Day(3), Day(5), true),
        };

        var lanes = SchedulerMonthLayout.PackRow(WeekStart, events);

        Assert.Equal(0, lanes["first"]);
        Assert.Equal(0, lanes["second"]);
    }

    [Fact]
    public void Two_Overlapping_MultiDay_Events_Get_Different_Lanes()
    {
        var events = new (string, DateTime, DateTime, bool)[]
        {
            ("a", Day(0), Day(3), true), // Mon-Wed
            ("b", Day(1), Day(4), true), // Tue-Thu, overlaps a on Tue/Wed
        };

        var lanes = SchedulerMonthLayout.PackRow(WeekStart, events);

        Assert.NotEqual(lanes["a"], lanes["b"]);
    }

    [Fact]
    public void Lane_Freed_Up_Mid_Row_By_An_Event_Ending_Is_Reused_By_A_Later_Event()
    {
        // "a" occupies Mon-Tue only (lane 0). "b" occupies Wed-Fri and overlaps nothing "a"
        // touches, so it should ALSO land on lane 0 — proving lane reuse is computed per
        // day-cell, not "lane busy for the whole row" once any event claims it.
        var events = new (string, DateTime, DateTime, bool)[]
        {
            ("a", Day(0), Day(2), true), // Mon-Tue
            ("b", Day(2), Day(5), true), // Wed-Fri
        };

        var lanes = SchedulerMonthLayout.PackRow(WeekStart, events);

        Assert.Equal(0, lanes["a"]);
        Assert.Equal(0, lanes["b"]);
    }

    [Fact]
    public void A_Later_Event_That_Still_Overlaps_An_Earlier_One_Takes_The_Next_Free_Lane_Not_Lane_Zero()
    {
        // "a" spans the whole row (Mon-Sun). "b" only touches Wed-Thu but conflicts with "a" on
        // those days, so "b" must NOT reuse lane 0 even though lane 0 is free on every day BUT
        // the ones "a" occupies... which is every day, since "a" spans the full row.
        var events = new (string, DateTime, DateTime, bool)[]
        {
            ("a", Day(0), Day(7), true), // Mon-Sun (full row)
            ("b", Day(2), Day(4), true), // Wed-Thu
        };

        var lanes = SchedulerMonthLayout.PackRow(WeekStart, events);

        Assert.Equal(0, lanes["a"]);
        Assert.Equal(1, lanes["b"]);
    }

    [Fact]
    public void MultiDay_Events_Are_Assigned_Before_Single_Day_Timed_Events_Even_When_Their_Own_Literal_Start_Time_Is_Later()
    {
        // "bar" (multi-day, Tue-Thu) carries a LATER literal clock time (20:00) than "early"
        // (single-day timed, Tue 06:00-07:00) — a sort keyed purely on literal StartDate would
        // put "early" first. The multi-day/all-day group must still be assigned FIRST regardless
        // (spec §1.4 step 1: "wider things first"), landing on lane 0 and pushing "early" (which
        // conflicts with it on Tuesday) to lane 1 — proving the grouping itself, not just
        // incidental time ordering, is what's load-bearing here. See the task's disable-check:
        // collapsing the multi-day/single-day grouping into one plain start-time sort reproduces
        // exactly the reversed assignment (early=lane0, bar=lane1).
        var events = new (string, DateTime, DateTime, bool)[]
        {
            ("early", Day(1).AddHours(6), Day(1).AddHours(7), false), // Tue 06:00-07:00
            ("bar", Day(1).AddHours(20), Day(4), true),               // Tue 20:00 (clamped date) -> Thu, all-day/multi-day
        };

        var lanes = SchedulerMonthLayout.PackRow(WeekStart, events);

        Assert.Equal(0, lanes["bar"]);
        Assert.Equal(1, lanes["early"]);
    }

    [Fact]
    public void Single_Day_Timed_Events_Are_Ordered_By_Start_Time_Ascending()
    {
        // Two single-day timed events on the SAME day: the earlier one gets the lower lane once
        // both need distinct lanes (they conflict on the same day-cell, no all-day/multi-day
        // events present to occupy lane 0 first).
        var events = new (string, DateTime, DateTime, bool)[]
        {
            ("late", Day(0).AddHours(14), Day(0).AddHours(15), false),
            ("early", Day(0).AddHours(9), Day(0).AddHours(10), false),
        };

        var lanes = SchedulerMonthLayout.PackRow(WeekStart, events);

        Assert.Equal(0, lanes["early"]);
        Assert.Equal(1, lanes["late"]);
    }

    [Fact]
    public void ThreeDay_Event_Spanning_Two_WeekRows_Is_Computed_Independently_Per_Row()
    {
        // A single logical multi-day event crosses a week boundary. The caller clamps it per-row
        // (spec §1.4's own input contract) — this test proves PackRow itself makes no attempt to
        // preserve lane continuity ACROSS that boundary: the SAME event id ("crossing") lands on
        // lane 0 in one row (nothing else present to conflict with) and lane 1 in the other
        // (deliberately forced there by a conflicting event) — entirely independent per call.
        var priorRowEvents = new (string, DateTime, DateTime, bool)[]
        {
            ("crossing", Day(5), Day(7), true), // clamped to Sat-Sun of the "prior" row — no conflicts here.
        };
        var priorLanes = SchedulerMonthLayout.PackRow(WeekStart, priorRowEvents);

        // Same start day and same span length as "crossing" below, listed first: the sort tie
        // (start ascending, then span length descending) is stable, so "blocker" — earlier in
        // input order — is assigned first and claims lane 0, pushing "crossing" to lane 1.
        var nextWeekStart = WeekStart.AddDays(7);
        var nextRowEvents = new (string, DateTime, DateTime, bool)[]
        {
            ("blocker", nextWeekStart, nextWeekStart.AddDays(2), true),  // Mon-Tue of the next row.
            ("crossing", nextWeekStart, nextWeekStart.AddDays(2), true), // same event id, clamped to Mon-Tue of the "next" row — conflicts with "blocker".
        };
        var nextLanes = SchedulerMonthLayout.PackRow(nextWeekStart, nextRowEvents);

        Assert.Equal(0, priorLanes["crossing"]);
        Assert.Equal(1, nextLanes["crossing"]); // different lane in the next row — no cross-row continuity guaranteed.
    }

    // ── HiddenCounts ─────────────────────────────────────────────────────────

    [Fact]
    public void HiddenCounts_Is_Zero_Everywhere_When_Under_The_Visible_Lane_Budget()
    {
        var events = new (string, DateTime, DateTime, bool)[]
        {
            ("a", Day(0), Day(1), true),
        };
        var lanes = SchedulerMonthLayout.PackRow(WeekStart, events);
        var hidden = SchedulerMonthLayout.HiddenCounts(WeekStart, lanes, events);

        Assert.All(hidden.Values, v => Assert.Equal(0, v));
    }

    [Fact]
    public void HiddenCounts_Only_Flags_The_Crowded_Day_Not_The_Whole_Row()
    {
        // 4 events all landing on Monday only (lanes 0-3) exceed the default MaxVisibleLanes=3 by
        // exactly 1 on Monday; every other day in the row has zero events and must read 0.
        var events = new (string, DateTime, DateTime, bool)[]
        {
            ("a", Day(0), Day(1), true),
            ("b", Day(0), Day(1), true),
            ("c", Day(0), Day(1), true),
            ("d", Day(0), Day(1), true),
        };
        var lanes = SchedulerMonthLayout.PackRow(WeekStart, events);
        var hidden = SchedulerMonthLayout.HiddenCounts(WeekStart, lanes, events);

        Assert.Equal(1, hidden[Day(0)]);
        for (var i = 1; i < 7; i++)
            Assert.Equal(0, hidden[Day(i)]);
    }

    [Fact]
    public void HiddenCounts_Respects_A_Custom_MaxVisibleLanes()
    {
        var events = new (string, DateTime, DateTime, bool)[]
        {
            ("a", Day(0), Day(1), true),
            ("b", Day(0), Day(1), true),
        };
        var lanes = SchedulerMonthLayout.PackRow(WeekStart, events);
        var hidden = SchedulerMonthLayout.HiddenCounts(WeekStart, lanes, events, maxVisibleLanes: 1);

        Assert.Equal(1, hidden[Day(0)]);
    }

    // ── DayCellSpan semantics (via PackRow/HiddenCounts observable behavior) ──

    [Fact]
    public void AllDay_Single_Day_Event_Occupies_Exactly_One_Day_Cell()
    {
        var events = new (string, DateTime, DateTime, bool)[]
        {
            ("allday", Day(3), Day(4), true), // Thu only
            ("thuOnly", Day(3), Day(4), true),
        };
        var lanes = SchedulerMonthLayout.PackRow(WeekStart, events);
        // Both fully overlap on Thu -> distinct lanes.
        Assert.NotEqual(lanes["allday"], lanes["thuOnly"]);

        var hidden = SchedulerMonthLayout.HiddenCounts(WeekStart, lanes, events, maxVisibleLanes: 0);
        Assert.Equal(2, hidden[Day(3)]);
        Assert.Equal(0, hidden[Day(2)]); // Wed untouched
    }

    [Fact]
    public void CrossMidnight_Timed_Event_Touches_Only_The_Days_Its_Exclusive_End_Actually_Reaches()
    {
        // Mon 22:00 -> Wed 00:00 (exact midnight): exclusive end means Wed is NOT touched, only
        // Mon and Tue. A second event occupying only Wed must therefore NOT conflict.
        var events = new (string, DateTime, DateTime, bool)[]
        {
            ("overnight", Day(0).AddHours(22), Day(2), false), // Mon 22:00 -> Wed 00:00
            ("wed", Day(2), Day(3), true),                     // Wed only
        };

        var lanes = SchedulerMonthLayout.PackRow(WeekStart, events);

        Assert.Equal(0, lanes["overnight"]);
        Assert.Equal(0, lanes["wed"]); // no conflict -> both can be lane 0
    }

    [Fact]
    public void CrossMidnight_Timed_Event_With_A_NonMidnight_End_Does_Touch_The_Final_Day()
    {
        // Mon 22:00 -> Wed 01:00 (NOT exact midnight): this time the span DOES partially touch
        // Wed, so an event on Wed must conflict and take a different lane.
        var events = new (string, DateTime, DateTime, bool)[]
        {
            ("overnight", Day(0).AddHours(22), Day(2).AddHours(1), false), // Mon 22:00 -> Wed 01:00
            ("wed", Day(2), Day(3), true),                                 // Wed only
        };

        var lanes = SchedulerMonthLayout.PackRow(WeekStart, events);

        Assert.NotEqual(lanes["overnight"], lanes["wed"]);
    }
}
