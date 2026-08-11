using Lumeo.GanttV3;
using Xunit;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Regression tests for <see cref="GanttRollupModel"/> — the pure rollup-
/// computation logic feeding <see cref="GanttSummaryBar"/> (design spec Phase
/// 3, T3: "Duration-weighted progress rollup per parent/group row" +
/// "Overridable rollup math"). Covers: default duration-weighted math, the
/// zero-duration-milestone guard, transitive (multi-level) recursion, the
/// "hierarchy beats GroupBy" branch (ledger-pinned decision, T3 note 102),
/// and the defensive contract around a hostile/throwing <c>RollupMath</c>
/// override — see <see cref="GanttRowModelTests"/> for the sibling row-model
/// coverage this mirrors in style.
/// </summary>
public class GanttRollupModelTests
{
    private static DateTime D(int y, int m, int d) => new(y, m, d);

    private static GanttTask Task(string id, DateTime start, DateTime end, int progress = 0,
        string? parentId = null, string? groupLabel = null, bool isMilestone = false) =>
        new(id, id, start, end, progress, IsMilestone: isMilestone) { ParentId = parentId, GroupLabel = groupLabel };

    // ── Default math: duration weighting ────────────────────────────────────

    [Fact]
    public void DefaultRollupMath_Weights_Progress_By_Calendar_Day_Duration()
    {
        // c1: 4 days @ 100%, c2: 10 days @ 50% -> (4*100 + 10*50) / (4+10) = 900/14
        var children = new[]
        {
            Task("c1", D(2026, 3, 1), D(2026, 3, 5), progress: 100),
            Task("c2", D(2026, 3, 5), D(2026, 3, 15), progress: 50),
        };

        var rollup = GanttRollupModel.DefaultRollupMath(children);

        Assert.Equal(900.0 / 14.0, rollup.WeightedProgress, 10);
        Assert.Equal(D(2026, 3, 1), rollup.Start);
        Assert.Equal(D(2026, 3, 15), rollup.End);
    }

    [Fact]
    public void DefaultRollupMath_Start_End_Span_The_Min_And_Max_Of_All_Children_Regardless_Of_List_Order()
    {
        var children = new[]
        {
            Task("c1", D(2026, 5, 1), D(2026, 5, 3)),
            Task("c2", D(2026, 3, 1), D(2026, 3, 2)), // earliest start
            Task("c3", D(2026, 4, 1), D(2026, 6, 1)), // latest end
        };

        var rollup = GanttRollupModel.DefaultRollupMath(children);

        Assert.Equal(D(2026, 3, 1), rollup.Start);
        Assert.Equal(D(2026, 6, 1), rollup.End);
    }

    // ── Zero-duration milestone guard (T3 decision #3) ──────────────────────

    [Fact]
    public void DefaultRollupMath_All_Milestone_Children_Do_Not_Divide_By_Zero()
    {
        var children = new[]
        {
            Task("m1", D(2026, 3, 1), D(2026, 3, 1), progress: 100, isMilestone: true),
            Task("m2", D(2026, 3, 10), D(2026, 3, 10), progress: 0, isMilestone: true),
        };

        var rollup = GanttRollupModel.DefaultRollupMath(children);

        // Every child floors to the SAME MinimumWeightDays (1.0), so this
        // degenerates to a plain, unweighted mean: (100+0)/2 = 50.
        Assert.Equal(50.0, rollup.WeightedProgress, 10);
        Assert.False(double.IsNaN(rollup.WeightedProgress));
        // The milestones still widen the envelope — neither "vanishes".
        Assert.Equal(D(2026, 3, 1), rollup.Start);
        Assert.Equal(D(2026, 3, 10), rollup.End);
    }

    [Fact]
    public void DefaultRollupMath_Milestone_Child_Contributes_A_Floored_Weight_Not_A_Zero_One()
    {
        // DISABLE-CHECK fixture: a real 10-day task at 0% alongside a
        // milestone at 100%. With the floor (weight=1 for the milestone):
        // (10*0 + 1*100) / (10+1) = 100/11 ≈ 9.09%. Disabling the floor (i.e.
        // weighting the milestone at its LITERAL 0-day duration) would instead
        // compute (10*0 + 0*100) / (10+0) = 0/10 = 0% exactly — a concretely
        // different, predictable wrong value this test would catch.
        var children = new[]
        {
            Task("c1", D(2026, 3, 1), D(2026, 3, 11), progress: 0),
            Task("ms", D(2026, 3, 20), D(2026, 3, 20), progress: 100, isMilestone: true),
        };

        var rollup = GanttRollupModel.DefaultRollupMath(children);

        Assert.Equal(100.0 / 11.0, rollup.WeightedProgress, 10);
    }

