using System.Globalization;
using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native.Charts;

public class NativeBarChartTests : IAsyncLifetime
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
    public void Renders_One_Bar_Per_Category()
    {
        var cut = _ctx.Render<L.NativeBarChart>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B", "C" })
            .Add(b => b.Series, new List<L.NativeBarChart.ChartSeriesData> { new() { Name = "S1", Values = new List<double> { 1, 2, 3 } } }));

        Assert.Equal(3, cut.FindAll(".lumeo-chart-native-bar").Count);
    }

    [Fact]
    public void Stacked_True_Flows_Through_To_Cumulative_Bar_Geometry()
    {
        var cut = _ctx.Render<L.NativeBarChart>(p => p
            .Add(b => b.Categories, new List<string> { "A" })
            .Add(b => b.Series, new List<L.NativeBarChart.ChartSeriesData>
            {
                new() { Name = "S1", Values = new List<double> { 10 } },
                new() { Name = "S2", Values = new List<double> { 10 } },
            })
            .Add(b => b.Stacked, true));

        var bars = cut.FindAll(".lumeo-chart-native-bar");
        var firstY = Num(bars[0].GetAttribute("data-y"));
        var firstH = Num(bars[0].GetAttribute("data-height"));
        var secondY = Num(bars[1].GetAttribute("data-y"));
        Assert.Equal(firstY, secondY + firstH, 3);
    }

    [Fact]
    public void Horizontal_True_Renders_Wider_Than_Tall_Bars()
    {
        var cut = _ctx.Render<L.NativeBarChart>(p => p
            .Add(b => b.Categories, new List<string> { "A" })
            .Add(b => b.Series, new List<L.NativeBarChart.ChartSeriesData> { new() { Name = "S1", Values = new List<double> { 10 } } })
            .Add(b => b.Horizontal, true));

        var bar = cut.Find(".lumeo-chart-native-bar");
        Assert.True(Num(bar.GetAttribute("data-width")) > Num(bar.GetAttribute("data-height")));
    }
}
