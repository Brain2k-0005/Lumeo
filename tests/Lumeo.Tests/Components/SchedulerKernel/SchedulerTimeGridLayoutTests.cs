using Lumeo.SchedulerKernel;
using Xunit;

namespace Lumeo.Tests.Components.SchedulerKernel;

/// <summary>
/// Pure-data tests for <see cref="SchedulerTimeGridLayout.Pack"/> — spec §1.3's exact test
/// matrix: no-overlap, full-overlap, staircase/transitive cluster, nested event, exact-boundary
/// non-overlap, zero-duration guard.
/// </summary>
public class SchedulerTimeGridLayoutTests
{
    private static Dictionary<string, (int Column, int ColumnsInCluster)> ById(
        IReadOnlyList<(string Id, int Column, int ColumnsInCluster)> packed) =>
        packed.ToDictionary(p => p.Id, p => (p.Column, p.ColumnsInCluster));

    [Fact]
    public void Empty_Input_Produces_Empty_Output()
    {
        var result = SchedulerTimeGridLayout.Pack(Array.Empty<(string, int, int)>());
        Assert.Empty(result);
    }

    [Fact]
    public void Non_Overlapping_Events_All_Get_Column_Zero_Of_One()
    {
        var input = new (string, int, int)[]
        {
            ("a", 0, 60),
            ("b", 60, 120),
            ("c", 120, 180),
        };

        var result = ById(SchedulerTimeGridLayout.Pack(input));

        Assert.Equal((0, 1), result["a"]);
        Assert.Equal((0, 1), result["b"]);
        Assert.Equal((0, 1), result["c"]);
    }

    [Fact]
    public void Two_Fully_Overlapping_Events_Get_Two_Columns()
    {
        var input = new (string, int, int)[]
        {
            ("a", 0, 60),
            ("b", 0, 60),
        };

        var result = ById(SchedulerTimeGridLayout.Pack(input));

        Assert.Equal(2, result["a"].ColumnsInCluster);
        Assert.Equal(2, result["b"].ColumnsInCluster);
        Assert.NotEqual(result["a"].Column, result["b"].Column);
        Assert.Equal(new[] { 0, 1 }, new[] { result["a"].Column, result["b"].Column }.OrderBy(x => x));
    }

    [Fact]
    public void Staircase_Of_Three_Events_Each_Overlapping_Only_Its_Neighbor_Stays_One_Transitive_Cluster()
    {
        // a: 0-90, b: 60-150, c: 120-210. a/b overlap (60-90), b/c overlap (120-150), a/c do NOT
        // directly overlap (a ends at 90, c starts at 120).
        //
        // Verified against the uploaded demo's own layoutTimed() (samples/Gantt_EventCalendar/
        // scheduler-demo.html, ~line 988), which this method is a direct port of: because c's
        // start (120) is still before the RUNNING cluster envelope (150, extended by b) at the
        // moment c is processed, c joins the SAME cluster as a/b rather than starting a fresh one
        // — even though c is free to reuse column 0 (a's column) since a has already finished
        // (a ends at 90, before c's start at 120). The transitive-cluster rule's actual payoff is
        // NOT "c always gets a 3rd column" (a literal a/c non-overlap always makes column 0
        // legitimately reusable, so a correct greedy packer gives this 2 columns, not 3) — it's
        // that c's ColumnsInCluster stays 2 (matching a and b's own shared width) instead of a
        // WRONG naive implementation treating c as an unrelated single-column cluster of its own
        // (ColumnsInCluster=1, rendering c at full width while a/b render at half width — a real,
        // visible layout bug a "does c belong to the same cluster" check catches and this test
        // exists to pin down).
        var input = new (string, int, int)[]
        {
            ("a", 0, 90),
            ("b", 60, 150),
            ("c", 120, 210),
        };

        var result = ById(SchedulerTimeGridLayout.Pack(input));

        // All three share ONE cluster width (2 columns) — not c alone at width 1.
        Assert.Equal(2, result["a"].ColumnsInCluster);
        Assert.Equal(2, result["b"].ColumnsInCluster);
        Assert.Equal(2, result["c"].ColumnsInCluster);

        // b directly conflicts with both a and c, so it can never share their column.
        Assert.NotEqual(result["a"].Column, result["b"].Column);
        Assert.NotEqual(result["b"].Column, result["c"].Column);
        // a and c legitimately reuse the same column (a has already ended by the time c starts).
        Assert.Equal(result["a"].Column, result["c"].Column);
    }

