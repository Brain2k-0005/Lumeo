using System.Globalization;

namespace Lumeo;

/// <summary>
/// Builds an annular-sector ("donut wedge") SVG path — the one shared arc
/// primitive covering Donut/Pie/Nightingale/Radial/PolarBar/Gauge (spec §2.3).
/// Direct port of the owner's reference demo (chartsdemo.html's
/// <c>arcPath()</c>, ~lines 811-816).
/// </summary>
internal static class ChartArcPath
{
    /// <summary>
    /// Builds the path for the sector between radii <paramref name="r0"/>
    /// (inner, 0 for a plain pie wedge) and <paramref name="r1"/> (outer), and
    /// angles <paramref name="startAngle"/>/<paramref name="endAngle"/> (radians,
    /// 0 = +X axis, increasing clockwise in SVG's y-down space).
    /// </summary>
    public static string Build(double cx, double cy, double r0, double r1, double startAngle, double endAngle)
    {
        endAngle = Math.Min(endAngle, startAngle + Math.PI * 2 - 0.0001);
        var large = endAngle - startAngle > Math.PI ? 1 : 0;

        var p1 = Point(cx, cy, r1, startAngle);
        var p2 = Point(cx, cy, r1, endAngle);
        var p3 = Point(cx, cy, r0, endAngle);
        var p4 = Point(cx, cy, r0, startAngle);

        return string.Create(CultureInfo.InvariantCulture, $"M{Fmt(p1.X)},{Fmt(p1.Y)}A{Fmt(r1)},{Fmt(r1)} 0 {large} 1 {Fmt(p2.X)},{Fmt(p2.Y)}L{Fmt(p3.X)},{Fmt(p3.Y)}A{Fmt(r0)},{Fmt(r0)} 0 {large} 0 {Fmt(p4.X)},{Fmt(p4.Y)}Z");
    }

    private static (double X, double Y) Point(double cx, double cy, double r, double angle) =>
        (cx + r * Math.Cos(angle), cy + r * Math.Sin(angle));

    private static string Fmt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
