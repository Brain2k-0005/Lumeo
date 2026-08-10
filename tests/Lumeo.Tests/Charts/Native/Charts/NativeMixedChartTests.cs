using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native.Charts;

/// <summary>Covers <see cref="L.NativeMixedChart"/> — the type the task calls
/// out as the contract stress-test (multiple series kinds sharing axes, with
/// independent y-scales).</summary>
public class NativeMixedChartTests : IAsyncLifetime
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
    public void Bar_And_Line_Series_Render_As_Their_Own_Primitives()
    {
        var cut = _ctx.Render<L.NativeMixedChart>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B" })
            .Add(b => b.Series, new List<L.NativeMixedChart.MixedSeriesData>
            {
                new() { Name = "Bars", Values = new List<double> { 1, 2 }, Type = "bar" },
                new() { Name = "Line", Values = new List<double> { 3, 4 }, Type = "line" },
            }));

        Assert.NotEmpty(cut.FindAll(".lumeo-chart-native-bar"));
        Assert.Single(cut.FindAll("path.lumeo-chart-native-line"));
    }

    [Fact]
    public void Null_Type_Defaults_To_Bar_Matching_The_Legacy_Wrapper()
    {
        var cut = _ctx.Render<L.NativeMixedChart>(p => p
            .Add(b => b.Categories, new List<string> { "A" })
            .Add(b => b.Series, new List<L.NativeMixedChart.MixedSeriesData> { new() { Name = "S1", Values = new List<double> { 5 }, Type = null } }));

        Assert.NotEmpty(cut.FindAll(".lumeo-chart-native-bar"));
        Assert.Empty(cut.FindAll("path.lumeo-chart-native-line"));
    }

    [Fact]
    public void YAxisIndex_One_Enables_A_Second_Right_Axis()
    {
        var withSecondAxis = _ctx.Render<L.NativeMixedChart>(p => p
            .Add(b => b.Categories, new List<string> { "A" })
            .Add(b => b.Series, new List<L.NativeMixedChart.MixedSeriesData>
            {
                new() { Name = "Primary", Values = new List<double> { 5 }, Type = "bar", YAxisIndex = 0 },
                new() { Name = "Secondary", Values = new List<double> { 5000 }, Type = "line", YAxisIndex = 1 },
            }));

        var withoutSecondAxis = _ctx.Render<L.NativeMixedChart>(p => p
            .Add(b => b.Categories, new List<string> { "A" })
            .Add(b => b.Series, new List<L.NativeMixedChart.MixedSeriesData>
            {
                new() { Name = "Primary", Values = new List<double> { 5 }, Type = "bar", YAxisIndex = 0 },
                new() { Name = "Secondary", Values = new List<double> { 5000 }, Type = "line", YAxisIndex = 0 },
            }));

        // With a genuinely independent secondary axis, the bar's own geometry is
        // unaffected by the line series' huge value — proving the two scales don't
        // share one domain. Without it (both on axis 0), the tiny bar would be
        // squashed to near-zero height by the shared domain reaching 5000.
        var barWithSecondAxis = withSecondAxis.Find(".lumeo-chart-native-bar");
        var barWithoutSecondAxis = withoutSecondAxis.Find(".lumeo-chart-native-bar");
        var hWith = double.Parse(barWithSecondAxis.GetAttribute("data-height")!, System.Globalization.CultureInfo.InvariantCulture);
        var hWithout = double.Parse(barWithoutSecondAxis.GetAttribute("data-height")!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(hWith > hWithout * 10, $"expected the independently-scaled bar ({hWith}) to be far taller than the shared-scale one ({hWithout})");
    }
}
