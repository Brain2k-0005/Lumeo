using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// rc.42: drag-to-group was unified into the SAME pointer engine that drives column
/// reorder (mouse + touch + pen) — native HTML5 DnD (dragstart on a Groupable header,
/// dragenter/dragleave for the panel accept-highlight, drop on the panel) is gone
/// entirely, replacing <c>DataGridDragOverHotPathTests</c>.
///
/// components.js's registerColumnReorder now hit-tests an armed column drag against
/// the group panel's cached rect and, on a valid drop there, invokes the EXACT SAME
/// <c>OnColumnReorderCommit</c> channel column reorder already used — passing a
/// sentinel ("__group-panel__", DataGrid.razor's <c>GroupPanelDropTargetId</c>) as the
/// "target id" instead of a real column id. These tests drive that boundary directly
/// via <see cref="TrackingInteropService.SimulateColumnReorderCommit"/>, mirroring
/// DataGridReorderConstraintTests' own pointer-commit coverage — no real DOM/JS needed
/// to prove the C# routing (branch to <c>HandleColumnDroppedOnGroupPanel</c> /
/// <c>AddGroupField</c> instead of <c>ReorderColumnByIdAsync</c>) is correct.
/// </summary>
public class DataGridReorderGroupPanelUnificationTests : IAsyncLifetime
{
    // Must match DataGrid.razor's private GroupPanelDropTargetId and
    // components.js's GROUP_PANEL_DROP_TARGET_ID exactly — there is no shared
    // symbol across the JS/.NET boundary, so this lock-in test is the contract.
    private const string GroupPanelSentinel = "__group-panel__";

    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DataGridReorderGroupPanelUnificationTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Employee(int Id, string Name, string Department, string Status);

    private static List<Employee> GetEmployees() => new()
    {
        new(1, "Alice", "Engineering", "Active"),
        new(2, "Bob",   "Marketing",   "Inactive"),
    };

    // Column instances are handed back to the test (not re-looked-up through the
    // grid's own private EffectiveColumns) so their stable .Id can be used
    // directly to build a commit — mirrors DataGridReorderConstraintTests' Cols()
    // pattern.
    private (DataGridColumn<Employee> id, DataGridColumn<Employee> name, DataGridColumn<Employee> dept, DataGridColumn<Employee> status) Cols()
        => (
            new DataGridColumn<Employee> { Field = "Id", Title = "ID" },
            new DataGridColumn<Employee> { Field = "Name", Title = "Name" },
            new DataGridColumn<Employee> { Field = "Department", Title = "Department", Groupable = true },
            new DataGridColumn<Employee> { Field = "Status", Title = "Status", Groupable = true });

    private IRenderedComponent<Lumeo.DataGrid<Employee>> RenderGrid(
        List<DataGridColumn<Employee>> cols, bool showGroupPanel = true) =>
        _ctx.Render<Lumeo.DataGrid<Employee>>(p =>
        {
            p.Add(x => x.Items, GetEmployees());
            p.Add(x => x.Columns, cols);
            p.Add(x => x.ShowGroupPanel, showGroupPanel);
            p.Add(x => x.Reorderable, true);
        });

    // --- No native DnD bindings survive on the panel or the header at all ---
    // (DataGridDragOverHotPathTests' old dragover-hot-path regression coverage,
    // extended to every native drag event since rc.42 removes all four, not just
    // dragover).

    [Theory]
    [InlineData("ondragover")]
    [InlineData("ondragenter")]
    [InlineData("ondragleave")]
    [InlineData("ondrop")]
    public void GroupPanel_Has_No_Native_Drag_Event_Bound_To_DotNet(string domEvent)
    {
        var (id, name, dept, status) = Cols();
        var cut = RenderGrid(new() { id, name, dept, status });
        var panel = cut.Find("[data-slot=\"datagrid-group-panel\"]");

        Assert.Throws<MissingEventHandlerException>(() =>
            panel.TriggerEvent(domEvent, new Microsoft.AspNetCore.Components.Web.DragEventArgs()));
    }

