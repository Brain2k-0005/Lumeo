using System.Globalization;

namespace Lumeo;

/// <summary>
/// Builds rounded-rectangle SVG paths for bar geometry. <see cref="TopRounded"/>
/// is a direct port of the owner's reference demo's <c>topRounded()</c>
/// (chartsdemo.html, ~lines 612-615) — used by grouped/stacked Bar tops.
/// </summary>
internal static class ChartRoundedRectPath
{
    /// <summary>Rectangle with only its two TOP corners rounded (bar-chart bars).</summary>
    public static string TopRounded(double x, double y, double w, double h, double r)
    {
        r = Math.Max(0, Math.Min(r, Math.Min(w / 2, h)));
        return string.Create(CultureInfo.InvariantCulture, $"M{Fmt(x)},{Fmt(y + h)}V{Fmt(y + r)}Q{Fmt(x)},{Fmt(y)} {Fmt(x + r)},{Fmt(y)}H{Fmt(x + w - r)}Q{Fmt(x + w)},{Fmt(y)} {Fmt(x + w)},{Fmt(y + r)}V{Fmt(y + h)}Z");
    }

    /// <summary>Rectangle with all four corners rounded.</summary>
    public static string AllRounded(double x, double y, double w, double h, double r)
    {
        r = Math.Max(0, Math.Min(r, Math.Min(w / 2, h / 2)));
        return string.Create(CultureInfo.InvariantCulture, $"M{Fmt(x + r)},{Fmt(y)}H{Fmt(x + w - r)}Q{Fmt(x + w)},{Fmt(y)} {Fmt(x + w)},{Fmt(y + r)}V{Fmt(y + h - r)}Q{Fmt(x + w)},{Fmt(y + h)} {Fmt(x + w - r)},{Fmt(y + h)}H{Fmt(x + r)}Q{Fmt(x)},{Fmt(y + h)} {Fmt(x)},{Fmt(y + h - r)}V{Fmt(y + r)}Q{Fmt(x)},{Fmt(y)} {Fmt(x + r)},{Fmt(y)}Z");
    }

    private static string Fmt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
