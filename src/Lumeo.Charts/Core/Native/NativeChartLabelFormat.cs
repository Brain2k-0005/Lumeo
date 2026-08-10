using System.Globalization;

namespace Lumeo;

/// <summary>
/// Minimal <c>{c}</c>-token value-label formatter — the small subset of
/// ECharts' label <c>formatter</c> template syntax the legacy wrappers'
/// <c>LabelFormat</c> parameter actually documents (e.g. <c>"{c}%"</c>).
/// A full ECharts formatter (functions, <c>{a}</c>/<c>{b}</c>/percent
/// tokens) has no native equivalent and isn't attempted here — only the one
/// token every existing <c>LabelFormat</c> usage in this codebase's own docs
/// examples relies on.
/// </summary>
internal static class NativeChartLabelFormat
{
    public static string Format(double value, string? template)
    {
        var formatted = value.ToString("0.##", CultureInfo.InvariantCulture);
        if (string.IsNullOrEmpty(template)) return formatted;
        return template.Replace("{c}", formatted);
    }
}
