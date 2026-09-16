using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Field report (Northlight demo, PR #488): a grid with more columns than fit the
/// viewport scrolls horizontally, and the expanded row-detail &lt;td colspan&gt; used to be
/// as wide as the TABLE's scroll width — so an expanded detail panel laid itself out
/// across the FULL scrolled table width, with its right portion sitting off-screen
/// (only visible once the user scrolled right). Fixed by declaring the grid's
/// horizontal scroll wrapper a CSS inline-size query container (<c>@container</c>) and
/// pinning the detail cell's content to <c>100cqw</c> (the VISIBLE viewport, not the
/// table's scrolled width) via <c>position: sticky; left: 0</c>.
///
/// See <see cref="DataGrid{TItem}.DetailStickyToViewport"/> (default true) for the
/// opt-out, and <see cref="DataGridDetailRow{TItem}"/> for the sticky wrapper itself.
/// </summary>
public class DataGridDetailStickyToViewportTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridDetailStickyToViewportTests() => _ctx.AddLumeoServices();
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

    private IRenderedComponent<DataGrid<Row>> RenderGrid(bool? stickyToViewport = null, bool compact = false)
    {
        return _ctx.Render<DataGrid<Row>>(p =>
        {
            p.Add(g => g.Items, Data);
            p.Add(g => g.Columns, Cols());
            p.Add(g => g.DetailTemplate, DetailTemplate());
            p.Add(g => g.Compact, compact);
            if (stickyToViewport is { } v) p.Add(g => g.DetailStickyToViewport, v);
        });
    }

    private static async Task ExpandFirstRowAsync(IRenderedComponent<DataGrid<Row>> cut)
    {
        var toggle = cut.Find("td.w-8 button");
        await cut.InvokeAsync(() => toggle.Click());
    }

    [Fact]
    public void ScrollWrapper_Declares_Inline_Size_Container()
    {
        var cut = RenderGrid();

        // The grid's own overflow-auto scroll wrapper (DataGrid.razor) — NOT the table
        // itself — must be the CSS query container so descendant `cqw` units resolve
        // against the visible scrolled viewport rather than the table's full width.
        var wrapper = cut.Find("div.overflow-auto.flex-1");
        Assert.Contains("@container", wrapper.GetAttribute("class"));
    }

    [Fact]
    public async Task Default_ExpandedDetail_Wraps_Content_In_Sticky_Viewport_Div()
    {
        var cut = RenderGrid();
        await ExpandFirstRowAsync(cut);

        var marker = cut.Find($"p.{DetailMarker}");
        var wrapper = marker.ParentElement;
        Assert.NotNull(wrapper);
        var cls = wrapper!.GetAttribute("class") ?? "";
        Assert.Contains("sticky", cls);
        Assert.Contains("left-0", cls);
        // 100cqw resolves against the container (the scroll wrapper); 2rem subtracts
        // the detail cell's own p-4 (1rem each side) horizontal padding.
        Assert.Contains("w-[calc(100cqw-2rem)]", cls);
    }

    [Fact]
    public async Task DetailStickyToViewport_True_Explicit_Same_As_Default()
    {
        var cut = RenderGrid(stickyToViewport: true);
        await ExpandFirstRowAsync(cut);

        var marker = cut.Find($"p.{DetailMarker}");
        var wrapper = marker.ParentElement;
        Assert.NotNull(wrapper);
        Assert.Contains("sticky", wrapper!.GetAttribute("class") ?? "");
    }

    [Fact]
    public async Task DetailStickyToViewport_False_Renders_Content_Directly_In_Cell()
    {
        var cut = RenderGrid(stickyToViewport: false);
        await ExpandFirstRowAsync(cut);

        var marker = cut.Find($"p.{DetailMarker}");
        var wrapper = marker.ParentElement;
        Assert.NotNull(wrapper);
        // No sticky-viewport wrapper: the marker's direct parent is the <td> itself.
        Assert.Equal("td", wrapper!.TagName.ToLowerInvariant());
        Assert.DoesNotContain("sticky", wrapper.GetAttribute("class") ?? "");
    }

    [Fact]
    public async Task Sticky_Wrapper_Renders_In_Compact_Density_Too()
    {
        var cut = RenderGrid(compact: true);
        await ExpandFirstRowAsync(cut);

        var marker = cut.Find($"p.{DetailMarker}");
        var wrapper = marker.ParentElement;
        Assert.NotNull(wrapper);
        Assert.Contains("w-[calc(100cqw-2rem)]", wrapper!.GetAttribute("class") ?? "");
    }
}
