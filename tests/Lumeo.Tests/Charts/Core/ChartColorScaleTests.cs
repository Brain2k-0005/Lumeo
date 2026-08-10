using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Core;

public class ChartColorScaleTests
{
    [Fact]
    public void Value_Below_Lowest_Stop_Clamps_To_First_Color()
    {
        var stops = new[] { new L.ChartColorStop(0, "var(--color-chart-1)"), new L.ChartColorStop(10, "var(--color-chart-2)") };
        Assert.Equal("var(--color-chart-1)", L.ChartColorScale.Resolve(stops, -5));
    }

    [Fact]
    public void Value_Above_Highest_Stop_Clamps_To_Last_Color()
    {
        var stops = new[] { new L.ChartColorStop(0, "var(--color-chart-1)"), new L.ChartColorStop(10, "var(--color-chart-2)") };
        Assert.Equal("var(--color-chart-2)", L.ChartColorScale.Resolve(stops, 999));
    }

    [Fact]
    public void Value_Exactly_On_A_Stop_Returns_That_Stops_Color_Literally()
    {
        var stops = new[] { new L.ChartColorStop(0, "var(--color-chart-1)"), new L.ChartColorStop(10, "var(--color-chart-2)") };
        Assert.Equal("var(--color-chart-2)", L.ChartColorScale.Resolve(stops, 10));
    }

    [Fact]
    public void Midpoint_Value_Produces_A_FiftyFifty_ColorMix_Expression()
    {
        var stops = new[] { new L.ChartColorStop(0, "var(--color-chart-1)"), new L.ChartColorStop(10, "var(--color-chart-2)") };
        var result = L.ChartColorScale.Resolve(stops, 5);

        Assert.StartsWith("color-mix(in oklab,", result);
        Assert.Contains("var(--color-chart-2) 50%", result);
        Assert.Contains("var(--color-chart-1) 50%", result);
    }

    [Fact]
    public void Single_Stop_Always_Returns_That_Color()
    {
        var stops = new[] { new L.ChartColorStop(5, "var(--color-chart-3)") };
        Assert.Equal("var(--color-chart-3)", L.ChartColorScale.Resolve(stops, 999));
    }

    [Fact]
    public void Empty_Stops_Throws()
    {
        Assert.Throws<ArgumentException>(() => L.ChartColorScale.Resolve(Array.Empty<L.ChartColorStop>(), 5));
    }

    [Fact]
    public void Stops_Need_Not_Be_PreSorted()
    {
        var sorted = new[] { new L.ChartColorStop(0, "A"), new L.ChartColorStop(10, "B") };
        var unsorted = new[] { new L.ChartColorStop(10, "B"), new L.ChartColorStop(0, "A") };

        Assert.Equal(L.ChartColorScale.Resolve(sorted, 3), L.ChartColorScale.Resolve(unsorted, 3));
    }
}
