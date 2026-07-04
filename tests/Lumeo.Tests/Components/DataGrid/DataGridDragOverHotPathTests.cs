using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Performance-regression guard for the header/group-panel drag hot path.
///
/// dragover fires ~60×/second per element. Binding it to a .NET handler forced a
/// Blazor re-render on every tick — on the group panel (which lives on the DataGrid
/// root) that meant a full-grid re-render measured at ~198ms each, freezing the main
/// thread while dragging a header over the panel; on each header cell it re-rendered
/// the cell ~60×/s. The fix removes the @ondragover .NET bindings entirely and keeps
/// only the native :preventDefault directive (no C# round-trip) so the browser still
/// accepts the drop. The drop indicator / accept-highlight are driven by the
/// boundary events dragenter / dragleave, which fire a handful of times per drag.
///
/// These tests lock that contract in: dragover must NOT be a .NET-bound handler on
/// either the group panel or a draggable header, while dragstart → drop must still
/// add a grouping level (drag semantics preserved).
/// </summary>
public class DataGridDragOverHotPathTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridDragOverHotPathTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Employee(int Id, string Name, string Department, string Status);

    private static List<Employee> GetEmployees() => new()
    {
        new(1, "Alice", "Engineering", "Active"),
        new(2, "Bob",   "Marketing",   "Inactive"),
    };

    private static List<DataGridColumn<Employee>> GetColumns() => new()
    {
        new() { Field = "Id",         Title = "ID" },
        new() { Field = "Name",       Title = "Name" },
        new() { Field = "Department", Title = "Department", Groupable = true },
        new() { Field = "Status",     Title = "Status",     Groupable = true },
    };

    private IRenderedComponent<DataGrid<Employee>> RenderGrid() =>
        _ctx.Render<DataGrid<Employee>>(p =>
        {
            p.Add(x => x.Items, GetEmployees());
            p.Add(x => x.Columns, GetColumns());
            p.Add(x => x.ShowGroupPanel, true);
            p.Add(x => x.Reorderable, true);
        });

    // The core regression: dragover on the group panel is NOT wired to a .NET
    // handler. bUnit throws MissingEventHandlerException when asked to raise an
    // event that has no registered C# callback — that is exactly the proof that
    // a dragover tick can never cross into .NET (and therefore can never force a
    // full-grid re-render). If someone re-adds @ondragover="Handler", this throws
    // nothing and the test fails.
    [Fact]
    public void GroupPanel_DragOver_IsNotBoundToDotNet()
    {
        var cut = RenderGrid();
        var panel = cut.Find("[data-slot=\"datagrid-group-panel\"]");

        Assert.Throws<MissingEventHandlerException>(() =>
            panel.TriggerEvent("ondragover", new Microsoft.AspNetCore.Components.Web.DragEventArgs()));
    }

    [Fact]
    public void DraggableHeader_DragOver_IsNotBoundToDotNet()
    {
        var cut = RenderGrid();
        // Department (index 2) is Groupable + draggable.
        var header = cut.FindAll("th[data-slot=\"datagrid-header-cell\"]")[2];

        Assert.Throws<MissingEventHandlerException>(() =>
            header.TriggerEvent("ondragover", new Microsoft.AspNetCore.Components.Web.DragEventArgs()));
    }

    // Drag semantics preserved: starting a drag on a Groupable header and dropping
    // on the panel still adds that column as a grouping level — proving we only
    // removed the redundant dragover churn, not the drop pipeline.
    [Fact]
    public void DragStart_Then_DropOnPanel_StillAddsGroupLevel()
    {
        var cut = RenderGrid();

        var deptHeader = cut.FindAll("th[data-slot=\"datagrid-header-cell\"]")[2];
        deptHeader.TriggerEvent("ondragstart", new Microsoft.AspNetCore.Components.Web.DragEventArgs());

        var panel = cut.Find("[data-slot=\"datagrid-group-panel\"]");
        panel.TriggerEvent("ondrop", new Microsoft.AspNetCore.Components.Web.DragEventArgs());

        // A Department grouping chip must now exist in the panel.
        panel = cut.Find("[data-slot=\"datagrid-group-panel\"]");
        Assert.Contains("Department", panel.TextContent);
        Assert.DoesNotContain("Drag a Groupable column header here", cut.Markup);
    }

    // The accept-highlight is still driven by the boundary events, so dragging over
    // the panel gives visual feedback without the per-tick dragover cost.
    [Fact]
    public void GroupPanel_DragEnter_TogglesAcceptHighlight()
    {
        var cut = RenderGrid();

        var deptHeader = cut.FindAll("th[data-slot=\"datagrid-header-cell\"]")[2];
        deptHeader.TriggerEvent("ondragstart", new Microsoft.AspNetCore.Components.Web.DragEventArgs());

        var panel = cut.Find("[data-slot=\"datagrid-group-panel\"]");
        panel.TriggerEvent("ondragenter", new Microsoft.AspNetCore.Components.Web.DragEventArgs());

        // Highlight classes applied on enter.
        Assert.Contains("border-primary", cut.Find("[data-slot=\"datagrid-group-panel\"]").GetAttribute("class"));

        cut.Find("[data-slot=\"datagrid-group-panel\"]").TriggerEvent("ondragleave", new Microsoft.AspNetCore.Components.Web.DragEventArgs());
        Assert.DoesNotContain("border-primary", cut.Find("[data-slot=\"datagrid-group-panel\"]").GetAttribute("class"));
    }
}
