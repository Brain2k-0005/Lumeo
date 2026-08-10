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
        return $"var(--color-chart-{(seriesIndex % ThemeTokenCount) + 1})";
    }
}
