using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lumeo;

/// <summary>
/// Builds the generic <c>{series:[...], xAxis:[...]}</c> JSON shape
/// <see cref="ChartAccessibility.Build"/> already parses — so the native
/// engine reuses that class AS-IS (per the task's explicit instruction: it's
/// already ECharts-independent, already unit-tested) without needing
/// <c>EChartOption</c> at all. <see cref="ChartAccessibility"/> only ever reads
/// generic <c>series[].name</c>/<c>type</c>/<c>data</c> and
/// <c>xAxis[0].data</c> — this type emits exactly that shape from
/// <see cref="NativeCartesianSeries"/> / raw x/y point data.
/// </summary>
internal static class NativeChartAccessibilityJson
{
    /// <summary>Category-based shape (Line/Area/Bar/Mixed/Waterfall).</summary>
    public static string BuildCartesian(
        IReadOnlyList<string> categories, IReadOnlyList<NativeCartesianSeries> series, string echartsTypeLabel)
    {
        var root = new JsonObject
        {
            ["xAxis"] = new JsonArray { new JsonObject { ["data"] = ToArray(categories) } },
            ["series"] = new JsonArray(series.Where(s => s.IncludeInTooltip).Select(s => (JsonNode)new JsonObject
            {
                ["name"] = s.Name,
                ["type"] = echartsTypeLabel,
                ["data"] = new JsonArray((s.DisplayValues ?? s.Values).Select(v => (JsonNode?)(v.HasValue ? JsonValue.Create(v.Value) : null)).ToArray()),
            }).ToArray()),
        };
        return root.ToJsonString();
    }

    /// <summary>Continuous x/y shape (Scatter/EffectScatter) — no category
    /// axis, so <see cref="ChartAccessibility.Build"/> falls into its
    /// "categorical" (Name/Value or Series/Item/Value) table form, exactly
    /// like the legacy ECharts scatter option does today (it never sets a
    /// category xAxis either).</summary>
    public static string BuildXy(IReadOnlyList<(string Name, IReadOnlyList<(double X, double Y)> Points)> series, string echartsTypeLabel)
    {
        var root = new JsonObject
        {
            ["series"] = new JsonArray(series.Select(s => (JsonNode)new JsonObject
            {
                ["name"] = s.Name,
                ["type"] = echartsTypeLabel,
                ["data"] = new JsonArray(s.Points.Select(p => (JsonNode)new JsonArray(JsonValue.Create(p.X), JsonValue.Create(p.Y))).ToArray()),
            }).ToArray()),
        };
        return root.ToJsonString();
    }

    private static JsonArray ToArray(IReadOnlyList<string> values) =>
        new(values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray());
}
