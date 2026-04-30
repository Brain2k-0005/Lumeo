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
    /// <summary>Rounded rect + centered ring — fallback for unrecognized chart types.</summary>
    Generic,
    /// <summary>Polygon web with radial axes — for RadarChart.</summary>
    Radar,
    /// <summary>Flow diagram (source → target nodes with curved links) — for SankeyChart.</summary>
    Sankey,
    /// <summary>Force-directed nodes + edges — for GraphChart.</summary>
    Graph,
    /// <summary>Branching hierarchy — for TreeChart.</summary>
    Tree,
    /// <summary>Parallel-coordinates lines across multiple axes — for ParallelChart.</summary>
    Parallel,
    /// <summary>Funnel-shaped stepped trapezoids — for FunnelChart.</summary>
    Funnel,
    /// <summary>Semi-circle dial with needle — for GaugeChart.</summary>
    Gauge,
    /// <summary>Radial bars around a polar origin — for PolarBar.</summary>
    Polar,
    /// <summary>OHLC-style wicks + boxes — for CandlestickChart.</summary>
    Candlestick,
}

/// <summary>How the loading state is presented.
/// Phantom = render the real chart with synthesized data in a muted palette and morph
/// to real data via ECharts' option-change animation (default — feels continuous).
/// Silhouette = fall back to the legacy SVG skeleton overlay.
/// Spinner = dim the chart and show a centered circular spinner with a "Loading…" label
/// (closest to what most consumers expect from chart libraries — Recharts, Highcharts).</summary>
public enum ChartSkeletonStyle
{
    Phantom,
    Silhouette,
    Spinner,
}
