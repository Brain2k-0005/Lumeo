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
}
