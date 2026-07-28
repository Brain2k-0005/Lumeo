using Lumeo.GanttV3;
using Xunit;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Regression tests for <see cref="GanttSelectionModel"/> — the pure
/// tri-state selection logic feeding the GanttV3 tree pane's checkbox column
/// (design spec Phase 3, T6: "Leaf-row checkbox selection... parent/group
/// checkbox = tri-state select-descendants"). Covers: leaf/parent/group
/// tri-state computation, leaf-id resolution for both hierarchy and flat
/// GroupLabel modes, "SelectedIds only ever contains leaves" (never a
/// parent's own id), collapse-independence, and cycle safety — mirrors
/// <see cref="GanttRollupModelTests"/>'s own style (see that file for the
/// sibling coverage this parallels).
/// </summary>
public class GanttSelectionModelTests
{
    private static DateTime D(int y, int m, int d) => new(y, m, d);

    private static GanttTask Task(string id, string? parentId = null, string? groupLabel = null) =>
        new(id, id, D(2026, 3, 1), D(2026, 3, 5)) { ParentId = parentId, GroupLabel = groupLabel };

    private static HashSet<string> Ids(params string[] ids) => new(ids, StringComparer.Ordinal);

    // ── ComputeStates: hierarchy mode ───────────────────────────────────────

    [Fact]
    public void ComputeStates_Parent_Is_Unselected_When_No_Child_Selected()
    {
        var tasks = new[] { Task("p1"), Task("c1", parentId: "p1"), Task("c2", parentId: "p1") };
        var states = GanttSelectionModel.ComputeStates(tasks, Ids());

        Assert.Equal(GanttRowSelectionState.Unselected, states["p1"]);
    }

    [Fact]
    public void ComputeStates_Parent_Is_PartiallySelected_When_Some_Children_Selected()
    {
        var tasks = new[] { Task("p1"), Task("c1", parentId: "p1"), Task("c2", parentId: "p1") };
        var states = GanttSelectionModel.ComputeStates(tasks, Ids("c1"));

        Assert.Equal(GanttRowSelectionState.PartiallySelected, states["p1"]);
    }

    [Fact]
    public void ComputeStates_Parent_Is_Selected_When_Every_Child_Selected()
    {
        var tasks = new[] { Task("p1"), Task("c1", parentId: "p1"), Task("c2", parentId: "p1") };
        var states = GanttSelectionModel.ComputeStates(tasks, Ids("c1", "c2"));

        Assert.Equal(GanttRowSelectionState.Selected, states["p1"]);
    }

    [Fact]
    public void ComputeStates_Transitive_Grandparent_Reflects_A_Fully_Selected_Grandchild_Subtree()
    {
        // gp -> p -> [c1, c2], both c1/c2 selected -> p Selected -> gp Selected too.
        var tasks = new[]
        {
            Task("gp"), Task("p", parentId: "gp"),
            Task("c1", parentId: "p"), Task("c2", parentId: "p"),
        };
        var states = GanttSelectionModel.ComputeStates(tasks, Ids("c1", "c2"));

        Assert.Equal(GanttRowSelectionState.Selected, states["p"]);
        Assert.Equal(GanttRowSelectionState.Selected, states["gp"]);
    }

    [Fact]
    public void ComputeStates_Transitive_Grandparent_Is_PartiallySelected_When_A_Middle_Parent_Is_Only_Partial()
    {
        // gp -> [p1 -> [c1(selected), c2(unselected)], p2(leaf, unselected)]
        var tasks = new[]
        {
            Task("gp"), Task("p1", parentId: "gp"), Task("p2", parentId: "gp"),
            Task("c1", parentId: "p1"), Task("c2", parentId: "p1"),
        };
        var states = GanttSelectionModel.ComputeStates(tasks, Ids("c1"));

        Assert.Equal(GanttRowSelectionState.PartiallySelected, states["p1"]);
        // p1 itself is neither fully Selected nor Unselected -> gp can only be Partial too.
        Assert.Equal(GanttRowSelectionState.PartiallySelected, states["gp"]);
    }

    [Fact]
    public void ComputeStates_A_Selected_Parent_Id_In_SelectedIds_Does_Not_Count_As_Its_Own_Selection()
    {
        // Pathological input: a caller/consumer manually stuffs the PARENT's
        // own id into selectedIds (never something this library itself does
        // — see ResolveLeafIds' own remarks). ComputeStates only ever
        // inspects DIRECT CHILDREN's states when deciding a parent row's own
        // tri-state, so a parent id sitting in selectedIds has no special
        // effect on ITS OWN entry — it isn't even consulted for that purpose,
        // it only counts as a "leaf" if some OTHER node references it via
        // ParentId, which is not the case here.
        var tasks = new[] { Task("p1"), Task("c1", parentId: "p1") };
        var states = GanttSelectionModel.ComputeStates(tasks, Ids("p1")); // p1's OWN id, not c1's

        Assert.Equal(GanttRowSelectionState.Unselected, states["p1"]); // c1 (its only child) isn't selected
    }

    // ── ComputeStates: flat GroupLabel mode ─────────────────────────────────

