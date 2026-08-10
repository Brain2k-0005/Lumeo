using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native.Core;

public class NativeChartPaletteTests
{
    [Fact]
    public void ColorPalette_Takes_Precedence_Over_Colors()
    {
        var palette = new List<string> { "#111", "#222" };
        var colors = new List<string> { "#aaa", "#bbb" };

        Assert.Equal("#111", L.NativeChartPalette.Resolve(palette, colors, 0));
    }

    [Fact]
    public void Colors_Used_When_No_Palette()
    {
        var colors = new List<string> { "#aaa", "#bbb" };
        Assert.Equal("#bbb", L.NativeChartPalette.Resolve(null, colors, 1));
    }

    [Fact]
    public void Falls_Back_To_Theme_Token_Cycling_When_Nothing_Set()
    {
        Assert.Equal("var(--color-chart-1)", L.NativeChartPalette.Resolve(null, null, 0));
        Assert.Equal("var(--color-chart-5)", L.NativeChartPalette.Resolve(null, null, 4));
    }

    [Fact]
    public void Beyond_Five_Series_Varies_The_Wrapped_Token_Instead_Of_Repeating_It_Exactly()
    {
        // Index 5 wraps back to chart-1's token, but must NOT resolve to the
        // exact same literal string as index 0 — two unrelated series would
        // otherwise be visually indistinguishable in the legend/tooltip.
        var round0 = L.NativeChartPalette.Resolve(null, null, 0);
        var round1 = L.NativeChartPalette.Resolve(null, null, 5);
        var round2 = L.NativeChartPalette.Resolve(null, null, 10);

        Assert.Equal("var(--color-chart-1)", round0);
        Assert.NotEqual(round0, round1);
        Assert.NotEqual(round0, round2);
        Assert.NotEqual(round1, round2);
        Assert.Contains("var(--color-chart-1)", round1);
        Assert.Contains("var(--color-chart-1)", round2);
    }

    [Fact]
    public void Explicit_Palette_Wraps_When_Series_Count_Exceeds_Palette_Length()
    {
        var palette = new List<string> { "#111", "#222" };
        Assert.Equal("#111", L.NativeChartPalette.Resolve(palette, null, 2));
    }
}