    [Fact]
    public void GroupableHeader_Has_No_Native_Drag_Event_Bound_To_DotNet()
    {
        var (id, name, dept, status) = Cols();
        var cut = RenderGrid(new() { id, name, dept, status });
        var deptHeader = cut.FindAll("th[data-slot=\"datagrid-header-cell\"]")
            .First(h => h.GetAttribute("data-col-id") == dept.Id);

        Assert.Throws<MissingEventHandlerException>(() =>
            deptHeader.TriggerEvent("ondragstart", new Microsoft.AspNetCore.Components.Web.DragEventArgs()));
    }

    // --- Group-panel commit routing (the unified pointer engine's job in prod) ---

    [Fact]
    public async Task Commit_With_GroupPanelSentinel_Adds_Grouping_Level_For_Groupable_Column()
    {
        var (id, name, dept, status) = Cols();
        var cut = RenderGrid(new() { id, name, dept, status });
        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id")!;

        await cut.InvokeAsync(() => _interop.SimulateColumnReorderCommit(gridId, dept.Id, GroupPanelSentinel));

        cut.WaitForAssertion(() =>
        {
            var panel = cut.Find("[data-slot=\"datagrid-group-panel\"]");
            Assert.Contains("Department", panel.TextContent);
        });
        var layout = cut.Instance.GetCurrentLayout();
        Assert.Contains("Department", layout.GroupByFields!);
    }

    [Fact]
    public async Task Commit_With_GroupPanelSentinel_For_NonGroupable_Column_Is_A_No_Op()
    {
        // JS only ever sends the sentinel when the dragged column's own
        // data-groupable attribute was true (see components.js's
        // updateGroupPanelMode / onPointerUp) — but the C# boundary must reject
        // it defensively too, exactly like every other commit-guard check in
        // this area (ValidateColumnReorderCommitAsync's philosophy).
        var (id, name, dept, status) = Cols();
        var cut = RenderGrid(new() { id, name, dept, status }); // id is not Groupable
        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id")!;

        await cut.InvokeAsync(() => _interop.SimulateColumnReorderCommit(gridId, id.Id, GroupPanelSentinel));

        var layout = cut.Instance.GetCurrentLayout();
        Assert.True(layout.GroupByFields is null || layout.GroupByFields.Count == 0);
    }

    [Fact]
    public async Task Commit_With_GroupPanelSentinel_When_ShowGroupPanel_Off_Is_A_No_Op()
    {
        var (id, name, dept, status) = Cols();
        var cut = RenderGrid(new() { id, name, dept, status }, showGroupPanel: false);
        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id")!;

        await cut.InvokeAsync(() => _interop.SimulateColumnReorderCommit(gridId, dept.Id, GroupPanelSentinel));

        var layout = cut.Instance.GetCurrentLayout();
        Assert.True(layout.GroupByFields is null || layout.GroupByFields.Count == 0);
    }

    [Fact]
    public async Task Commit_With_GroupPanelSentinel_Reuses_AddGroupField_Same_As_Dropdown_Entry_Point()
    {
        // Same entry point as the panel's own "Add group level" dropdown — proves
        // the pointer-engine drop and the dropdown converge on identical grid
        // state (auto-hide/unpin of the grouped column, runtime group fields),
        // not two parallel code paths that could drift apart.
        var (id, name, dept, status) = Cols();
        var cut = RenderGrid(new() { id, name, dept, status });
        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id")!;

        await cut.InvokeAsync(() => _interop.SimulateColumnReorderCommit(gridId, status.Id, GroupPanelSentinel));

        cut.WaitForAssertion(() =>
        {
            // Grouped column is auto-hidden from the data columns, same as the
            // dropdown path (DataGrid.AddGroupField).
            Assert.DoesNotContain(cut.FindAll("th[data-slot='datagrid-header-cell']"),
                h => h.GetAttribute("data-col-id") == status.Id);
        });
    }
}
