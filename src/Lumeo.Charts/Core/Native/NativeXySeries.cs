namespace Lumeo;

/// <summary>
/// One series' worth of continuous (x,y) point data — the model
/// <c>XyChartHost</c> renders from (Scatter, EffectScatter). Unlike
/// <see cref="NativeCartesianSeries"/>, points here have no category index —
/// x and y are both independent continuous domains, matching the legacy
/// Scatter/EffectScatter wrappers' <c>double[] {x, y}</c> point shape.
/// </summary>
public sealed class NativeXySeries
{
    public required string Name { get; init; }
    public required IReadOnlyList<(double X, double Y)> Points { get; init; }
    public string? Color { get; init; }

    /// <summary>Marker diameter in logical SVG units (ECharts' <c>symbolSize</c> convention).</summary>
    public double SymbolSize { get; init; } = 10;

    /// <summary>Per-point diameter override, aligned with <see cref="Points"/>
    /// — Scatter's <c>BubbleSize</c> legacy parameter is chart-wide (all
    /// points/series share one size), so this is null for Scatter; reserved
    /// for a future per-point-size mode.</summary>
    public IReadOnlyList<double>? PerPointSymbolSize { get; init; }

    /// <summary>EffectScatter's animated ripple pulse. Per spec: no
    /// geometric hit-testing for non-ordered point sets — <c>XyChartHost</c>
    /// attaches native pointer events straight to each marker instead of
    /// going through <c>ChartInteractionSurface</c>.</summary>
    public bool Rippled { get; init; }
}
