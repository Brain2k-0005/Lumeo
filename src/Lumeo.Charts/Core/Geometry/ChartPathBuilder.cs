using System.Globalization;
using System.Text;

namespace Lumeo;

/// <summary>Line-interpolation strategy for an SVG path built from data points.</summary>
internal enum ChartCurve
{
    /// <summary>Straight segments between points.</summary>
    Linear,

    /// <summary>Fritsch–Carlson monotone cubic — smooth, but never overshoots
    /// past a point's neighbours (unlike a naive Catmull-Rom/basis spline).</summary>
    Monotone,

    /// <summary>Step transition BEFORE each point (horizontal-then-vertical).</summary>
    StepBefore,

    /// <summary>Step transition AFTER each point (vertical-then-horizontal).</summary>
    StepAfter,
}

/// <summary>
/// Builds SVG path "d" attribute strings from data points — the core owns 100%
/// of path-string construction (spec §2.4's C#/JS split table); JS never
/// touches geometry. The monotone-cubic implementation is a direct port of the
/// owner's reference demo (chartsdemo.html's <c>monotonePath()</c>, ~lines
/// 457-476).
/// </summary>
internal static class ChartPathBuilder
{
    /// <summary>Builds an open line path through <paramref name="points"/>.</summary>
    public static string BuildLine(IReadOnlyList<(double X, double Y)> points, ChartCurve curve = ChartCurve.Linear)
    {
        if (points.Count == 0) return string.Empty;
        if (points.Count == 1) return Invariant($"M{Fmt(points[0].X)},{Fmt(points[0].Y)}");

        return curve switch
        {
            ChartCurve.Monotone => BuildMonotone(points),
            ChartCurve.StepBefore => BuildStep(points, before: true),
            ChartCurve.StepAfter => BuildStep(points, before: false),
            _ => BuildLinear(points),
        };
    }

    public static string BuildLinear(IReadOnlyList<(double X, double Y)> points)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < points.Count; i++)
        {
            sb.Append(i == 0 ? 'M' : 'L').Append(Fmt(points[i].X)).Append(',').Append(Fmt(points[i].Y));
        }
        return sb.ToString();
    }

    private static string BuildStep(IReadOnlyList<(double X, double Y)> points, bool before)
    {
        var sb = new StringBuilder();
        sb.Append('M').Append(Fmt(points[0].X)).Append(',').Append(Fmt(points[0].Y));
        for (var i = 1; i < points.Count; i++)
        {
            var prev = points[i - 1];
            var cur = points[i];
            var midX = before ? prev.X : cur.X;
            var midY = before ? cur.Y : prev.Y;
            sb.Append('L').Append(Fmt(midX)).Append(',').Append(Fmt(midY));
            sb.Append('L').Append(Fmt(cur.X)).Append(',').Append(Fmt(cur.Y));
        }
        return sb.ToString();
    }

    // Fritsch–Carlson monotone cubic interpolation — direct port of the owner's
    // reference demo (chartsdemo.html monotonePath(), ~lines 457-476).
    private static string BuildMonotone(IReadOnlyList<(double X, double Y)> points)
    {
        var n = points.Count;
        if (n < 3) return BuildLinear(points);

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
        for (var i = 0; i < n - 1; i++)
        {
            if (m[i] == 0) { t[i] = 0; t[i + 1] = 0; continue; }
            var a = t[i] / m[i];
            var b = t[i + 1] / m[i];
            var s = a * a + b * b;
            if (s > 9)
            {
                var tau = 3 / Math.Sqrt(s);
                t[i] = tau * a * m[i];
                t[i + 1] = tau * b * m[i];
            }
        }

        var sb = new StringBuilder();
        sb.Append('M').Append(Fmt(points[0].X)).Append(',').Append(Fmt(points[0].Y));
        for (var i = 0; i < n - 1; i++)
        {
            var x1 = points[i].X + dx[i] / 3;
            var y1 = points[i].Y + t[i] * dx[i] / 3;
            var x2 = points[i + 1].X - dx[i] / 3;
            var y2 = points[i + 1].Y - t[i + 1] * dx[i] / 3;
            sb.Append('C').Append(Fmt(x1)).Append(',').Append(Fmt(y1)).Append(' ')
              .Append(Fmt(x2)).Append(',').Append(Fmt(y2)).Append(' ')
              .Append(Fmt(points[i + 1].X)).Append(',').Append(Fmt(points[i + 1].Y));
        }
        return sb.ToString();
    }

    /// <summary>Builds a filled area path: the top edge through
    /// <paramref name="topPoints"/>, closed down to <paramref name="baselineY"/>.</summary>
    public static string BuildArea(
        IReadOnlyList<(double X, double Y)> topPoints, double baselineY, ChartCurve curve = ChartCurve.Linear)
    {
        if (topPoints.Count == 0) return string.Empty;
        var top = BuildLine(topPoints, curve);
        var last = topPoints[^1];
        var first = topPoints[0];
        return Invariant($"{top}L{Fmt(last.X)},{Fmt(baselineY)}L{Fmt(first.X)},{Fmt(baselineY)}Z");
    }

    /// <summary>Builds a stacked band path: the upper edge (Y1 per point) drawn
    /// forward, the lower edge (Y0 per point) drawn backward, closing the band —
    /// used by stacked Area.</summary>
    public static string BuildBand(IReadOnlyList<(double X, double Y0, double Y1)> points, ChartCurve curve = ChartCurve.Linear)
    {
        if (points.Count == 0) return string.Empty;

        var top = new (double X, double Y)[points.Count];
        var bottom = new (double X, double Y)[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            top[i] = (points[i].X, points[i].Y1);
            bottom[points.Count - 1 - i] = (points[i].X, points[i].Y0);
        }

        var topPath = BuildLine(top, curve);
        var bottomPath = BuildLine(bottom, curve);
        // bottomPath starts with its own "M" — swap it for "L" to continue the
        // same path instead of starting a new subpath.
        var bottomContinuation = bottomPath.Length > 0 ? "L" + bottomPath[1..] : bottomPath;
        return topPath + bottomContinuation + "Z";
    }

    private static string Fmt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Invariant(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