    [Fact]
    public void A_Long_Event_Enveloping_A_Short_One_Keeps_Its_Own_Envelope_When_A_Later_Event_Arrives()
    {
        // a: 0-200 (long). b: 50-100 (short, nested inside a). c: 180-250.
        // a/b overlap (50-100). a/c overlap TOO (180-200) — unlike the staircase test above, c
        // genuinely conflicts with a, not just with b. This is the case that specifically
        // exercises `clusterEnd = Math.Max(clusterEnd, ev.EndMinute)`: after processing b (whose
        // own end, 100, is SMALLER than a's still-running 200), the cluster envelope must stay at
        // 200 (a's end), not shrink to 100 — otherwise c's start (180) would wrongly look like
        // it's past the envelope (180 >= 100) and get flushed into a brand-new cluster, landing
        // c on column 0 — the SAME column as "a" — even though a and c actually overlap. That
        // would be a real, visible rendering bug (two genuinely overlapping events drawn in the
        // same column). See the task's disable-check: replacing Math.Max with a plain overwrite
        // reproduces exactly that regression (c.Column becomes 0, c.ColumnsInCluster becomes 1,
        // colliding with "a").
        var input = new (string, int, int)[]
        {
            ("a", 0, 200),
            ("b", 50, 100),
            ("c", 180, 250),
        };

        var result = ById(SchedulerTimeGridLayout.Pack(input));

        // One cluster, 2 columns (b and c legitimately share a column — they don't overlap each
        // other — while a takes the other).
        Assert.Equal(2, result["a"].ColumnsInCluster);
        Assert.Equal(2, result["b"].ColumnsInCluster);
        Assert.Equal(2, result["c"].ColumnsInCluster);

        // a and c genuinely overlap (180-200) -> must NOT share a column.
        Assert.NotEqual(result["a"].Column, result["c"].Column);
    }

    [Fact]
    public void An_Event_Nested_Entirely_Inside_Another_Gets_Its_Own_Column()
    {
        var input = new (string, int, int)[]
        {
            ("outer", 0, 180),
            ("inner", 60, 90),
        };

        var result = ById(SchedulerTimeGridLayout.Pack(input));

        Assert.Equal(2, result["outer"].ColumnsInCluster);
        Assert.Equal(2, result["inner"].ColumnsInCluster);
        Assert.NotEqual(result["outer"].Column, result["inner"].Column);
    }

    [Fact]
    public void Zero_Duration_Event_Does_Not_Throw_And_Gets_A_Valid_Single_Column_Slot()
    {
        var input = new (string, int, int)[] { ("a", 60, 60) };

        var result = ById(SchedulerTimeGridLayout.Pack(input));

        Assert.Equal((0, 1), result["a"]);
    }

    [Fact]
    public void Zero_Duration_Event_Starting_Exactly_When_The_Prior_One_Ends_Does_Not_Overlap()
    {
        // A normal event [0,60) followed by a zero-duration event at [60,60): the open-interval
        // boundary rule (end == start is not overlap) must apply here too. Ordered this way
        // (prior event starts earlier, so the sort's start-ascending key alone — no end-descending
        // tie-break involved) keeps the scenario unambiguous; see the sort-tie-break test above
        // for why a same-START zero-duration pairing needs separate reasoning.
        var input = new (string, int, int)[]
        {
            ("prior", 0, 60),
            ("zero", 60, 60),
        };

        var result = ById(SchedulerTimeGridLayout.Pack(input));

        Assert.Equal((0, 1), result["prior"]);
        Assert.Equal((0, 1), result["zero"]);
    }

    [Fact]
    public void Exact_Boundary_End_Equals_Start_Is_Not_Overlap()
    {
        // Event a ends exactly when b starts: spec §1.3 explicitly requires this NOT be treated
        // as overlapping (open interval at the cluster-flush boundary: `>=`, not `>`).
        var input = new (string, int, int)[]
        {
            ("a", 0, 60),
            ("b", 60, 120),
        };

        var result = ById(SchedulerTimeGridLayout.Pack(input));

        Assert.Equal((0, 1), result["a"]);
        Assert.Equal((0, 1), result["b"]);
    }

    [Fact]
    public void Sort_Order_Breaks_Start_Ties_By_Longer_Event_First()
    {
        // Two events with the same start: the longer one (later end) sorts first and therefore
        // claims column 0.
        var input = new (string, int, int)[]
        {
            ("short", 0, 30),
            ("long", 0, 90),
        };

        var packed = SchedulerTimeGridLayout.Pack(input);
        // Processing order itself is the sorted order (documented on Pack): long first.
        Assert.Equal("long", packed[0].Id);
        Assert.Equal(0, packed[0].Column);
        Assert.Equal("short", packed[1].Id);
        Assert.Equal(1, packed[1].Column);
    }
}
