using Lumeo.GanttV3;
using Xunit;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Regression tests for <see cref="GanttReorderModel"/> — the pure
/// row-reorder logic feeding the GanttV3 tree pane's drag-to-reorder feature
/// (design spec Phase 3, T6: "reorder is within-parent only... cross-parent
/// moves are ParentId edits = out of scope"). Covers: bucket-key resolution
/// for both hierarchy and flat GroupLabel modes, sibling ordering, bulk
/// position computation, and <see cref="GanttReorderModel.Move"/>'s
/// slot-preserving list surgery — including the "non-sibling tasks keep
/// their EXACT original position" guarantee and the filtered/invalid-duration
/// no-op case. Mirrors <see cref="GanttRollupModelTests"/>/<see
/// cref="GanttSelectionModelTests"/>'s own style.
/// </summary>
public class GanttReorderModelTests
{
    private static DateTime D(int y, int m, int d) => new(y, m, d);

    private static GanttTask Task(string id, string? parentId = null, string? groupLabel = null, DateTime? start = null, DateTime? end = null) =>
        new(id, id, start ?? D(2026, 3, 1), end ?? D(2026, 3, 5)) { ParentId = parentId, GroupLabel = groupLabel };

    // ── BucketKey / SiblingsOf ───────────────────────────────────────────────

    [Fact]
    public void BucketKey_Hierarchy_Mode_Is_The_Tasks_Own_ParentId()
    {
        var tasks = new[] { Task("p1"), Task("c1", parentId: "p1") };

        Assert.Equal("p1", GanttReorderModel.BucketKey(tasks, tasks[1]));
        Assert.Null(GanttReorderModel.BucketKey(tasks, tasks[0])); // root bucket
    }

    [Fact]
    public void BucketKey_FlatGroup_Mode_Is_The_Tasks_Own_Normalized_GroupLabel()
    {
        var tasks = new[] { Task("a", groupLabel: "Design"), Task("b", groupLabel: "") };

        Assert.Equal("Design", GanttReorderModel.BucketKey(tasks, tasks[0]));
        Assert.Null(GanttReorderModel.BucketKey(tasks, tasks[1])); // "" normalizes to the root/ungrouped bucket
    }

    [Fact]
    public void SiblingsOf_Returns_Every_Same_Bucket_Task_Including_Self_In_Original_Order()
    {
        var tasks = new[] { Task("p1"), Task("c1", parentId: "p1"), Task("other"), Task("c2", parentId: "p1") };

        var siblings = GanttReorderModel.SiblingsOf(tasks, tasks[1]);

        Assert.Equal(new[] { "c1", "c2" }, siblings.Select(t => t.Id));
    }

    [Fact]
    public void SiblingsOf_Root_Bucket_Groups_Every_Null_ParentId_Task()
    {
        var tasks = new[] { Task("r1"), Task("r2"), Task("c1", parentId: "r1") };

        var siblings = GanttReorderModel.SiblingsOf(tasks, tasks[0]);

        Assert.Equal(new[] { "r1", "r2" }, siblings.Select(t => t.Id));
    }

    // ── ComputeBucketPositions ───────────────────────────────────────────────

    [Fact]
    public void ComputeBucketPositions_Matches_SiblingsOf_Ordering_For_Every_Task()
    {
        var tasks = new[] { Task("p1"), Task("c1", parentId: "p1"), Task("other"), Task("c2", parentId: "p1") };

        var positions = GanttReorderModel.ComputeBucketPositions(tasks);

        Assert.Equal(0, positions["c1"].Index);
        Assert.Equal(1, positions["c2"].Index);
        Assert.Equal(positions["c1"].BucketKey, positions["c2"].BucketKey); // same bucket
        Assert.Equal(GanttReorderModel.RootBucketSentinel, positions["p1"].BucketKey);
        Assert.NotEqual(positions["p1"].BucketKey, positions["c1"].BucketKey); // different buckets never share the sentinel/key
    }

    // ── Move: within-bucket reordering ──────────────────────────────────────

