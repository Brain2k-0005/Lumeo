using Bunit;
using Lumeo.GanttV3;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Gantt v3 Phase 3, T6 — leaf-row checkbox selection + tri-state
/// parent/group checkboxes (<c>ShowRowCheckboxes</c>/<c>SelectedIds</c>) and
/// tree-row drag reorder (<c>AllowRowReorder</c>/<c>OnRowReorder</c>). Covers
/// the plan's deliverables end-to-end through <c>Gantt3</c> (parameter
/// wiring, controlled/uncontrolled <c>SelectedIds</c>, the reorder commit
/// pipeline) — see <see cref="GanttSelectionModelTests"/>/<see
/// cref="GanttReorderModelTests"/> for the pure-logic coverage this builds
/// on, and <see cref="GanttV3RowReorderReadonlyGuardTests"/> for the
/// JSInvokable-level Readonly guards (including the mid-drag flip).
/// </summary>
public class GanttV3Phase3T6Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3Phase3T6Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // root1 -> [child1 -> [grandchild1], child2] — mirrors
    // GanttParityFixtures.TreeTasks()' own shape (2-member sibling bucket
    // under root1, a real transitive grandparent).
    private static List<L.GanttTask> HierarchyFixture() => new()
    {
        new("root1", "Root", D(2026, 3, 1), D(2026, 3, 30)),
        new("child1", "Child One", D(2026, 3, 1), D(2026, 3, 10)) { ParentId = "root1" },
        new("grandchild1", "Grandchild", D(2026, 3, 1), D(2026, 3, 5)) { ParentId = "child1" },
        new("child2", "Child Two", D(2026, 3, 10), D(2026, 3, 20)) { ParentId = "root1" },
    };

    private static List<L.GanttTask> GroupFixture() => new()
    {
        new("a", "A", D(2026, 1, 1), D(2026, 1, 5), GroupLabel: "Design"),
        new("b", "B", D(2026, 1, 5), D(2026, 1, 10), GroupLabel: "Design"),
    };

    private IRenderedComponent<L.Gantt3> RenderTree(Action<Bunit.ComponentParameterCollectionBuilder<L.Gantt3>> configure) =>
        _ctx.Render<L.Gantt3>(p =>
        {
            p.Add(c => c.ShowTreePane, true);
            configure(p);
        });

    // ── ShowRowCheckboxes: off by default ───────────────────────────────────

    [Fact]
    public void ShowRowCheckboxes_Default_Off_Renders_No_Checkbox_Column()
    {
        var cut = RenderTree(p => p.Add(c => c.Tasks, HierarchyFixture()));

        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-tree-checkbox"));
    }

    // ── Leaf checkbox ────────────────────────────────────────────────────────

    [Fact]
    public void Leaf_Checkbox_Reflects_SelectedIds_Membership()
    {
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.ShowRowCheckboxes, true)
            .Add(c => c.SelectedIds, (ISet<string>)new HashSet<string> { "grandchild1" }));

        var rows = cut.FindAll("[data-row-kind='task']");
        var grandchildRow = rows.Single(r => r.TextContent.Contains("Grandchild"));
        var checkbox = grandchildRow.QuerySelector(".lumeo-gantt-v3-tree-checkbox")!;

        Assert.Equal("true", checkbox.GetAttribute("aria-checked"));
    }

    [Fact]
    public async Task Clicking_A_Leaf_Checkbox_Selects_It_Uncontrolled()
    {
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.ShowRowCheckboxes, true));

        var grandchildRow = cut.FindAll("[data-row-kind='task']").Single(r => r.TextContent.Contains("Grandchild"));
        await cut.InvokeAsync(() => grandchildRow.QuerySelector(".lumeo-gantt-v3-tree-checkbox")!.Click());

        var checkbox = cut.FindAll("[data-row-kind='task']").Single(r => r.TextContent.Contains("Grandchild"))
            .QuerySelector(".lumeo-gantt-v3-tree-checkbox")!;
        Assert.Equal("true", checkbox.GetAttribute("aria-checked"));
    }

    // ── Parent/group tri-state ───────────────────────────────────────────────

    [Fact]
    public void Parent_Checkbox_Is_Indeterminate_When_Only_Some_Descendants_Selected()
    {
        // child1 has one child (grandchild1); root1 has two children (child1, child2).
        // Selecting grandchild1 alone: child1 -> fully Selected (its only child is selected),
        // root1 -> PartiallySelected (child2's subtree is untouched).
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.ShowRowCheckboxes, true)
            .Add(c => c.SelectedIds, (ISet<string>)new HashSet<string> { "grandchild1" }));

        var child1Checkbox = cut.FindAll("[data-row-kind='task']").Single(r => r.TextContent.Contains("Child One"))
            .QuerySelector(".lumeo-gantt-v3-tree-checkbox")!;
        var root1Checkbox = cut.FindAll("[data-row-kind='task']").Single(r => r.TextContent.Contains("Root"))
            .QuerySelector(".lumeo-gantt-v3-tree-checkbox")!;

        Assert.Equal("true", child1Checkbox.GetAttribute("aria-checked"));
        Assert.Equal("mixed", root1Checkbox.GetAttribute("aria-checked"));
    }

    [Fact]
    public async Task Clicking_A_Fully_Checked_Parent_Checkbox_Deselects_Every_Descendant()
    {
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.ShowRowCheckboxes, true)
            .Add(c => c.SelectedIds, (ISet<string>)new HashSet<string> { "grandchild1" }));

        var child1Row = cut.FindAll("[data-row-kind='task']").Single(r => r.TextContent.Contains("Child One"));
        await cut.InvokeAsync(() => child1Row.QuerySelector(".lumeo-gantt-v3-tree-checkbox")!.Click());

        var grandchildCheckbox = cut.FindAll("[data-row-kind='task']").Single(r => r.TextContent.Contains("Grandchild"))
            .QuerySelector(".lumeo-gantt-v3-tree-checkbox")!;
        Assert.Equal("false", grandchildCheckbox.GetAttribute("aria-checked"));
    }

    [Fact]
    public void FlatGroup_Header_Checkbox_Reflects_Tri_State_Over_Its_Members()
    {
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, GroupFixture())
            .Add(c => c.GroupBy, (L.GanttTask t) => t.GroupLabel ?? "")
            .Add(c => c.ShowRowCheckboxes, true)
            .Add(c => c.SelectedIds, (ISet<string>)new HashSet<string> { "a" }));

        var groupRow = cut.FindAll("[data-row-kind='group']").Single();
        var groupCheckbox = groupRow.QuerySelector(".lumeo-gantt-v3-tree-checkbox")!;

        Assert.Equal("mixed", groupCheckbox.GetAttribute("aria-checked"));
    }

    // ── Readonly ─────────────────────────────────────────────────────────────

    [Fact]
    public void Checkbox_Is_Disabled_When_Readonly()
    {
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.ShowRowCheckboxes, true)
            .Add(c => c.Readonly, true));

        var checkbox = cut.Find(".lumeo-gantt-v3-tree-checkbox");
        Assert.True(checkbox.HasAttribute("disabled"));
    }

    // ── SelectedIds controlled/uncontrolled ─────────────────────────────────

    [Fact]
    public async Task Controlled_SelectedIds_Click_Raises_SelectedIdsChanged_With_The_Full_Resulting_Set()
    {
        ISet<string>? notified = null;
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.ShowRowCheckboxes, true)
            .Add(c => c.SelectedIds, (ISet<string>)new HashSet<string>())
            .Add(c => c.SelectedIdsChanged, (ISet<string> s) => notified = s));

        var grandchildRow = cut.FindAll("[data-row-kind='task']").Single(r => r.TextContent.Contains("Grandchild"));
        await cut.InvokeAsync(() => grandchildRow.QuerySelector(".lumeo-gantt-v3-tree-checkbox")!.Click());

        Assert.NotNull(notified);
        Assert.Contains("grandchild1", notified!);
    }

    [Fact]
    public async Task Controlled_SelectedIds_The_Parent_Ignores_Reverts_On_The_Next_Render()
    {
        // Same veto contract as Tasks/TreePaneWidth/ViewMode: a controlled
        // parent that doesn't update its own bound value in response to
        // SelectedIdsChanged is a veto.
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.ShowRowCheckboxes, true)
            .Add(c => c.SelectedIds, (ISet<string>)new HashSet<string>())
            .Add(c => c.SelectedIdsChanged, (ISet<string> _) => { /* parent ignores it */ }));

        var grandchildRow = cut.FindAll("[data-row-kind='task']").Single(r => r.TextContent.Contains("Grandchild"));
        await cut.InvokeAsync(() => grandchildRow.QuerySelector(".lumeo-gantt-v3-tree-checkbox")!.Click());

        // Genuinely new parameter pass, SelectedIds still empty (the parent
        // never adopted the pick) — not the known-trap parameterless
        // cut.Render() (see GanttV3CodexRound20Tests.cs's own remarks).
        cut.Render(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.ShowTreePane, true)
            .Add(c => c.ShowRowCheckboxes, true)
            .Add(c => c.SelectedIds, (ISet<string>)new HashSet<string>()));

        var checkbox = cut.FindAll("[data-row-kind='task']").Single(r => r.TextContent.Contains("Grandchild"))
            .QuerySelector(".lumeo-gantt-v3-tree-checkbox")!;
        Assert.Equal("false", checkbox.GetAttribute("aria-checked"));
    }

    [Fact]
    public void Uncontrolled_SelectedIds_Starts_Empty()
    {
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.ShowRowCheckboxes, true));

        Assert.All(cut.FindAll(".lumeo-gantt-v3-tree-checkbox"), c => Assert.Equal("false", c.GetAttribute("aria-checked")));
    }

    [Fact]
    public async Task Readonly_Blocks_Selection_Change_Even_When_Bubbled_Directly()
    {
        // Component-level defensive guard (Gantt3.HandleRowSelectionChangeAsync)
        // distinct from GanttTree's own Checkbox.Disabled — belt-and-suspenders,
        // same "public surface, don't trust it blindly" posture as every
        // JSInvokable guard in this campaign.
        var fired = false;
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.ShowRowCheckboxes, true)
            .Add(c => c.Readonly, true)
            .Add(c => c.SelectedIdsChanged, (ISet<string> _) => { fired = true; }));

        var tree = cut.FindComponent<L.GanttTree>();
        await cut.InvokeAsync(() => tree.Instance.OnRowSelectionChange.InvokeAsync(("grandchild1", true)));

        Assert.False(fired);
    }

    // ── AllowRowReorder: grip rendering ──────────────────────────────────────

    [Fact]
    public void AllowRowReorder_Default_Off_Renders_No_Grip()
    {
        var cut = RenderTree(p => p.Add(c => c.Tasks, HierarchyFixture()));

        Assert.Empty(cut.FindAll("[data-row-reorder-grip]"));
    }

    [Fact]
    public void AllowRowReorder_Renders_A_Live_Grip_Per_Task_Row_Never_A_GroupHeader_Row()
    {
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.AllowRowReorder, true));

        // 4 task rows (root1, child1, grandchild1, child2), no group headers in hierarchy mode.
        Assert.Equal(4, cut.FindAll("[data-row-reorder-grip]").Count);
    }

    [Fact]
    public void AllowRowReorder_With_Readonly_Renders_An_Inert_Grip_Not_A_Live_One()
    {
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.AllowRowReorder, true)
            .Add(c => c.Readonly, true));

        Assert.Empty(cut.FindAll("[data-row-reorder-grip]"));
        Assert.NotEmpty(cut.FindAll(".lumeo-gantt-v3-tree-reorder-grip")); // still visible, just inert
    }

    // ── Reorder commit pipeline (Gantt3.HandleRowReorderAsync) ───────────────

    [Fact]
    public async Task Reorder_Commit_Reorders_The_Task_List_And_Fires_Both_Events()
    {
        GanttRowReorder? reported = null;
        IEnumerable<L.GanttTask>? pushedTasks = null;
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.AllowRowReorder, true)
            .Add(c => c.OnRowReorder, (GanttRowReorder r) => reported = r)
            .Add(c => c.TasksChanged, (IEnumerable<L.GanttTask> t) => pushedTasks = t));

        var tree = cut.FindComponent<L.GanttTree>();
        // child1/child2 share root1 as ParentId — moving child2 to bucket
        // index 0 puts it before child1.
        await cut.InvokeAsync(() => tree.Instance.CommitRowReorder("child2", 0));

        Assert.NotNull(reported);
        Assert.Equal("child2", reported!.TaskId);
        Assert.Equal(1, reported.PreviousIndex);
        Assert.Equal(0, reported.NewIndex);
        Assert.Equal("root1", reported.PreviousParentId);
        Assert.Equal("root1", reported.NewParentId);

        Assert.NotNull(pushedTasks);
        var order = pushedTasks!.Select(t => t.Id).ToList();
        Assert.True(order.IndexOf("child2") < order.IndexOf("child1"));
    }

    [Fact]
    public async Task Reorder_Commit_To_The_Same_Position_Does_Not_Fire_Either_Event()
    {
        var reorderFired = false;
        var tasksChangedFired = false;
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.AllowRowReorder, true)
            .Add(c => c.OnRowReorder, (GanttRowReorder _) => reorderFired = true)
            .Add(c => c.TasksChanged, (IEnumerable<L.GanttTask> _) => tasksChangedFired = true));

        var tree = cut.FindComponent<L.GanttTree>();
        await cut.InvokeAsync(() => tree.Instance.CommitRowReorder("child1", 0)); // already at bucket-index 0

        Assert.False(reorderFired);
        Assert.False(tasksChangedFired);
    }

    [Fact]
    public async Task Readonly_Blocks_Reorder_Commit_Even_When_Bubbled_Directly()
    {
        // Component-level defensive guard (Gantt3.HandleRowReorderAsync)
        // distinct from GanttTree's own CommitRowReorder JSInvokable guard —
        // belt-and-suspenders, same reasoning as the selection-change guard
        // above.
        var fired = false;
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.AllowRowReorder, true)
            .Add(c => c.Readonly, true)
            .Add(c => c.OnRowReorder, (GanttRowReorder _) => fired = true));

        var reorder = new GanttRowReorder("child2", "root1", "root1", 1, 0);
        var gantt3 = cut.Instance;
        var method = typeof(L.Gantt3).GetMethod("HandleRowReorderAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        await cut.InvokeAsync(async () => await (Task)method.Invoke(gantt3, new object[] { reorder })!);

        Assert.False(fired);
    }

    // ── CanDropRow wiring ─────────────────────────────────────────────────────

    [Fact]
    public async Task CanDropRow_Rejecting_A_Position_Blocks_ValidateRowDrop()
    {
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.AllowRowReorder, true)
            .Add(c => c.CanDropRow, (L.GanttTask _, GanttDropContext _) => false));

        var tree = cut.FindComponent<L.GanttTree>();
        var valid = tree.Instance.ValidateRowDrop("child2", 0);

        Assert.False(valid);
    }

    [Fact]
    public void CanDropRow_Unset_Permits_Every_Position()
    {
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.AllowRowReorder, true));

        var tree = cut.FindComponent<L.GanttTree>();
        Assert.True(tree.Instance.ValidateRowDrop("child2", 0));
    }
}
