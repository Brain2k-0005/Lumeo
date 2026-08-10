namespace Lumeo;

/// <summary>
/// Which primitive a <see cref="NativeCartesianSeries"/> renders as inside
/// <c>CartesianChartHost</c>. Deliberately narrow — the seven Cartesian-family
/// chart types (Line, Bar, Area, Mixed, Scatter, EffectScatter, Waterfall) all
/// reduce to a shared "Line/Area/Bar on a category or point x-axis" renderer;
/// Scatter/EffectScatter's continuous x/y domain is handled by the separate
/// <c>XyChartHost</c> instead (they have no category axis to share a band/point
/// scale with).
/// </summary>
public enum NativeCartesianSeriesKind
{
    Line,
    Area,
    Bar,
}

/// <summary>
/// One series' worth of category-aligned data plus per-series rendering flags —
/// the declarative model <c>CartesianChartHost</c> renders from. Every one of
/// this wave's category-based native types (Line, Area, Bar, Mixed, Waterfall)
/// builds a <c>List&lt;NativeCartesianSeries&gt;</c> and hands it to the shared
/// host rather than each type re-deriving scale/stacking/grouping math.
/// </summary>
public sealed class NativeCartesianSeries
{
    /// <summary>Series name — used by the legend, tooltip, and accessibility table.</summary>
    public required string Name { get; init; }

    public required NativeCartesianSeriesKind Kind { get; init; }

    /// <summary>Values aligned 1:1 with the host's <c>Categories</c> list.
    /// <c>null</c> at an index means "no data point there" (a gap): Line/Area
    /// skip it (breaking the drawn path into separate runs around the gap);
    /// Bar renders no bar for that category.</summary>
    public required IReadOnlyList<double?> Values { get; init; }

    /// <summary>Resolved CSS color (a literal color or a <c>var(--token)</c>
    /// reference). When null, the host assigns one from the active palette by
    /// series index.</summary>
    public string? Color { get; init; }

    /// <summary>Line/Area only: monotone-cubic interpolation instead of straight segments.</summary>
    public bool Smooth { get; init; }

    /// <summary>Line only: draw a marker dot at each point.</summary>
    public bool ShowDots { get; init; } = true;

    /// <summary>When true, this series stacks on top of every earlier
    /// Stacked=true series of the same <see cref="Kind"/> family (Bar stacks
    /// with Bar, Area stacks with Area) in list order — the same "single
    /// implicit stack group in series order" semantics as ECharts'
    /// <c>stack:"total"</c>, which is all every one of this wave's callers
    /// ever needs (no type requires two independent stacks side by side).</summary>
    public bool Stacked { get; init; }

    /// <summary>0 = primary (left) y-axis, 1 = secondary (right) y-axis.
    /// Only <c>NativeMixedChart</c> ever sets this to 1 — an opt-in,
    /// additive extension of the legacy Mixed wrapper (which has no
    /// equivalent parameter and always used a single shared axis); see the
    /// delivery report for why the core supports this cleanly.</summary>
    public int YAxisIndex { get; init; }

    /// <summary>Per-point color override (Waterfall's increase/decrease
    /// coloring). Falls back to <see cref="Color"/> where null.</summary>
    public IReadOnlyList<string?>? PointColors { get; init; }

    /// <summary>Per-point tooltip/accessibility value override — used by
    /// Waterfall, whose rendered bar height is an absolute delta but whose
    /// tooltip/SR text should show the signed value. Falls back to
    /// <see cref="Values"/> when null.</summary>
    public IReadOnlyList<double?>? DisplayValues { get; init; }

    /// <summary>Excludes this series from the legend and the shape-count /
    /// bar-slot grouping's "visible series" bookkeeping — used for
    /// Waterfall's invisible connector/base bar.</summary>
    public bool IncludeInLegend { get; init; } = true;

    /// <summary>Excludes this series from hit-testing/tooltip/accessibility
    /// (still renders and still participates in stacking) — Waterfall's base
    /// bar again: it must stack under the real bar but has no meaningful
    /// value of its own to show a user.</summary>
    public bool IncludeInTooltip { get; init; } = true;

    /// <summary>Explicit SVG fill paint (e.g. <c>url(#areaGrad0)</c> for a
    /// gradient defined by the caller in <c>CartesianChartHost.ExtraDefs</c>,
    /// or any literal CSS color/<c>color-mix()</c> expression). When null the
    /// host derives a fill from <see cref="Color"/>.</summary>
    public string? FillPaint { get; init; }

    /// <summary>Area fill opacity when <see cref="FillPaint"/> is null and the
    /// host derives the fill from <see cref="Color"/>. Ignored by Bar (always
    /// opaque) and Line (never filled).</summary>
    public double FillOpacity { get; init; } = 0.28;
}