    [Fact]
    public void DefaultRollupMath_Empty_Children_Does_Not_Throw()
    {
        var rollup = GanttRollupModel.DefaultRollupMath(Array.Empty<GanttTask>());
        Assert.Equal(default, rollup);
    }

    // ── ComputeRollups: keying + hierarchy-vs-group branching ───────────────

    [Fact]
    public void ComputeRollups_Hierarchy_Parent_Is_Keyed_By_Its_Own_TaskId()
    {
        var tasks = new[]
        {
            Task("p1", D(2026, 1, 1), D(2026, 1, 10)),
            Task("c1", D(2026, 1, 1), D(2026, 1, 5), progress: 100, parentId: "p1"),
            Task("c2", D(2026, 1, 5), D(2026, 1, 10), progress: 0, parentId: "p1"),
        };

        var rollups = GanttRollupModel.ComputeRollups(tasks, null);

        Assert.True(rollups.ContainsKey("p1"));
        Assert.False(rollups.ContainsKey("c1")); // leaf, never a rollup key
        Assert.Single(rollups);
    }

    [Fact]
    public void ComputeRollups_Group_Header_Is_Keyed_By_GroupToggleKey()
    {
        var tasks = new[]
        {
            Task("t1", D(2026, 1, 1), D(2026, 1, 5), progress: 100, groupLabel: "Design"),
            Task("t2", D(2026, 1, 5), D(2026, 1, 10), progress: 0, groupLabel: "Design"),
            Task("t3", D(2026, 1, 1), D(2026, 1, 3)), // ungrouped — no rollup
        };

        var rollups = GanttRollupModel.ComputeRollups(tasks, null);

        var key = GanttRowModel.GroupToggleKey("Design");
        Assert.True(rollups.ContainsKey(key));
        Assert.Single(rollups);
        // t1: 4 days @ 100%, t2: 5 days @ 0% -> (4*100 + 5*0) / 9 = 400/9.
        Assert.Equal(400.0 / 9.0, rollups[key].WeightedProgress, 10);
    }

    [Fact]
    public void ComputeRollups_Hierarchy_Wins_Over_GroupBy_When_A_Task_List_Sets_Both()
    {
        // T3 decision #2 — ledger-pinned "Hierarchie schlaegt GroupBy" (T3
        // controller note 102): the row model NEVER builds both hierarchy AND
        // flat-group rows for the same task list, and rollups mirror that
        // exactly via the SAME GanttRowModel.UsesHierarchy branch.
        var tasks = new[]
        {
            Task("p1", D(2026, 1, 1), D(2026, 1, 10), groupLabel: "Design"),
            Task("c1", D(2026, 1, 1), D(2026, 1, 5), parentId: "p1", groupLabel: "Design"),
        };

        var rollups = GanttRollupModel.ComputeRollups(tasks, null);

        Assert.True(rollups.ContainsKey("p1")); // hierarchy rollup present
        Assert.DoesNotContain(GanttRowModel.GroupToggleKey("Design"), rollups.Keys); // no group rollup at all
    }

    // ── Recursion (T3 decision #4): transitive multi-level rollups ──────────

