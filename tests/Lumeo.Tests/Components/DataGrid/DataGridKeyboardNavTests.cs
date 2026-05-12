using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Covers the rc.32 keyboard-navigation / ARIA-grid work on <see cref="DataGrid{TItem}"/>:
/// grid roles, sortable-header <c>aria-sort</c>, roving tabindex, arrow-key cell movement
/// and Space-to-toggle-selection.
/// </summary>
public class DataGridKeyboardNavTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridKeyboardNavTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record TestItem(int Id, string Name, decimal Amount);

    private static List<TestItem> Data() => new()
    {
        new(1, "Alice", 100m),
        new(2, "Bob", 200m),
        new(3, "Charlie", 150m),
    };

    private static List<DataGridColumn<TestItem>> Columns() => new()
    {
        new() { Field = "Id", Title = "ID", Sortable = true },
        new() { Field = "Name", Title = "Name" },
        new() { Field = "Amount", Title = "Amount", Sortable = true, Format = "C2" },
    };

    private IRenderedComponent<DataGrid<TestItem>> RenderGrid(
        DataGridSelectionMode selectionMode = DataGridSelectionMode.None)
        => _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.Columns, Columns())
            .Add(x => x.ShowPagination, false)
            .Add(x => x.SelectionMode, selectionMode));

    [Fact]
    public void Table_Has_Role_Grid()
    {
        var cut = RenderGrid();
        var table = cut.Find("table");
        Assert.Equal("grid", table.GetAttribute("role"));
        Assert.Equal("4", table.GetAttribute("aria-rowcount")); // 3 rows + header
    }

    [Fact]
    public void Body_Cells_Have_Role_Gridcell()
    {
        var cut = RenderGrid();
        var cells = cut.FindAll("tbody td[role='gridcell']");
        // 3 rows * 3 columns
        Assert.Equal(9, cells.Count);
    }

    [Fact]
    public void Sortable_Header_Has_Aria_Sort()
    {
        var cut = RenderGrid();
        var headerCells = cut.FindAll("thead th[role='columnheader']");
        // The sortable "ID" column starts at aria-sort="none".
        var idHeader = headerCells.First(c => c.TextContent.Contains("ID"));
        Assert.Equal("none", idHeader.GetAttribute("aria-sort"));

        // Non-sortable "Name" column carries no aria-sort.
        var nameHeader = headerCells.First(c => c.TextContent.Contains("Name"));
        Assert.Null(nameHeader.GetAttribute("aria-sort"));
    }

    [Fact]
    public void Exactly_One_Cell_Has_Tabindex_Zero()
    {
        var cut = RenderGrid();
        var focusable = cut.FindAll("[tabindex='0']");
        Assert.Single(focusable);
    }

    [Fact]
    public async Task ArrowDown_Moves_The_Focusable_Cell_Into_The_Body()
    {
        var cut = RenderGrid();

        // Initially the focusable cell is in the header row.
        var initial = cut.Find("[tabindex='0']");
        Assert.Equal("th", initial.NodeName, ignoreCase: true);

        // ArrowDown from the header → first body cell becomes focusable.
        await cut.InvokeAsync(() => cut.Find("[tabindex='0']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }));
        var afterFirst = cut.Find("[tabindex='0']");
        Assert.Equal("td", afterFirst.NodeName, ignoreCase: true);
        Assert.Equal("gridcell", afterFirst.GetAttribute("role"));
        var rowAfterFirst = afterFirst.ParentElement!.GetAttribute("aria-rowindex");

        // ArrowDown again → next body row.
        await cut.InvokeAsync(() => cut.Find("[tabindex='0']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }));
        var afterSecond = cut.Find("[tabindex='0']");
        var rowAfterSecond = afterSecond.ParentElement!.GetAttribute("aria-rowindex");
        Assert.NotEqual(rowAfterFirst, rowAfterSecond);

        // Still exactly one focusable cell.
        Assert.Single(cut.FindAll("[tabindex='0']"));
    }

    [Fact]
    public async Task Space_On_Focused_Cell_Toggles_Row_Selection_When_Multiple()
    {
        var cut = RenderGrid(DataGridSelectionMode.Multiple);

        // Move focus into the first body cell.
        await cut.InvokeAsync(() => cut.Find("[tabindex='0']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }));

        Assert.Empty(cut.FindAll("tbody tr[aria-selected='true']"));

        // Space toggles selection of that row.
        await cut.InvokeAsync(() => cut.Find("[tabindex='0']").KeyDown(new KeyboardEventArgs { Key = " " }));
        var afterSelect = cut.FindAll("tbody tr[aria-selected='true']");
        Assert.Single(afterSelect);

        // Space again clears it.
        await cut.InvokeAsync(() => cut.Find("[tabindex='0']").KeyDown(new KeyboardEventArgs { Key = " " }));
        Assert.Empty(cut.FindAll("tbody tr[aria-selected='true']"));
    }
}
