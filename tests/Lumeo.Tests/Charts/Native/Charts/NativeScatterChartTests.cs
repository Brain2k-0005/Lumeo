using System.Globalization;
using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native.Charts;

public class NativeScatterChartTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_One_Point_Per_Coordinate_Pair()
    {
        var cut = _ctx.Render<L.NativeScatterChart>(p => p.Add(b => b.Series, new List<L.NativeScatterChart.ScatterSeriesData>
        {
            new() { Name = "S1", Points = new List<double[]> { new[] { 1.0, 2.0 }, new[] { 3.0, 4.0 } } },
        }));

        Assert.Equal(2, cut.FindAll("circle.lumeo-chart-native-point").Count);
    }

    [Fact]
    public void No_Ripple_Circles_For_Plain_Scatter()
    {
        var cut = _ctx.Render<L.NativeScatterChart>(p => p.Add(b => b.Series, new List<L.NativeScatterChart.ScatterSeriesData>
        {
            new() { Name = "S1", Points = new List<double[]> { new[] { 1.0, 2.0 } } },
        }));

        Assert.Empty(cut.FindAll(".lumeo-chart-native-ripple"));
    }

    [Fact]
    public void BubbleSize_Sets_A_Fixed_Radius_For_Every_Point()
    {
        var cut = _ctx.Render<L.NativeScatterChart>(p => p
            .Add(b => b.Series, new List<L.NativeScatterChart.ScatterSeriesData>
            {
                new() { Name = "S1", Points = new List<double[]> { new[] { 1.0, 2.0 }, new[] { 3.0, 4.0 } } },
            })
            .Add(b => b.BubbleSize, 30));

        var radii = cut.FindAll("circle.lumeo-chart-native-point")
            .Select(c => double.Parse(c.GetAttribute("r")!, CultureInfo.InvariantCulture)).ToList();
        Assert.All(radii, r => Assert.Equal(15, r, 3)); // diameter 30 / 2
    }
}
