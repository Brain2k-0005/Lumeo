using System.Globalization;
using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native.Charts;

/// <summary>Covers <see cref="L.NativeWaterfallChart"/> — running-total math with
/// negative steps, ported 1:1 from the legacy wrapper's own algorithm.</summary>
public class NativeWaterfallChartTests : IAsyncLifetime
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
    public void Renders_One_Visible_Bar_Per_Category_Plus_An_Invisible_Floor()
    {
        // Two rendered "series" per category (floor + delta), but only one row
        // in the legend/tooltip/a11y surface since the floor is excluded.
        var cut = _ctx.Render<L.NativeWaterfallChart>(p => p
            .Add(b => b.Categories, new List<string> { "Start", "Q1", "Q2" })
            .Add(b => b.Values, new List<double> { 100, 40, -20 }));

        Assert.Equal(6, cut.FindAll(".lumeo-chart-native-bar").Count); // 3 categories x 2 layers
        var rows = cut.FindAll("table.sr-only tbody tr");
        Assert.Equal(3, rows.Count); // floor excluded from the a11y table
    }

    [Fact]
    public void Negative_Step_Produces_A_Bar_Touching_The_Floors_Top_Edge()
    {
        // Values: [100, -40] -> running total goes 0 -> 100 -> 60. For category 1
        // ("Drop"): floor spans domain [0,60], the visible delta bar spans [60,100]
        // (stacked directly on top of the floor, per ComputeStackedExtents' running-
        // total order). The two segments must touch exactly at the value=60
        // boundary: the delta bar's BOTTOM pixel edge (y + height) must equal the
        // floor bar's TOP pixel edge (y) — disable-check: if stacking order were
        // reversed or the running-total math were wrong, this boundary would gap
        // or overlap instead of matching exactly.
        var cut = _ctx.Render<L.NativeWaterfallChart>(p => p
            .Add(b => b.Categories, new List<string> { "Start", "Drop" })
            .Add(b => b.Values, new List<double> { 100, -40 }));

        var bars = cut.FindAll(".lumeo-chart-native-bar");
        // 4 bars, rendered series-major (all of the floor series' bars, then all of
        // the delta series' bars): [floor@0, floor@1, delta@0, delta@1].
        Assert.Equal(4, bars.Count);

        var floorY = Num(bars[1].GetAttribute("data-y"));      // floor bar for category 1 ("Drop") — top edge = value 60
        var deltaY = Num(bars[3].GetAttribute("data-y"));      // delta bar for category 1 — top edge = value 100
        var deltaH = Num(bars[3].GetAttribute("data-height"));

        Assert.Equal(floorY, deltaY + deltaH, 3);
        Assert.True(deltaH > 0);
    }

    [Fact]
    public void Positive_And_Negative_Points_Get_Different_Colors()
    {
        var cut = _ctx.Render<L.NativeWaterfallChart>(p => p
            .Add(b => b.Categories, new List<string> { "Up", "Down" })
            .Add(b => b.Values, new List<double> { 50, -30 })
            .Add(b => b.IncreaseColor, "#00ff00")
            .Add(b => b.DecreaseColor, "#ff0000"));

        var bars = cut.FindAll(".lumeo-chart-native-bar");
        // Series-major order: [floor@Up, floor@Down, delta@Up, delta@Down].
        // bars[2] = delta bar for "Up" (positive), bars[3] = delta bar for "Down" (negative)
        Assert.Equal("#00ff00", bars[2].GetAttribute("fill"));
        Assert.Equal("#ff0000", bars[3].GetAttribute("fill"));
    }

    [Fact]
    public void ShowLegend_Defaults_To_False_Matching_The_Legacy_Wrapper()
    {
        var cut = _ctx.Render<L.NativeWaterfallChart>(p => p
            .Add(b => b.Categories, new List<string> { "A" })
            .Add(b => b.Values, new List<double> { 10 }));

        Assert.Empty(cut.FindAll(".lumeo-chart-legend"));
    }
}
