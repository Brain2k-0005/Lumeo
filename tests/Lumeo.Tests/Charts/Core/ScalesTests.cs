using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Core;

public class LinearScaleTests
{
    [Theory]
    [InlineData(0, 100, 12.3)]
    [InlineData(-50, 50, -12.7)]
    [InlineData(1000, 2000, 1500)]
    public void RoundTrips_Value_Through_Pixel_And_Back(double min, double max, double value)
    {
        var scale = new L.LinearScale(min, max, 0, 400);

        var px = scale.Scale(value);
        var back = scale.Invert(px);

        Assert.Equal(value, back, precision: 9);
    }

    [Fact]
    public void Empty_Domain_Maps_Every_Value_To_Range_Midpoint()
    {
        var scale = new L.LinearScale(5, 5, 0, 400);

        Assert.Equal(200, scale.Scale(5));
        Assert.Equal(200, scale.Scale(999)); // even an out-of-domain value doesn't throw/NaN
    }

    [Fact]
    public void Domain_Min_Maps_To_Range_Min()
    {
        var scale = new L.LinearScale(0, 100, 10, 410);
        Assert.Equal(10, scale.Scale(0));
        Assert.Equal(410, scale.Scale(100));
    }
}

public class LogScaleTests
{
    [Fact]
    public void RoundTrips_Value_Through_Pixel_And_Back()
    {
        var scale = new L.LogScale(1, 1000, 0, 300);

        var px = scale.Scale(10);
        var back = scale.Invert(px);

        Assert.Equal(10, back, precision: 6);
    }

    [Fact]
    public void Zero_Domain_Min_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new L.LogScale(0, 100, 0, 300));
    }

    [Fact]
    public void Negative_Domain_Min_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new L.LogScale(-5, 100, 0, 300));
    }

    [Fact]
    public void Scaling_A_NonPositive_Value_Throws()
    {
        var scale = new L.LogScale(1, 1000, 0, 300);
        Assert.Throws<ArgumentOutOfRangeException>(() => scale.Scale(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => scale.Scale(-5));
    }

    [Fact]
    public void One_Decade_Midpoint_Lands_At_Half_Range()
    {
        var scale = new L.LogScale(1, 100, 0, 200, @base: 10);
        Assert.Equal(100, scale.Scale(10), precision: 9);
    }
}

public class TimeScaleTests
{
    [Fact]
    public void RoundTrips_DateTimeOffset_Through_Pixel_And_Back()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddDays(30);
        var mid = start.AddDays(15);
        var scale = new L.TimeScale(start, end, 0, 600);

        var px = scale.Scale(mid);
        var back = scale.Invert(px);

        Assert.Equal(mid, back);
    }

    [Fact]
    public void Domain_Start_Maps_To_Range_Min()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var scale = new L.TimeScale(start, start.AddDays(1), 5, 305);
        Assert.Equal(5, scale.Scale(start));
    }
}

public class BandScaleTests
{
    [Fact]
    public void Five_Bands_Are_Evenly_Spaced_And_Ordered()
    {
        var scale = new L.BandScale(5, 0, 500, paddingInner: 0.2, paddingOuter: 0.1);

        for (var i = 1; i < 5; i++)
            Assert.True(scale.Center(i) > scale.Center(i - 1));

        Assert.True(scale.Bandwidth > 0);
        Assert.True(scale.Start(0) >= 0);
        Assert.True(scale.Start(4) + scale.Bandwidth <= 500.0001);
    }

    [Fact]
    public void Zero_Count_Has_Zero_Bandwidth_And_Does_Not_Throw()
    {
        var scale = new L.BandScale(0, 0, 500);
        Assert.Equal(0, scale.Bandwidth);
    }

    [Fact]
    public void Out_Of_Range_Index_Throws()
    {
        var scale = new L.BandScale(3, 0, 300);
        Assert.Throws<ArgumentOutOfRangeException>(() => scale.Start(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => scale.Start(-1));
    }

    [Fact]
    public void Zero_Padding_Bands_Exactly_Fill_The_Range()
    {
        var scale = new L.BandScale(4, 0, 400, paddingInner: 0, paddingOuter: 0);
        Assert.Equal(100, scale.Bandwidth, precision: 9);
        Assert.Equal(0, scale.Start(0), precision: 9);
        Assert.Equal(300, scale.Start(3), precision: 9);
    }
}

public class PointScaleTests
{
    [Fact]
    public void First_And_Last_Point_Sit_At_Range_Ends_With_No_Padding()
    {
        var scale = new L.PointScale(5, 0, 400, paddingOuter: 0);

        Assert.Equal(0, scale.Position(0), precision: 9);
        Assert.Equal(400, scale.Position(4), precision: 9);
    }

    [Fact]
    public void Single_Point_Centers_In_The_Range()
    {
        var scale = new L.PointScale(1, 0, 400);
        Assert.Equal(200, scale.Position(0));
    }

    [Fact]
    public void Zero_Points_Throws_On_Position_Access()
    {
        var scale = new L.PointScale(0, 0, 400);
        Assert.Throws<InvalidOperationException>(() => scale.Position(0));
    }

    [Fact]
    public void Points_Are_Evenly_Spaced()
    {
        var scale = new L.PointScale(5, 0, 400, paddingOuter: 0);
        var spacing = scale.Position(1) - scale.Position(0);
        for (var i = 2; i < 5; i++)
            Assert.Equal(spacing, scale.Position(i) - scale.Position(i - 1), precision: 9);
    }
}
