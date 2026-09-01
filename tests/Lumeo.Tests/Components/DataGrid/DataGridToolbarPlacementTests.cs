using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// #441. The toolbar tools could only render inside the grid's own chrome, and the layouts
/// menu appeared unconditionally with layout persistence. A consumer who wanted saved layouts
/// but a toolbar carrying only their own controls had no expression for that, and one who
/// wanted the column chooser in a page-level action bar had to rebuild it by hand against
/// each column's Visible binding — losing the reorder arrows and the pin submenu with it.
/// </summary>
public class DataGridToolbarPlacementTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridToolbarPlacementTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static List<DataGridColumn<Row>> Cols() =>
        new() { new DataGridColumn<Row> { Field = "Name", Title = "Name" } };

    private IRenderedComponent<Lumeo.DataGrid<Row>> RenderGrid(bool showLayouts) =>
        _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, new List<Row> { new(1, "a") })
            .Add(g => g.Columns, Cols())
            .Add(g => g.ShowToolbar, true)
            .Add(g => g.EnableLayoutPersistence, true)
            .Add(g => g.ShowLayouts, showLayouts));

    [Fact]
    public void ShowLayouts_Hides_The_Menu_Without_Giving_Up_Persistence()
    {
        var with = RenderGrid(showLayouts: true);
        var without = RenderGrid(showLayouts: false);

        // The layouts tool renders its own trigger; the marker is enough to tell them apart.
        var hasWith = with.FindAll("[id^='dg-layouts-']").Count;
        var hasWithout = without.FindAll("[id^='dg-layouts-']").Count;

        Assert.True(hasWith > 0, "layouts menu should render when ShowLayouts is on");
        Assert.Equal(0, hasWithout);
    }

    [Fact]
    public void A_Toolbar_Tool_Outside_The_Grid_Drives_It_Through_The_Grid_Parameter()
    {
        // The grid renders WITHOUT its toolbar, exactly as the reporter configured it.
        var grid = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, new List<Row> { new(1, "a") })
            .Add(g => g.Columns, Cols())
            .Add(g => g.ShowToolbar, false));

        // ...and the column chooser is placed somewhere else entirely, with no cascade to
        // inherit from. Before 5.1 this rendered nothing at all.
        var tool = _ctx.Render<Lumeo.DataGridToolbarColumns<Row>>(p => p
            .Add(t => t.Grid, grid.Instance));

        Assert.Contains("dg-columns-trigger-", tool.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Inside_The_Grid_The_Cascade_Still_Wins()
    {
        // Grid unset, cascade present: the ordinary in-toolbar case must not regress.
        var cut = RenderGrid(showLayouts: true);

        Assert.Contains("dg-columns-trigger-", cut.Markup, StringComparison.Ordinal);
    }
}
