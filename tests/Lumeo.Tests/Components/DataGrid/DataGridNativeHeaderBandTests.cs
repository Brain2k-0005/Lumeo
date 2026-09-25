using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Coverage for the classic single-table layout's OWN default fix — no parameter: the
/// grid's scroll container gets the <c>lumeo-dg-native-header-band</c> CSS class and a
/// live <c>RegisterGridHeaderOffset</c> registration (writes <c>--lumeo-grid-header-offset</c>)
/// unless <c>OverlayScrollbar</c> or the <c>ScrollbarBelowHeader</c> split already solve the
/// "no scrollbar/notch next to the header" problem their own way. The actual band paint and
/// the Chromium/Safari scrollbar-track margin live in CSS/JS and are covered by the DataGrid
/// E2E suite; here we assert the C# side.
/// </summary>
public class DataGridNativeHeaderBandTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DataGridNativeHeaderBandTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);
    private static List<Row> Data() => new() { new(1, "Alice"), new(2, "Bob") };

    [Fact]
    public void Classic_Layout_Gets_The_Band_Class_And_Registers_The_Offset_Observer_By_Default()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .AddChildContent<DataGridColumnDef<Row>>(c => c.Add(x => x.Field, "Id").Add(x => x.Title, "ID")));

        var viewport = cut.Find("[data-slot='datagrid-viewport']");
        Assert.Contains("lumeo-dg-native-header-band", viewport.ClassName);
        Assert.Contains("overflow-auto", viewport.ClassName);
        Assert.Contains(viewport.Id!, _interop.RegisterGridHeaderOffsetCalls);
    }

    [Fact]
    public void OverlayScrollbar_Skips_The_Band_Class_And_The_Offset_Observer()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.OverlayScrollbar, true)
            .AddChildContent<DataGridColumnDef<Row>>(c => c.Add(x => x.Field, "Id").Add(x => x.Title, "ID")));

        var viewport = cut.Find("[data-slot='datagrid-viewport']");
        Assert.DoesNotContain("lumeo-dg-native-header-band", viewport.ClassName);
        Assert.Empty(_interop.RegisterGridHeaderOffsetCalls);
    }

    [Fact]
    public void ScrollbarBelowHeader_Split_Skips_The_Band_Class_And_The_Offset_Observer()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.ScrollbarBelowHeader, true)
            .AddChildContent<DataGridColumnDef<Row>>(c => c.Add(x => x.Field, "Id").Add(x => x.Title, "ID").Add(x => x.Width, 120.0)));

        var viewport = cut.Find("[data-slot='datagrid-viewport']");
        Assert.DoesNotContain("lumeo-dg-native-header-band", viewport.ClassName);
        Assert.Empty(_interop.RegisterGridHeaderOffsetCalls);
    }

    [Fact]
    public void Flipping_On_OverlayScrollbar_At_Runtime_Unregisters_The_Offset_Observer()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .AddChildContent<DataGridColumnDef<Row>>(c => c.Add(x => x.Field, "Id").Add(x => x.Title, "ID")));

        var viewportId = cut.Find("[data-slot='datagrid-viewport']").Id!;
        Assert.Contains(viewportId, _interop.RegisterGridHeaderOffsetCalls);

        cut.Render(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.OverlayScrollbar, true)
            .AddChildContent<DataGridColumnDef<Row>>(c => c.Add(x => x.Field, "Id").Add(x => x.Title, "ID")));

        Assert.Contains(viewportId, _interop.UnregisterGridHeaderOffsetCalls);
    }

    [Fact]
    public async Task Dispose_Unregisters_The_Offset_Observer_When_Registered()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .AddChildContent<DataGridColumnDef<Row>>(c => c.Add(x => x.Field, "Id").Add(x => x.Title, "ID")));

        var viewportId = cut.Find("[data-slot='datagrid-viewport']").Id!;
        Assert.Contains(viewportId, _interop.RegisterGridHeaderOffsetCalls);

        await cut.Instance.DisposeAsync();

        Assert.Contains(viewportId, _interop.UnregisterGridHeaderOffsetCalls);
    }
}