    [Fact]
    public void ComputeRollups_Grandparent_Rolls_Up_Transitively_Through_An_Intermediate_Parent()
    {
        // grandparent -> parent -> {leaf1, leaf2}; parent itself has no OTHER
        // direct children besides that one intermediate node, so grandparent's
        // math receives ONE effective child (the intermediate parent's own
        // Start/End/Progress REPLACED by ITS rollup) — asserted exactly.
        var tasks = new[]
        {
            Task("gp", D(2026, 1, 1), D(2026, 2, 1)),
            Task("p", D(2026, 1, 1), D(2026, 1, 20), parentId: "gp"),
            Task("leaf1", D(2026, 1, 1), D(2026, 1, 6), progress: 100, parentId: "p"),  // 5 days
            Task("leaf2", D(2026, 1, 6), D(2026, 1, 16), progress: 0, parentId: "p"),   // 10 days
        };

        var rollups = GanttRollupModel.ComputeRollups(tasks, null);

        // Parent's own rollup: (5*100 + 10*0)/(15) = 500/15
        var parentRollup = rollups["p"];
        Assert.Equal(500.0 / 15.0, parentRollup.WeightedProgress, 10);
        Assert.Equal(D(2026, 1, 1), parentRollup.Start);
        Assert.Equal(D(2026, 1, 16), parentRollup.End);

        // Grandparent has exactly ONE direct child ("p") — its rollup must
        // equal "p"'s OWN rollup exactly (Start/End/WeightedProgress), proving
        // the substitution (not "p"'s raw Start=1/1..End=1/20, Progress=0)
        // actually reached the outer computation.
        var gpRollup = rollups["gp"];
        Assert.Equal(parentRollup.Start, gpRollup.Start);
        Assert.Equal(parentRollup.End, gpRollup.End);
        Assert.Equal(Math.Round(parentRollup.WeightedProgress), gpRollup.WeightedProgress, 10);
    }

    [Fact]
    public void ComputeRollups_Grandparent_With_A_Sibling_Leaf_Blends_The_Substituted_Parent_With_The_Leaf()
    {
        // DISABLE-CHECK fixture: grandparent has TWO direct children — the
        // intermediate parent "p" (rolled up from its own leaves to 100%
        // progress, spanning 10 days after rounding) and a genuine sibling
        // leaf "sib" (5 days @ 0%). If recursion were DISABLED (i.e. "p"'s raw
        // stored Progress=0/duration were used instead of its computed
        // rollup), the grandparent's weighted progress would be a
        // concretely different, predictable value: with p's raw fields
        // (5 days @ 0%) blended with sib (5 days @ 0%) -> 0% flat. With
        // recursion ENABLED (p's rolled-up 10-day-equivalent-weight @ 100%
        // blended with sib's 5 days @ 0%) -> (10*100 + 5*0)/15 = 1000/15 ≈ 66.7%.
        var tasks = new[]
        {
            Task("gp", D(2026, 1, 1), D(2026, 3, 1)),
            Task("p", D(2026, 1, 1), D(2026, 1, 6), parentId: "gp"),   // raw: 5 days, progress 0 (never read once it has children)
            Task("leaf", D(2026, 1, 1), D(2026, 1, 11), progress: 100, parentId: "p"), // p's rollup: 10 days @ 100%
            Task("sib", D(2026, 1, 11), D(2026, 1, 16), progress: 0, parentId: "gp"),   // 5 days @ 0%
        };

        var rollups = GanttRollupModel.ComputeRollups(tasks, null);

        Assert.Equal(1000.0 / 15.0, rollups["gp"].WeightedProgress, 8);
    }

    // ── Custom RollupMath: direct-children contract + transitivity ──────────

    [Fact]
    public void RollupMath_Override_Is_Invoked_Once_Per_Parent_Row_With_Only_Its_Direct_Children()
    {
        var tasks = new[]
        {
            Task("p1", D(2026, 1, 1), D(2026, 1, 10)),
            Task("c1", D(2026, 1, 1), D(2026, 1, 5), parentId: "p1"),
            Task("c2", D(2026, 1, 5), D(2026, 1, 10), parentId: "p1"),
            Task("c3", D(2026, 1, 1), D(2026, 1, 5), parentId: "p1"),
        };
        var calls = new List<IReadOnlyList<GanttTask>>();
        GanttRollup Capture(IReadOnlyList<GanttTask> children)
        {
            calls.Add(children);
            return new GanttRollup(D(2026, 1, 1), D(2026, 1, 10), 42);
        }

        GanttRollupModel.ComputeRollups(tasks, Capture);

        Assert.Single(calls); // one parent row -> one invocation
        Assert.Equal(3, calls[0].Count);
        Assert.Equal(new[] { "c1", "c2", "c3" }, calls[0].Select(t => t.Id));
    }

