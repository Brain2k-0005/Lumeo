using System.Linq;

namespace Lumeo;

/// <summary>A color scale stop: a data value paired with any valid CSS color
/// (including a <c>var(--token)</c> reference).</summary>
internal readonly record struct ChartColorStop(double Value, string Color);

/// <summary>
/// CSS-variable-native value→color scale (spec §2.3/§2.5's "shared with
/// CalendarHeatmap/GeoMap's visualMap" primitive). Rather than resolving to a
/// concrete interpolated hex in C#, it emits a live <c>color-mix()</c> CSS
/// expression between the two bracketing stop colors — so a heatmap cell colored
/// this way keeps repainting correctly across theme swaps (dark mode, custom
/// themes) with zero JS and zero runtime resolution, matching the project's
/// "CSS variables only" rule and the engine's export-time-only color resolution
/// policy (spec §2.5).
/// </summary>
internal static class ChartColorScale
{
    /// <summary>
    /// Resolves <paramref name="value"/> against <paramref name="stops"/>
    /// (need not be pre-sorted). Values outside the stop range clamp to the
    /// nearest end color. A value exactly on a stop returns that stop's color
    /// literally, without a <c>color-mix()</c> wrapper.
    /// </summary>
    public static string Resolve(IReadOnlyList<ChartColorStop> stops, double value, string colorSpace = "oklab")
    {
        if (stops.Count == 0)
            throw new ArgumentException("ChartColorScale requires at least one stop.", nameof(stops));
        if (stops.Count == 1) return stops[0].Color;

        var sorted = stops.OrderBy(s => s.Value).ToList();
        if (value <= sorted[0].Value) return sorted[0].Color;
        if (value >= sorted[^1].Value) return sorted[^1].Color;

        for (var i = 0; i < sorted.Count - 1; i++)
        {
            var lo = sorted[i];
            var hi = sorted[i + 1];
            if (value < lo.Value || value > hi.Value) continue;

            var span = hi.Value - lo.Value;
            var t = span == 0 ? 0 : (value - lo.Value) / span;
            if (t <= 0) return lo.Color;
            if (t >= 1) return hi.Color;

            var hiPct = Math.Round(t * 100, 2);
            var loPct = Math.Round(100 - hiPct, 2);
            return string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"color-mix(in {colorSpace}, {hi.Color} {hiPct}%, {lo.Color} {loPct}%)");
        }

        return sorted[^1].Color;
    }
}