    [Fact]
    public void ComputeStates_FlatGroup_Tri_State_Mirrors_Hierarchy_Semantics()
    {
        var tasks = new[]
        {
            Task("a", groupLabel: "Design"), Task("b", groupLabel: "Design"), Task("c", groupLabel: "Build"),
        };
        var key = GanttRowModel.GroupToggleKey("Design");

        Assert.Equal(GanttRowSelectionState.Unselected, GanttSelectionModel.ComputeStates(tasks, Ids())[key]);
        Assert.Equal(GanttRowSelectionState.PartiallySelected, GanttSelectionModel.ComputeStates(tasks, Ids("a"))[key]);
        Assert.Equal(GanttRowSelectionState.Selected, GanttSelectionModel.ComputeStates(tasks, Ids("a", "b"))[key]);
    }

    [Fact]
    public void ComputeStates_FlatGroup_Empty_String_GroupLabel_Is_Ungrouped_Not_A_Bucket()
    {
        // Same truthiness normalization as GanttRowModel.BuildFlatGroupRows —
        // an empty GroupLabel task never appears in any group's tri-state at all.
        var tasks = new[] { Task("a", groupLabel: "") };
        var states = GanttSelectionModel.ComputeStates(tasks, Ids());

        Assert.Empty(states);
    }

    // ── ComputeStates: collapse-independence ────────────────────────────────

    [Fact]
    public void ComputeStates_Ignores_Collapsed_State_Entirely_Selecting_A_Hidden_Descendant_Still_Counts()
    {
        // ComputeStates never even receives `collapsed` — a hidden descendant
        // (behind whatever ancestor a caller happens to have collapsed) still
        // counts toward its parent's tri-state, by construction.
        var tasks = new[] { Task("p1"), Task("c1", parentId: "p1") };
        var states = GanttSelectionModel.ComputeStates(tasks, Ids("c1"));

        Assert.Equal(GanttRowSelectionState.Selected, states["p1"]);
    }

    // ── ComputeStates: cycle safety ──────────────────────────────────────────

    [Fact]
    public void ComputeStates_Cyclic_ParentId_Graph_Does_Not_Throw_Or_Loop_Forever()
    {
        // a's parent is b, b's parent is a — invalid input, must resolve to
        // SOME bounded value (Unselected, the documented neutral fallback),
        // never crash/hang.
        var tasks = new[]
        {
            new GanttTask("a", "a", D(2026, 3, 1), D(2026, 3, 2)) { ParentId = "b" },
            new GanttTask("b", "b", D(2026, 3, 1), D(2026, 3, 2)) { ParentId = "a" },
        };

        var states = GanttSelectionModel.ComputeStates(tasks, Ids());

        Assert.Equal(GanttRowSelectionState.Unselected, states["a"]);
        Assert.Equal(GanttRowSelectionState.Unselected, states["b"]);
    }

    // ── ResolveLeafIds: hierarchy mode ──────────────────────────────────────

    [Fact]
    public void ResolveLeafIds_A_Plain_Leaf_Click_Returns_Just_That_One_Id()
    {
        var tasks = new[] { Task("p1"), Task("c1", parentId: "p1") };

        var result = GanttSelectionModel.ResolveLeafIds(tasks, "c1");

        Assert.Equal(new[] { "c1" }, result);
    }

    [Fact]
    public void ResolveLeafIds_A_Parent_Click_Returns_Every_Recursive_Descendant_Leaf_Never_Its_Own_Id()
    {
        var tasks = new[]
        {
            Task("gp"), Task("p", parentId: "gp"),
            Task("c1", parentId: "p"), Task("c2", parentId: "p"),
        };

        var result = GanttSelectionModel.ResolveLeafIds(tasks, "gp");

        Assert.Equal(new HashSet<string> { "c1", "c2" }, result.ToHashSet());
        Assert.DoesNotContain("gp", result);
        Assert.DoesNotContain("p", result); // p is itself a parent, never a leaf
    }

    [Fact]
    public void ResolveLeafIds_Unknown_Key_Returns_Empty_Not_A_Throw()
    {
        var tasks = new[] { Task("p1") };

        Assert.Empty(GanttSelectionModel.ResolveLeafIds(tasks, "does-not-exist"));
    }

    [Fact]
    public void ResolveLeafIds_Cyclic_ParentId_Graph_Does_Not_Throw_Or_Loop_Forever()
    {
        var tasks = new[]
        {
            new GanttTask("a", "a", D(2026, 3, 1), D(2026, 3, 2)) { ParentId = "b" },
            new GanttTask("b", "b", D(2026, 3, 1), D(2026, 3, 2)) { ParentId = "a" },
        };

        var result = GanttSelectionModel.ResolveLeafIds(tasks, "a");

        Assert.NotNull(result); // bounded, no hang/throw — exact membership is not the point of this test
    }

    // ── ResolveLeafIds: flat GroupLabel mode ────────────────────────────────

    [Fact]
    public void ResolveLeafIds_FlatGroup_Header_Click_Returns_Every_Member()
    {
        var tasks = new[]
        {
            Task("a", groupLabel: "Design"), Task("b", groupLabel: "Design"), Task("c", groupLabel: "Build"),
        };

        var result = GanttSelectionModel.ResolveLeafIds(tasks, GanttRowModel.GroupToggleKey("Design"));

        Assert.Equal(new HashSet<string> { "a", "b" }, result.ToHashSet());
    }

    [Fact]
    public void ResolveLeafIds_FlatGroup_Leaf_Click_Returns_Just_That_One_Id()
    {
        var tasks = new[] { Task("a", groupLabel: "Design"), Task("b", groupLabel: "Design") };

        Assert.Equal(new[] { "a" }, GanttSelectionModel.ResolveLeafIds(tasks, "a"));
    }
}
