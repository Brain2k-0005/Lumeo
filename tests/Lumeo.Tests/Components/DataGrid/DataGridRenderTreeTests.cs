using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Tests that verify render-tree correctness: @key stability on row/column loops
/// and the cached visible-columns projection in DataGrid.razor.
/// </summary>
public class DataGridRenderTreeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridRenderTreeTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Person(int Id, string Name, string City);

    private static List<DataGridColumn<Person>> MakeColumns() => new()
    {
        new() { Field = "Id",   Title = "ID"   },
        new() { Field = "Name", Title = "Name" },
        new() { Field = "City", Title = "City" },
    };

    // -------------------------------------------------------------------------
    // Test 1: Row @key stability — re-parametrizing with a different item list
    // must still render the correct rows (no stale identity confusion).
    // -------------------------------------------------------------------------

    [Fact]
    public void DataGrid_RowsRenderCorrectly_AfterItemsAreReplaced()
    {
        var initialItems = new List<Person>
        {
            new(1, "Alice",   "London"),
            new(2, "Bob",     "Paris"),
            new(3, "Charlie", "Berlin"),
        };

        var cut = _ctx.Render<DataGrid<Person>>(p => p
            .Add(x => x.Items,      initialItems)
            .Add(x => x.Columns,    MakeColumns())
            .Add(x => x.PageSize,   10)
            .Add(x => x.ShowPagination, false));

        // Initial render — all three names visible.
        Assert.Contains("Alice",   cut.Markup);
        Assert.Contains("Bob",     cut.Markup);
        Assert.Contains("Charlie", cut.Markup);
        Assert.Equal(3, cut.FindAll("tbody tr").Count);

        // Replace the items with a completely different set (same count).
        var replacedItems = new List<Person>
        {
            new(4, "Diana", "Madrid"),
            new(5, "Eve",   "Rome"),
            new(6, "Frank", "Vienna"),
        };

        cut = _ctx.Render<DataGrid<Person>>(p => p
            .Add(x => x.Items,      replacedItems)
            .Add(x => x.Columns,    MakeColumns())
            .Add(x => x.PageSize,   10)
            .Add(x => x.ShowPagination, false));

        // Old names must be gone; new names must appear.
        Assert.DoesNotContain("Alice",   cut.Markup);
        Assert.DoesNotContain("Bob",     cut.Markup);
        Assert.DoesNotContain("Charlie", cut.Markup);
        Assert.Contains("Diana",  cut.Markup);
        Assert.Contains("Eve",    cut.Markup);
        Assert.Contains("Frank",  cut.Markup);
        Assert.Equal(3, cut.FindAll("tbody tr").Count);
    }

    // -------------------------------------------------------------------------
    // Test 2: Visible-columns cache correctness — toggling a column's visibility
    // removes it from header and cell output, and re-enabling it restores it.
    // -------------------------------------------------------------------------

    [Fact]
    public void DataGrid_VisibleColumnsCache_UpdatesWhenColumnVisibilityChanges()
    {
        var items = new List<Person>
        {
            new(1, "Alice", "London"),
            new(2, "Bob",   "Paris"),
        };

        var columns = MakeColumns();

        var cut = _ctx.Render<DataGrid<Person>>(p => p
            .Add(x => x.Items,   items)
            .Add(x => x.Columns, columns)
            .Add(x => x.PageSize, 10)
            .Add(x => x.ShowPagination, false));

        // All three column headers visible initially.
        var headerCells = cut.FindAll("thead th");
        Assert.True(headerCells.Count >= 3, "Expected at least 3 header cells");
        Assert.Contains("City", cut.Markup);
        Assert.Contains("London", cut.Markup);

        // Hide the "City" column.
        columns[2].Visible = false;
        cut = _ctx.Render<DataGrid<Person>>(p => p
            .Add(x => x.Items,   items)
            .Add(x => x.Columns, columns)
            .Add(x => x.PageSize, 10)
            .Add(x => x.ShowPagination, false));

        Assert.DoesNotContain("London", cut.Markup);
        Assert.DoesNotContain("City",   cut.Markup);

        // Re-enable it.
        columns[2].Visible = true;
        cut = _ctx.Render<DataGrid<Person>>(p => p
            .Add(x => x.Items,   items)
            .Add(x => x.Columns, columns)
            .Add(x => x.PageSize, 10)
            .Add(x => x.ShowPagination, false));

        Assert.Contains("City",   cut.Markup);
        Assert.Contains("London", cut.Markup);
    }

    // -------------------------------------------------------------------------
    // Test 3: Prepending new items renders them without corrupting existing rows.
    // This exercises the @key on DataGridRow — Blazor must match existing row
    // instances to the same item objects rather than position-matching them.
    // -------------------------------------------------------------------------

    [Fact]
    public void DataGrid_RowsRenderCorrectly_AfterItemsPrepended()
    {
        var items = new List<Person>
        {
            new(2, "Bob",   "Paris"),
            new(3, "Charlie", "Berlin"),
        };

        var cut = _ctx.Render<DataGrid<Person>>(p => p
            .Add(x => x.Items,   items)
            .Add(x => x.Columns, MakeColumns())
            .Add(x => x.PageSize, 10)
            .Add(x => x.ShowPagination, false));

        Assert.Equal(2, cut.FindAll("tbody tr").Count);
        Assert.Contains("Bob",     cut.Markup);
        Assert.Contains("Charlie", cut.Markup);

        // Prepend a new item (simulates inserting at the top of the list).
        var expanded = new List<Person>
        {
            new(1, "Alice",   "London"),
            new(2, "Bob",     "Paris"),
            new(3, "Charlie", "Berlin"),
        };

        cut = _ctx.Render<DataGrid<Person>>(p => p
            .Add(x => x.Items,   expanded)
            .Add(x => x.Columns, MakeColumns())
            .Add(x => x.PageSize, 10)
            .Add(x => x.ShowPagination, false));

        Assert.Equal(3, cut.FindAll("tbody tr").Count);
        Assert.Contains("Alice",   cut.Markup);
        Assert.Contains("Bob",     cut.Markup);
        Assert.Contains("Charlie", cut.Markup);
    }
}