    [Fact]
    public void Move_Relocates_The_Task_Within_Its_Own_Bucket_Preserving_Non_Sibling_Slots_Exactly()
    {
        // [root1, child1(p:root1), grandchild1(p:child1), child2(p:root1), root2]
        // — mirrors GanttParityFixtures.TreeTasks()' own shape. Moving child2
        // to bucket-index 0 (before child1) must NOT touch grandchild1's own
        // slot (a DIFFERENT bucket, p:child1) or either root's slot at all.
        var root1 = Task("root1");
        var child1 = Task("child1", parentId: "root1");
        var grandchild1 = Task("grandchild1", parentId: "child1");
        var child2 = Task("child2", parentId: "root1");
        var root2 = Task("root2");
        var tasks = new List<GanttTask> { root1, child1, grandchild1, child2, root2 };

        var result = GanttReorderModel.Move(tasks, "child2", 0);

        Assert.Equal(new[] { "root1", "child2", "grandchild1", "child1", "root2" }, result.Select(t => t.Id));
        // Untouched-slot identity check (not just id-equality) — the exact
        // same GanttTask instances for every non-sibling.
        Assert.Same(root1, result[0]);
        Assert.Same(grandchild1, result[2]);
        Assert.Same(root2, result[4]);
    }

    [Fact]
    public void Move_To_The_Current_Index_Is_A_No_Op_Returns_The_Same_List_Reference()
    {
        var tasks = new List<GanttTask> { Task("p1"), Task("c1", parentId: "p1"), Task("c2", parentId: "p1") };

        var result = GanttReorderModel.Move(tasks, "c1", 0); // c1 is already at bucket-index 0

        Assert.Same(tasks, result);
    }

    [Fact]
    public void Move_Clamps_An_Out_Of_Range_Target_Index_To_The_Bucket_Bounds()
    {
        var tasks = new List<GanttTask> { Task("p1"), Task("c1", parentId: "p1"), Task("c2", parentId: "p1") };

        var result = GanttReorderModel.Move(tasks, "c1", 999); // way past the 2-member bucket

        Assert.Equal(new[] { "p1", "c2", "c1" }, result.Select(t => t.Id)); // clamped to index 1 (last)
    }

    [Fact]
    public void Move_Unknown_Task_Id_Is_A_No_Op_Returns_The_Same_List_Reference()
    {
        var tasks = new List<GanttTask> { Task("p1") };

        Assert.Same(tasks, GanttReorderModel.Move(tasks, "does-not-exist", 0));
    }

    [Fact]
    public void Move_A_Filtered_Out_Invalid_Duration_Task_Is_A_No_Op()
    {
        // End < Start (non-milestone) — GanttRowModel.FilterValidDurationTasks
        // drops this from rendering entirely, so it was never offered a drag
        // grip in the first place; Move defends the same way CommitCreate's
        // own "public JSInvokable surface" reasoning already establishes.
        var invalid = new GanttTask("bad", "bad", D(2026, 3, 10), D(2026, 3, 1)) { ParentId = "p1" };
        var tasks = new List<GanttTask> { Task("p1"), invalid, Task("c2", parentId: "p1") };

        Assert.Same(tasks, GanttReorderModel.Move(tasks, "bad", 0));
    }

    [Fact]
    public void Move_FlatGroup_Mode_Reorders_Within_The_Same_GroupLabel_Bucket()
    {
        var tasks = new List<GanttTask>
        {
            Task("a", groupLabel: "Design"), Task("b", groupLabel: "Design"), Task("x", groupLabel: "Build"),
        };

        var result = GanttReorderModel.Move(tasks, "b", 0);

        Assert.Equal(new[] { "b", "a", "x" }, result.Select(t => t.Id));
    }

    [Fact]
    public void Move_A_Single_Member_Bucket_Is_Always_A_No_Op()
    {
        var tasks = new List<GanttTask> { Task("p1"), Task("only-child", parentId: "p1") };

        Assert.Same(tasks, GanttReorderModel.Move(tasks, "only-child", 0));
    }

