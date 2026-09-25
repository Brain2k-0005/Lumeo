using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Coverage for the <c>ScrollbarBelowHeader</c> parameter — the native-scrollbar
/// counterpart to <c>OverlayScrollbar</c>. The actual scroll mirror / scrollbar-gutter
/// padding lives in JS and is covered by the DataGrid E2E suite; here we assert the C#
/// side: default-off behaviour (classic single-table markup unchanged for existing
/// consumers), the table-layout:fixed eligibility fallback (columns without an explicit
/// width keep the classic layout even when the parameter is on), the split DOM
/// structure and its ARIA grid semantics, and the interop lifecycle.
/// </summary>
public class DataGridScrollbarBelowHeaderTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DataGridScrollbarBelowHeaderTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);
    private static List<Row> Data() => new() { new(1, "Alice"), new(2, "Bob") };

    private static void FixedWidthColumn(ComponentParameterCollectionBuilder<DataGridColumnDef<Row>> c) =>
        c.Add(x => x.Field, "Id").Add(x => x.Title, "ID").Add(x => x.Width, 120.0);

    [Fact]
    public void ScrollbarBelowHeader_Default_Off_Renders_Classic_Single_Table()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .AddChildContent<DataGridColumnDef<Row>>(FixedWidthColumn));

        Assert.Single(cut.FindAll("table"));
        Assert.Empty(cut.FindAll("[data-slot='datagrid-table']"));
        Assert.Empty(_interop.RegisterScrollbarBelowHeaderCalls);
    }

    [Fact]
    public void ScrollbarBelowHeader_True_With_Fixed_Widths_Splits_Into_Two_Tables()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.ScrollbarBelowHeader, true)
            .AddChildContent<DataGridColumnDef<Row>>(FixedWidthColumn));

        var tables = cut.FindAll("table");
        Assert.Equal(2, tables.Count);
        Assert.Single(cut.FindAll("thead"));
        Assert.Single(cut.FindAll("tbody"));
        // The header table has no tbody, the body table has no thead.
        Assert.Empty(tables[0].QuerySelectorAll("tbody"));
        Assert.Empty(tables[1].QuerySelectorAll("thead"));

        var viewport = cut.Find("[data-slot='datagrid-viewport']");
        Assert.Contains(viewport.Id!, _interop.RegisterScrollbarBelowHeaderCalls.Select(x => x.ViewportId));
    }

    [Fact]
    public void ScrollbarBelowHeader_Grid_Role_And_Aria_Rowcount_Move_To_The_Wrapping_Div()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.ScrollbarBelowHeader, true)
            .AddChildContent<DataGridColumnDef<Row>>(FixedWidthColumn));

        var wrapper = cut.Find("[data-slot='datagrid-table']");
        Assert.Equal("grid", wrapper.GetAttribute("role"));
        Assert.NotNull(wrapper.GetAttribute("aria-rowcount"));
        Assert.Equal("1", wrapper.GetAttribute("aria-colcount"));

        // Neither inner <table> claims its own table/grid role — a screen reader must
        // see ONE grid (the wrapper), not two nested tables.
        foreach (var table in cut.FindAll("table"))
        {
            Assert.Equal("none", table.GetAttribute("role"));
        }
    }

    [Fact]
    public void ScrollbarBelowHeader_Without_Fixed_Column_Widths_Falls_Back_To_Classic_Layout()
    {
        // No Width set -> TableStyle stays table-layout: auto -> two independent tables
        // can't be guaranteed to agree on column widths, so the split is skipped.
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.ScrollbarBelowHeader, true)
            .AddChildContent<DataGridColumnDef<Row>>(c => c.Add(x => x.Field, "Id").Add(x => x.Title, "ID")));

        Assert.Single(cut.FindAll("table"));
        Assert.Empty(cut.FindAll("[data-slot='datagrid-table']"));
        Assert.Empty(_interop.RegisterScrollbarBelowHeaderCalls);
    }

    [Fact]
    public void ScrollbarBelowHeader_Ignored_When_OverlayScrollbar_Is_Also_On()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.ScrollbarBelowHeader, true)
            .Add(g => g.OverlayScrollbar, true)
            .AddChildContent<DataGridColumnDef<Row>>(FixedWidthColumn));

        Assert.Single(cut.FindAll("table"));
        Assert.Empty(_interop.RegisterScrollbarBelowHeaderCalls);
        Assert.NotEmpty(_interop.RegisterOverlayScrollbarCalls);
    }

    [Fact]
    public void ScrollbarBelowHeader_Flipped_Off_At_Runtime_Unregisters_And_Reverts_To_One_Table()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.ScrollbarBelowHeader, true)
            .AddChildContent<DataGridColumnDef<Row>>(FixedWidthColumn));

        var viewportId = cut.Find("[data-slot='datagrid-viewport']").Id!;
        Assert.Contains(viewportId, _interop.RegisterScrollbarBelowHeaderCalls.Select(x => x.ViewportId));

        cut.Render(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.ScrollbarBelowHeader, false)
            .AddChildContent<DataGridColumnDef<Row>>(FixedWidthColumn));

        Assert.Contains(viewportId, _interop.UnregisterScrollbarBelowHeaderCalls);
        Assert.Single(cut.FindAll("table"));
    }

    [Fact]
    public async Task Dispose_Unregisters_Scrollbar_Below_Header_When_Registered()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.ScrollbarBelowHeader, true)
            .AddChildContent<DataGridColumnDef<Row>>(FixedWidthColumn));

        var viewportId = cut.Find("[data-slot='datagrid-viewport']").Id!;
        Assert.Contains(viewportId, _interop.RegisterScrollbarBelowHeaderCalls.Select(x => x.ViewportId));

        await cut.Instance.DisposeAsync();

        Assert.Contains(viewportId, _interop.UnregisterScrollbarBelowHeaderCalls);
    }
}
