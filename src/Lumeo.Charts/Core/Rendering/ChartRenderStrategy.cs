namespace Lumeo;

/// <summary>Which primitive a chart type paints with for a given series/shape set.</summary>
internal enum ChartRenderMode
{
    Svg,
    Canvas,
}

/// <summary>
/// SVG-vs-Canvas policy (spec §2.4) — the concrete, testable decision function.
/// Policy, stated plainly:
/// <list type="number">
/// <item>Default is ALWAYS SVG. DOM node count, not raw point count, is the
/// real cost driver; an ordered series is downsampled via LTTB
/// (<see cref="ChartDownsampling"/>) to at most
/// <see cref="ChartDownsampling.MaxTargetPoints"/> points BEFORE it ever
/// reaches this decision, so an ordered line/area series never needs Canvas by
/// default — LTTB already bounds its DOM node count.</item>
/// <item>Canvas is opt-in, never an automatic fallback for ordered series — it
/// only kicks in for an explicit live-high-frequency data mode.</item>
/// <item>Discrete-shape series (scatter, dense-cell heatmap/calendar-heatmap)
/// have no path to simplify the way a line does, so THEY get a shape-count
/// budget instead: under it, individual SVG shapes with native pointer events;
/// over it, Canvas + a spatial-grid hit-test (<see cref="ChartSpatialGrid"/>).</item>
/// </list>
/// </summary>
internal static class ChartRenderStrategy
{
    /// <summary>
    /// Discrete-shape primitive budget (scatter points, dense heatmap/calendar-
    /// heatmap cells, …) above which per-shape SVG DOM nodes + native pointer
    /// listeners are assumed to cost more than a Canvas paint + spatial-grid
    /// hit-test. Set to the midpoint of the spec's stated 5,000–10,000 range.
    /// This constant is a POLICY choice carried over from the spec, not an
    /// empirically re-derived number — this core module is pure C# with no
    /// browser to benchmark real SVG paint/pointer-listener cost against. Needs
    /// re-confirming empirically once a chart type renders real scatter/heatmap
    /// DOM (flagged as a follow-up for the chart-types wave).
    /// </summary>
    public const int DiscreteShapeBudget = 8000;

    /// <summary>Ordered (line/area/bar-like) series render mode. Always SVG
    /// unless the caller has explicitly opted into a live-high-frequency mode
    /// (spec §2.4 point 3) — LTTB already bounds the SVG path's point count for
    /// every other case.</summary>
    public static ChartRenderMode ForOrderedSeries(bool liveHighFrequencyOptIn) =>
        liveHighFrequencyOptIn ? ChartRenderMode.Canvas : ChartRenderMode.Svg;

    /// <summary>Discrete-shape series render mode (spec §2.4 point 4).</summary>
    public static ChartRenderMode ForDiscreteShapes(int shapeCount, bool liveHighFrequencyOptIn)
    {
        if (liveHighFrequencyOptIn) return ChartRenderMode.Canvas;
        return shapeCount > DiscreteShapeBudget ? ChartRenderMode.Canvas : ChartRenderMode.Svg;
    }
}
