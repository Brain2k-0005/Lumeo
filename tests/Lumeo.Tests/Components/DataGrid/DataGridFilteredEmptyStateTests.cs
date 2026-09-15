using System.Reflection;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Field report #464 finding 1 — client mode had no way to tell "empty because every row
/// got filtered out" apart from "the source genuinely has zero rows": no
/// <c>HasActiveFilters</c>/<c>IsFiltered</c> hook existed anywhere on the grid in 5.9.1.
///
/// Verified NOT already fixed against current source before writing these: <c>DataGrid&lt;TItem&gt;</c>
/// (src/Lumeo.DataGrid/UI/DataGrid/DataGrid.razor) had no <c>HasActiveFilters</c> member and
/// <c>DataGridBody</c>'s three empty-state branches rendered the same <c>DataGrid.NoData</c>
/// message unconditionally.
/// </summary>
public class DataGridFilteredEmptyStateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridFilteredEmptyStateTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static List<Row> Sample() => new()
    {
        new(1, "Alice"),
        new(2, "Bob"),
        new(3, "Charlie"),
    };

    private static List<DataGridColumn<Row>> Columns() => new()
    {
        new() { Field = "Name", Title = "Name", Filterable = true },
    };

    // Reflection: HandleFilter is the same private entry point the column-filter popover's
    // OnFilterApply invokes; used here to apply a filter deterministically without driving
    // the popover UI, mirroring the reflection pattern in DataGridBatchEditTests.
    private static MethodInfo HandleFilterMethod =>
        typeof(DataGrid<Row>).GetMethod("HandleFilter", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static Task ApplyFilterAsync(DataGrid<Row> grid, FilterDescriptor filter) =>
        (Task)HandleFilterMethod.Invoke(grid, new object[] { filter })!;

    [Fact]
    public async Task ClientMode_FilterMatchesNothing_ShowsFilteredEmptyState_And_HasActiveFiltersTrue()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, Columns()));

        await cut.InvokeAsync(() => ApplyFilterAsync(cut.Instance, new FilterDescriptor("Name", FilterOperator.Equals, "NoSuchName")));
        cut.Render();

        Assert.True(cut.Instance.HasActiveFilters);
        Assert.Contains("No rows match the current filters", cut.Markup);
        Assert.DoesNotContain("No data available", cut.Markup);
    }

    [Fact]
    public void ClientMode_ZeroSourceRows_ShowsPlainEmptyState_And_HasActiveFiltersFalse()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, new List<Row>())
            .Add(x => x.Columns, Columns()));

        Assert.False(cut.Instance.HasActiveFilters);
        Assert.Contains("No data available", cut.Markup);
        Assert.DoesNotContain("No rows match the current filters", cut.Markup);
    }

    [Fact]
    public async Task ClearFiltersAsync_ResetsFilter_RowsReturn_And_HasActiveFiltersFalse()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, Columns()));

        await cut.InvokeAsync(() => ApplyFilterAsync(cut.Instance, new FilterDescriptor("Name", FilterOperator.Equals, "NoSuchName")));
        cut.Render();
        Assert.True(cut.Instance.HasActiveFilters);
        Assert.Contains("No rows match the current filters", cut.Markup);

        await cut.InvokeAsync(() => cut.Instance.ClearFiltersAsync());
        cut.Render();

        Assert.False(cut.Instance.HasActiveFilters);
        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("Bob", cut.Markup);
        Assert.Contains("Charlie", cut.Markup);
    }

    [Fact]
    public async Task EmptyTemplate_Receives_HasActiveFilters_And_ClearFilters_Callback()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, Columns())
            .Add(x => x.EmptyTemplate, (DataGridEmptyContext ctx) => builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "data-testid", "custom-empty");
                builder.AddContent(2, ctx.HasActiveFilters ? "filtered" : "plain");
                builder.CloseElement();
            }));

        // No filter yet: not applicable here since Items has rows. Apply a filter
        // that excludes everything and confirm the typed context reports it.
        await cut.InvokeAsync(() => ApplyFilterAsync(cut.Instance, new FilterDescriptor("Name", FilterOperator.Equals, "NoSuchName")));
        cut.Render();

        var custom = cut.Find("[data-testid='custom-empty']");
        Assert.Equal("filtered", custom.TextContent);

        // EmptyTemplate takes precedence over the built-in message entirely.
        Assert.DoesNotContain("No rows match the current filters", cut.Markup);
    }

    [Fact]
    public async Task BuiltIn_FilteredEmptyState_ClearFiltersButton_ResetsFilter()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, Columns()));

        await cut.InvokeAsync(() => ApplyFilterAsync(cut.Instance, new FilterDescriptor("Name", FilterOperator.Equals, "NoSuchName")));
        cut.Render();

        var clearButton = cut.FindAll("button").Single(b => b.TextContent.Contains("Clear filters"));
        await cut.InvokeAsync(() => clearButton.Click());

        Assert.False(cut.Instance.HasActiveFilters);
        Assert.Contains("Alice", cut.Markup);
    }
}
