using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native.Charts;

public class NativeAreaChartTests : IAsyncLifetime
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

    [Fact]
    public void Renders_An_Area_Path()
    {
        var cut = _ctx.Render<L.NativeAreaChart>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B" })
            .Add(b => b.Series, new List<L.NativeAreaChart.ChartSeriesData> { new() { Name = "S1", Values = new List<double> { 1, 2 } } }));

        Assert.Single(cut.FindAll("path.lumeo-chart-native-area"));
    }

    [Fact]
    public void GradientFill_Emits_A_LinearGradient_Def_And_References_It_As_The_Fill()
    {
        var cut = _ctx.Render<L.NativeAreaChart>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B" })
            .Add(b => b.Series, new List<L.NativeAreaChart.ChartSeriesData> { new() { Name = "S1", Values = new List<double> { 1, 2 } } })
            .Add(b => b.GradientFill, true));

        var gradient = cut.Find("defs linearGradient");
        var id = gradient.GetAttribute("id")!;
        var fill = cut.Find("path.lumeo-chart-native-area").GetAttribute("fill")!;

        Assert.Equal($"url(#{id})", fill);
        Assert.Equal(2, cut.FindAll("defs linearGradient stop").Count); // color -> transparent, two stops
    }

    [Fact]
    public void GradientFill_False_Uses_A_ColorMix_Fill_Not_A_Gradient_Url()
    {
        var cut = _ctx.Render<L.NativeAreaChart>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B" })
            .Add(b => b.Series, new List<L.NativeAreaChart.ChartSeriesData> { new() { Name = "S1", Values = new List<double> { 1, 2 } } })
            .Add(b => b.GradientFill, false));

        var fill = cut.Find("path.lumeo-chart-native-area").GetAttribute("fill")!;
        Assert.DoesNotContain("url(#", fill);
        Assert.Contains("color-mix", fill);
    }

    [Fact]
    public void Stacked_Areas_Second_Series_Builds_On_The_First()
    {
        // Predicted: a stacked area's band path encodes cumulative Y0/Y1 values,
        // producing a DIFFERENT (non-degenerate) "d" than an unstacked render of
        // the same data (which would start every series' band at the baseline).
        var stacked = _ctx.Render<L.NativeAreaChart>(p => p
            .Add(b => b.Categories, new List<string> { "A" })
            .Add(b => b.Series, new List<L.NativeAreaChart.ChartSeriesData>
            {
                new() { Name = "S1", Values = new List<double> { 10 } },
                new() { Name = "S2", Values = new List<double> { 10 } },
            })
            .Add(b => b.Stacked, true));

        var unstacked = _ctx.Render<L.NativeAreaChart>(p => p
            .Add(b => b.Categories, new List<string> { "A" })
            .Add(b => b.Series, new List<L.NativeAreaChart.ChartSeriesData>
            {
                new() { Name = "S1", Values = new List<double> { 10 } },
                new() { Name = "S2", Values = new List<double> { 10 } },
            })
            .Add(b => b.Stacked, false));

        var stackedPaths = stacked.FindAll("path.lumeo-chart-native-area");
        var unstackedPaths = unstacked.FindAll("path.lumeo-chart-native-area");
        Assert.NotEqual(unstackedPaths[1].GetAttribute("d"), stackedPaths[1].GetAttribute("d"));
    }
}
