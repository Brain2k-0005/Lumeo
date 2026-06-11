using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Regression tests for GroupBy/GroupByFields combined with DECLARATIVE
/// columns (<see cref="DataGridColumnDef{TItem}"/> children). Markup-declared
/// columns register in their own <c>OnInitialized</c> — one render AFTER the
/// grid's first <c>OnParametersSetAsync</c> — so the grouping seed must not
/// validate (and silently drop) the requested fields against a still-empty
/// column list and then short-circuit forever. All pre-existing grouping
/// tests pass columns via the <c>Columns</c> parameter, which masked this.
/// </summary>
public class DataGridDeclarativeGroupingTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridDeclarativeGroupingTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
    }

    private static List<Row> Data() => new()
    {
        new Row { Id = 1, Name = "Bolt", Category = "Hardware" },
        new Row { Id = 2, Name = "Nut", Category = "Hardware" },
        new Row { Id = 3, Name = "Manual", Category = "Docs" },
    };

    [Fact]
    public void GroupBy_With_Declarative_ColumnDefs_Renders_Group_Rows()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.GroupBy, "Category")
            .AddChildContent<DataGridColumnDef<Row>>(c => c
                .Add(x => x.Field, "Category").Add(x => x.Title, "Category").Add(x => x.Groupable, true))
            .AddChildContent<DataGridColumnDef<Row>>(c => c
                .Add(x => x.Field, "Name").Add(x => x.Title, "Name")));

        Assert.Contains("datagrid-group-row", cut.Markup);
        Assert.Contains("Hardware", cut.Markup);
        Assert.Contains("Docs", cut.Markup);
    }

    [Fact]
    public void GroupBy_With_Declarative_ColumnDefs_Survives_Late_Items_Update()
    {
        // Typical app shape: the grid renders first, data arrives async and the
        // parent re-renders with new Items. The grouping seed's short-circuit
        // must not pin the empty seed taken before columns registered.
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, new List<Row>())
            .Add(x => x.GroupBy, "Category")
            .AddChildContent<DataGridColumnDef<Row>>(c => c
                .Add(x => x.Field, "Category").Add(x => x.Title, "Category").Add(x => x.Groupable, true))
            .AddChildContent<DataGridColumnDef<Row>>(c => c
                .Add(x => x.Field, "Name").Add(x => x.Title, "Name")));

        cut.Render(p => p.Add(x => x.Items, Data()));

        Assert.Contains("datagrid-group-row", cut.Markup);
        Assert.Contains("Hardware", cut.Markup);
    }

    [Fact]
    public void GroupByFields_With_Declarative_ColumnDefs_Renders_Group_Rows()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.GroupByFields, (IReadOnlyList<string>)new[] { "Category" })
            .AddChildContent<DataGridColumnDef<Row>>(c => c
                .Add(x => x.Field, "Category").Add(x => x.Title, "Category").Add(x => x.Groupable, true))
            .AddChildContent<DataGridColumnDef<Row>>(c => c
                .Add(x => x.Field, "Name").Add(x => x.Title, "Name")));

        Assert.Contains("datagrid-group-row", cut.Markup);
        Assert.Contains("Hardware", cut.Markup);
        Assert.Contains("Docs", cut.Markup);
    }
}
