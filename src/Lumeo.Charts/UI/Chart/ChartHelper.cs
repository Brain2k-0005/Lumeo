using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lumeo;

/// <summary>
/// Shared helpers for chart convenience components.
/// </summary>
internal static class ChartHelper
{
    /// <summary>
    /// Merges OptionOverride entries into the option's ExtensionData.
    /// </summary>
    /// <remarks>
    /// An override key that names a property the typed <see cref="EChartOption"/> ALSO
    /// serializes (most commonly <c>series</c>, but any of title/tooltip/legend/grid/
    /// xAxis/yAxis/... apply) used to land in <see cref="EChartOption.ExtensionData"/>
    /// alongside the already-populated typed property — System.Text.Json's
    /// <c>[JsonExtensionData]</c> writer does not de-dupe against regular properties, so
    /// BOTH were emitted under the same JSON key (confirmed: <c>JsonNode.Parse</c> on the
    /// resulting text throws <c>ArgumentException: An item with the same key has already
    /// been added</c>, and a plain <c>JSON.parse</c> on the JS side silently keeps
    /// whichever duplicate came last, discarding the generated series/tooltip/etc.
    /// entirely). Clearing the colliding typed property first — so the override always
    /// wins outright for that top-level key, per the OptionOverride escape-hatch
    /// contract — guarantees the key is emitted exactly once.
    /// </remarks>
    public static void ApplyOptionOverride(EChartOption option, Dictionary<string, object>? overrides)
    {
        if (overrides is null || overrides.Count == 0) return;

        option.ExtensionData ??= new Dictionary<string, JsonElement>();
        foreach (var kvp in overrides)
        {
            ClearCollidingTypedProperty(option, kvp.Key);

            var json = JsonSerializer.Serialize(kvp.Value);
            var element = JsonSerializer.Deserialize<JsonElement>(json);
            option.ExtensionData[kvp.Key] = element;
        }
    }

    /// <summary>
    /// Nulls out whichever typed <see cref="EChartOption"/> property serializes under
    /// the given camelCase JSON key, so a same-named <see cref="EChartOption.ExtensionData"/>
    /// entry set right after doesn't collide with it at serialization time. A no-op for
    /// any key that isn't one of <see cref="EChartOption"/>'s own top-level properties
    /// (the common case — most OptionOverride keys name something the typed model has no
    /// dedicated parameter for at all).
    /// </summary>
    private static void ClearCollidingTypedProperty(EChartOption option, string key)
    {
        switch (key)
        {
            case "title": option.Title = null; break;
            case "tooltip": option.Tooltip = null; break;
            case "legend": option.Legend = null; break;
            case "grid": option.Grid = null; break;
            case "xAxis": option.XAxis = null; break;
            case "yAxis": option.YAxis = null; break;
            case "radar": option.Radar = null; break;
            case "angleAxis": option.AngleAxis = null; break;
            case "radiusAxis": option.RadiusAxis = null; break;
            case "polar": option.Polar = null; break;
            case "series": option.Series = null; break;
            case "color": option.Color = null; break;
            case "visualMap": option.VisualMap = null; break;
            case "animation": option.Animation = null; break;
            case "singleAxis": option.SingleAxis = null; break;
            case "parallel": option.Parallel = null; break;
            case "parallelAxis": option.ParallelAxis = null; break;
            case "dataZoom": option.DataZoom = null; break;
            case "toolbox": option.Toolbox = null; break;
            case "brush": option.Brush = null; break;
            case "geo": option.Geo = null; break;
            case "calendar": option.Calendar = null; break;
            case "dataset": option.Dataset = null; break;
            case "animationEnabled": option.AnimationEnabled = null; break;
            case "animationDuration": option.AnimationDuration = null; break;
            case "animationEasing": option.AnimationEasing = null; break;
            case "animationDelay": option.AnimationDelay = null; break;
        }
    }

    /// <summary>
    /// Applies the consumer's optional per-chart animation override onto the option.
    /// Previously duplicated verbatim (<c>if (AnimationDuration.HasValue) ...</c>) across
    /// every one of the 30 chart wrappers — a single design decision copy-pasted 28+
    /// times. When neither is set the shared Lumeo ECharts theme's own
    /// animationDuration/animationEasing defaults apply instead (see
    /// echarts-interop.js's buildLumeoTheme), which is why this only sets the
    /// properties when the consumer explicitly overrides them.
    /// </summary>
    public static void ApplyAnimation(EChartOption option, int? duration, string? easing)
    {
        if (duration.HasValue) option.AnimationDuration = duration;
        if (!string.IsNullOrEmpty(easing)) option.AnimationEasing = easing;
    }

