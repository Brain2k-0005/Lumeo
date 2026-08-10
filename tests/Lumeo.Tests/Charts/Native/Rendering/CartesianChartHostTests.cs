using System.Globalization;
using Bunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native.Rendering;

/// <summary>Covers <see cref="L.CartesianChartHost"/> — the shared native
/// rendering/interaction orchestrator behind Line/Area/Bar/Mixed/Waterfall.
/// Assertions target rendered SVG attribute VALUES (path "d", rect x/y/w/h),
/// not class strings, per this repo's own documented testability rule; each
/// test states the wrong value a disabled/reverted piece of logic would
/// produce.</summary>
public class CartesianChartHostTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        var module = _ctx.JSInterop.SetupModule("./_content/Lumeo.Charts/js/chart-interop.js");
        module.Mode = Bunit.JSRuntimeMode.Loose;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static double Num(string? s) => double.Parse(s!, CultureInfo.InvariantCulture);

    [Fact]
    public void Bar_Renders_One_Shape_Per_Category()
    {
        var cut = _ctx.Render<L.CartesianChartHost>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B", "C" })
            .Add(b => b.Series, new List<L.NativeCartesianSeries>
            {
                new() { Name = "S1", Kind = L.NativeCartesianSeriesKind.Bar, Values = new double?[] { 10, 20, 30 } },
            })
            .Add(b => b.UsePointScale, false));

        var bars = cut.FindAll(".lumeo-chart-native-bar");
        Assert.Equal(3, bars.Count);
    }

    [Fact]
    public void Stacked_Bars_Second_Series_Sits_Exactly_On_Top_Of_The_First()
    {
        // Disable-check: if stacking were turned off, both series would start
        // from the SAME baseline y and this equality would fail.
        var cut = _ctx.Render<L.CartesianChartHost>(p => p
            .Add(b => b.Categories, new List<string> { "A" })
            .Add(b => b.Series, new List<L.NativeCartesianSeries>
            {
                new() { Name = "S1", Kind = L.NativeCartesianSeriesKind.Bar, Values = new double?[] { 10 }, Stacked = true },
                new() { Name = "S2", Kind = L.NativeCartesianSeriesKind.Bar, Values = new double?[] { 10 }, Stacked = true },
            })
            .Add(b => b.UsePointScale, false));

        var bars = cut.FindAll(".lumeo-chart-native-bar");
        Assert.Equal(2, bars.Count);

        // Bars carry data-x/y/width/height regardless of whether they rendered as a
        // plain <rect> or a rounded-top <path> (see CartesianChartHost's comment) —
        // asserting on those keeps the geometry check independent of the corner-
        // rounding cosmetic detail.
        var firstY = Num(bars[0].GetAttribute("data-y"));
        var firstH = Num(bars[0].GetAttribute("data-height"));
        var secondY = Num(bars[1].GetAttribute("data-y"));
        Assert.Equal(firstY, secondY + firstH, 3);
    }

    [Fact]
    public void NonStacked_Bar_Series_Occupy_SideBySide_SubBands()
    {
        var cut = _ctx.Render<L.CartesianChartHost>(p => p
            .Add(b => b.Categories, new List<string> { "A" })
            .Add(b => b.Series, new List<L.NativeCartesianSeries>
            {
                new() { Name = "S1", Kind = L.NativeCartesianSeriesKind.Bar, Values = new double?[] { 10 } },
                new() { Name = "S2", Kind = L.NativeCartesianSeriesKind.Bar, Values = new double?[] { 10 } },
            })
            .Add(b => b.UsePointScale, false));

        var bars = cut.FindAll(".lumeo-chart-native-bar");
        Assert.Equal(2, bars.Count);
        var x0 = Num(bars[0].GetAttribute("data-x"));
        var w0 = Num(bars[0].GetAttribute("data-width"));
        var x1 = Num(bars[1].GetAttribute("data-x"));
        // Disable-check: without grouping, both bars would share the exact same x.
        Assert.NotEqual(x0, x1, 3);
        Assert.True(x1 >= x0 + w0 - 0.01, "second bar's sub-band must not overlap the first's");
    }

    [Fact]
    public void Horizontal_Bar_Swaps_Which_Dimension_Encodes_The_Value()
    {
        // Two categories with different values: in vertical mode the two bars
        // must share the same WIDTH (bandwidth is per-category, value-independent)
        // but differ in HEIGHT (value-driven); in horizontal mode it's the exact
        // transpose. This is layout/margin-independent, unlike comparing absolute
        // width-vs-height on a single bar (which depends on the viewport's own
        // aspect ratio and would be a flaky assertion).
        var categories = new List<string> { "Low", "High" };
        var series = new List<L.NativeCartesianSeries> { new() { Name = "S", Kind = L.NativeCartesianSeriesKind.Bar, Values = new double?[] { 10, 90 } } };

        var vertical = _ctx.Render<L.CartesianChartHost>(p => p.Add(b => b.Categories, categories).Add(b => b.Series, series)
            .Add(b => b.UsePointScale, false).Add(b => b.Horizontal, false));
        var horizontal = _ctx.Render<L.CartesianChartHost>(p => p.Add(b => b.Categories, categories).Add(b => b.Series, series)
            .Add(b => b.UsePointScale, false).Add(b => b.Horizontal, true));

        var vBars = vertical.FindAll(".lumeo-chart-native-bar");
        var hBars = horizontal.FindAll(".lumeo-chart-native-bar");

        // Disable-check: if Horizontal had no effect at all, hBars' width would
        // vary (like vBars') instead of staying constant across the two bars.
        Assert.Equal(Num(vBars[0].GetAttribute("data-width")), Num(vBars[1].GetAttribute("data-width")), 3);
        Assert.NotEqual(Num(vBars[0].GetAttribute("data-height")), Num(vBars[1].GetAttribute("data-height")), 3);

        Assert.Equal(Num(hBars[0].GetAttribute("data-height")), Num(hBars[1].GetAttribute("data-height")), 3);
        Assert.NotEqual(Num(hBars[0].GetAttribute("data-width")), Num(hBars[1].GetAttribute("data-width")), 3);

        // Horizontal bars never round their corners (see CartesianChartHost's comment) —
        // confirms this render definitely took the plain-<rect> path.
        Assert.Equal("rect", hBars[0].LocalName);
    }

    [Fact]
    public void Line_Series_Path_Starts_And_Ends_At_The_PointScale_Edges()
    {
        // UsePointScale=true (Line/Area) places the first/last point AT the plot
        // edges (boundaryGap:false parity) — unlike BandScale, which insets them.
        var cut = _ctx.Render<L.CartesianChartHost>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B", "C" })
            .Add(b => b.Series, new List<L.NativeCartesianSeries>
            {
                new() { Name = "S1", Kind = L.NativeCartesianSeriesKind.Line, Values = new double?[] { 1, 2, 3 } },
            })
            .Add(b => b.UsePointScale, true));

        var path = cut.Find("path.lumeo-chart-native-line");
        var d = path.GetAttribute("d")!;
        Assert.StartsWith("M", d);
        // Exactly 2 "L" commands for 3 linear points (no smoothing).
        Assert.Equal(2, d.Count(c => c == 'L'));
    }

    [Fact]
    public void Smooth_Line_Uses_Cubic_Curve_Commands()
    {
        var cut = _ctx.Render<L.CartesianChartHost>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B", "C" })
            .Add(b => b.Series, new List<L.NativeCartesianSeries>
            {
                new() { Name = "S1", Kind = L.NativeCartesianSeriesKind.Line, Values = new double?[] { 1, 5, 2 }, Smooth = true },
            })
            .Add(b => b.UsePointScale, true));

        var d = cut.Find("path.lumeo-chart-native-line").GetAttribute("d")!;
        // Disable-check: a non-smoothed path would use only M/L, never C.
        Assert.Contains("C", d);
    }

    [Fact]
    public void Line_With_A_Gap_Renders_Two_Separate_Path_Elements()
    {
        var cut = _ctx.Render<L.CartesianChartHost>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B", "C" })
            .Add(b => b.Series, new List<L.NativeCartesianSeries>
            {
                new() { Name = "S1", Kind = L.NativeCartesianSeriesKind.Line, Values = new double?[] { 1, null, 3 } },
            })
            .Add(b => b.UsePointScale, true));

        // Two contiguous runs of length 1 each (around the gap at index 1) ->
        // two separate <path> elements, each a bare "M" (single point, no "L").
        // Disable-check: if gap-splitting were disabled, this would be ONE path
        // joining all three points with an "L" straight across the gap.
        var paths = cut.FindAll("path.lumeo-chart-native-line");
        Assert.Equal(2, paths.Count);
        Assert.All(paths, path => Assert.DoesNotContain("L", path.GetAttribute("d")));
    }

    [Fact]
    public void Area_Fill_Defaults_To_A_ColorMix_Derived_From_The_Series_Color()
    {
        var cut = _ctx.Render<L.CartesianChartHost>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B" })
            .Add(b => b.Series, new List<L.NativeCartesianSeries>
            {
                new() { Name = "S1", Kind = L.NativeCartesianSeriesKind.Area, Values = new double?[] { 1, 2 }, Color = "var(--color-chart-1)" },
            })
            .Add(b => b.UsePointScale, true));

        var fill = cut.Find("path.lumeo-chart-native-area").GetAttribute("fill")!;
        Assert.Contains("color-mix", fill);
        Assert.Contains("var(--color-chart-1)", fill);
        Assert.DoesNotContain("#", fill); // never resolved to a raw hex — CSS-variable-native per spec §2.5
    }

    [Fact]
    public void Legend_Toggle_Removes_The_Series_From_The_Plot()
    {
        var cut = _ctx.Render<L.CartesianChartHost>(p => p
            .Add(b => b.Categories, new List<string> { "A" })
            .Add(b => b.Series, new List<L.NativeCartesianSeries>
            {
                new() { Name = "S1", Kind = L.NativeCartesianSeriesKind.Bar, Values = new double?[] { 10 } },
                new() { Name = "S2", Kind = L.NativeCartesianSeriesKind.Bar, Values = new double?[] { 20 } },
            })
            .Add(b => b.UsePointScale, false).Add(b => b.ShowLegend, true));

        Assert.Equal(2, cut.FindAll(".lumeo-chart-native-bar").Count);

        var toggleButtons = cut.FindAll("button.lumeo-chart-legend-item");
        toggleButtons[0].Click();

        var remaining = cut.FindAll(".lumeo-chart-native-bar").ToList();
        Assert.Single(remaining);
        Assert.Equal("1", remaining[0].GetAttribute("data-series")); // series 0 hidden, series 1 (index 1) remains
    }

    [Fact]
    public void Keyboard_ArrowRight_From_No_Selection_Activates_The_First_Point_Tooltip()
    {
        var cut = _ctx.Render<L.CartesianChartHost>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B", "C" })
            .Add(b => b.Series, new List<L.NativeCartesianSeries> { new() { Name = "S1", Kind = L.NativeCartesianSeriesKind.Line, Values = new double?[] { 1, 2, 3 } } })
            .Add(b => b.UsePointScale, true));

        Assert.Empty(cut.FindAll(".lumeo-chart-native-tooltip"));

        cut.Find("rect.lumeo-chart-interaction-surface").KeyDown("ArrowRight");

        var tooltip = cut.Find(".lumeo-chart-native-tooltip");
        Assert.Contains("A", tooltip.TextContent);
        Assert.Contains("1", tooltip.TextContent);
    }

    [Fact]
    public void Accessibility_Table_Has_One_Row_Per_Category()
    {
        var cut = _ctx.Render<L.CartesianChartHost>(p => p
            .Add(b => b.Categories, new List<string> { "Jan", "Feb", "Mar" })
            .Add(b => b.Series, new List<L.NativeCartesianSeries> { new() { Name = "S1", Kind = L.NativeCartesianSeriesKind.Bar, Values = new double?[] { 1, 2, 3 } } })
            .Add(b => b.UsePointScale, false).Add(b => b.EChartsTypeLabel, "bar"));

        var rows = cut.FindAll("table.sr-only tbody tr");
        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public void DataZoom_Slices_The_Visible_Category_Window()
    {
        var cut = _ctx.Render<L.CartesianChartHost>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B", "C", "D" })
            .Add(b => b.Series, new List<L.NativeCartesianSeries> { new() { Name = "S1", Kind = L.NativeCartesianSeriesKind.Bar, Values = new double?[] { 1, 2, 3, 4 } } })
            .Add(b => b.UsePointScale, false).Add(b => b.DataZoom, true));

        // Full window by default: 4 bars.
        Assert.Equal(4, cut.FindAll(".lumeo-chart-native-bar").Count);
        Assert.NotEmpty(cut.FindAll(".lumeo-chart-zoom-slider"));
    }

    [Fact]
    public void ShowDataLabels_Renders_One_Text_Per_Point()
    {
        var cut = _ctx.Render<L.CartesianChartHost>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B" })
            .Add(b => b.Series, new List<L.NativeCartesianSeries> { new() { Name = "S1", Kind = L.NativeCartesianSeriesKind.Line, Values = new double?[] { 1, 2 } } })
            .Add(b => b.UsePointScale, true).Add(b => b.ShowDataLabels, true).Add(b => b.LabelFormat, "{c}%"));

        var texts = cut.FindAll("svg text").Where(t => t.TextContent.EndsWith('%')).ToList();
        Assert.Equal(2, texts.Count);
        Assert.Equal("1%", texts[0].TextContent);
        Assert.Equal("2%", texts[1].TextContent);
    }
}
