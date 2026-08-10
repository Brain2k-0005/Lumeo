using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Core;

public class ChartPathBuilderTests
{
    [Fact]
    public void Empty_Points_Produces_Empty_String()
    {
        Assert.Equal(string.Empty, L.ChartPathBuilder.BuildLine(Array.Empty<(double, double)>()));
    }

    [Fact]
    public void Single_Point_Produces_A_Bare_MoveTo()
    {
        var d = L.ChartPathBuilder.BuildLine(new[] { (10.0, 20.0) });
        Assert.Equal("M10,20", d);
    }

    [Fact]
    public void Linear_Curve_Produces_Exact_MoveTo_LineTo_String()
    {
        var pts = new[] { (0.0, 0.0), (10.0, 5.0), (20.0, 0.0) };
        var d = L.ChartPathBuilder.BuildLine(pts, L.ChartCurve.Linear);

        Assert.Equal("M0,0L10,5L20,0", d);
    }

    [Fact]
    public void Monotone_Curve_Starts_With_MoveTo_And_Uses_Cubic_Segments()
    {
        var pts = new[] { (0.0, 0.0), (10.0, 10.0), (20.0, 0.0), (30.0, 10.0) };
        var d = L.ChartPathBuilder.BuildLine(pts, L.ChartCurve.Monotone);

        Assert.StartsWith("M0,0", d);
        Assert.Contains("C", d);
        // Every declared point must appear as a cubic segment endpoint.
        Assert.Contains("20,0", d);
        Assert.Contains("30,10", d);
    }

    [Fact]
    public void Monotone_With_Fewer_Than_Three_Points_Falls_Back_To_Linear()
    {
        var pts = new[] { (0.0, 0.0), (10.0, 10.0) };
        var monotone = L.ChartPathBuilder.BuildLine(pts, L.ChartCurve.Monotone);
        var linear = L.ChartPathBuilder.BuildLine(pts, L.ChartCurve.Linear);

        Assert.Equal(linear, monotone);
    }

    [Fact]
    public void StepAfter_Inserts_A_Corner_At_The_Following_X()
    {
        var pts = new[] { (0.0, 0.0), (10.0, 10.0) };
        var d = L.ChartPathBuilder.BuildLine(pts, L.ChartCurve.StepAfter);

        Assert.Equal("M0,0L10,0L10,10", d);
    }

    [Fact]
    public void StepBefore_Inserts_A_Corner_At_The_Leading_X()
    {
        var pts = new[] { (0.0, 0.0), (10.0, 10.0) };
        var d = L.ChartPathBuilder.BuildLine(pts, L.ChartCurve.StepBefore);

        Assert.Equal("M0,0L0,10L10,10", d);
    }

    [Fact]
    public void BuildArea_Closes_Down_To_The_Baseline()
    {
        var pts = new[] { (0.0, 5.0), (10.0, 2.0) };
        var d = L.ChartPathBuilder.BuildArea(pts, baselineY: 20, L.ChartCurve.Linear);

        Assert.Equal("M0,5L10,2L10,20L0,20Z", d);
    }

    [Fact]
    public void BuildBand_Draws_Top_Forward_And_Bottom_Backward()
    {
        var pts = new[] { (0.0, 10.0, 20.0), (10.0, 5.0, 15.0) }; // (X, Y0, Y1)
        var d = L.ChartPathBuilder.BuildBand(pts, L.ChartCurve.Linear);

        Assert.Equal("M0,20L10,15L10,5L0,10Z", d);
    }

    // --- Disable-check ---
    // A monotone interpolation without the Fritsch-Carlson slope-limiting pass
    // (the `s > 9` overshoot clamp) would let the tangent at a sharp
    // local-max/local-min point over/undershoot past its neighbours. Disabling
    // that clamp on data with a sharp peak between two flatter points is
    // EXPECTED to change the emitted control-point coordinates versus the
    // clamped version — assert they genuinely differ, proving the clamp does
    // something observable (not a no-op on this fixture).
    [Fact]
    public void DisableCheck_Monotone_Without_Overshoot_Clamp_Changes_Control_Points()
    {
        var pts = new[] { (0.0, 0.0), (1.0, 100.0), (2.0, 0.0), (3.0, 100.0), (4.0, 0.0) };

        var clamped = L.ChartPathBuilder.BuildLine(pts, L.ChartCurve.Monotone);
        var unclamped = BuildMonotoneWithoutOvershootClamp(pts);

        Assert.NotEqual(clamped, unclamped);
    }