    // ── Move: downward index normalization (Codex round — P1 #1) ────────────
    //
    // gantt-v3.js's siblingRows() excludes the MOVING row itself, so a hovered
    // candidate's own `data-reorder-index` attribute (assigned by
    // ComputeBucketPositions over the FULL, unfiltered bucket including the
    // moving task) is a position in the PRE-removal, N-slot index space —
    // "insert after candidate C" sends `C's original index + 1`. But Move's
    // own `siblings.Insert(clamped, moving)` runs AFTER `siblings.RemoveAt(oldIndex)`,
    // so `clamped` gets interpreted as a position in the POST-removal, (N-1)-slot
    // list instead — a bug for any candidate positioned AFTER the moving task
    // (clamped > oldIndex), since removing the earlier `moving` element shifts
    // every later index down by one that the caller-supplied index never
    // accounted for. Only matters when the moving task moves DOWNWARD past at
    // least one former later-sibling; moving UPWARD (clamped < oldIndex) was
    // never affected, since removal doesn't shift anything before it.
    [Fact]
    public void Move_Downward_Past_A_Sibling_Lands_Immediately_After_The_Hovered_Candidate()
    {
        // [a, b, c] — drag "a" (bucket-index 0) to just AFTER "b". siblingRows()
        // in gantt-v3.js excludes "a" itself, so the candidate list is [b, c];
        // "b"'s own `data-reorder-index` is still its ORIGINAL bucket position
        // (1), and "after" hovering sends index+1 = 2 as the target.
        var tasks = new List<GanttTask> { Task("a", groupLabel: "G"), Task("b", groupLabel: "G"), Task("c", groupLabel: "G") };

        var result = GanttReorderModel.Move(tasks, "a", 2);

        // Expected: "a" lands directly after "b", i.e. [b, a, c] — NOT at the
        // very end ([b, c, a], the pre-fix off-by-one result: clamped=2 got
        // inserted into the post-removal 2-element list [b, c] at position 2,
        // i.e. the tail).
        Assert.Equal(new[] { "b", "a", "c" }, result.Select(t => t.Id));
    }

    [Fact]
    public void Move_Downward_To_Just_Before_The_Next_Sibling_Is_A_No_Op()
    {
        // Same [a, b, c] fixture — "a" is already immediately before "b", so
        // hovering just BEFORE "b" (target index = b's own original index, 1)
        // must be a no-op: "a" is already exactly there.
        var tasks = new List<GanttTask> { Task("a", groupLabel: "G"), Task("b", groupLabel: "G"), Task("c", groupLabel: "G") };

        var result = GanttReorderModel.Move(tasks, "a", 1);

        // Expected: unchanged order [a, b, c] — NOT the pre-fix off-by-one
        // result, which swapped "a" and "b" to [b, a, c] (clamped=1 inserted
        // into the post-removal list [b, c] at position 1, i.e. after "b").
        Assert.Equal(new[] { "a", "b", "c" }, result.Select(t => t.Id));
    }

    [Fact]
    public void Move_Downward_Past_Every_Sibling_Appends_At_The_Very_End()
    {
        // Same [a, b, c] fixture — drag "a" to just AFTER "c" (the LAST
        // sibling). "c"'s own original bucket index is 2 (the highest), so
        // "after" hovering sends 2 + 1 = 3 == the PRE-removal bucket size —
        // a legitimate "append" position, distinct from "insert before c"
        // (target 2). Exercises the widened clamp upper bound (now
        // `siblings.Count`, not `siblings.Count - 1`) alongside the same
        // post-removal index normalization the two cases above cover.
        var tasks = new List<GanttTask> { Task("a", groupLabel: "G"), Task("b", groupLabel: "G"), Task("c", groupLabel: "G") };

        var result = GanttReorderModel.Move(tasks, "a", 3);

        Assert.Equal(new[] { "b", "c", "a" }, result.Select(t => t.Id));
    }
}
