using System.Globalization;
using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native.Rendering;

/// <summary>Covers <see cref="L.XyChartHost"/> — Scatter/EffectScatter's shared
/// continuous-x/y host. Per spec §3.3, non-ordered point sets use native
/// per-shape pointer events instead of the JS-registered index tracking
/// CartesianChartHost uses, so no chart-interop.js module setup is needed here.</summary>
public class XyChartHostTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static double Num(string? s) => double.Parse(s!, CultureInfo.InvariantCulture);

    [Fact]
    public void Renders_One_Point_Per_Coordinate()
    {
        var cut = _ctx.Render<L.XyChartHost>(p => p.Add(b => b.Series, new List<L.NativeXySeries>
        {
            new() { Name = "S1", Points = new List<(double, double)> { (1, 2), (3, 4), (5, 6) } },
        }));

        Assert.Equal(3, cut.FindAll("circle.lumeo-chart-native-point").Count);
    }

    [Fact]
    public void Higher_X_Value_Maps_To_A_Larger_Cx()
    {
        var cut = _ctx.Render<L.XyChartHost>(p => p.Add(b => b.Series, new List<L.NativeXySeries>
        {
            new() { Name = "S1", Points = new List<(double, double)> { (0, 0), (100, 0) } },
        }));

        var points = cut.FindAll("circle.lumeo-chart-native-point");
        var cx0 = Num(points[0].GetAttribute("cx"));
        var cx1 = Num(points[1].GetAttribute("cx"));
        Assert.True(cx1 > cx0);
    }

    [Fact]
    public void Higher_Y_Value_Maps_To_A_Smaller_Cy_SvgIsYDown()
    {
        var cut = _ctx.Render<L.XyChartHost>(p => p.Add(b => b.Series, new List<L.NativeXySeries>
        {
            new() { Name = "S1", Points = new List<(double, double)> { (0, 0), (0, 100) } },
        }));

        var points = cut.FindAll("circle.lumeo-chart-native-point");
        var cy0 = Num(points[0].GetAttribute("cy"));
        var cy1 = Num(points[1].GetAttribute("cy"));
        // Disable-check: without inverting the y-range, a higher data value would map
        // to a LARGER cy (further down the screen) instead of smaller.
        Assert.True(cy1 < cy0);
    }

    [Fact]
    public void Rippled_Series_Renders_An_Extra_Ripple_Circle_Per_Point()
    {
        var normal = _ctx.Render<L.XyChartHost>(p => p.Add(b => b.Series, new List<L.NativeXySeries>
        {
            new() { Name = "S1", Points = new List<(double, double)> { (1, 1) }, Rippled = false },
        }));
        var rippled = _ctx.Render<L.XyChartHost>(p => p.Add(b => b.Series, new List<L.NativeXySeries>
        {
            new() { Name = "S1", Points = new List<(double, double)> { (1, 1) }, Rippled = true },
        }));

        Assert.Empty(normal.FindAll(".lumeo-chart-native-ripple"));
        Assert.Single(rippled.FindAll(".lumeo-chart-native-ripple"));
    }

    [Fact]
    public void Ripple_Circle_Shares_The_Same_Center_As_Its_Point()
    {
        var cut = _ctx.Render<L.XyChartHost>(p => p.Add(b => b.Series, new List<L.NativeXySeries>
        {
            new() { Name = "S1", Points = new List<(double, double)> { (5, 5) }, Rippled = true },
        }));

        var point = cut.Find("circle.lumeo-chart-native-point");
        var ripple = cut.Find("circle.lumeo-chart-native-ripple");
        Assert.Equal(Num(point.GetAttribute("cx")), Num(ripple.GetAttribute("cx")), 3);
        Assert.Equal(Num(point.GetAttribute("cy")), Num(ripple.GetAttribute("cy")), 3);
    }

    [Fact]
    public void BubbleSize_Sets_The_Marker_Radius()
    {
        var small = _ctx.Render<L.XyChartHost>(p => p.Add(b => b.Series, new List<L.NativeXySeries>
        {
            new() { Name = "S1", Points = new List<(double, double)> { (1, 1) }, SymbolSize = 10 },
        }));
        var big = _ctx.Render<L.XyChartHost>(p => p.Add(b => b.Series, new List<L.NativeXySeries>
        {
            new() { Name = "S1", Points = new List<(double, double)> { (1, 1) }, SymbolSize = 40 },
        }));

        var rSmall = Num(small.Find("circle.lumeo-chart-native-point").GetAttribute("r"));
        var rBig = Num(big.Find("circle.lumeo-chart-native-point").GetAttribute("r"));
        Assert.Equal(5, rSmall, 3);   // diameter/2
        Assert.Equal(20, rBig, 3);
    }

    [Fact]
    public void Legend_Only_Renders_When_MultiSeries_Matches_Legacy_Default()
    {
        // Matches the legacy ScatterChart/EffectScatterChart wrappers' own guard:
        // legend only appears with >1 series, even when ShowLegend=true.
        var single = _ctx.Render<L.XyChartHost>(p => p
            .Add(b => b.ShowLegend, true)
            .Add(b => b.Series, new List<L.NativeXySeries> { new() { Name = "Only", Points = new List<(double, double)> { (1, 1) } } }));

        Assert.Empty(single.FindAll(".lumeo-chart-legend"));

        var multi = _ctx.Render<L.XyChartHost>(p => p
            .Add(b => b.ShowLegend, true)
            .Add(b => b.Series, new List<L.NativeXySeries>
            {
                new() { Name = "A", Points = new List<(double, double)> { (1, 1) } },
                new() { Name = "B", Points = new List<(double, double)> { (2, 2) } },
            }));

        Assert.Single(multi.FindAll(".lumeo-chart-legend"));
    }

    [Fact]
    public void Hover_Sets_Active_Tooltip_With_The_Points_Coordinates()
    {
        var cut = _ctx.Render<L.XyChartHost>(p => p.Add(b => b.Series, new List<L.NativeXySeries>
        {
            new() { Name = "S1", Points = new List<(double, double)> { (7, 9) } },
        }));

        cut.Find("circle.lumeo-chart-native-point").MouseEnter();

        var tooltip = cut.Find(".lumeo-chart-native-tooltip");
        Assert.Contains("7", tooltip.TextContent);
        Assert.Contains("9", tooltip.TextContent);
    }

    [Fact]
    public void Keyboard_ArrowRight_From_No_Selection_Activates_The_First_Point()
    {
        var cut = _ctx.Render<L.XyChartHost>(p => p.Add(b => b.Series, new List<L.NativeXySeries>
        {
            new() { Name = "S1", Points = new List<(double, double)> { (1, 1), (2, 2) } },
        }));

        cut.Find(".lumeo-chart-native-kbd-surface").KeyDown("ArrowRight");

        var tooltip = cut.Find(".lumeo-chart-native-tooltip");
        Assert.Contains("S1", tooltip.TextContent);
    }

    [Fact]
    public void Accessibility_Table_Lists_Every_Point()
    {
        var cut = _ctx.Render<L.XyChartHost>(p => p.Add(b => b.Series, new List<L.NativeXySeries>
        {
            new() { Name = "S1", Points = new List<(double, double)> { (1, 1), (2, 2), (3, 3) } },
        }));

        var rows = cut.FindAll("table.sr-only tbody tr");
        Assert.Equal(3, rows.Count);
    }
}
