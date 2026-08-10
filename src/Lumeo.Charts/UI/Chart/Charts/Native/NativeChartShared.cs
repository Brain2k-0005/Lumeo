using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Lumeo;

/// <summary>
/// Small helpers shared by the native (first-party, non-ECharts) proportional /
/// distribution / radial chart types built directly against the charts-core
/// primitives (<c>Core/</c>, <c>Interaction/</c>, <c>Rendering/</c>). This is new
/// code written FOR this wave of chart types — it is not part of the core and
/// does not modify it.
/// </summary>
internal static class NativeChartShared
{
    private const int PaletteSize = 5;

    /// <summary>
    /// Resolves the color for series/segment <paramref name="index"/>: an
    /// explicit <paramref name="colorPalette"/> entry first, then a
    /// <paramref name="colors"/> entry, then the theme's cycling
    /// <c>--color-chart-N</c> CSS variable (1-based, 5-color cycle) — the same
    /// <c>ColorPalette ?? Colors</c> precedence the legacy ECharts wrappers use,
    /// falling back to a CSS-variable-native default instead of resolving to a
    /// concrete hex (project rule: CSS variables only, never raw hex).
    /// </summary>
    public static string ColorFor(int index, IReadOnlyList<string>? colorPalette, IReadOnlyList<string>? colors)
    {
        if (colorPalette is { Count: > 0 }) return colorPalette[index % colorPalette.Count];
        if (colors is { Count: > 0 }) return colors[index % colors.Count];
        return $"var(--color-chart-{index % PaletteSize + 1})";
    }

    /// <summary>Formats a coordinate/geometry value for an SVG attribute.</summary>
    public static string Fmt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Formats a data value for display (labels, tooltips, SR table).</summary>
    public static string FmtValue(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Expands a legacy ECharts-style label formatter template's three most
    /// common tokens — <c>{b}</c> (name), <c>{c}</c> (value), <c>{d}</c>
    /// (percent, already formatted, caller appends a literal <c>%</c> if wanted)
    /// — so consumers migrating a <c>LabelFormat</c> string like
    /// <c>"{b}: {d}%"</c> from a typed wrapper keep working unchanged.
    /// </summary>
    public static string ExpandLabelFormat(string template, string name, double value, double percent) =>
        template.Replace("{b}", name).Replace("{c}", FmtValue(value)).Replace("{d}", FmtValue(percent));

    /// <summary>
    /// Builds the minimal generic-option JSON shape <see cref="ChartAccessibility.Build"/>
    /// already parses for a single-series categorical chart (Pie/Donut/Nightingale) —
    /// reusing the already-ECharts-independent, already-unit-tested SR-table
    /// projection instead of re-implementing it (spec §3.6 tier 1 / §7.3).
    /// </summary>
    public static string BuildCategoricalOptionJson(string type, IReadOnlyList<(string Name, double Value)> items)
    {
        var data = new JsonArray();
        foreach (var item in items)
            data.Add(new JsonObject { ["name"] = item.Name, ["value"] = item.Value });

        var series = new JsonObject { ["type"] = type, ["data"] = data };
        var root = new JsonObject { ["series"] = new JsonArray(series) };
        return root.ToJsonString();
    }

    /// <summary>
    /// Linearly interpolates two <c>#RRGGBB</c> (or <c>#RGB</c>) hex colors in
    /// sRGB space at fraction <paramref name="t"/> (0..1). Used ONLY on the
    /// Canvas-fallback paint path (spec §2.5: "the one place resolved-hex-at-
    /// paint-time is still needed continuously") — the SVG path never resolves
    /// to hex at all, it emits a live <c>color-mix()</c> expression via
    /// <see cref="ChartColorScale"/> instead. A plain RGB lerp is a documented,
    /// lower-fidelity stand-in for that oklab-based mix on the Canvas path,
    /// where perceptual accuracy is traded for paint-command simplicity.
    /// Malformed input returns <paramref name="from"/> unchanged rather than
    /// throwing, since Canvas fallback must never crash a render.
    /// </summary>
    public static string LerpHex(string from, string to, double t)
    {
        if (!TryParseHex(from, out var r1, out var g1, out var b1)) return from;
        if (!TryParseHex(to, out var r2, out var g2, out var b2)) return from;
        t = Math.Clamp(t, 0, 1);
        var r = (int)Math.Round(r1 + (r2 - r1) * t);
        var g = (int)Math.Round(g1 + (g2 - g1) * t);
        var b = (int)Math.Round(b1 + (b2 - b1) * t);
        return $"#{r:x2}{g:x2}{b:x2}";
    }

    private static bool TryParseHex(string hex, out int r, out int g, out int b)
    {
        r = g = b = 0;
        if (string.IsNullOrEmpty(hex) || hex[0] != '#') return false;
        var h = hex[1..];
        try
        {
            if (h.Length == 3)
            {
                r = Convert.ToInt32(new string(h[0], 2), 16);
                g = Convert.ToInt32(new string(h[1], 2), 16);
                b = Convert.ToInt32(new string(h[2], 2), 16);
                return true;
            }
            if (h.Length >= 6)
            {
                r = Convert.ToInt32(h[..2], 16);
                g = Convert.ToInt32(h[2..4], 16);
                b = Convert.ToInt32(h[4..6], 16);
                return true;
            }
        }
        catch (FormatException) { }
        return false;
    }

    /// <summary>
    /// Builds the minimal generic-option JSON shape for a cartesian-shaped
    /// (categories × N series) chart — Radar (axes × series), BoxPlot
    /// (categories × five-number-summary "series"), Candlestick (categories ×
    /// OHLC "series") — so <see cref="ChartAccessibility.Build"/> projects a
    /// proper category/series SR table for these types too.
    /// </summary>
    public static string BuildCartesianOptionJson(
        string type, IReadOnlyList<string> categories, IReadOnlyList<(string Name, IReadOnlyList<double> Values)> series)
    {
        var categoryData = new JsonArray();
        foreach (var c in categories) categoryData.Add(c);

        var seriesArray = new JsonArray();
        foreach (var s in series)
        {
            var values = new JsonArray();
            foreach (var v in s.Values) values.Add(v);
            seriesArray.Add(new JsonObject { ["type"] = type, ["name"] = s.Name, ["data"] = values });
        }

        var root = new JsonObject
        {
            ["xAxis"] = new JsonObject { ["data"] = categoryData },
            ["series"] = seriesArray,
        };
        return root.ToJsonString();
    }
}
