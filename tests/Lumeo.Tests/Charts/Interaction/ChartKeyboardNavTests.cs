using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Interaction;

public class ChartKeyboardNavTests
{
    [Fact]
    public void ArrowRight_From_No_Selection_Lands_On_First_Point()
    {
        var idx = L.ChartKeyboardNav.MoveIndex(currentIndex: null, pointCount: 10, delta: 1);
        Assert.Equal(0, idx);
    }

    [Fact]
    public void ArrowLeft_From_No_Selection_Lands_On_Last_Point()
    {
        var idx = L.ChartKeyboardNav.MoveIndex(currentIndex: null, pointCount: 10, delta: -1);
        Assert.Equal(9, idx);
    }

    [Fact]
    public void ArrowRight_Advances_By_One()
    {
        var idx = L.ChartKeyboardNav.MoveIndex(currentIndex: 3, pointCount: 10, delta: 1);
        Assert.Equal(4, idx);
    }

    [Fact]
    public void ArrowRight_At_The_End_Clamps_Does_Not_Wrap()
    {
        var idx = L.ChartKeyboardNav.MoveIndex(currentIndex: 9, pointCount: 10, delta: 1);
        Assert.Equal(9, idx);
    }

    [Fact]
    public void ArrowLeft_At_The_Start_Clamps_Does_Not_Wrap()
    {
        var idx = L.ChartKeyboardNav.MoveIndex(currentIndex: 0, pointCount: 10, delta: -1);
        Assert.Equal(0, idx);
    }

    [Fact]
    public void Zero_Points_Returns_Null()
    {
        Assert.Null(L.ChartKeyboardNav.MoveIndex(5, pointCount: 0, delta: 1));
    }

    [Fact]
    public void Home_Is_Always_Index_Zero()
    {
        Assert.Equal(0, L.ChartKeyboardNav.First());
    }

    [Fact]
    public void End_Is_The_Last_Index()
    {
        Assert.Equal(99, L.ChartKeyboardNav.Last(pointCount: 100));
    }

    [Fact]
    public void End_On_Empty_Series_Does_Not_Go_Negative()
    {
        Assert.Equal(0, L.ChartKeyboardNav.Last(pointCount: 0));
    }

    [Fact]
    public void MoveSeries_Wraps_Around_Both_Directions()
    {
        Assert.Equal(0, L.ChartKeyboardNav.MoveSeries(currentSeriesIndex: 2, seriesCount: 3, delta: 1));
        Assert.Equal(2, L.ChartKeyboardNav.MoveSeries(currentSeriesIndex: 0, seriesCount: 3, delta: -1));
    }

    [Fact]
    public void MoveSeries_With_Zero_Series_Returns_Zero()
    {
        Assert.Equal(0, L.ChartKeyboardNav.MoveSeries(0, seriesCount: 0, delta: 1));
    }
}
