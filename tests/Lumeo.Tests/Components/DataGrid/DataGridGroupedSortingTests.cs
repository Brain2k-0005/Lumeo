using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Regression tests for a reported bug: with grouping active, clicking a
/// column header to toggle Ascending/Descending sort appeared to do nothing.
///
/// Root cause: <c>ProcessSingleLevelGrouping</c> and <c>BuildGroupNodes</c>
/// (multi-level) built the group list via
/// <c>.GroupBy(...).OrderBy(g => g.Key, StringComparer.CurrentCulture)</c> —
/// the group ORDER (which group section/node comes first) was hardcoded
/// ascending-by-key, completely independent of the grid's active Sorts. Row
/// order WITHIN a group was never actually broken: LINQ's GroupBy preserves
/// the source sequence's relative order inside each group, and the sort is
/// applied to `_processedItems` before grouping — so sorting a column OTHER
/// than the grouped one already worked. The bug is specifically: sorting the
/// GROUPED column itself had zero visible effect, since a group's members
/// all share the same value for that field (sorting within the group is
/// necessarily a no-op) and group order — the only place the sort direction
/// COULD show up — was hardcoded.
///
/// Fix: group order now honours the active <c>Sorts</c> entry for the
/// grouping field (Descending reverses the group run), falling back to the
/// original ascending-by-key default when that field has no active sort.
/// This matches how grouped columns behave in TanStack Table / AG Grid / MUI
/// DataGrid — sorting the grouped field controls group order; sorting any
/// other field controls order within each group.
/// </summary>
public class DataGridGroupedSortingTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridGroupedSortingTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private class Row
    {
        public int Id { get; set; }
        public string Category { get; set; } = "";
        public string Name { get; set; } = "";
    }

    private static List<Row> Data() => new()
    {
        new Row { Id = 1, Category = "B", Name = "Zed" },
        new Row { Id = 2, Category = "A", Name = "Amy" },
        new Row { Id = 3, Category = "B", Name = "Anna" },
        new Row { Id = 4, Category = "A", Name = "Bob" },
    };

    private static List<DataGridColumn<Row>> Columns() => new()
    {
        new() { Field = "Category", Title = "Category", Sortable = true, Groupable = true },
        new() { Field = "Name", Title = "Name", Sortable = true },
    };

    private static AngleSharp.Dom.IElement SortButtonFor<TItem>(IRenderedComponent<Lumeo.DataGrid<TItem>> cut, string title)
    {
        var header = cut.FindAll("th[data-slot='datagrid-header-cell']").First(h => h.TextContent.Contains(title));
        return header.QuerySelector("button[data-slot='datagrid-sort-button']")!;
    }

    private static List<string> NameCellValues(IRenderedComponent<Lumeo.DataGrid<Row>> cut) =>
        cut.FindAll("[data-slot='datagrid-row']")
            .Select(row => row.QuerySelectorAll("td").ElementAt(1).TextContent.Trim())
            .ToList();

    // ── Group order follows the GROUPED column's own sort direction ─────────

    [Fact]
    public void Sorting_The_Grouped_Column_Ascending_Keeps_Default_AZ_Group_Order()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.GroupBy, "Category")
            .Add(x => x.Columns, Columns()));

        SortButtonFor(cut, "Category").Click(); // None -> Ascending

        Assert.Equal(new[] { "A", "B" }, cut.FindAll("[data-slot='datagrid-group-row']").Select(e => e.TextContent.Trim()[..1]));
    }

    [Fact]
    public void Sorting_The_Grouped_Column_Descending_Reverses_Group_Order()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.GroupBy, "Category")
            .Add(x => x.Columns, Columns()));

        var sortBtn = SortButtonFor(cut, "Category");
        sortBtn.Click(); // Ascending
        sortBtn.Click(); // Descending

        Assert.Equal(new[] { "B", "A" }, cut.FindAll("[data-slot='datagrid-group-row']").Select(e => e.TextContent.Trim()[..1]));
    }

    [Fact]
    public void Clearing_Sort_On_The_Grouped_Column_Restores_Default_AZ_Order()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.GroupBy, "Category")
            .Add(x => x.Columns, Columns()));

        var sortBtn = SortButtonFor(cut, "Category");
        sortBtn.Click(); // Ascending
        sortBtn.Click(); // Descending
        sortBtn.Click(); // None

        Assert.Equal(new[] { "A", "B" }, cut.FindAll("[data-slot='datagrid-group-row']").Select(e => e.TextContent.Trim()[..1]));
    }

    // ── Sorting a NON-grouped column still reorders rows WITHIN each group ──

    [Fact]
    public void Sorting_A_Nongrouped_Column_Reorders_Rows_Within_Groups_Both_Directions()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.GroupBy, "Category")
            .Add(x => x.Columns, Columns()));

        var sortBtn = SortButtonFor(cut, "Name");
        sortBtn.Click(); // Ascending by Name
        var namesAsc = NameCellValues(cut);

        sortBtn.Click(); // Descending by Name
        var namesDesc = NameCellValues(cut);

        // Category groups stay A, B (no sort active on Category) in BOTH cases;
        // only the row order within each group flips. Group A = {Amy, Bob},
        // Group B = {Zed, Anna}; sorting by Name reorders each group's OWN
        // members (Amy/Bob within A, Anna/Zed within B) without touching
        // which group comes first.
        Assert.Equal(new List<string> { "Amy", "Bob", "Anna", "Zed" }, namesAsc);
        Assert.Equal(new List<string> { "Bob", "Amy", "Zed", "Anna" }, namesDesc);
    }

    // ── Ungrouped sorting is unaffected by the fix ───────────────────────────

    [Fact]
    public void Ungrouped_Sorting_Still_Works_Ascending_And_Descending()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.Columns, Columns()));

        var sortBtn = SortButtonFor(cut, "Name");
        sortBtn.Click(); // Ascending
        Assert.Equal(new List<string> { "Amy", "Anna", "Bob", "Zed" }, NameCellValues(cut));

        sortBtn.Click(); // Descending
        Assert.Equal(new List<string> { "Zed", "Bob", "Anna", "Amy" }, NameCellValues(cut));
    }

    // ── Toggling grouping on/off keeps sort working ──────────────────────────

    [Fact]
    public void Sort_Keeps_Working_After_Toggling_From_Grouped_To_Ungrouped()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.GroupBy, "Category")
            .Add(x => x.Columns, Columns()));

        SortButtonFor(cut, "Name").Click(); // Ascending by Name, while grouped

        // Un-group.
        cut.Render(p => p.Add(x => x.GroupBy, (string?)null));

        Assert.Empty(cut.FindAll("[data-slot='datagrid-group-row']"));
        // Ungrouped now — plain Name-ascending order across all rows, no group influence.
        Assert.Equal(new List<string> { "Amy", "Anna", "Bob", "Zed" }, NameCellValues(cut));

        // Sort is still live: clicking again flips to Descending.
        SortButtonFor(cut, "Name").Click();
        Assert.Equal(new List<string> { "Zed", "Bob", "Anna", "Amy" }, NameCellValues(cut));
    }

    [Fact]
    public void Sort_Keeps_Working_After_Toggling_From_Ungrouped_To_Grouped()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.Columns, Columns()));

        SortButtonFor(cut, "Name").Click(); // Ascending by Name, ungrouped
        Assert.Equal(new List<string> { "Amy", "Anna", "Bob", "Zed" }, NameCellValues(cut));

        // Now group by Category.
        cut.Render(p => p.Add(x => x.GroupBy, "Category"));

        Assert.NotEmpty(cut.FindAll("[data-slot='datagrid-group-row']"));
        // Category groups default A, B (no sort on Category); within each group,
        // the pre-existing Name-ascending sort still applies: Group A = {Amy, Bob},
        // Group B = {Anna, Zed} (their relative order from the Name-sorted list).
        Assert.Equal(new List<string> { "Amy", "Bob", "Anna", "Zed" }, NameCellValues(cut));
    }

    // ── Multi-level grouping: each level honours its OWN sort direction ─────

    private class MultiRow
    {
        public string Category { get; set; } = "";
        public string Status { get; set; } = "";
        public string Name { get; set; } = "";
    }

    private static List<MultiRow> MultiData() => new()
    {
        new MultiRow { Category = "B", Status = "Y", Name = "1" },
        new MultiRow { Category = "A", Status = "X", Name = "2" },
        new MultiRow { Category = "B", Status = "X", Name = "3" },
        new MultiRow { Category = "A", Status = "Y", Name = "4" },
    };

    private static List<DataGridColumn<MultiRow>> MultiColumns() => new()
    {
        new() { Field = "Category", Title = "Category", Sortable = true, Groupable = true },
        new() { Field = "Status", Title = "Status", Sortable = true, Groupable = true },
        new() { Field = "Name", Title = "Name" },
    };

    [Fact]
    public void MultiLevel_Grouping_Outer_Level_Sort_Reverses_Outer_Group_Order_Independently()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<MultiRow>>(p => p
            .Add(x => x.Items, MultiData())
            .Add(x => x.GroupByFields, (IReadOnlyList<string>)new[] { "Category", "Status" })
            .Add(x => x.Columns, MultiColumns()));

        var outerFirstBefore = cut.FindAll("[data-slot='datagrid-group-row']")[0].TextContent.Trim()[..1];
        Assert.Equal("A", outerFirstBefore); // default ascending: Category A before B

        var catBtn = SortButtonFor(cut, "Category");
        catBtn.Click(); // Ascending
        catBtn.Click(); // Descending

        var outerFirstAfter = cut.FindAll("[data-slot='datagrid-group-row']")[0].TextContent.Trim()[..1];
        Assert.Equal("B", outerFirstAfter); // outer level reversed
    }

    // ── ServerMode: RegroupServerItems shares the same (fixed) code path ─────

    [Fact]
    public void ServerMode_Sorting_The_Grouped_Column_Reverses_Group_Order()
    {
        // In a real app, ServerMode's header-click path (HandleSort -> ServerMode
        // branch -> RequestServerData) updates _sorts synchronously, then awaits
        // OnServerRequest — whose consumer handler re-fetches server-side and
        // reassigns Items, which is what actually re-triggers OnParametersSetAsync
        // -> RegroupServerItems() with the new _sorts. With no OnServerRequest
        // wired (as in this test and the pre-existing ServerMode grouping tests),
        // that reassignment never happens automatically, so the round trip is
        // simulated explicitly here via a second cut.Render(...) re-supplying
        // Items — exactly what the consumer's callback would trigger for real.
        var items = Data();
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(x => x.ServerMode, true)
            .Add(x => x.TotalCount, items.Count)
            .Add(x => x.Items, items)
            .Add(x => x.GroupBy, "Category")
            .Add(x => x.Columns, Columns()));

        var sortBtn = SortButtonFor(cut, "Category");
        sortBtn.Click(); // Ascending
        cut.Render(p => p.Add(x => x.Items, items));
        sortBtn.Click(); // Descending
        cut.Render(p => p.Add(x => x.Items, items));

        Assert.Equal(new[] { "B", "A" }, cut.FindAll("[data-slot='datagrid-group-row']").Select(e => e.TextContent.Trim()[..1]));
    }
}
