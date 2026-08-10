using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Core;

public class ChartLayoutTests
{
    [Fact]
    public void Left_Margin_Sized_From_Widest_Y_Label()
    {
        var margin = L.ChartLayout.ComputeCartesianMargin(
            yAxisTickLabelWidths: new[] { 12.0, 40.0, 25.0 },
            yAxisTitleHeight: 0,
            xAxisTickLabelHeight: 14,
            xAxisTitleHeight: 0,
            tickLength: 6,
            basePadding: 8);

        Assert.Equal(40 + 6 + 8, margin.Left);
    }

    [Fact]
    public void Bottom_Margin_Includes_Tick_Label_Height_And_Title()
    {
        var margin = L.ChartLayout.ComputeCartesianMargin(
            yAxisTickLabelWidths: Array.Empty<double>(),
            yAxisTitleHeight: 0,
            xAxisTickLabelHeight: 14,
            xAxisTitleHeight: 18,
            tickLength: 6,
            basePadding: 8);

        Assert.Equal(14 + 6 + 8 + 18, margin.Bottom);
    }

    [Fact]
    public void Empty_Label_Set_Still_Produces_A_Sane_Left_Margin()
    {
        var margin = L.ChartLayout.ComputeCartesianMargin(
            yAxisTickLabelWidths: Array.Empty<double>(),
            yAxisTitleHeight: 0,
            xAxisTickLabelHeight: 0,
            xAxisTitleHeight: 0,
            tickLength: 6,
            basePadding: 8);

        Assert.Equal(6 + 8, margin.Left);
    }

    [Fact]
    public void PlotRect_Subtracts_Margins_From_Container()
    {
        var margin = new L.ChartMargin(Top: 10, Right: 5, Bottom: 20, Left: 40);
        var rect = L.ChartLayout.ComputePlotRect(400, 300, margin);

        Assert.Equal(40, rect.X);
        Assert.Equal(10, rect.Y);
        Assert.Equal(400 - 40 - 5, rect.Width);
        Assert.Equal(300 - 10 - 20, rect.Height);
        Assert.Equal(rect.X + rect.Width, rect.Right);
        Assert.Equal(rect.Y + rect.Height, rect.Bottom);
    }

    [Fact]
    public void PlotRect_Never_Goes_Negative_When_Margins_Exceed_Container()
    {
        var margin = new L.ChartMargin(Top: 100, Right: 100, Bottom: 100, Left: 100);
        var rect = L.ChartLayout.ComputePlotRect(50, 50, margin);

        Assert.Equal(0, rect.Width);
        Assert.Equal(0, rect.Height);
    }
}