    [Fact]
    public void RollupMath_Override_Sees_A_Nested_Parents_Rolled_Up_Values_Not_Its_Raw_Ones()
    {
        var tasks = new[]
        {
            Task("gp", D(2026, 1, 1), D(2026, 3, 1)),
            Task("p", D(2026, 1, 1), D(2026, 1, 2), progress: 5, parentId: "gp"), // raw values — must NOT reach gp's call
            Task("leaf", D(2026, 6, 1), D(2026, 6, 11), progress: 77, parentId: "p"),
        };
        GanttTask? seenByGrandparent = null;
        GanttRollup Capture(IReadOnlyList<GanttTask> children)
        {
            if (children.Count == 1 && children[0].Id == "p") seenByGrandparent = children[0];
            return GanttRollupModel.DefaultRollupMath(children);
        }

        GanttRollupModel.ComputeRollups(tasks, Capture);

        Assert.NotNull(seenByGrandparent);
        // "p"'s raw Start/End/Progress (1/1..1/2, 5%) must have been REPLACED
        // by its own computed rollup (from "leaf": 6/1..6/11, 77%).
        Assert.Equal(D(2026, 6, 1), seenByGrandparent!.Start);
        Assert.Equal(D(2026, 6, 11), seenByGrandparent.End);
        Assert.Equal(77, seenByGrandparent.Progress);
    }

    // ── Defensive contract: throwing / out-of-range RollupMath ──────────────

    [Fact]
    public void RollupMath_Throwing_Override_Falls_Back_To_Default_Math_For_That_Row_Only()
    {
        var tasks = new[]
        {
            Task("p1", D(2026, 1, 1), D(2026, 1, 10)),
            Task("c1", D(2026, 1, 1), D(2026, 1, 5), progress: 100, parentId: "p1"),
            Task("c2", D(2026, 1, 5), D(2026, 1, 10), progress: 0, parentId: "p1"),
        };
        GanttRollup Throws(IReadOnlyList<GanttTask> _) => throw new InvalidOperationException("boom");

        var rollups = GanttRollupModel.ComputeRollups(tasks, Throws);

        var expected = GanttRollupModel.DefaultRollupMath(new[] { tasks[1], tasks[2] });
        Assert.Equal(expected.WeightedProgress, rollups["p1"].WeightedProgress, 10);
        Assert.Equal(expected.Start, rollups["p1"].Start);
        Assert.Equal(expected.End, rollups["p1"].End);
    }

    [Fact]
    public void RollupMath_Override_Returning_End_Before_Start_Is_Clamped_To_A_Zero_Width_Envelope()
    {
        var tasks = new[]
        {
            Task("p1", D(2026, 1, 1), D(2026, 1, 10)),
            Task("c1", D(2026, 1, 1), D(2026, 1, 5), parentId: "p1"),
        };
        GanttRollup Backwards(IReadOnlyList<GanttTask> _) => new(D(2026, 1, 10), D(2026, 1, 1), 50);

        var rollups = GanttRollupModel.ComputeRollups(tasks, Backwards);

        var r = rollups["p1"];
        Assert.Equal(D(2026, 1, 10), r.Start);
        Assert.Equal(D(2026, 1, 10), r.End); // clamped up to Start, never left negative-width
        Assert.True(r.End >= r.Start);
    }

    [Theory]
    [InlineData(150.0, 100.0)]
    [InlineData(-30.0, 0.0)]
    [InlineData(double.NaN, 0.0)]
    [InlineData(double.PositiveInfinity, 0.0)]
    public void RollupMath_Override_Returning_An_Out_Of_Range_Progress_Is_Clamped(double raw, double expected)
    {
        var tasks = new[]
        {
            Task("p1", D(2026, 1, 1), D(2026, 1, 10)),
            Task("c1", D(2026, 1, 1), D(2026, 1, 5), parentId: "p1"),
        };
        GanttRollup Hostile(IReadOnlyList<GanttTask> _) => new(D(2026, 1, 1), D(2026, 1, 5), raw);

        var rollups = GanttRollupModel.ComputeRollups(tasks, Hostile);

        Assert.Equal(expected, rollups["p1"].WeightedProgress, 10);
    }

    // ── Cyclic ParentId defensive guard ──────────────────────────────────────

    [Fact]
    public void ComputeRollups_Self_Referential_ParentId_Does_Not_Throw_Or_Hang()
    {
        var tasks = new[] { Task("a", D(2026, 1, 1), D(2026, 1, 5), progress: 33, parentId: "a") };

        var rollups = GanttRollupModel.ComputeRollups(tasks, null);

        // Falls back to the task's own raw Start/End/Progress rather than
        // crashing or looping forever.
        Assert.Equal(D(2026, 1, 1), rollups["a"].Start);
        Assert.Equal(D(2026, 1, 5), rollups["a"].End);
        Assert.Equal(33, rollups["a"].WeightedProgress);
    }
}
