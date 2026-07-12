using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Regression coverage for the PR 365 review finding: each <see cref="DataGridGroupRow{TItem}"/>
/// occupies a real table row, but <see cref="DataGridBody{TItem}"/>'s running row-index
/// counter (<c>globalIdx</c>) only ever counted DATA items — so the first item row after a
/// group header (and every row after that) under-reported its <c>aria-rowindex</c> by one
/// per preceding group, and the group header rows themselves carried no
/// <c>aria-rowindex</c> at all. Covers both grouped render paths in DataGridBody: single-level
/// (the <c>GroupedSections</c> branch) and multi-level (the <c>GroupTree</c> branch).
/// </summary>
public class DataGridGroupRowIndexTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public DataGridGroupRowIndexTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Node(int Id, string Name, string Region, string Country);

    private static List<Node> FlatData() => new()
    {
        new(1, "Alice", "EMEA", "UK"),
        new(2, "Bob", "EMEA", "DE"),
        new(3, "Carol", "AMER", "US"),
    };

    private static List<DataGridColumn<Node>> Columns() => new()
    {
        new() { Field = "Region", Title = "Region", Groupable = true },
        new() { Field = "Country", Title = "Country", Groupable = true },
        new() { Field = "Name", Title = "Name" },
    };

    private static int[] AriaRowIndexes(IRenderedComponent<DataGrid<Node>> cut) =>
        cut.FindAll("tbody tr")
            .Select(tr => int.Parse(tr.GetAttribute("aria-rowindex")!))
            .ToArray();

    [Fact]
    public void SingleLevelGrouping_AriaRowIndex_Counts_GroupHeaders_Too()
    {
        var cut = _ctx.Render<DataGrid<Node>>(p => p
            .Add(x => x.Items, FlatData())
            .Add(x => x.Columns, Columns())
            .Add(x => x.GroupBy, "Region")
            .Add(x => x.ShowPagination, false));

        // DOM order: [EMEA group row, Alice, Bob, AMER group row, Carol].
        // The header occupies aria-rowindex 1, so body rows must run 2..6
        // with every row — group AND item — getting a distinct position.
        // Before the fix, group rows had no aria-rowindex at all, and the
        // item-only globalIdx counter reported Alice=2, Bob=3, Carol=4 —
        // colliding with the (uncounted) group rows' table positions.
        Assert.Equal(new[] { 2, 3, 4, 5, 6 }, AriaRowIndexes(cut));
    }

    [Fact]
    public void MultiLevelGrouping_AriaRowIndex_Counts_GroupHeaders_Too()
    {
        var cut = _ctx.Render<DataGrid<Node>>(p => p
            .Add(x => x.Items, FlatData())
            .Add(x => x.Columns, Columns())
            .Add(x => x.GroupByFields, (IReadOnlyList<string>)new[] { "Region", "Country" })
            .Add(x => x.ShowPagination, false));

        // DOM order: [EMEA group, UK group, Alice, DE group, Bob, AMER group, US group, Carol]
        // — 5 group headers + 3 items = 8 rows, each a distinct table
        // position, so aria-rowindex must run 2..9 with no gaps or reuse.
        var indexes = AriaRowIndexes(cut);
        Assert.Equal(8, indexes.Length);
        Assert.Equal(Enumerable.Range(2, 8), indexes);
    }
}