    // --- Design-pass form helpers (decal + line differentiation) --------------------
    // See the charts-design PR description for the full reasoning. Both exist because
    // the owner kept shadcn's chart palette unchanged (chart-1 is achromatic, chart-4/
    // chart-5 sit only ~29° apart in hue) — colour alone can no longer carry "which
    // series/slice is this", so these give bar/pie/line series a colour-independent,
    // colour-blind-safe FORM signature instead.

    /// <summary>
    /// Five decal (pattern-fill) recipes, one per <c>--color-chart-N</c> palette slot —
    /// deliberately different SHAPE FAMILIES (diagonal stripes / dots / fine grid /
    /// diamonds / triangles), not just rotated or rescaled variants of each other, so
    /// the difference still reads once a screenshot is converted to greyscale (verified
    /// against a rendered screenshot during the charts-design pass — see PR description).
    /// Colour is always <c>var(--color-border)</c> — a token, per the "no raw hex" rule —
    /// left for the chart interop's existing <c>resolveCssVars</c> pass to resolve, same
    /// as every other <c>var(...)</c> reference already flowing through a chart option.
    /// </summary>
    private static readonly Func<EChartDecal>[] DecalRecipes =
    {
        () => new EChartDecal { Symbol = "rect", DashArrayX = new object[] { 1, 0 }, DashArrayY = new object[] { 4, 4 }, Rotation = Math.PI / 4, SymbolSize = 1, Color = "var(--color-border)" },
        () => new EChartDecal { Symbol = "circle", DashArrayX = new object[] { new object[] { 8, 4 } }, DashArrayY = new object[] { 8, 4 }, SymbolSize = 0.6, Color = "var(--color-border)" },
        () => new EChartDecal { Symbol = "rect", DashArrayX = new object[] { 3, 3 }, DashArrayY = new object[] { 3, 3 }, SymbolSize = 0.5, Color = "var(--color-border)" },
        () => new EChartDecal { Symbol = "diamond", DashArrayX = new object[] { new object[] { 10, 6 } }, DashArrayY = new object[] { 10, 4 }, SymbolSize = 0.7, Color = "var(--color-border)" },
        () => new EChartDecal { Symbol = "triangle", DashArrayX = new object[] { new object[] { 10, 6 } }, DashArrayY = new object[] { 10, 6 }, Rotation = Math.PI, SymbolSize = 0.6, Color = "var(--color-border)" },
    };

    /// <summary>
    /// Item/series count beyond which decal is skipped entirely for a chart. Above this,
    /// the 5 recipes just repeat (same cycle length as the 5-colour palette, so nothing
    /// NEW is communicated past the 5th item) while making already-thin pie slivers or
    /// a crowded bar cluster busier to read — the "cardinality problem" a previous pass
    /// declined decal over. Scoping to a bounded count, rather than turning decal on
    /// theme-wide, is what makes it safe to turn on at all.
    /// </summary>
    public const int MaxDecalItems = 8;

    /// <summary>
    /// Returns the decal recipe for <paramref name="index"/> (wraps every 5, same period
    /// as the colour palette), or <c>null</c> when there's nothing to differentiate
    /// (<paramref name="totalCount"/> &lt; 2) or too much to differentiate usefully
    /// (&gt; <see cref="MaxDecalItems"/>). Callers (BarChart for multi-series groups,
    /// PieChart/DonutChart for slices) are expected to skip assigning <c>ItemStyle.Decal</c>
    /// entirely when this returns null, rather than assign a "no pattern" decal.
    /// </summary>
    public static EChartDecal? GetDecal(int index, int totalCount)
    {
        if (totalCount < 2 || totalCount > MaxDecalItems) return null;
        return DecalRecipes[index % DecalRecipes.Length]();
    }

    private static readonly string[] LineSymbols = { "circle", "diamond", "triangle", "rect", "roundRect" };

    // null = solid (theme default; left unset so the theme's own lineStyle merges
    // through untouched). Only the 2nd/3rd-in-cycle series get an explicit dash — see
    // ApplyLineForm.
    private static readonly string?[] LineDashTypes = { null, "dashed", "dotted" };

