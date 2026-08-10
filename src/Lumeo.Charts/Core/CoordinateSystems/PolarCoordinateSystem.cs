namespace Lumeo;

/// <summary>
/// Polar (angle + radius) coordinate system (spec §2.3) — shared by Donut, Pie,
/// Nightingale, Radial, PolarBar, Gauge and Funnel-as-trapezoid-stack. Owns the
/// angle bookkeeping so chart types only supply values; <see cref="ChartArcPath"/>
/// does the actual path-string construction.
/// </summary>
internal sealed class PolarCoordinateSystem
{
    public double CenterX { get; }
    public double CenterY { get; }
    public double InnerRadius { get; }
    public double OuterRadius { get; }
    public double StartAngle { get; }

    public PolarCoordinateSystem(
        double centerX, double centerY, double innerRadius, double outerRadius, double startAngle = -Math.PI / 2)
    {
        CenterX = centerX;
        CenterY = centerY;
        InnerRadius = innerRadius;
        OuterRadius = outerRadius;
        StartAngle = startAngle;
    }

    /// <summary>Cartesian pixel coordinates for a given radius/angle.</summary>
    public (double X, double Y) PointAt(double radius, double angle) =>
        (CenterX + radius * Math.Cos(angle), CenterY + radius * Math.Sin(angle));

    /// <summary>Builds the wedge/annular-sector path for the given radii and angles.</summary>
    public string ArcPath(double r0, double r1, double startAngle, double endAngle) =>
        ChartArcPath.Build(CenterX, CenterY, r0, r1, startAngle, endAngle);

    /// <summary>
    /// Splits a full turn proportionally by <paramref name="values"/> (Pie/Donut/
    /// Nightingale slice sizing), starting at <paramref name="startAngle"/> and
    /// sweeping clockwise. A non-positive total (empty/zero/negative-summing
    /// data) degenerates every segment to a zero-width angle at
    /// <paramref name="startAngle"/> rather than dividing by zero or producing
    /// NaN sweeps.
    /// </summary>
    public static IReadOnlyList<(double Start, double End)> SplitByValue(
        IReadOnlyList<double> values, double startAngle = -Math.PI / 2)
    {
        var total = 0.0;
        foreach (var v in values) total += v;

        var segments = new List<(double, double)>(values.Count);
        if (total <= 0)
        {
            foreach (var _ in values) segments.Add((startAngle, startAngle));
            return segments;
        }

        var angle = startAngle;
        foreach (var v in values)
        {
            var sweep = v <= 0 ? 0 : v / total * Math.PI * 2;
            segments.Add((angle, angle + sweep));
            angle += sweep;
        }
        return segments;
    }

    /// <summary>
    /// Splits a full turn into <paramref name="count"/> EQUAL-angle wedges (as
    /// opposed to <see cref="SplitByValue"/>'s value-proportional split) — the
    /// layout Nightingale/PolarBar/Sunburst's inner rings actually use: every
    /// category gets the same angular width, and it's the RADIUS (not the
    /// angle) that encodes the value. <paramref name="gap"/> (radians) shrinks
    /// each wedge symmetrically to leave a visible seam between neighbours,
    /// clamped so a wedge can never invert.
    /// </summary>
    public static IReadOnlyList<(double Start, double End)> EvenSplit(
        int count, double gap = 0, double startAngle = -Math.PI / 2)
    {
        if (count <= 0) return Array.Empty<(double, double)>();

        var step = Math.PI * 2 / count;
        var halfGap = Math.Min(Math.Max(gap, 0) / 2, step / 2);
        var segments = new List<(double, double)>(count);
        for (var i = 0; i < count; i++)
        {
            var a0 = startAngle + i * step + halfGap;
            var a1 = startAngle + (i + 1) * step - halfGap;
            segments.Add((a0, Math.Max(a0, a1)));
        }
        return segments;
    }
}
