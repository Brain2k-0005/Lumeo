using Bunit;
using Microsoft.AspNetCore.Components;
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

    private const string DetailMarker = "row-detail-marker";

    private static RenderFragment<Node> DetailTemplate() =>
        item => builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", DetailMarker);
            builder.AddContent(2, $"Detail of {item.Name}");
            builder.CloseElement();
        };

    /// <summary>
    /// Full-sequence regression for the DataGridRowIndexer consolidation (PR 365
    /// round-12 review, DataGridBody.razor x2 findings): a grouped grid whose
    /// group ALSO has an expanded <see cref="DataGrid{TItem}.DetailTemplate"/> row
    /// must produce ONE unbroken table-wide aria-rowindex sequence across the
    /// header, every group row, every item row, AND the detail row — with no gaps,
    /// no collisions, and aria-rowcount on the &lt;table&gt; matching the total.
    /// Before the fix, the detail row had no aria-rowindex at all and every row
    /// after it under-reported by one; aria-rowcount also never counted group or
    /// detail rows in the first place.
    /// </summary>
    [Fact]
    public async Task GroupedGrid_WithExpandedDetailRow_AriaRowIndex_Is_One_Continuous_TableWide_Sequence()
    {
        var cut = _ctx.Render<DataGrid<Node>>(p => p
            .Add(x => x.Items, FlatData())
            .Add(x => x.Columns, Columns())
            .Add(x => x.GroupBy, "Region")
            .Add(x => x.DetailTemplate, DetailTemplate())
            .Add(x => x.ShowPagination, false));

        // Expand Alice's (first EMEA row's) detail via its toggle button — the
        // leading <td class="... w-8"> holds the detail-expand chevron since
        // there's no selection column or row-reorder handle configured.
        var toggle = cut.Find("tbody td.w-8 button");
        await cut.InvokeAsync(() => toggle.Click());
        Assert.Contains(DetailMarker, cut.Markup);

        // DOM order: [EMEA group, Alice, Alice-detail, Bob, AMER group, Carol].
        // The header occupies aria-rowindex 1, so the six rows below must run
        // 2..7 with no gap where the detail row sits and no reuse afterward.
        var indexes = AriaRowIndexes(cut);
        Assert.Equal(new[] { 2, 3, 4, 5, 6, 7 }, indexes);

        // The detail <tr> itself is a real row=... element with its own index
        // (4 — immediately after Alice's row, before Bob's).
        var detailRow = cut.Find($".{DetailMarker}").Closest("tr")!;
        Assert.Equal("row", detailRow.GetAttribute("role"));
        Assert.Equal("4", detailRow.GetAttribute("aria-rowindex"));

        // aria-rowcount on the <table> must equal the header row (1) plus every
        // rendered body row (6) — 7 — matching the last aria-rowindex above
        // exactly, so assistive tech never sees an out-of-range row position.
        var table = cut.Find("table");
        Assert.Equal("7", table.GetAttribute("aria-rowcount"));
    }
}
