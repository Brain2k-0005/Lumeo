namespace Lumeo;

/// <summary>
/// Radar/spider coordinate system (spec §2.3) — N straight axes radiating from
/// a shared center, evenly spaced by angle. Polygon math ported from the
/// approach already prototyped in the owner's reference demo.
/// </summary>
internal sealed class RadarCoordinateSystem
{
    public double CenterX { get; }
    public double CenterY { get; }
    public double Radius { get; }
    public int AxisCount { get; }
    public double StartAngle { get; }

    public RadarCoordinateSystem(double centerX, double centerY, double radius, int axisCount, double startAngle = -Math.PI / 2)
    {
        if (axisCount < 3)
            throw new ArgumentOutOfRangeException(nameof(axisCount), "Radar needs at least 3 axes to form a polygon.");

        CenterX = centerX;
        CenterY = centerY;
        Radius = radius;
        AxisCount = axisCount;
        StartAngle = startAngle;
    }

    /// <summary>
    /// Angle (radians) of the Nth axis, evenly spaced around the circle. Indices
    /// advance CLOCKWISE from <see cref="StartAngle"/> (default: straight up) —
    /// matching the legacy ECharts RadarChart's convention (ECharts' polar/radar
    /// coordinate system walks indicator order clockwise from its startAngle).
    /// Screen Y grows downward, so subtracting the per-axis increment from the
    /// angle (rather than adding it) is what produces clockwise motion here; a
    /// `+` would mirror every series across the vertical axis versus ECharts for
    /// the exact same indicator order and data.
    /// </summary>
    public double AngleForAxis(int index) => StartAngle - index * (2 * Math.PI / AxisCount);

    /// <summary>
    /// Pixel position for a value on axis <paramref name="axisIndex"/>, where
    /// <paramref name="radiusFraction"/> is that value already normalized to
    /// <c>[0,1]</c> of the axis' own scale (clamped defensively).
    /// </summary>
    public (double X, double Y) PointAt(double radiusFraction, int axisIndex)
    {
        var angle = AngleForAxis(axisIndex);
        var r = Radius * Math.Clamp(radiusFraction, 0, 1);
        return (CenterX + r * Math.Cos(angle), CenterY + r * Math.Sin(angle));
    }

    /// <summary>
    /// Builds a closed polygon "d" path from one normalized (0..1) value per
    /// axis, in axis order.
    /// </summary>
    public string PolygonPath(IReadOnlyList<double> normalizedValues)
    {
        if (normalizedValues.Count != AxisCount)
        {
            throw new ArgumentException(
                $"Expected {AxisCount} values (one per axis), got {normalizedValues.Count}.",
                nameof(normalizedValues));
        }

        var pts = new (double X, double Y)[AxisCount];
        for (var i = 0; i < AxisCount; i++) pts[i] = PointAt(normalizedValues[i], i);
        return ChartPathBuilder.BuildLine(pts, ChartCurve.Linear) + "Z";
    }
}
