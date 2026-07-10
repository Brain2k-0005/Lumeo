using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Pinned-column reorder constraint: a column may only be reordered WITHIN its pin
/// partition (left / unpinned / right). A cross-partition drop is silently undone by
/// the pin re-partition on the next render, so the grid must reject it up front — no
/// drop indicator, no reorder event — on BOTH entry paths:
///   * the desktop native-DnD header drag (dragenter/drop), and
///   * the pointer-based (touch/pen) reorder commit routed through RegisterColumnReorder.
/// Same-partition reorders are the control and must still work.
/// </summary>
public class DataGridReorderConstraintTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DataGridReorderConstraintTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name, string Dept);

    private static List<Row> Data() => new() { new(1, "Alice", "Eng"), new(2, "Bob", "Sales") };

    // A = pinned Left, B + C = unpinned. Partition order: A | B, C.
    private (DataGridColumn<Row> a, DataGridColumn<Row> b, DataGridColumn<Row> c) Cols()
        => (
            new DataGridColumn<Row> { Field = "Id", Title = "A", Pin = PinDirection.Left },
            new DataGridColumn<Row> { Field = "Name", Title = "B" },
            new DataGridColumn<Row> { Field = "Dept", Title = "C" });

    private IRenderedComponent<Lumeo.DataGrid<Row>> RenderGrid(
        List<DataGridColumn<Row>> cols, Action<ColumnReorderEventArgs>? onReorder = null)
        => _ctx.Render<Lumeo.DataGrid<Row>>(p =>
        {
            p.Add(g => g.Items, Data());
            p.Add(g => g.Columns, cols);
            p.Add(g => g.Reorderable, true);
            if (onReorder is not null) p.Add(g => g.OnColumnReorder, onReorder);
        });

    private static List<string?> HeaderOrder(IRenderedComponent<Lumeo.DataGrid<Row>> cut)
        => cut.FindAll("th[data-slot='datagrid-header-cell']")
              .Select(h => h.GetAttribute("data-col-id")).ToList();

    // --- Drag path (desktop native DnD) ---

    [Fact]
    public void CrossPartition_DragEnter_Shows_No_Drop_Indicator()
    {
        var (a, b, c) = Cols();
        var cut = RenderGrid(new() { a, b, c });

        var headers = cut.FindAll("th[data-slot='datagrid-header-cell']");
        // Drag the pinned column A, hover the unpinned column B.
        headers[0].TriggerEvent("ondragstart", new DragEventArgs());
        cut.FindAll("th[data-slot='datagrid-header-cell']")[1].TriggerEvent("ondragenter", new DragEventArgs());

        Assert.Empty(cut.FindAll(".lumeo-datagrid-drop-indicator"));
    }

    [Fact]
    public void SamePartition_DragEnter_Shows_Drop_Indicator()
    {
        var (a, b, c) = Cols();
        var cut = RenderGrid(new() { a, b, c });

        var headers = cut.FindAll("th[data-slot='datagrid-header-cell']");
        // Drag unpinned B, hover unpinned C — both in the same (None) partition.
        headers[1].TriggerEvent("ondragstart", new DragEventArgs());
        cut.FindAll("th[data-slot='datagrid-header-cell']")[2].TriggerEvent("ondragenter", new DragEventArgs());

        Assert.NotEmpty(cut.FindAll(".lumeo-datagrid-drop-indicator"));
    }

    [Fact]
    public void CrossPartition_Drop_Is_Rejected_No_Reorder()
    {
        var (a, b, c) = Cols();
        ColumnReorderEventArgs? fired = null;
        var cut = RenderGrid(new() { a, b, c }, args => fired = args);
        var before = HeaderOrder(cut);

        var headers = cut.FindAll("th[data-slot='datagrid-header-cell']");
        headers[0].TriggerEvent("ondragstart", new DragEventArgs());
        cut.FindAll("th[data-slot='datagrid-header-cell']")[2].TriggerEvent("ondrop", new DragEventArgs());

        Assert.Null(fired);
        Assert.Equal(before, HeaderOrder(cut));
    }

    // --- Pointer/touch path (RegisterColumnReorder commit) ---

    [Fact]
    public void Reorderable_Grid_Registers_Pointer_Reorder_Listener()
    {
        var (a, b, c) = Cols();
        var cut = RenderGrid(new() { a, b, c });

        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id");
        Assert.Contains(gridId, _interop.ColumnReorderRegistrations);
    }

    [Fact]
    public async Task Touch_CrossPartition_Commit_Is_Rejected()
    {
        var (a, b, c) = Cols();
        ColumnReorderEventArgs? fired = null;
        var cut = RenderGrid(new() { a, b, c }, args => fired = args);
        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id")!;
        var before = HeaderOrder(cut);

        // Drop pinned A onto unpinned C — different partitions.
        await cut.InvokeAsync(() => _interop.SimulateColumnReorderCommit(gridId, a.Id, c.Id));

        Assert.Null(fired);
        Assert.Equal(before, HeaderOrder(cut));
    }

    [Fact]
    public async Task Touch_SamePartition_Commit_Reorders()
    {
        var (a, b, c) = Cols();
        ColumnReorderEventArgs? fired = null;
        var cut = RenderGrid(new() { a, b, c }, args => fired = args);
        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id")!;

        // Move unpinned B to C's slot — same (None) partition.
        await cut.InvokeAsync(() => _interop.SimulateColumnReorderCommit(gridId, b.Id, c.Id));

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(fired);
            Assert.Equal(b.Id, fired!.ColumnId);
            // A stays pinned first; B and C have swapped.
            Assert.Equal(new List<string?> { a.Id, c.Id, b.Id }, HeaderOrder(cut));
        });
    }

    [Fact]
    public async Task Touch_NonReorderable_Column_Commit_Is_Rejected()
    {
        var a = new DataGridColumn<Row> { Field = "Id", Title = "A", Reorderable = false };
        var b = new DataGridColumn<Row> { Field = "Name", Title = "B" };
        var c = new DataGridColumn<Row> { Field = "Dept", Title = "C" };
        ColumnReorderEventArgs? fired = null;
        var cut = RenderGrid(new() { a, b, c }, args => fired = args);
        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id")!;
        var before = HeaderOrder(cut);

        await cut.InvokeAsync(() => _interop.SimulateColumnReorderCommit(gridId, a.Id, c.Id));

        Assert.Null(fired);
        Assert.Equal(before, HeaderOrder(cut));
    }

    // --- Affordance structure ---

    [Fact]
    public void Reorderable_Columns_Render_Grip_And_Pin_Metadata()
    {
        var (a, b, c) = Cols();
        var cut = RenderGrid(new() { a, b, c });

        // Every reorderable header carries a touch drag grip.
        Assert.Equal(3, cut.FindAll("[data-reorder-grip]").Count);
        // The pinned column's th advertises its partition for the JS reorder clamp.
        var pinnedHeader = cut.FindAll("th[data-slot='datagrid-header-cell']")
                              .First(h => h.GetAttribute("data-col-id") == a.Id);
        Assert.Equal("Left", pinnedHeader.GetAttribute("data-col-pin"));
    }
}
