using System.Linq;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Core;

public class CartesianCoordinateSystemTests
{
    [Fact]
    public void Delegates_Roundtrip_Through_X_And_Y()
    {
        var linearX = new L.LinearScale(0, 100, 0, 400);
        var linearY = new L.LinearScale(0, 50, 300, 0);
        var plot = new L.ChartPlotRect(0, 0, 400, 300);
        var cs = new L.CartesianCoordinateSystem(plot, linearX.Scale, linearX.Invert, linearY.Scale, linearY.Invert);

        var px = cs.X(25);
        var py = cs.Y(10);

        Assert.Equal(25, cs.InvertX(px), precision: 9);
        Assert.Equal(10, cs.InvertY(py), precision: 9);
    }
}

public class PolarCoordinateSystemTests
{
    [Fact]
    public void SplitByValue_Sweeps_Cover_A_Full_Turn()
    {
        var segs = L.PolarCoordinateSystem.SplitByValue(new[] { 10.0, 20.0, 30.0 });

        var totalSweep = segs.Sum(s => s.End - s.Start);
        Assert.Equal(Math.PI * 2, totalSweep, precision: 6);
    }

    [Fact]
    public void SplitByValue_Segment_Sizes_Are_Proportional_To_Value()
    {
        var segs = L.PolarCoordinateSystem.SplitByValue(new[] { 25.0, 75.0 });

        var sweep0 = segs[0].End - segs[0].Start;
        var sweep1 = segs[1].End - segs[1].Start;
        Assert.Equal(0.25, sweep0 / (sweep0 + sweep1), precision: 6);
        Assert.Equal(0.75, sweep1 / (sweep0 + sweep1), precision: 6);
    }

    [Fact]
    public void SplitByValue_Zero_Total_Degenerates_To_ZeroWidth_Segments_Not_NaN()
    {
        var segs = L.PolarCoordinateSystem.SplitByValue(new[] { 0.0, 0.0 });

        Assert.All(segs, s => Assert.Equal(s.Start, s.End));
        Assert.All(segs, s => Assert.False(double.IsNaN(s.Start)));
    }

    [Fact]
    public void EvenSplit_Divides_Into_Equal_Angle_Wedges()
    {
        var segs = L.PolarCoordinateSystem.EvenSplit(count: 4, gap: 0);

        foreach (var s in segs)
            Assert.Equal(Math.PI / 2, s.End - s.Start, precision: 9);
    }

    [Fact]
    public void EvenSplit_Gap_Shrinks_Each_Wedge_Symmetrically()
    {
        var noGap = L.PolarCoordinateSystem.EvenSplit(4, gap: 0);
        var withGap = L.PolarCoordinateSystem.EvenSplit(4, gap: 0.1);

        for (var i = 0; i < 4; i++)
        {
            var noGapWidth = noGap[i].End - noGap[i].Start;
            var gapWidth = withGap[i].End - withGap[i].Start;
            Assert.True(gapWidth < noGapWidth);
        }
    }

    [Fact]
    public void EvenSplit_Zero_Count_Returns_Empty()
    {
        Assert.Empty(L.PolarCoordinateSystem.EvenSplit(0));
    }

    [Fact]
    public void ArcPath_Delegates_To_Center()
    {
        var polar = new L.PolarCoordinateSystem(50, 50, 0, 40);
        var d = polar.ArcPath(0, 40, 0, Math.PI / 2);

        Assert.StartsWith("M90,50", d); // p1 = (cx+r1*cos(0), cy+r1*sin(0)) = (50+40, 50)
    }
}

public class RadarCoordinateSystemTests
{
    [Fact]
    public void Axes_Are_Evenly_Spaced_Around_The_Circle()
    {
        var radar = new L.RadarCoordinateSystem(0, 0, 100, axisCount: 4);
        var spacing = radar.AngleForAxis(1) - radar.AngleForAxis(0);

        for (var i = 2; i < 4; i++)
            Assert.Equal(spacing, radar.AngleForAxis(i) - radar.AngleForAxis(i - 1), precision: 9);
    }

    [Fact]
    public void PointAt_Zero_Fraction_Is_The_Center()
    {
        var radar = new L.RadarCoordinateSystem(10, 10, 100, axisCount: 3);
        var (x, y) = radar.PointAt(0, axisIndex: 0);

        Assert.Equal(10, x, precision: 6);
        Assert.Equal(10, y, precision: 6);
    }

    [Fact]
    public void PolygonPath_Requires_One_Value_Per_Axis()
    {
        var radar = new L.RadarCoordinateSystem(0, 0, 100, axisCount: 5);
        Assert.Throws<ArgumentException>(() => radar.PolygonPath(new[] { 0.5, 0.5 }));
    }

    [Fact]
    public void PolygonPath_Closes_The_Shape()
    {
        var radar = new L.RadarCoordinateSystem(0, 0, 100, axisCount: 3);
        var d = radar.PolygonPath(new[] { 1.0, 1.0, 1.0 });

        Assert.EndsWith("Z", d);
    }

    [Fact]
    public void Fewer_Than_Three_Axes_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new L.RadarCoordinateSystem(0, 0, 100, axisCount: 2));
    }
}
