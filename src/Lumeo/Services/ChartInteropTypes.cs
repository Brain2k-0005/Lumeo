namespace Lumeo.Services;

/// <summary>
/// One batched text-measurement request for
/// <see cref="IComponentInteropService.ChartMeasureTextWidths"/> — the charting
/// engine's single required text-metrics JS call (a font/label pair per
/// request, not one shared font for the whole batch, so a single round trip
/// can cover both uniform axis-label fonts AND per-item varying fonts, e.g. a
/// word cloud sizing each word by weight).
/// </summary>
public record ChartTextMeasureRequest(string Font, string Text)
{
    // Trim safety: see ElementRect's parameterless ctor. Do not remove.
    public ChartTextMeasureRequest() : this(string.Empty, string.Empty) { }
}
