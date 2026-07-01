using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Tests for the rc.35 DataGrid enterprise features: tree-grid mode
/// (<see cref="DataGrid{TItem}.ChildItemsSelector"/>) and the drag-to-group
/// panel (<see cref="DataGrid{TItem}.ShowGroupPanel"/> / multi-level grouping
/// via <see cref="DataGrid{TItem}.GroupByFields"/>).
/// </summary>
public class DataGridTreeBatchGroupTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridTreeBatchGroupTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private class Node
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Region { get; set; } = "";
        public string Country { get; set; } = "";
        public decimal Amount { get; set; }
        public List<Node>? Children { get; set; }
    }

    private static List<Node> TreeData() => new()
    {
        new Node { Id = 1, Name = "Acme Corp", Amount = 100, Children = new()
        {
            new Node { Id = 2, Name = "Acme East", Amount = 40 },
            new Node { Id = 3, Name = "Acme West", Amount = 60 },
        }},
        new Node { Id = 4, Name = "Globex", Amount = 200 },
    };

    private static List<Node> FlatData() => new()
    {
        new Node { Id = 1, Name = "Alice", Region = "EMEA", Country = "UK", Amount = 10 },
        new Node { Id = 2, Name = "Bob", Region = "EMEA", Country = "DE", Amount = 20 },
        new Node { Id = 3, Name = "Carol", Region = "AMER", Country = "US", Amount = 30 },
    };

    private static List<DataGridColumn<Node>> TreeColumns() => new()
    {
        new() { Field = "Name", Title = "Name" },
        new() { Field = "Amount", Title = "Amount", Aggregate = AggregateType.Sum },
    };

    private static List<DataGridColumn<Node>> GroupColumns() => new()
    {
        new() { Field = "Region", Title = "Region", Groupable = true },
        new() { Field = "Country", Title = "Country", Groupable = true },
        new() { Field = "Name", Title = "Name" },
        new() { Field = "Amount", Title = "Amount", Aggregate = AggregateType.Sum },
    };

    // --- Tree-grid mode ---

    [Fact]
    public void TreeGrid_RendersRootRows_AndUsesTreegridRole()
    {
        var cut = _ctx.Render<DataGrid<Node>>(p => p
            .Add(x => x.Items, TreeData())
            .Add(x => x.Columns, TreeColumns())
            .Add(x => x.ChildItemsSelector, (Func<Node, IEnumerable<Node>?>)(n => n.Children)));

        Assert.Contains("role=\"treegrid\"", cut.Markup);
        Assert.Contains("Acme Corp", cut.Markup);
        Assert.Contains("Globex", cut.Markup);
        // Children are collapsed by default.
        Assert.DoesNotContain("Acme East", cut.Markup);
    }

    [Fact]
    public void TreeGrid_DefaultExpanded_ShowsChildren()
    {
        var cut = _ctx.Render<DataGrid<Node>>(p => p
            .Add(x => x.Items, TreeData())
            .Add(x => x.Columns, TreeColumns())
            .Add(x => x.TreeGridDefaultExpanded, true)
            .Add(x => x.ChildItemsSelector, (Func<Node, IEnumerable<Node>?>)(n => n.Children)));

        Assert.Contains("Acme East", cut.Markup);
        Assert.Contains("Acme West", cut.Markup);
        Assert.Contains("aria-expanded", cut.Markup);
    }

    [Fact]
    public void TreeGrid_DefaultExpanded_Survives_Empty_Items_Flicker()
    {
        // Async-load pattern: an empty list renders first (pre-load), then the real
        // data arrives. The one-shot default-expand seed must NOT be consumed by the
        // empty render, or the tree renders collapsed once data is present despite
        // TreeGridDefaultExpanded=true (the sibling of the grouping empty-flicker bug).
        var cut = _ctx.Render<DataGrid<Node>>(p => p
            .Add(x => x.Items, new List<Node>())
            .Add(x => x.Columns, TreeColumns())
            .Add(x => x.TreeGridDefaultExpanded, true)
            .Add(x => x.ChildItemsSelector, (Func<Node, IEnumerable<Node>?>)(n => n.Children)));

        cut.Render(p => p.Add(x => x.Items, TreeData()));

        Assert.Contains("Acme East", cut.Markup);
        Assert.Contains("Acme West", cut.Markup);
    }

    [Fact]
    public void FlatMode_StillWorks_WhenChildItemsSelectorNull()
    {
        var cut = _ctx.Render<DataGrid<Node>>(p => p
            .Add(x => x.Items, FlatData())
            .Add(x => x.Columns, GroupColumns()));

        Assert.Contains("role=\"grid\"", cut.Markup);
        Assert.DoesNotContain("role=\"treegrid\"", cut.Markup);
        Assert.Contains("Alice", cut.Markup);
    }

    // --- Group panel / multi-level grouping ---

    [Fact]
    public void GroupPanel_RendersStrip_WhenShowGroupPanel()
    {
        var cut = _ctx.Render<DataGrid<Node>>(p => p
            .Add(x => x.Items, FlatData())
            .Add(x => x.Columns, GroupColumns())
            .Add(x => x.ShowGroupPanel, true));

        Assert.Contains("datagrid-group-panel", cut.Markup);
        // No grouping yet → placeholder hint + add-level dropdown listing Groupable columns.
        Assert.Contains("Region", cut.Markup);
    }

    [Fact]
    public void GroupPanel_ShowsChip_ForActiveGroupField()
    {
        var cut = _ctx.Render<DataGrid<Node>>(p => p
            .Add(x => x.Items, FlatData())
            .Add(x => x.Columns, GroupColumns())
            .Add(x => x.ShowGroupPanel, true)
            .Add(x => x.GroupByFields, (IReadOnlyList<string>)new[] { "Region" }));

        Assert.Contains("datagrid-group-panel", cut.Markup);
        // Grouped rows for the two regions present.
        Assert.Contains("EMEA", cut.Markup);
        Assert.Contains("AMER", cut.Markup);
    }

    [Fact]
    public void Grouping_SingleField_RendersGroupRows()
    {
        var cut = _ctx.Render<DataGrid<Node>>(p => p
            .Add(x => x.Items, FlatData())
            .Add(x => x.Columns, GroupColumns())
            .Add(x => x.GroupBy, "Region"));

        Assert.Contains("datagrid-group-row", cut.Markup);
        Assert.Contains("EMEA", cut.Markup);
        Assert.Contains("AMER", cut.Markup);
    }

    [Fact]
    public void Grouping_MultiLevel_RendersNestedGroups()
    {
        var cut = _ctx.Render<DataGrid<Node>>(p => p
            .Add(x => x.Items, FlatData())
            .Add(x => x.Columns, GroupColumns())
            .Add(x => x.GroupByFields, (IReadOnlyList<string>)new[] { "Region", "Country" }));

        // Outer (Region) and inner (Country) levels both present.
        Assert.Contains("EMEA", cut.Markup);
        Assert.Contains("UK", cut.Markup);
        Assert.Contains("DE", cut.Markup);
        Assert.Contains("US", cut.Markup);
    }
}
