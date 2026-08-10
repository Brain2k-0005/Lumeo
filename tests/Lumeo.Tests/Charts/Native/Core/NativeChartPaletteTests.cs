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
        Assert.Equal("var(--color-chart-1)", L.NativeChartPalette.Resolve(null, null, 5)); // wraps
    }

    [Fact]
    public void Explicit_Palette_Wraps_When_Series_Count_Exceeds_Palette_Length()
    {
        var palette = new List<string> { "#111", "#222" };
        Assert.Equal("#111", L.NativeChartPalette.Resolve(palette, null, 2));
    }
}
