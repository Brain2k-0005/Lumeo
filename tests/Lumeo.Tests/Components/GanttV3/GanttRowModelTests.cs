using Lumeo.GanttV3;
using Xunit;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Regression tests for <see cref="GanttRowModel"/> — the pure row-flattening
/// logic feeding GanttTree/GanttTimeline/GanttArrowLayer (design spec Phase 2,
/// T3). Covers: flat <see cref="GanttTask.GroupLabel"/> grouping (v2 parity —
/// header rows interleaved per gantt-v2.js's <c>lastGroupLabel</c> transition
/// check), <see cref="GanttTask.ParentId"/> hierarchy nesting/depth, collapse
/// filtering in both modes, and the <c>ShowTreePane</c> default.
/// </summary>
public class GanttRowModelTests
{
    private static readonly HashSet<string> None = new();

    private static GanttTask Task(string id, string? parentId = null, string? groupLabel = null) =>
        new(id, id, new DateTime(2026, 1, 1), new DateTime(2026, 1, 5)) { ParentId = parentId, GroupLabel = groupLabel };

    // ── Flat GroupLabel grouping (v2 parity) ────────────────────────────────

    [Fact]
    public void Flat_Grouping_Interleaves_One_Header_Row_Per_Group_In_V2_Order()
    {
        var tasks = new[]
        {
            Task("t1", groupLabel: "Design"),
            Task("t2", groupLabel: "Design"),
            Task("t3", groupLabel: "Build"),
        };

        var rows = GanttRowModel.BuildVisibleRows(tasks, None);

        Assert.Equal(5, rows.Count); // 2 group headers + 3 tasks
        Assert.Equal(GanttRowKind.GroupHeader, rows[0].Kind);
        Assert.Equal("Design", rows[0].Label);
        Assert.Equal(GanttRowKind.Task, rows[1].Kind);
        Assert.Equal("t1", rows[1].Task!.Id);
        Assert.Equal(GanttRowKind.Task, rows[2].Kind);
        Assert.Equal("t2", rows[2].Task!.Id);
        Assert.Equal(GanttRowKind.GroupHeader, rows[3].Kind);
        Assert.Equal("Build", rows[3].Label);
        Assert.Equal(GanttRowKind.Task, rows[4].Kind);
        Assert.Equal("t3", rows[4].Task!.Id);
    }

    [Fact]
    public void Flat_Grouping_Indents_Grouped_Members_One_Level_And_Ungrouped_Tasks_At_Root()
    {
        var tasks = new[] { Task("t1", groupLabel: "Design"), Task("t2") };

        var rows = GanttRowModel.BuildVisibleRows(tasks, None);

        var header = rows.Single(r => r.Kind == GanttRowKind.GroupHeader);
        Assert.Equal(0, header.Depth);
        var grouped = rows.Single(r => r.Task?.Id == "t1");
        Assert.Equal(1, grouped.Depth);
        var ungrouped = rows.Single(r => r.Task?.Id == "t2");
        Assert.Equal(0, ungrouped.Depth);
    }

    [Fact]
    public void Collapsing_A_Group_Header_Hides_Its_Member_Rows()
    {
        var tasks = new[]
        {
            Task("t1", groupLabel: "Design"),
            Task("t2", groupLabel: "Design"),
            Task("t3", groupLabel: "Build"),
        };
        var collapsed = new HashSet<string> { GanttRowModel.GroupToggleKey("Design") };

        var rows = GanttRowModel.BuildVisibleRows(tasks, collapsed);

        // Design header stays (so it can be re-expanded), t1/t2 hidden, Build unaffected.
        Assert.Equal(3, rows.Count); // Design header, Build header, t3
        Assert.Contains(rows, r => r.Kind == GanttRowKind.GroupHeader && r.Label == "Design" && r.IsCollapsed);
        Assert.DoesNotContain(rows, r => r.Task?.Id is "t1" or "t2");
        Assert.Contains(rows, r => r.Task?.Id == "t3");
    }

    [Fact]
    public void Group_Header_Row_Always_Reports_HasChildren_And_A_ToggleKey()
    {
        var tasks = new[] { Task("t1", groupLabel: "Design") };

        var rows = GanttRowModel.BuildVisibleRows(tasks, None);

        var header = rows.Single(r => r.Kind == GanttRowKind.GroupHeader);
        Assert.True(header.HasChildren);
        Assert.Equal(GanttRowModel.GroupToggleKey("Design"), header.ToggleKey);
        Assert.False(header.IsCollapsed);
    }

    // ── ParentId hierarchy ───────────────────────────────────────────────────

    [Fact]
    public void Hierarchy_Nests_Children_Directly_After_Their_Parent_Depth_First()
    {
        var tasks = new[]
        {
            Task("parent"),
            Task("child-a", parentId: "parent"),
            Task("grandchild", parentId: "child-a"),
            Task("child-b", parentId: "parent"),
            Task("root2"),
        };

        var rows = GanttRowModel.BuildVisibleRows(tasks, None);

        Assert.Equal(new[] { "parent", "child-a", "grandchild", "child-b", "root2" },
            rows.Select(r => r.Task!.Id).ToArray());
        Assert.Equal(new[] { 0, 1, 2, 1, 0 }, rows.Select(r => r.Depth).ToArray());
    }

    [Fact]
    public void Hierarchy_Parent_Row_Reports_HasChildren_And_Its_Own_Id_As_ToggleKey()
    {
        var tasks = new[] { Task("parent"), Task("child", parentId: "parent") };

        var rows = GanttRowModel.BuildVisibleRows(tasks, None);

        var parentRow = rows.Single(r => r.Task!.Id == "parent");
        Assert.True(parentRow.HasChildren);
        Assert.Equal("parent", parentRow.ToggleKey);
        var childRow = rows.Single(r => r.Task!.Id == "child");
        Assert.False(childRow.HasChildren);
        Assert.Null(childRow.ToggleKey);
    }

