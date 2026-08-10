using Bunit;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native;

public class NativeRadarChartTests
{
    private readonly BunitContext _ctx = new();

    private static List<L.NativeRadarChart.RadarIndicator> FourAxes() => new()
    {
        new() { Name = "Perf", Max = 10 },
        new() { Name = "Bundle", Max = 10 },
        new() { Name = "DX", Max = 10 },
        new() { Name = "Docs", Max = 10 },
    };

    [Fact]
    public void Polygon_Points_Match_RadarCoordinateSystem_For_Known_Normalized_Values()
    {
        var series = new List<L.NativeRadarChart.RadarSeriesData>
        {
            new() { Name = "Lumeo", Values = new List<double> { 10, 5, 0, 7.5 } }, // normalized: 1, 0.5, 0, 0.75
        };
        var cut = _ctx.Render<L.NativeRadarChart>(p => p.Add(b => b.Indicators, FourAxes()).Add(b => b.Series, series));

        const double cx = 210, cy = 156, r = 100; // CenterX=ViewW/2, CenterY=ViewH/2+6
        var radar = new[] { (1.0, 0), (0.5, 1), (0.0, 2), (0.75, 3) };
        var expectedPoints = string.Join(' ', radar.Select(v =>
        {
            // Axis index advances CLOCKWISE from the top (matching ECharts'
            // RadarChart convention) — RadarCoordinateSystem.AngleForAxis
            // SUBTRACTS the per-axis increment, not adds it.
            var angle = -Math.PI / 2 - v.Item2 * (Math.PI * 2 / 4);
            var x = cx + r * v.Item1 * Math.Cos(angle);
            var y = cy + r * v.Item1 * Math.Sin(angle);
            return $"{x:0.###},{y:0.###}";
        }));

        // The FIRST <polygon> is the fill (FillArea=true by default); the SECOND
        // is the stroked outline — both share the same points.
        var polygons = cut.FindAll("svg polygon").Where(p => p.GetAttribute("points") == expectedPoints).ToList();
        Assert.True(polygons.Count >= 1, $"No polygon matched the expected points string. Actual polygons: {string.Join(" | ", cut.FindAll("svg polygon").Select(p => p.GetAttribute("points")))}");
    }

    [Fact]
    public void Fewer_Than_Three_Axes_Renders_No_Series_Instead_Of_Throwing()
    {
        // RadarCoordinateSystem throws for axisCount < 3 — the component must
        // guard against that rather than crash on a 1- or 2-indicator radar.
        var indicators = new List<L.NativeRadarChart.RadarIndicator> { new() { Name = "Only", Max = 10 } };
        var series = new List<L.NativeRadarChart.RadarSeriesData> { new() { Name = "S", Values = new List<double> { 5 } } };

        var cut = _ctx.Render<L.NativeRadarChart>(p => p.Add(b => b.Indicators, indicators).Add(b => b.Series, series));

        Assert.Empty(cut.FindAll("svg polygon"));
    }

    [Fact]
    public void ArrowUp_Switches_The_Active_Series_And_Wraps_Backward()
    {
        var series = new List<L.NativeRadarChart.RadarSeriesData>
        {
            new() { Name = "First", Values = new List<double> { 1, 2, 3, 4 } },
            new() { Name = "Second", Values = new List<double> { 5, 6, 7, 8 } },
        };
        var cut = _ctx.Render<L.NativeRadarChart>(p => p.Add(b => b.Indicators, FourAxes()).Add(b => b.Series, series));

        var host = cut.Find("svg rect[tabindex]");
        cut.InvokeAsync(() => host.KeyDown("ArrowUp")); // wraps 0 -> 1 (last)

        var status = cut.Find("[role='status']");
        Assert.Contains("Second", status.TextContent);
    }

    [Fact]
    public void ArrowRight_Moves_To_The_Next_Axis_And_Status_Shows_Its_Raw_Value()
    {
        var series = new List<L.NativeRadarChart.RadarSeriesData>
        {
            new() { Name = "Lumeo", Values = new List<double> { 1, 2, 3, 4 } },
        };
        var cut = _ctx.Render<L.NativeRadarChart>(p => p.Add(b => b.Indicators, FourAxes()).Add(b => b.Series, series));

        var host = cut.Find("svg rect[tabindex]");
        cut.InvokeAsync(() => host.KeyDown("ArrowRight")); // axis 0 -> 1 ("Bundle", value 2)

        var status = cut.Find("[role='status']");
        Assert.Contains("Bundle", status.TextContent);
        Assert.Contains("2", status.TextContent);
    }

    /// <summary>See <c>NativePieChartTests.Legend_Items_Are_Not_Permanently_Dimmed_When_Nothing_Is_Hovered</c>
    /// — the same missing-<c>@</c> <c>HoveredKey</c> binding bug, same fix, same component family
    /// (Radar has its own legend too, via the identical <c>ChartLegend</c> host pattern).</summary>
    [Fact]
    public void Legend_Items_Are_Not_Permanently_Dimmed_When_Nothing_Is_Hovered()
    {
        var series = new List<L.NativeRadarChart.RadarSeriesData>
        {
            new() { Name = "First", Values = new List<double> { 1, 2, 3, 4 } },
            new() { Name = "Second", Values = new List<double> { 5, 6, 7, 8 } },
        };
        var cut = _ctx.Render<L.NativeRadarChart>(p => p.Add(b => b.Indicators, FourAxes()).Add(b => b.Series, series));

        var items = cut.FindAll(".lumeo-chart-legend-item");
        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.Equal("opacity:1", item.GetAttribute("style")));
    }
}
