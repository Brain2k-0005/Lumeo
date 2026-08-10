namespace Lumeo;

/// <summary>Outer padding on each side of a chart's plot area.</summary>
internal readonly record struct ChartMargin(double Top, double Right, double Bottom, double Left)
{
    public static readonly ChartMargin Zero = new(0, 0, 0, 0);
}

/// <summary>The rectangle data actually plots into, after margins are subtracted
/// from the container size.</summary>
internal readonly record struct ChartPlotRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

/// <summary>
/// Auto-margin / plot-rect computation (spec §2.2) — the C# side of the
/// engine's ONE required text-measurement JS call. ECharts' <c>grid.containLabel:
/// true</c> auto-computes margins by measuring rendered label text, which pure
/// C# cannot do with pixel accuracy (font metrics are a browser/OS fact, not a
/// computable one). The engine's JS interop batches raw pixel widths once per
/// axis-label-set per render (<c>measureTextWidths</c>); this type owns 100% of
/// the resulting margin arithmetic — JS never decides layout, only measures text.
/// </summary>
internal static class ChartLayout
{
    /// <summary>
    /// Computes the four-sided margin for a cartesian chart given the axis
    /// label metrics.
    /// </summary>
    /// <param name="yAxisTickLabelWidths">Pixel widths of every Y-axis tick
    /// label (from <c>measureTextWidths</c>); the widest one sizes the left
    /// margin.</param>
    /// <param name="yAxisTitleHeight">Extra left-margin space reserved for a Y
    /// axis title (0 when there is none).</param>
    /// <param name="xAxisTickLabelHeight">Vertical space the X-axis tick labels
    /// need — a single line's font height for unrotated labels, or the rotated
    /// label's measured height for angled labels (spec's <c>ChartLabelHelper</c>
    /// rotation policy feeds this).</param>
    /// <param name="xAxisTitleHeight">Extra bottom-margin space reserved for an
    /// X axis title (0 when there is none).</param>
    public static ChartMargin ComputeCartesianMargin(
        IReadOnlyList<double> yAxisTickLabelWidths,
        double yAxisTitleHeight,
        double xAxisTickLabelHeight,
        double xAxisTitleHeight,
        double tickLength = 6,
        double basePadding = 8,
        double topPadding = 12,
        double rightPadding = 10)
    {
        var maxYLabelWidth = 0.0;
        foreach (var w in yAxisTickLabelWidths)
            if (w > maxYLabelWidth) maxYLabelWidth = w;

        var left = maxYLabelWidth + tickLength + basePadding + yAxisTitleHeight;
        var bottom = xAxisTickLabelHeight + tickLength + basePadding + xAxisTitleHeight;
        return new ChartMargin(topPadding, rightPadding, bottom, left);
    }

    /// <summary>Subtracts <paramref name="margin"/> from the container size to
    /// get the plot rectangle. Never returns negative dimensions — an
    /// over-large margin (tiny container) clamps width/height to 0 rather than
    /// producing a rect that would invert scale ranges.</summary>
    public static ChartPlotRect ComputePlotRect(double containerWidth, double containerHeight, ChartMargin margin)
    {
        var width = Math.Max(0, containerWidth - margin.Left - margin.Right);
        var height = Math.Max(0, containerHeight - margin.Top - margin.Bottom);
        return new ChartPlotRect(margin.Left, margin.Top, width, height);
    }
}