    [Fact]
    public void Collapsing_A_Parent_Removes_All_Its_Descendants_Recursively()
    {
        var tasks = new[]
        {
            Task("parent"),
            Task("child", parentId: "parent"),
            Task("grandchild", parentId: "child"),
            Task("sibling"),
        };
        var collapsed = new HashSet<string> { "parent" };

        var rows = GanttRowModel.BuildVisibleRows(tasks, collapsed);

        // "parent" stays (collapsed indicator), "child"/"grandchild" both gone, "sibling" unaffected.
        Assert.Equal(new[] { "parent", "sibling" }, rows.Select(r => r.Task!.Id).ToArray());
        Assert.True(rows.Single(r => r.Task!.Id == "parent").IsCollapsed);
    }

    [Fact]
    public void Orphaned_ParentId_Falls_Back_To_Root_Depth_Instead_Of_Being_Dropped()
    {
        var tasks = new[] { Task("t1", parentId: "does-not-exist") };

        var rows = GanttRowModel.BuildVisibleRows(tasks, None);

        var row = Assert.Single(rows);
        Assert.Equal("t1", row.Task!.Id);
        Assert.Equal(0, row.Depth);
    }

    // ── Cyclic ParentId (invalid input — must render, never silently drop) ──
    //
    // A cyclic ParentId graph is invalid input (no task in the cycle is
    // reachable from a real root, and none of them is a "true orphan" either,
    // since every parent id in the cycle DOES correspond to a real task) —
    // but it must never cause those tasks to silently vanish from the row
    // list. Empirically verified (before the safety-net pass below existed):
    // a two-node cycle rendered NEITHER member, and a self-loop rendered 0
    // rows. The fix walks `tasks` in original order and promotes the first
    // not-yet-visited member of a remaining cycle to a root row, letting the
    // rest of that cycle unwind as its descendants (Walk's own visited-guard
    // stops it from re-entering the loop) — deterministic given the input's
    // own order, not the "smartest" possible rendering of invalid data.

    [Fact]
    public void Two_Node_Cycle_Renders_Both_Members_Instead_Of_Silently_Dropping_Them()
    {
        var tasks = new[] { Task("a", parentId: "b"), Task("b", parentId: "a") };

        var rows = GanttRowModel.BuildVisibleRows(tasks, None);

        // "a" is the first unvisited task in input order, so it's promoted to
        // a root; "b" — its bucket's only "child" per the cyclic ParentId
        // wiring — renders once, one level deeper, and the recursion back
        // into "a" is silently absorbed by the visited-guard instead of looping.
        Assert.Equal(new[] { "a", "b" }, rows.Select(r => r.Task!.Id).ToArray());
        Assert.Equal(new[] { 0, 1 }, rows.Select(r => r.Depth).ToArray());
    }

    [Fact]
    public void Self_Loop_ParentId_Renders_The_Task_Once_Instead_Of_Zero_Rows()
    {
        var tasks = new[] { Task("a", parentId: "a") };

        var rows = GanttRowModel.BuildVisibleRows(tasks, None);

        var row = Assert.Single(rows);
        Assert.Equal("a", row.Task!.Id);
        Assert.Equal(0, row.Depth);
    }

    [Fact]
    public void Cycle_With_A_Non_Cyclic_Tail_Renders_The_Tail_Task_Under_The_Cycles_Entry_Point()
    {
        // A/B cycle each other; C is a genuine (non-cyclic) child of A.
        var tasks = new[]
        {
            Task("a", parentId: "b"),
            Task("b", parentId: "a"),
            Task("c", parentId: "a"),
        };

        var rows = GanttRowModel.BuildVisibleRows(tasks, None);

        Assert.Equal(new[] { "a", "b", "c" }, rows.Select(r => r.Task!.Id).ToArray());
        Assert.Equal(new[] { 0, 1, 1 }, rows.Select(r => r.Depth).ToArray());
    }

    [Fact]
    public void UsesHierarchy_Wins_Over_Flat_Grouping_When_Both_Are_Present_On_The_Same_List()
    {
        var tasks = new[]
        {
            Task("t1", groupLabel: "Design"),
            Task("t2", parentId: "t1"),
        };

        var rows = GanttRowModel.BuildVisibleRows(tasks, None);

        Assert.DoesNotContain(rows, r => r.Kind == GanttRowKind.GroupHeader);
        Assert.Equal(2, rows.Count);
    }

    // ── ShowTreePane default ─────────────────────────────────────────────────

    [Fact]
    public void DefaultShowTreePane_Is_False_For_A_Flat_Task_List_With_No_GroupBy()
    {
        var tasks = new[] { Task("t1"), Task("t2") };
        Assert.False(GanttRowModel.DefaultShowTreePane(tasks, groupBySet: false));
    }

    [Fact]
    public void DefaultShowTreePane_Is_True_When_GroupBy_Is_Set_Even_With_No_Hierarchy()
    {
        var tasks = new[] { Task("t1"), Task("t2") };
        Assert.True(GanttRowModel.DefaultShowTreePane(tasks, groupBySet: true));
    }

    [Fact]
    public void DefaultShowTreePane_Is_True_When_Any_Task_Has_A_ParentId()
    {
        var tasks = new[] { Task("t1"), Task("t2", parentId: "t1") };
        Assert.True(GanttRowModel.DefaultShowTreePane(tasks, groupBySet: false));
    }
}
