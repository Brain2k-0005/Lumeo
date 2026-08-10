using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native.Charts;

/// <summary>Covers <see cref="L.NativeEffectScatterChart"/> — every point gets a
/// ripple, whose <c>prefers-reduced-motion</c> handling lives in a hand-authored
/// CSS rule (lumeo.css's <c>.lumeo-chart-native-ripple</c>), not a Tailwind
/// utility, so it's covered by structural presence here rather than a computed
/// animation-name assertion (bUnit has no CSSOM/browser to read that from).</summary>
public class NativeEffectScatterChartTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Every_Point_Gets_A_Ripple_Circle()
    {
        var cut = _ctx.Render<L.NativeEffectScatterChart>(p => p.Add(b => b.Series, new List<L.NativeEffectScatterChart.EffectScatterSeriesData>
        {
            new() { Name = "S1", Points = new List<double[]> { new[] { 1.0, 2.0 }, new[] { 3.0, 4.0 }, new[] { 5.0, 6.0 } } },
        }));

        Assert.Equal(3, cut.FindAll("circle.lumeo-chart-native-point").Count);
        Assert.Equal(3, cut.FindAll(".lumeo-chart-native-ripple").Count);
    }

    [Fact]
    public void Ripple_Is_Marked_Aria_Hidden_So_It_Is_Not_Announced_Twice()
    {
        var cut = _ctx.Render<L.NativeEffectScatterChart>(p => p.Add(b => b.Series, new List<L.NativeEffectScatterChart.EffectScatterSeriesData>
        {
            new() { Name = "S1", Points = new List<double[]> { new[] { 1.0, 2.0 } } },
        }));

        Assert.Equal("true", cut.Find(".lumeo-chart-native-ripple").GetAttribute("aria-hidden"));
    }

    [Fact]
    public void SymbolSize_Defaults_To_Twenty_Matching_The_Legacy_Wrapper()
    {
        var cut = _ctx.Render<L.NativeEffectScatterChart>(p => p.Add(b => b.Series, new List<L.NativeEffectScatterChart.EffectScatterSeriesData>
        {
            new() { Name = "S1", Points = new List<double[]> { new[] { 1.0, 2.0 } } },
        }));

        var r = double.Parse(cut.Find("circle.lumeo-chart-native-point").GetAttribute("r")!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(10, r, 3); // 20/2, matches legacy's SymbolSize ?? 20 default
    }
}
