namespace Lumeo;

/// <summary>
/// Per-series color resolution shared by every native Cartesian type — same
/// precedence as the legacy wrappers' <c>ColorPalette ?? Colors</c> pattern,
/// falling back to the theme's cycling <c>var(--color-chart-1..5)</c> tokens
/// (CSS-variable-native, never resolved to a concrete hex — spec §2.5) instead
/// of ECharts' own internal default palette.
/// </summary>
internal static class NativeChartPalette
{
    private const int ThemeTokenCount = 5;

    public static string Resolve(IReadOnlyList<string>? colorPalette, IReadOnlyList<string>? colors, int seriesIndex)
    {
        var explicitPalette = colorPalette is { Count: > 0 } ? colorPalette : colors;
        if (explicitPalette is { Count: > 0 })
            return explicitPalette[seriesIndex % explicitPalette.Count];

        var baseToken = $"var(--color-chart-{(seriesIndex % ThemeTokenCount) + 1})";
        var round = seriesIndex / ThemeTokenCount;
        if (round == 0) return baseToken;

        // A 6th+ series (no explicit palette given) cycles back through the
        // same 5 theme tokens — repeating an EXACT color makes two unrelated
        // series read as one in the legend/tooltip. Vary each further "round"
        // through color-mix (still CSS-variable-native, no raw hex — project
        // rule): odd rounds lighten toward white, even rounds darken toward
        // black, alternating so round 1 and round 2 aren't just "round 0
        // again but lighter than round 1", they're distinguishable from each
        // other too.
        return round % 2 == 1
            ? $"color-mix(in oklab, {baseToken} 68%, white)"
            : $"color-mix(in oklab, {baseToken} 68%, black)";
    }
}
