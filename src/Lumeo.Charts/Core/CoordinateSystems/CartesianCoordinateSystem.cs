namespace Lumeo;

/// <summary>
/// Cartesian (x/y grid) coordinate system (spec §2.3) — the foundational system
/// every bar/line/area/scatter/mixed/waterfall/boxplot/candlestick/heatmap
/// chart type builds on. Wraps caller-supplied domain↔pixel delegates rather
/// than a concrete scale type, because <see cref="LinearScale"/>,
/// <see cref="LogScale"/>, <see cref="TimeScale"/>, <see cref="BandScale"/> and
/// <see cref="PointScale"/> don't share a common interface (their domains
/// differ in shape — continuous vs. calendar vs. discrete-index) — so the
/// coordinate system itself stays scale-agnostic and a chart type just plugs in
/// whichever scale its axis needs.
/// </summary>
internal sealed class CartesianCoordinateSystem
{
    private readonly Func<double, double> _mapX;
    private readonly Func<double, double> _invertX;
    private readonly Func<double, double> _mapY;
    private readonly Func<double, double> _invertY;

    public ChartPlotRect PlotRect { get; }

    public CartesianCoordinateSystem(
        ChartPlotRect plotRect,
        Func<double, double> mapX,
        Func<double, double> invertX,
        Func<double, double> mapY,
        Func<double, double> invertY)
    {
        PlotRect = plotRect;
        _mapX = mapX;
        _invertX = invertX;
        _mapY = mapY;
        _invertY = invertY;
    }

    /// <summary>Maps a data X value to a pixel X position.</summary>
    public double X(double dataX) => _mapX(dataX);

    /// <summary>Maps a data Y value to a pixel Y position.</summary>
    public double Y(double dataY) => _mapY(dataY);

    /// <summary>Inverse of <see cref="X"/>.</summary>
    public double InvertX(double pixelX) => _invertX(pixelX);

    /// <summary>Inverse of <see cref="Y"/>.</summary>
    public double InvertY(double pixelY) => _invertY(pixelY);
}
