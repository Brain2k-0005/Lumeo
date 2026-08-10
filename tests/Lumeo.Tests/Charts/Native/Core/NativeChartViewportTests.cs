using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native.Core;

public class NativeChartViewportTests
{
    [Fact]
    public void Parses_A_Pixel_Value()
    {
        Assert.Equal(420, L.NativeChartViewport.ParseViewBoxDimension("420px", 999));
    }

    [Fact]
    public void Percentage_Value_Falls_Back_To_Default()
    {
        Assert.Equal(600, L.NativeChartViewport.ParseViewBoxDimension("100%", 600));
    }

    [Fact]
    public void Null_Falls_Back_To_Default()
    {
        Assert.Equal(350, L.NativeChartViewport.ParseViewBoxDimension(null, 350));
    }

    [Fact]
    public void Zero_Or_Negative_Pixel_Value_Falls_Back_To_Default()
    {
        Assert.Equal(350, L.NativeChartViewport.ParseViewBoxDimension("0px", 350));
        Assert.Equal(350, L.NativeChartViewport.ParseViewBoxDimension("-10px", 350));
    }

    [Fact]
    public void Label_Width_Estimate_Scales_With_Character_Count()
    {
        var shortW = L.NativeChartViewport.EstimateLabelWidth("1");
        var longW = L.NativeChartViewport.EstimateLabelWidth("10000");
        Assert.True(longW > shortW);
        Assert.Equal(5 * shortW, longW, 3);
    }
}
