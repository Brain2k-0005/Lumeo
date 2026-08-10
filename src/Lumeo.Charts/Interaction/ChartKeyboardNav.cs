namespace Lumeo;

/// <summary>
/// Pure index arithmetic for keyboard access (spec §3.6 tier 2 — genuinely NEW
/// behavior versus the ECharts wrapper baseline, called out as an explicit
/// acceptance criterion, not parity). ArrowLeft/Right move the current point
/// index ±1, Home/End jump to the ends, and a multi-series chart switches its
/// active series via ArrowUp/Down. Triggered by <c>onkeydown</c> instead of
/// <c>onpointermove</c>; otherwise the same index model as
/// <see cref="ChartHitTester"/> — pure C#, no JS.
/// </summary>
internal static class ChartKeyboardNav
{
    /// <summary>Moves the current point index by <paramref name="delta"/>
    /// (typically ±1), clamped to <c>[0, pointCount-1]</c>. When there is no
    /// current index yet, ArrowRight (delta &gt;= 0) starts at the first point
    /// and ArrowLeft (delta &lt; 0) starts at the last — mirroring how a fresh
    /// keyboard focus should reveal data immediately rather than requiring two
    /// key presses.</summary>
    public static int? MoveIndex(int? currentIndex, int pointCount, int delta)
    {
        if (pointCount <= 0) return null;
        var start = currentIndex ?? (delta >= 0 ? -1 : pointCount);
        return Math.Clamp(start + delta, 0, pointCount - 1);
    }

    public static int First() => 0;

    public static int Last(int pointCount) => Math.Max(0, pointCount - 1);

    /// <summary>Switches the active series by <paramref name="delta"/>, wrapping
    /// around <paramref name="seriesCount"/>.</summary>
    public static int MoveSeries(int currentSeriesIndex, int seriesCount, int delta)
    {
        if (seriesCount <= 0) return 0;
        var next = (currentSeriesIndex + delta) % seriesCount;
        if (next < 0) next += seriesCount;
        return next;
    }
}
