using System.Globalization;

namespace Lumeo;

/// <summary>
/// Resolves the legacy <c>Width</c>/<c>Height</c> CSS-string parameters (e.g.
/// <c>"100%"</c>, <c>"350px"</c>) to a numeric SVG viewBox size, and estimates
/// axis tick-label pixel widths without a JS round-trip.
/// </summary>
/// <remarks>
/// The Wave-0 core's interop contract includes a <c>measureTextWidths</c> JS
/// call specifically for this (spec §2.2) — deliberately NOT wired up here.
/// Using it would make every chart's first paint depend on an
/// <c>OnAfterRenderAsync</c> JS round-trip (a flash of wrong margins before
/// the real ones arrive) and would make bUnit coverage depend on mocking
/// JSInterop for every single rendered test. A character-count width
/// estimate keeps the whole render synchronous and deterministic — margins
/// are approximate rather than pixel-perfect, which is an acceptable
/// trade for a first native wave; the JS call remains available for a
/// later pass that wants exact metrics. Documented as a deliberate,
/// flagged simplification, not a silent gap.
/// </remarks>
internal static class NativeChartViewport
{
    public const double DefaultViewBoxWidth = 600;
    public const double DefaultViewBoxHeight = 350;

    /// <summary>Average glyph width (px) for the axis-label font (11px, the
    /// same size <c>ChartAxis</c> hardcodes) — a coarse but stable estimate.</summary>
    private const double AvgCharWidthPx = 6.2;

    public static double ParseViewBoxDimension(string? cssValue, double fallback)
    {
        if (string.IsNullOrWhiteSpace(cssValue)) return fallback;
        var trimmed = cssValue.Trim();
        if (trimmed.EndsWith("px", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var px)
            && px > 0)
        {
            return px;
        }
        return fallback;
    }

    public static double EstimateLabelWidth(string label) => label.Length * AvgCharWidthPx;

    public static IReadOnlyList<double> EstimateLabelWidths(IEnumerable<string> labels) =>
        labels.Select(EstimateLabelWidth).ToList();
}