    /// <summary>
    /// Cycles marker symbol + stroke dash + stroke weight across a line/area series so a
    /// 2nd or 3rd series reads apart from the 1st by FORM alone, not just colour — the
    /// symbol cycle in particular does NOT happen by default in ECharts (confirmed via a
    /// live probe: every series defaults to a uniform <c>emptyCircle</c>, unlike the
    /// colour palette which does cycle — see PR description). Series 0 additionally gets
    /// a deliberately heavier stroke (3 vs the theme's default 2): it's the series most
    /// often alone in the chart AND the one that inherits the achromatic
    /// <c>--color-chart-1</c> token, so a bolder line asserts "this is the primary
    /// series" on weight rather than a hue it doesn't reliably have.
    /// </summary>
    public static void ApplyLineForm(EChartSeries series, int index)
    {
        series.Symbol = LineSymbols[index % LineSymbols.Length];
        var dash = LineDashTypes[index % LineDashTypes.Length];
        var width = index == 0 ? 3 : 2;
        if (dash is not null || width != 2)
        {
            series.LineStyle = new EChartLineStyle { Type = dash, Width = width };
        }
    }

    // --- Bar geometry pass: sign-aware corner rounding -------------------------
    // See the charts-design geometry-gap pass PR description. Bar-only for now:
    // Stacked bars aren't covered (see BuildBarData doc below).

    /// <summary>
    /// A single ECharts bar-series data point, `{ value, itemStyle }` — the object
    /// form ECharts accepts in a series' <c>data</c> array when a datapoint needs
    /// its OWN style override instead of inheriting the series-level one. Kept as a
    /// small typed class (rather than an anonymous object) so the JSON property
    /// names are guaranteed correct regardless of the caller's C# naming.
    /// </summary>
    internal sealed class BarDataPoint
    {
        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("itemStyle")]
        public EChartItemStyle? ItemStyle { get; set; }
    }

    /// <summary>
    /// Flips a corner-radius array for a negative datapoint. <paramref name="forwardCorners"/>
    /// is the array the caller already computed for a NON-negative value — the
    /// "rounded end grows away from the zero baseline in the normal direction" case
    /// (e.g. <c>[r,r,0,0]</c> for a vertical bar rounding its top, <c>[0,r,r,0]</c>
    /// for a horizontal bar rounding its right/leading end). A negative value's
    /// rect extends the OPPOSITE way from the baseline — its own far tip is the
    /// opposite pair of corners — so the array must rotate 180°
    /// (<c>[topLeft,topRight,bottomRight,bottomLeft]</c> -> <c>[bottomRight,
    /// bottomLeft,topLeft,topRight]</c>) or the "rounded" end would sit at the zero
    /// baseline (touching the axis / the next bar) instead of at the bar's own tip.
    /// Returns <paramref name="forwardCorners"/> unchanged for non-negative values.
    /// </summary>
    public static object[] BarCorners(object[] forwardCorners, double value)
    {
        if (value >= 0 || forwardCorners.Length != 4) return forwardCorners;
        return new[] { forwardCorners[2], forwardCorners[3], forwardCorners[0], forwardCorners[1] };
    }

    /// <summary>
    /// Builds a bar series' <c>Data</c> payload, applying <see cref="BarCorners"/>
    /// per point when (and only when) <paramref name="values"/> contains a negative
    /// value — a uniform series-level <c>ItemStyle.BorderRadiusCorners</c> always
    /// rounds the SAME end regardless of a point's own sign, which is backwards for
    /// any point that hangs below (or left of, when horizontal) the zero baseline.
    /// For an all-non-negative series — the overwhelming common case — this returns
    /// the flat <c>List&lt;double&gt;</c> unchanged: same wire shape as before this
    /// method existed, so nothing changes for existing all-positive datasets.
    ///
    /// Scoped to the two NON-stacked cases (grouped vertical, single-series
    /// horizontal) — see BarChart.razor. Stacked bars aren't covered: identifying
    /// which datapoint is the "outermost of its own sign's stack" varies PER
    /// CATEGORY (a category can have a positive sub-stack and a negative sub-stack
    /// growing from the same zero baseline), which is a materially harder problem
    /// than the brief's callout and out of scope for this pass.
    /// </summary>
    public static object BuildBarData(List<double> values, object[] forwardCorners)
    {
        if (!values.Exists(v => v < 0)) return values;
        return values.ConvertAll(v => (object)new BarDataPoint
        {
            Value = v,
            ItemStyle = new EChartItemStyle { BorderRadiusCorners = BarCorners(forwardCorners, v) }
        });
    }
}
