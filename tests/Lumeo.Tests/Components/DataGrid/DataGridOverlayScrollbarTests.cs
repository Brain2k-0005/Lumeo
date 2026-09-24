using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Coverage for the <c>OverlayScrollbar</c> parameter (issue #517 / field report
/// DocFlow D4). The actual overlay thumbs (position, drag-to-scroll, RTL) live in
/// JS and are covered by the DataGrid E2E suite; here we assert the C# side:
/// default-off behaviour (no markup/registration change for existing consumers),
/// the interop lifecycle (register on mount, unregister on flip-off/dispose), and
/// that the viewport carries the CSS hook the native-scrollbar-hiding rules in
/// lumeo.css target.
/// </summary>
public class DataGridOverlayScrollbarTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DataGridOverlayScrollbarTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);
    private static List<Row> Data() => new() { new(1, "Alice"), new(2, "Bob") };

    [Fact]
    public void OverlayScrollbar_Default_Off_Does_Not_Register_Or_Add_The_CSS_Class()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .AddChildContent<DataGridColumnDef<Row>>(c => c.Add(x => x.Field, "Id").Add(x => x.Title, "ID")));

        var viewport = cut.Find("table").ParentElement!;
        Assert.DoesNotContain("lumeo-dg-overlay-scroll", viewport.ClassName);
        Assert.Empty(_interop.RegisterOverlayScrollbarCalls);
    }

    [Fact]
    public void OverlayScrollbar_True_Adds_CSS_Class_And_Registers_Interop()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.OverlayScrollbar, true)
            .AddChildContent<DataGridColumnDef<Row>>(c => c.Add(x => x.Field, "Id").Add(x => x.Title, "ID")));

        var viewport = cut.Find("table").ParentElement!;
        Assert.Contains("lumeo-dg-overlay-scroll", viewport.ClassName);
        // overflow-auto/flex-1 must still be present — the class is additive, not a replacement.
        Assert.Contains("overflow-auto", viewport.ClassName);

        var viewportId = viewport.Id!;
        Assert.Contains(viewportId, _interop.RegisterOverlayScrollbarCalls);
    }

    [Fact]
    public void OverlayScrollbar_Flipped_Off_At_Runtime_Unregisters()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.OverlayScrollbar, true)
            .AddChildContent<DataGridColumnDef<Row>>(c => c.Add(x => x.Field, "Id").Add(x => x.Title, "ID")));

        var viewportId = cut.Find("table").ParentElement!.Id!;
        Assert.Contains(viewportId, _interop.RegisterOverlayScrollbarCalls);

        cut.Render(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.OverlayScrollbar, false)
            .AddChildContent<DataGridColumnDef<Row>>(c => c.Add(x => x.Field, "Id").Add(x => x.Title, "ID")));

        Assert.Contains(viewportId, _interop.UnregisterOverlayScrollbarCalls);
        Assert.DoesNotContain("lumeo-dg-overlay-scroll", cut.Find("table").ParentElement!.ClassName);
    }

    [Fact]
    public async Task Dispose_Unregisters_Overlay_Scrollbar_When_Registered()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.OverlayScrollbar, true)
            .AddChildContent<DataGridColumnDef<Row>>(c => c.Add(x => x.Field, "Id").Add(x => x.Title, "ID")));

        var viewportId = cut.Find("table").ParentElement!.Id!;
        Assert.Contains(viewportId, _interop.RegisterOverlayScrollbarCalls);

        await cut.Instance.DisposeAsync();

        Assert.Contains(viewportId, _interop.UnregisterOverlayScrollbarCalls);
    }
}
