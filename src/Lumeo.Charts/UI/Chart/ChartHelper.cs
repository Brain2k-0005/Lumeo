using System.Text.Json;

namespace Lumeo;

/// <summary>
/// Shared helpers for chart convenience components.
/// </summary>
internal static class ChartHelper
{
    /// <summary>
    /// Merges OptionOverride entries into the option's ExtensionData.
    /// </summary>
    public static void ApplyOptionOverride(EChartOption option, Dictionary<string, object>? overrides)
    {
        if (overrides is null || overrides.Count == 0) return;

        option.ExtensionData ??= new Dictionary<string, JsonElement>();
        foreach (var kvp in overrides)
        {
            var json = JsonSerializer.Serialize(kvp.Value);
            var element = JsonSerializer.Deserialize<JsonElement>(json);
            option.ExtensionData[kvp.Key] = element;
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
}
