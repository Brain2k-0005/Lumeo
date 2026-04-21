namespace Lumeo;

/// <summary>Shape family for <see cref="ChartSkeleton"/>. Each value renders a different
/// SVG silhouette that matches the shape of an actual chart, so loading states don't cause
/// layout shift or empty whitespace flicker before ECharts mounts.</summary>
public enum ChartSkeletonKind
{
    /// <summary>Row of vertical bars with staggered pulse — for Bar/Waterfall/Candlestick/BoxPlot.</summary>
    Bars,
    /// <summary>Zigzag polyline — for LineChart / MixedChart.</summary>
    Line,
    /// <summary>Zigzag polyline with filled area beneath — for AreaChart.</summary>
    Area,
    /// <summary>Donut ring — for Pie/Donut/Gauge/Radial/Sunburst/Funnel/Nightingale/LiquidFill.</summary>
    Pie,
    /// <summary>Randomised pulsing dots — for Scatter / EffectScatter.</summary>
    Scatter,
    /// <summary>5x5 grid with varying opacities — for Treemap/Heatmap/CalendarHeatmap.</summary>
    Grid,
    /// <summary>Rounded rect + centered ring — fallback for Radar/Sankey/Tree/Graph/Geo/etc.</summary>
    Generic,
}

/// <summary>How the loading state is presented.
/// Phantom = render the real chart with synthesized data in a muted palette and morph
/// to real data via ECharts' option-change animation (default — feels continuous).
/// Silhouette = fall back to the legacy SVG skeleton overlay.</summary>
public enum ChartSkeletonStyle
{
    Phantom,
    Silhouette,
}
