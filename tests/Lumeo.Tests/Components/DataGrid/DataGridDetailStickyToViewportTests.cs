using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Field report (Northlight demo, PR #488): a grid with more columns than fit the
/// viewport scrolls horizontally, and the expanded row-detail &lt;td colspan&gt; used to be
/// as wide as the TABLE's scroll width — so an expanded detail panel laid itself out
/// across the FULL scrolled table width, with its right portion sitting off-screen
/// (only visible once the user scrolled right).
///
/// Round 1 of this fix used a CSS `container-type: inline-size` + `cqw` approach, but
/// that makes the scroll wrapper the CONTAINING BLOCK for `position: fixed` descendants —
/// which broke DataGridHeaderCell's filter popover / pin menu / column menu (positioned
/// in viewport coordinates via Interop.PositionFixed) on EVERY grid, detail template or
/// not. Fixed instead with NO CSS containment: DataGrid observes its own scroll
/// wrapper's client width via a plain JS ResizeObserver (Interop.RegisterViewportWidth)
/// and writes it as the `--lumeo-grid-viewport-w` CSS custom property, which
/// DataGridDetailRow's sticky detail wrapper reads back through an ordinary (inherited)
/// CSS rule in lumeo.css — `[data-slot="datagrid-detail-viewport"] { width: calc(var(
/// --lumeo-grid-viewport-w, 100%) - 2rem) }`.
///
/// See <see cref="Lumeo.DataGrid{TItem}.DetailStickyToViewport"/> (default true) for the
/// opt-out, and <see cref="DataGridDetailRow{TItem}"/> for the sticky wrapper itself.
/// </summary>
public class DataGridDetailStickyToViewportTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DataGridDetailStickyToViewportTests()
    {
        _ctx.AddLumeoServices();
        // AddLumeoServices binds IComponentInteropService -> ComponentInteropService.
        // Last-registration-wins for the interface, so DataGrid (which injects the
        // interface) resolves to the tracking impl instead.
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static readonly Row[] Data = { new(1, "Alice"), new(2, "Bob") };

    private static List<DataGridColumn<Row>> Cols() => new()
    {
        new() { Field = "Id", Title = "ID" },
        new() { Field = "Name", Title = "Name" },
    };

    private const string DetailMarker = "detail-marker";

    private static RenderFragment<Row> DetailTemplate() =>
        item => builder =>
        {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "class", DetailMarker);
            builder.AddContent(2, $"Detail of {item.Name}");
            builder.CloseElement();
        };

    private IRenderedComponent<Lumeo.DataGrid<Row>> RenderGrid(
        bool? stickyToViewport = null, bool compact = false, bool withDetailTemplate = true)
    {
        return _ctx.Render<Lumeo.DataGrid<Row>>(p =>
        {
            p.Add(g => g.Items, Data);
            p.Add(g => g.Columns, Cols());
            if (withDetailTemplate) p.Add(g => g.DetailTemplate, DetailTemplate());
            p.Add(g => g.Compact, compact);
            if (stickyToViewport is { } v) p.Add(g => g.DetailStickyToViewport, v);
        });
    }

    private static async Task ExpandFirstRowAsync(IRenderedComponent<Lumeo.DataGrid<Row>> cut)
    {
        var toggle = cut.Find("td.w-8 button");
        await cut.InvokeAsync(() => toggle.Click());
    }

    [Fact]
    public void ScrollWrapper_Has_Stable_Id_And_No_Container_Class()
    {
        var cut = RenderGrid();

        // The grid's own overflow-auto scroll wrapper (DataGrid.razor) — NOT the table
        // itself — is what Interop.RegisterViewportWidth observes by id. Regression
        // guard: this must never carry a `@container` class again (CSS containment
        // there breaks DataGridHeaderCell's position:fixed popovers — see class doc).
        var wrapper = cut.Find("div.overflow-auto.flex-1");
        var id = wrapper.GetAttribute("id");
        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.DoesNotContain("container", wrapper.GetAttribute("class"));
    }

    [Fact]
    public async Task Default_ExpandedDetail_Wraps_Content_In_DataSlot_Sticky_Div()
    {
        var cut = RenderGrid();
        await ExpandFirstRowAsync(cut);

        var wrapper = cut.Find("[data-slot='datagrid-detail-viewport']");
        var cls = wrapper.GetAttribute("class") ?? "";
        Assert.Contains("sticky", cls);
        Assert.Contains("left-0", cls);
        // No Tailwind arbitrary width class — width comes from the plain lumeo.css rule
        // keyed off the data-slot attribute (var()-based arbitrary classes never reach
        // the precompiled bundle).
        Assert.DoesNotContain("w-[", cls);
        Assert.DoesNotContain("cqw", cls);

        var marker = cut.Find($"p.{DetailMarker}");
        Assert.Equal("datagrid-detail-viewport", marker.ParentElement!.GetAttribute("data-slot"));
    }

    [Fact]
    public async Task DetailStickyToViewport_True_Explicit_Same_As_Default()
    {
        var cut = RenderGrid(stickyToViewport: true);
        await ExpandFirstRowAsync(cut);

        Assert.NotEmpty(cut.FindAll("[data-slot='datagrid-detail-viewport']"));
    }

    [Fact]
    public async Task DetailStickyToViewport_False_Renders_Content_Directly_In_Cell_No_DataSlot()
    {
        var cut = RenderGrid(stickyToViewport: false);
        await ExpandFirstRowAsync(cut);

        // No sticky-viewport wrapper at all: the marker's direct parent is the <td> itself.
        Assert.Empty(cut.FindAll("[data-slot='datagrid-detail-viewport']"));
        var marker = cut.Find($"p.{DetailMarker}");
        Assert.Equal("td", marker.ParentElement!.TagName.ToLowerInvariant());
    }

    [Fact]
    public async Task Sticky_Wrapper_Renders_In_Compact_Density_Too()
    {
        var cut = RenderGrid(compact: true);
        await ExpandFirstRowAsync(cut);

        Assert.NotEmpty(cut.FindAll("[data-slot='datagrid-detail-viewport']"));
    }

    [Fact]
    public void RegistersViewportWidthObserver_When_DetailTemplate_And_StickyToViewport_On()
    {
        var cut = RenderGrid();

        var wrapperId = cut.Find("div.overflow-auto.flex-1").GetAttribute("id");
        Assert.Single(_interop.RegisterViewportWidthCalls);
        Assert.Equal(wrapperId, _interop.RegisterViewportWidthCalls[0]);
    }

    [Fact]
    public void Does_Not_Register_Observer_When_DetailStickyToViewport_False()
    {
        RenderGrid(stickyToViewport: false);

        Assert.Empty(_interop.RegisterViewportWidthCalls);
    }

    [Fact]
    public void Does_Not_Register_Observer_When_No_DetailTemplate()
    {
        RenderGrid(withDetailTemplate: false);

        Assert.Empty(_interop.RegisterViewportWidthCalls);
    }
}
