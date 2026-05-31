namespace Lumeo;

/// <summary>
/// One series&rsquo; snapshot at the hovered category. Axis-trigger tooltips fire once per
/// hover with N entries (one per series at that x-position); the chart projects them into
/// <see cref="ChartTooltipContext.Points"/> so the slot can iterate. The 8 positional
/// fields on <see cref="ChartTooltipContext"/> still surface the <em>first</em> point as
/// the &ldquo;headline&rdquo; for back-compat.
/// </summary>
public sealed record ChartTooltipPoint(
    string SeriesName,
    string SeriesType,
    int SeriesIndex,
    int DataIndex,
    double? Value,
    string? Color,
    string? Marker);

/// <summary>
/// Data passed to a <see cref="ChartTooltip"/>'s <c>ChildContent</c> on each hover.
/// Mirrors the most commonly used fields of ECharts' tooltip <c>formatter</c> params
/// object. Use <see cref="Raw"/> for fields not surfaced explicitly.
/// </summary>
public sealed record ChartTooltipContext(
    /// <summary>Series the hovered point belongs to. Empty when the chart has no series name.</summary>
    string SeriesName,
    /// <summary>Series type (e.g. <c>"line"</c>, <c>"bar"</c>, <c>"pie"</c>).</summary>
    string SeriesType,
    /// <summary>Index of the series in <c>option.series</c>.</summary>
    int SeriesIndex,
    /// <summary>Display name of the point. For category axes this is the category label;
    /// for value axes this is the formatted axis value; for pie slices this is the slice name.</summary>
    string DataName,
    /// <summary>Index of the point within its series.</summary>
    int DataIndex,
    /// <summary>Numeric value of the point. Multi-value series (scatter / candlestick) surface
    /// the first dimension here; the rest are available via <see cref="Raw"/>.</summary>
    double? Value,
    /// <summary>Hex / RGB color ECharts assigned to the point (matches the legend swatch).</summary>
    string? Color,
    /// <summary>Raw ECharts params object as a dictionary &mdash; escape hatch for fields the
    /// strongly-typed properties above don't cover (e.g. multi-dim scatter, percent, marker).</summary>
    IReadOnlyDictionary<string, object?> Raw)
{
    /// <summary>
    /// All series&rsquo; points at the hovered category. For an axis-trigger tooltip on a
    /// multi-series chart this carries one entry per visible series; item-trigger tooltips
    /// surface a single entry that matches the typed fields above. Added as an init-only
    /// property so existing callers using the 8-positional constructor continue to compile.
    /// </summary>
    public IReadOnlyList<ChartTooltipPoint> Points { get; init; } = Array.Empty<ChartTooltipPoint>();

    /// <summary>Empty context used as the initial portal render before any hover has happened.</summary>
    public static ChartTooltipContext Empty { get; } = new(
        SeriesName: string.Empty,
        SeriesType: string.Empty,
        SeriesIndex: 0,
        DataName: string.Empty,
        DataIndex: 0,
        Value: null,
        Color: null,
        Raw: new Dictionary<string, object?>());
}
