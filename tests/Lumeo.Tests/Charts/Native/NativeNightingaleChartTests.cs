using Bunit;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native;

public class NativeNightingaleChartTests
{
    private readonly BunitContext _ctx = new();

    [Fact]
    public void Segments_Get_Equal_Angular_Width_Regardless_Of_Value()
    {
        // Nightingale uses EQUAL-angle wedges (PolarCoordinateSystem.EvenSplit),
        // NOT value-proportional angles (SplitByValue) — the defining difference
        // from Pie. Two very different values (10 vs 90) must still each occupy
        // exactly 1/3 of the circle here (3 categories), only the RADIUS differs.
        var data = new List<L.NativeNightingaleChart.NightingaleData>
        {
            new() { Name = "Small", Value = 10 },
            new() { Name = "Mid", Value = 50 },
            new() { Name = "Big", Value = 90 },
        };
        var cut = _ctx.Render<L.NativeNightingaleChart>(p => p.Add(b => b.Data, data));

        const double cx = 210, cy = 140, innerR = 16, maxR = 92;
        var gap = 0.06;
        var step = Math.PI * 2 / 3;
        var halfGap = gap / 2;

        var expectedSmallR = innerR + 10.0 / 90 * (maxR - innerR);
        var a0 = -Math.PI / 2 + halfGap;
        var a1 = -Math.PI / 2 + step - halfGap;
        var expectedSmall = L.ChartArcPath.Build(cx, cy, innerR, expectedSmallR, a0, a1);

        var expectedBigR = innerR + 90.0 / 90 * (maxR - innerR); // = maxR exactly
        var b0 = -Math.PI / 2 + 2 * step + halfGap;
        var b1 = -Math.PI / 2 + 3 * step - halfGap;
        var expectedBig = L.ChartArcPath.Build(cx, cy, innerR, expectedBigR, b0, b1);

        var paths = cut.FindAll("svg path");
        Assert.Equal(3, paths.Count);
        Assert.Equal(expectedSmall, paths[0].GetAttribute("d"));
        Assert.Equal(expectedBig, paths[2].GetAttribute("d"));
    }

    [Fact]
    public void The_Max_Value_Category_Reaches_The_Outer_Radius_Exactly()
    {
        var data = new List<L.NativeNightingaleChart.NightingaleData>
        {
            new() { Name = "A", Value = 20 },
            new() { Name = "B", Value = 40 },
            new() { Name = "C", Value = 40 }, // ties for max with B
        };
        var cut = _ctx.Render<L.NativeNightingaleChart>(p => p.Add(b => b.Data, data));

        const double cx = 210, cy = 140, innerR = 16, maxR = 92;
        var step = Math.PI * 2 / 3;
        var halfGap = 0.03;
        var b0 = -Math.PI / 2 + step + halfGap;
        var b1 = -Math.PI / 2 + 2 * step - halfGap;
        var expectedB = L.ChartArcPath.Build(cx, cy, innerR, maxR, b0, b1);

        var paths = cut.FindAll("svg path");
        Assert.Equal(expectedB, paths[1].GetAttribute("d"));
    }

    [Fact]
    public void Legend_Toggle_Removes_A_Wedge_And_Recomputes_Equal_Angles_For_The_Rest()
    {
        var data = new List<L.NativeNightingaleChart.NightingaleData>
        {
            new() { Name = "A", Value = 10 },
            new() { Name = "B", Value = 20 },
        };
        var cut = _ctx.Render<L.NativeNightingaleChart>(p => p.Add(b => b.Data, data));

        var legendButtons = cut.FindAll(".lumeo-chart-legend-item");
        cut.InvokeAsync(() => legendButtons[0].Click());

        // Only "B" remains — as the sole visible category it should span the FULL
        // circle (minus the gap), matching EvenSplit(1, gap).
        var paths = cut.FindAll("svg path");
        Assert.Single(paths);
    }

    [Fact]
    public void Accessibility_Summary_Describes_A_Pie_Style_Categorical_Chart()
    {
        var data = new List<L.NativeNightingaleChart.NightingaleData> { new() { Name = "A", Value = 1 } };
        var cut = _ctx.Render<L.NativeNightingaleChart>(p => p.Add(b => b.Data, data));

        var caption = cut.Find("table.sr-only caption");
        Assert.Contains("1 data point", caption.TextContent);
    }
}
