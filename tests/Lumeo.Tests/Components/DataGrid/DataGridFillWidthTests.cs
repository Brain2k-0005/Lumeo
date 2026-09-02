using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>Fixed layout once every visible column is sized, and the FillWidth column that
/// absorbs the remainder: the pieces that make a drag move one edge only.</summary>
public class DataGridFillWidthTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridFillWidthTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name, string City);

    private IRenderedComponent<DataGrid<Row>> RenderGrid(List<DataGridColumn<Row>> cols)
        => _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, new List<Row> { new(1, "Alice", "Berlin") })
            .Add(x => x.Columns, cols)
            .Add(x => x.ShowPagination, false)
            .Add(x => x.ShowToolbar, false));

    private static string TableStyle(IRenderedComponent<DataGrid<Row>> cut) => cut.Find("table").GetAttribute("style") ?? "";
    private static string HeadStyle(IRenderedComponent<DataGrid<Row>> cut, int i) => cut.FindAll("th[data-slot=\"datagrid-header-cell\"]")[i].GetAttribute("style") ?? "";

    [Fact]
    public void All_Columns_Sized_Lays_Out_Fixed_At_The_Sum()
    {
        var cut = RenderGrid(new()
        {
            new() { Field = "Name", Title = "Name", Width = 200 },
            new() { Field = "City", Title = "City", Width = 150 },
        });
        Assert.Equal("table-layout: fixed; width: 350px;", TableStyle(cut));
        Assert.Contains("width: 200px", HeadStyle(cut, 0));
    }

    [Fact]
    public void A_Fill_Column_Keeps_The_Table_At_Container_Width_And_Carries_No_Width()
    {
        var cut = RenderGrid(new()
        {
            new() { Field = "Name", Title = "Name", Width = 200 },
            new() { Field = "City", Title = "City", Width = 150, FillWidth = true },
        });
        Assert.Equal("table-layout: fixed; width: 350px; min-width: 100%;", TableStyle(cut));
        var fill = cut.FindAll("th[data-slot=\"datagrid-header-cell\"]")[1];
        Assert.Equal("true", fill.GetAttribute("data-fill-width"));
        Assert.DoesNotContain("width: 150px", HeadStyle(cut, 1).Replace("min-width: 150px", ""));
        Assert.Contains("min-width: 150px", HeadStyle(cut, 1));
        Assert.Null(cut.FindAll("th[data-slot=\"datagrid-header-cell\"]")[0].GetAttribute("data-fill-width"));
    }

    [Fact]
    public void An_Unsized_Column_Keeps_The_Auto_Layout()
    {
        var cut = RenderGrid(new()
        {
            new() { Field = "Name", Title = "Name", Width = 200 },
            new() { Field = "City", Title = "City" },
        });
        Assert.Equal("", TableStyle(cut));
    }

    [Fact]
    public void ColumnDef_FillWidth_Reaches_The_Column()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, new List<Row> { new(1, "Alice", "Berlin") })
            .Add(x => x.ShowPagination, false)
            .Add(x => x.ShowToolbar, false)
            .AddChildContent<DataGridColumnDef<Row>>(c => c.Add(x => x.Field, "Name").Add(x => x.Width, 120.0).Add(x => x.FillWidth, true)));
        Assert.Equal("true", cut.Find("th[data-slot=\"datagrid-header-cell\"]").GetAttribute("data-fill-width"));
    }
}