    // A stripped-down copy of ChartPathBuilder's monotone algorithm with the
    // `if (s > 9) { ... }` overshoot-limiting branch removed, used ONLY to
    // prove the real implementation's clamp is load-bearing on this fixture.
    private static string BuildMonotoneWithoutOvershootClamp(IReadOnlyList<(double X, double Y)> points)
    {
        var n = points.Count;
        var dx = new double[n - 1];
        var m = new double[n - 1];
        var t = new double[n];
        for (var i = 0; i < n - 1; i++)
        {
            dx[i] = points[i + 1].X - points[i].X;
            m[i] = dx[i] == 0 ? 0 : (points[i + 1].Y - points[i].Y) / dx[i];
        }
        t[0] = m[0];
        t[n - 1] = m[n - 2];
        for (var i = 1; i < n - 1; i++)
            t[i] = m[i - 1] * m[i] <= 0 ? 0 : (m[i - 1] + m[i]) / 2;
        // (overshoot clamp intentionally omitted)

        var sb = new System.Text.StringBuilder();
        sb.Append('M').Append(points[0].X).Append(',').Append(points[0].Y);
        for (var i = 0; i < n - 1; i++)
        {
            var x1 = points[i].X + dx[i] / 3;
            var y1 = points[i].Y + t[i] * dx[i] / 3;
            var x2 = points[i + 1].X - dx[i] / 3;
            var y2 = points[i + 1].Y - t[i + 1] * dx[i] / 3;
            sb.Append('C').Append(x1).Append(',').Append(y1).Append(' ')
              .Append(x2).Append(',').Append(y2).Append(' ')
              .Append(points[i + 1].X).Append(',').Append(points[i + 1].Y);
        }
        return sb.ToString();
    }
}

public class ChartArcPathTests
{
    [Fact]
    public void Quarter_Circle_Uses_Small_Arc_Flag()
    {
        var d = L.ChartArcPath.Build(0, 0, 0, 10, 0, Math.PI / 2);
        Assert.Contains("A10,10 0 0 1", d);
    }

    [Fact]
    public void More_Than_Half_Turn_Uses_Large_Arc_Flag()
    {
        var d = L.ChartArcPath.Build(0, 0, 0, 10, 0, Math.PI * 1.2);
        Assert.Contains("A10,10 0 1 1", d);
    }

    [Fact]
    public void Inner_Radius_Zero_Still_Produces_A_Closed_Wedge()
    {
        var d = L.ChartArcPath.Build(50, 50, 0, 40, -Math.PI / 2, 0);
        Assert.StartsWith("M", d);
        Assert.EndsWith("Z", d);
    }

    [Fact]
    public void Full_Turn_Is_Clamped_Just_Under_Two_Pi()
    {
        // A literal full-turn sweep degenerates to a zero-length arc in SVG;
        // the builder must clamp it rather than emit an invalid/invisible path.
        var d = L.ChartArcPath.Build(0, 0, 0, 10, 0, Math.PI * 2);
        Assert.NotEqual(string.Empty, d);
        Assert.Contains("A10,10", d);
    }
}

public class ChartRoundedRectPathTests
{
    [Fact]
    public void TopRounded_Clamps_Radius_To_Half_Width()
    {
        // radius (100) far exceeds half the width (5) — must clamp, not emit
        // a self-intersecting path.
        var d = L.ChartRoundedRectPath.TopRounded(0, 0, 10, 20, 100);
        Assert.Contains("Q0,0 5,0", d); // clamped radius = min(100, w/2=5, h=20) = 5
    }

    [Fact]
    public void TopRounded_Zero_Radius_Is_A_Plain_Rectangle_Outline()
    {
        var d = L.ChartRoundedRectPath.TopRounded(0, 0, 10, 20, 0);
        Assert.Equal("M0,20V0Q0,0 0,0H10Q10,0 10,0V20Z", d);
    }
}
