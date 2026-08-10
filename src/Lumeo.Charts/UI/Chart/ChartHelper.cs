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
}
