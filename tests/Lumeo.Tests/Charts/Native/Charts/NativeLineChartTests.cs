using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native.Charts;

public class NativeLineChartTests : IAsyncLifetime
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
    public void Renders_One_Line_Path_Per_Series()
    {
        var cut = _ctx.Render<L.NativeLineChart>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B", "C" })
            .Add(b => b.Series, new List<L.NativeLineChart.ChartSeriesData>
            {
                new() { Name = "S1", Values = new List<double> { 1, 2, 3 } },
                new() { Name = "S2", Values = new List<double> { 3, 2, 1 } },
            }));

        Assert.Equal(2, cut.FindAll("path.lumeo-chart-native-line").Count);
    }

    [Fact]
    public void ShowDots_False_Renders_No_Marker_Circles()
    {
        var cut = _ctx.Render<L.NativeLineChart>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B" })
            .Add(b => b.Series, new List<L.NativeLineChart.ChartSeriesData> { new() { Name = "S1", Values = new List<double> { 1, 2 } } })
            .Add(b => b.ShowDots, false));

        Assert.Empty(cut.FindAll("circle.lumeo-chart-native-dot"));
    }

    [Fact]
    public void ShowDots_True_Renders_A_Marker_Per_Point()
    {
        var cut = _ctx.Render<L.NativeLineChart>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B" })
            .Add(b => b.Series, new List<L.NativeLineChart.ChartSeriesData> { new() { Name = "S1", Values = new List<double> { 1, 2 } } })
            .Add(b => b.ShowDots, true));

        Assert.Equal(2, cut.FindAll("circle.lumeo-chart-native-dot").Count);
    }

    [Fact]
    public void Smooth_Parameter_Flows_Through_To_A_Cubic_Path()
    {
        var cut = _ctx.Render<L.NativeLineChart>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B", "C" })
            .Add(b => b.Series, new List<L.NativeLineChart.ChartSeriesData> { new() { Name = "S1", Values = new List<double> { 1, 5, 2 } } })
            .Add(b => b.Smooth, true));

        Assert.Contains("C", cut.Find("path.lumeo-chart-native-line").GetAttribute("d"));
    }
}
