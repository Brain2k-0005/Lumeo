using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Structural invariants for the FLIP-reorder pipeline: the grid root carries
/// a stable <c>data-grid-id</c>, and every header cell carries a stable
/// <c>data-col-id</c>. JS captureColumnRects / animateColumnReorder rely on
/// these attributes to look up the right grid and to identify columns across
/// the reorder (the DOM <c>id</c> on header cells is index-based and can't
/// survive a reorder).
/// </summary>
public class DataGridReorderFlipTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DataGridReorderFlipTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    [Fact]
    public void Grid_Root_Has_Data_Grid_Id_Attribute()
    {
        var cols = new List<DataGridColumn<Row>>
        {
            new() { Field = "Id",   Title = "ID" },
            new() { Field = "Name", Title = "Name" },
        };

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, new List<Row> { new(1, "Alice") })
            .Add(g => g.Columns, cols));

        var root = cut.Find("[data-slot='datagrid']");
        Assert.True(root.HasAttribute("data-grid-id"));
        Assert.False(string.IsNullOrWhiteSpace(root.GetAttribute("data-grid-id")));
    }

    [Fact]
    public void Header_Cells_Carry_Data_Col_Id_Matching_Column_Id()
    {
        var idCol = new DataGridColumn<Row> { Field = "Id", Title = "ID" };
        var nameCol = new DataGridColumn<Row> { Field = "Name", Title = "Name" };

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, new List<Row> { new(1, "Alice") })
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { idCol, nameCol }));

        var headers = cut.FindAll("th[data-slot='datagrid-header-cell']");
        Assert.Equal(2, headers.Count);
        // Order: cols list order → ID first, Name second
        Assert.Equal(idCol.Id, headers[0].GetAttribute("data-col-id"));
        Assert.Equal(nameCol.Id, headers[1].GetAttribute("data-col-id"));
    }
}
