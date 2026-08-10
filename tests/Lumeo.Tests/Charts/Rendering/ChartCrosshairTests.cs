using Bunit;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Rendering;

public class ChartCrosshairTests
{
    private readonly BunitContext _ctx = new();

    [Fact]
    public void Inactive_Renders_Nothing()
    {
        var cut = _ctx.Render<L.ChartCrosshair>(p => p.Add(b => b.Active, false));
        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void Active_Renders_A_Vertical_Guide_Line_At_X()
    {
        var cut = _ctx.Render<L.ChartCrosshair>(p => p
            .Add(b => b.Active, true)
            .Add(b => b.X, 123.4)
            .Add(b => b.GridStart, 10)
            .Add(b => b.GridEnd, 200));

        var line = cut.Find("line");
        Assert.Equal("123.4", line.GetAttribute("x1"));
        Assert.Equal("123.4", line.GetAttribute("x2"));
        Assert.Equal("10", line.GetAttribute("y1"));
        Assert.Equal("200", line.GetAttribute("y2"));
    }

    [Fact]
    public void Renders_One_Marker_Circle_Per_Series()
    {
        var markers = new[]
        {
            new L.ChartCrosshairMarker(100, 50, "var(--color-chart-1)"),
            new L.ChartCrosshairMarker(100, 80, "var(--color-chart-2)"),
        };
        var cut = _ctx.Render<L.ChartCrosshair>(p => p
            .Add(b => b.Active, true)
            .Add(b => b.Markers, markers));

        var circles = cut.FindAll("circle");
        Assert.Equal(2, circles.Count);
        Assert.Equal("50", circles[0].GetAttribute("cy"));
        Assert.Equal("80", circles[1].GetAttribute("cy"));
        Assert.Equal("var(--color-chart-1)", circles[0].GetAttribute("fill"));
    }
}
