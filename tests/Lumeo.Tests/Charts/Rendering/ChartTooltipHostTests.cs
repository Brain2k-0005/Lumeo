using System.Collections.Generic;
using Bunit;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Rendering;

public class ChartTooltipHostTests
{
    private readonly BunitContext _ctx = new();

    [Fact]
    public void Inactive_Renders_Nothing_Even_With_ChildContent_Set()
    {
        var cut = _ctx.Render<L.ChartTooltipHost>(p => p
            .Add(b => b.Active, false)
            .Add(b => b.ChildContent, ctx => builder => builder.AddContent(0, "hi")));

        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void Active_Without_ChildContent_Renders_Nothing()
    {
        var cut = _ctx.Render<L.ChartTooltipHost>(p => p.Add(b => b.Active, true));
        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void Active_Positions_The_Host_At_Pointer_Plus_Offset()
    {
        var cut = _ctx.Render<L.ChartTooltipHost>(p => p
            .Add(b => b.Active, true)
            .Add(b => b.X, 100)
            .Add(b => b.Y, 200)
            .Add(b => b.OffsetX, 14)
            .Add(b => b.OffsetY, 14)
            .Add(b => b.ChildContent, ctx => builder => builder.AddContent(0, "content")));

        var div = cut.Find("div.lumeo-chart-tooltip-host");
        Assert.Contains("left:114px", div.GetAttribute("style"));
        Assert.Contains("top:214px", div.GetAttribute("style"));
    }

    [Fact]
    public void ChildContent_Receives_The_Supplied_Context()
    {
        var context = new L.ChartTooltipContext(
            "Series 1", "line", 0, "Jan", 3, 42.5, "var(--color-chart-1)", new Dictionary<string, object?>());

        var cut = _ctx.Render<L.ChartTooltipHost>(p => p
            .Add(b => b.Active, true)
            .Add(b => b.Context, context)
            .Add(b => b.ChildContent, ctx => builder => builder.AddContent(0, ctx.SeriesName)));

        Assert.Contains("Series 1", cut.Markup);
    }
}
