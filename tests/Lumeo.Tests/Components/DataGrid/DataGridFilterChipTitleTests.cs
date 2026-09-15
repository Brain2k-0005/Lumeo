using System.Reflection;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Field report #464 finding 2 — the toolbar's active-filter chip showed the raw
/// <c>FilterDescriptor.Field</c> instead of the owning column's <c>Title</c>.
///
/// Verified NOT already fixed against current source before writing this test:
/// <c>DataGridToolbar.razor</c> rendered <c>@filter.Field</c> directly with no lookup into
/// <c>EffectiveColumns</c>. The other candidate site named in the field report,
/// <c>UI/Filters/FilterChip.razor</c>, is part of the separate advanced filter-builder
/// (<c>Filters</c>/<c>FiltersAdvanced</c>) and already renders <c>FilterField.Label</c> — a
/// consumer-supplied label, not a raw field name — so it needed no change; this file only
/// covers the DataGrid toolbar chip.
/// </summary>
public class DataGridFilterChipTitleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridFilterChipTitleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string CustomerNo, string Untitled);

    private static List<Row> Sample() => new()
    {
        new(1, "C-100", "x"),
        new(2, "C-200", "y"),
    };

    private static MethodInfo HandleFilterMethod =>
        typeof(DataGrid<Row>).GetMethod("HandleFilter", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static Task ApplyFilterAsync(DataGrid<Row> grid, FilterDescriptor filter) =>
        (Task)HandleFilterMethod.Invoke(grid, new object[] { filter })!;

    [Fact]
    public async Task FilterChip_Shows_Column_Title_Not_RawField()
    {
        var columns = new List<DataGridColumn<Row>>
        {
            new() { Field = "CustomerNo", Title = "Customer number", Filterable = true },
        };

        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, columns)
            .Add(x => x.ShowToolbar, true));

        await cut.InvokeAsync(() => ApplyFilterAsync(cut.Instance, new FilterDescriptor("CustomerNo", FilterOperator.Contains, "C-1")));
        cut.Render();

        Assert.Contains("Customer number", cut.Markup);
        Assert.DoesNotContain(">CustomerNo<", cut.Markup);
    }

    [Fact]
    public async Task FilterChip_Falls_Back_To_Field_When_Column_Has_No_Title()
    {
        var columns = new List<DataGridColumn<Row>>
        {
            new() { Field = "Untitled", Title = null, Filterable = true },
        };

        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, columns)
            .Add(x => x.ShowToolbar, true));

        await cut.InvokeAsync(() => ApplyFilterAsync(cut.Instance, new FilterDescriptor("Untitled", FilterOperator.Contains, "x")));
        cut.Render();

        Assert.Contains("Untitled", cut.Markup);
    }

    [Fact]
    public async Task FilterChip_Title_Is_Reactive_To_Runtime_Title_Change()
    {
        // Title became reactive in 5.7.0 (DataGridColumnDef.OnParametersSet pushes a
        // changed Title into the already-registered column via UpdateColumnHeader) — the
        // chip label must follow a runtime Title change the same way the header cell does.
        var column = new DataGridColumn<Row> { Field = "CustomerNo", Title = "Customer number", Filterable = true };
        var columns = new List<DataGridColumn<Row>> { column };

        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, columns)
            .Add(x => x.ShowToolbar, true));

        await cut.InvokeAsync(() => ApplyFilterAsync(cut.Instance, new FilterDescriptor("CustomerNo", FilterOperator.Contains, "C-1")));
        cut.Render();
        Assert.Contains("Customer number", cut.Markup);

        // Simulate a runtime title change the way a re-rendered DataGridColumnDef with a
        // new Title does: DataGridColumnDef.OnParametersSet mutates the SAME live
        // DataGridColumn instance the toolbar reads from EffectiveColumns (via
        // DataGrid.UpdateColumnHeader) rather than replacing the column — so mutating the
        // instance directly and re-rendering is the accurate equivalent here.
        column.Title = "Kundennummer";
        cut.Render();

        Assert.Contains("Kundennummer", cut.Markup);
        Assert.DoesNotContain("Customer number", cut.Markup);
    }
}
