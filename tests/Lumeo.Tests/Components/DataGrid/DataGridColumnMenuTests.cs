using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>ColumnMenu="true": the header click opens shadcn/ReUI's column menu instead of
/// sorting, the entries follow the column's flags, and OnHeaderClick can cancel either
/// response (field report 1.18).</summary>
public class DataGridColumnMenuTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridColumnMenuTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name, string City);

    private static List<Row> Data() => new()
    {
        new(1, "Alice", "Berlin"), new(2, "Bob", "Zagreb"), new(3, "Charlie", "Vienna"),
    };

    private static List<DataGridColumn<Row>> Columns() => new()
    {
        new() { Field = "Name", Title = "Name", Sortable = true, Pinnable = true, Reorderable = true, Resizable = true },
        new() { Field = "City", Title = "City", Sortable = false, Pinnable = false, Resizable = false, Reorderable = true },
    };

    private IRenderedComponent<DataGrid<Row>> RenderGrid(bool columnMenu = true, EventCallback<ColumnHeaderClickEventArgs>? onHeaderClick = null)
        => _ctx.Render<DataGrid<Row>>(p =>
        {
            p.Add(x => x.Items, Data())
             .Add(x => x.Columns, Columns())
             .Add(x => x.ColumnMenu, columnMenu)
             .Add(x => x.Reorderable, true)
             .Add(x => x.ShowPagination, false)
             .Add(x => x.ShowToolbar, false);
            if (onHeaderClick is not null) p.Add(x => x.OnHeaderClick, onHeaderClick.Value);
        });

    private static AngleSharp.Dom.IElement SortButton(IRenderedComponent<DataGrid<Row>> cut, int col)
        => cut.FindAll("th[data-slot=\"datagrid-header-cell\"]")[col].QuerySelector("[data-slot=\"datagrid-sort-button\"]")!;

    private static string AriaSort(IRenderedComponent<DataGrid<Row>> cut, int col)
        => cut.FindAll("th[data-slot=\"datagrid-header-cell\"]")[col].GetAttribute("aria-sort") ?? "";

    private static List<string> MenuLabels(IRenderedComponent<DataGrid<Row>> cut)
        => cut.FindAll("[role=\"menu\"] button span.grow").Select(e => e.TextContent.Trim()).ToList();

    [Fact]
    public void Header_Click_Opens_The_Menu_Instead_Of_Sorting()
    {
        var cut = RenderGrid();
        SortButton(cut, 0).Click();

        var menu = cut.Find("[role=\"menu\"]");
        Assert.Equal("Column menu", menu.GetAttribute("aria-label"));
        Assert.Equal("true", SortButton(cut, 0).GetAttribute("aria-expanded"));
        Assert.Equal("none", AriaSort(cut, 0));
    }

    [Fact]
    public void Entries_Follow_The_Column_Flags()
    {
        var cut = RenderGrid();
        SortButton(cut, 0).Click();
        Assert.Equal(new[] { "Sort ascending", "Sort descending", "Fit to content", "Pin to left", "Pin to right", "Move left", "Move right" }, MenuLabels(cut));
        Assert.Equal(3, cut.FindAll("[role=\"menu\"] [role=\"separator\"]").Count);

        // the City column: not sortable, not pinnable, not resizable, so only the move entries remain
        SortButton(cut, 0).Click();
        SortButton(cut, 1).Click();
        Assert.Equal(new[] { "Move left", "Move right" }, MenuLabels(cut));
        Assert.Empty(cut.FindAll("[role=\"menu\"] [role=\"separator\"]"));
    }

    [Fact]
    public void Sort_Ascending_Entry_Sorts_And_Picking_It_Again_Clears()
    {
        var cut = RenderGrid();
        SortButton(cut, 0).Click();
        cut.FindAll("[role=\"menu\"] button")[0].Click();

        Assert.Equal("ascending", AriaSort(cut, 0));
        Assert.Empty(cut.FindAll("[role=\"menu\"]"));

        SortButton(cut, 0).Click();
        Assert.Equal("true", cut.FindAll("[role=\"menu\"] button")[0].GetAttribute("aria-checked"));
        cut.FindAll("[role=\"menu\"] button")[0].Click();
        Assert.Equal("none", AriaSort(cut, 0));
    }

    [Fact]
    public void Pin_Left_Entry_Pins_The_Column()
    {
        var cut = RenderGrid();
        SortButton(cut, 0).Click();
        cut.FindAll("[role=\"menu\"] button").First(b => b.TextContent.Contains("Pin to left")).Click();

        var th = cut.FindAll("th[data-slot=\"datagrid-header-cell\"]").First(t => t.GetAttribute("data-col-id") is not null);
        Assert.Equal("Left", th.GetAttribute("data-col-pin"));
    }

    [Fact]
    public void Move_Right_Entry_Reorders_And_The_Last_Column_Cannot_Move_Further()
    {
        var cut = RenderGrid();
        SortButton(cut, 0).Click();
        var right = cut.FindAll("[role=\"menu\"] button").First(b => b.TextContent.Contains("Move right"));
        Assert.False(right.HasAttribute("disabled"));
        right.Click();

        var titles = cut.FindAll("th[data-slot=\"datagrid-header-cell\"] [data-slot=\"datagrid-sort-button\"] span.truncate").Select(e => e.TextContent.Trim()).ToList();
        Assert.Equal(new[] { "City", "Name" }, titles);

        SortButton(cut, 1).Click();
        right = cut.FindAll("[role=\"menu\"] button").First(b => b.TextContent.Contains("Move right"));
        Assert.True(right.HasAttribute("disabled"));
    }

    [Fact]
    public void OnHeaderClick_With_PreventDefault_Stops_Both_The_Menu_And_The_Sort()
    {
        string? clicked = null;
        var cb = EventCallback.Factory.Create<ColumnHeaderClickEventArgs>(this, a => { clicked = a.Field; a.PreventDefault = true; });

        var withMenu = RenderGrid(columnMenu: true, onHeaderClick: cb);
        SortButton(withMenu, 0).Click();
        Assert.Equal("Name", clicked);
        Assert.Empty(withMenu.FindAll("[role=\"menu\"]"));

        clicked = null;
        var sorting = RenderGrid(columnMenu: false, onHeaderClick: cb);
        SortButton(sorting, 0).Click();
        Assert.Equal("Name", clicked);
        Assert.Equal("none", AriaSort(sorting, 0));
    }

    [Fact]
    public void Without_ColumnMenu_The_Header_Still_Sorts()
    {
        var cut = RenderGrid(columnMenu: false);
        SortButton(cut, 0).Click();
        Assert.Equal("ascending", AriaSort(cut, 0));
        Assert.Empty(cut.FindAll("[role=\"menu\"]"));
        Assert.Null(SortButton(cut, 0).GetAttribute("aria-haspopup"));
    }
}
