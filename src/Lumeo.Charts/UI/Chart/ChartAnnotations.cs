using System.Text.Json.Nodes;

namespace Lumeo;

/// <summary>
/// One <see cref="ChartThreshold"/>'s configuration. Translates to a single entry in the
/// chart's <c>series[0].markLine.data</c> array (ECharts native annotation).
/// </summary>
public sealed record ChartThresholdInfo(
    string Id,
    /// <summary>Value on the perpendicular axis (yAxis for horizontal threshold, xAxis for
    /// vertical). Numbers are emitted as-is; strings address category-axis points.</summary>
    object Value,
    ChartThresholdAxis Axis,
    string? Label,
    /// <summary>CSS / hex color for the line. When null ECharts uses the series' colour.</summary>
    string? Color,
    /// <summary>"solid", "dashed", or "dotted". Defaults to "dashed" for visual distinction.</summary>
    string LineStyle);

/// <summary>Which axis a threshold is perpendicular to. Horizontal = a yAxis line spanning
/// the chart width; Vertical = an xAxis line spanning the chart height.</summary>
public enum ChartThresholdAxis
{
    Horizontal,
    Vertical
}

/// <summary>
/// One <see cref="ChartReferenceZone"/>'s configuration. Translates to a single entry in
/// the chart's <c>series[0].markArea.data</c> array — a pair of endpoints describing the
/// rectangle (or band) ECharts should shade.
/// </summary>
public sealed record ChartReferenceZoneInfo(
    string Id,
    object From,
    object To,
    ChartThresholdAxis Axis,
    string? Label,
    /// <summary>Fill colour (typically a low-alpha hex like <c>"#22c55e22"</c>). Required —
    /// without it the zone would be invisible.</summary>
    string Color);

/// <summary>
/// Cascaded by <see cref="Chart"/> to its declared <see cref="ChartThreshold"/> and
/// <see cref="ChartReferenceZone"/> children. Children register themselves on
/// <c>OnInitialized</c> and unregister on dispose; the chart picks up the lists in its
/// JSON build phase and merges them into <c>series[0].markLine</c> / <c>markArea</c>
/// without mutating the consumer-passed <see cref="EChartOption"/>.
/// </summary>
public sealed class ChartAnnotationsContext
{
    private readonly Action _onChanged;
    private readonly Dictionary<string, ChartThresholdInfo> _thresholds = new();
    private readonly Dictionary<string, ChartReferenceZoneInfo> _zones = new();

    public ChartAnnotationsContext(Action onChanged) => _onChanged = onChanged;

    public IReadOnlyCollection<ChartThresholdInfo> Thresholds => _thresholds.Values;
    public IReadOnlyCollection<ChartReferenceZoneInfo> Zones => _zones.Values;

    public void RegisterThreshold(ChartThresholdInfo info)
    {
        if (_thresholds.TryGetValue(info.Id, out var existing) && existing == info) return;
        _thresholds[info.Id] = info;
        _onChanged();
    }

    public void UnregisterThreshold(string id)
    {
        if (_thresholds.Remove(id)) _onChanged();
    }

    public void RegisterZone(ChartReferenceZoneInfo info)
    {
        if (_zones.TryGetValue(info.Id, out var existing) && existing == info) return;
        _zones[info.Id] = info;
        _onChanged();
    }

    public void UnregisterZone(string id)
    {
        if (_zones.Remove(id)) _onChanged();
    }
}

/// <summary>
/// Pure JSON-merge helper for the declarative annotation children. Splitting this out
/// of <c>Chart.razor</c> keeps the merge logic unit-testable without spinning up the
/// full chart + JS interop and lets <see cref="Chart"/> stay a thin orchestration layer.
/// </summary>
public static class ChartAnnotationMerger
{
    /// <summary>
    /// Merge cascaded <see cref="ChartThresholdInfo"/> / <see cref="ChartReferenceZoneInfo"/>
    /// registrations into <c>series[0].markLine.data</c> / <c>markArea.data</c> on a chart
    /// option JSON string. Operates on <c>JsonNode</c> so we never mutate the consumer's
    /// <see cref="EChartOption"/>. No-op when there are no registrations or no series.
    /// </summary>
    public static string Merge(
        string baseJson,
        IReadOnlyCollection<ChartThresholdInfo> thresholds,
        IReadOnlyCollection<ChartReferenceZoneInfo> zones)
    {
        if (thresholds.Count == 0 && zones.Count == 0) return baseJson;
        try
        {
            var root = JsonNode.Parse(baseJson);
            if (root is not JsonObject obj) return baseJson;
            if (obj["series"] is not JsonArray series || series.Count == 0) return baseJson;
            if (series[0] is not JsonObject targetSeries) return baseJson;

            if (thresholds.Count > 0)
            {
                var markLine = EnsureObject(targetSeries, "markLine");
                var data = EnsureArray(markLine, "data");
                foreach (var t in thresholds) data.Add(BuildMarkLineEntry(t));
            }

            if (zones.Count > 0)
            {
                var markArea = EnsureObject(targetSeries, "markArea");
                var data = EnsureArray(markArea, "data");
                foreach (var z in zones) data.Add(BuildMarkAreaEntry(z));
            }

            return root.ToJsonString();
        }
        catch
        {
            // Annotation merge must never break the chart — fall through to the
            // unmodified base JSON so the data still renders.
            return baseJson;
        }
    }

    private static JsonObject EnsureObject(JsonObject parent, string key)
    {
        if (parent[key] is JsonObject existing) return existing;
        var created = new JsonObject();
        parent[key] = created;
        return created;
    }

    private static JsonArray EnsureArray(JsonObject parent, string key)
    {
        if (parent[key] is JsonArray existing) return existing;
        var created = new JsonArray();
        parent[key] = created;
        return created;
    }

    private static JsonObject BuildMarkLineEntry(ChartThresholdInfo t)
    {
        var entry = new JsonObject();
        var axisKey = t.Axis == ChartThresholdAxis.Horizontal ? "yAxis" : "xAxis";
        entry[axisKey] = ValueToNode(t.Value);
        if (!string.IsNullOrEmpty(t.Label)) entry["name"] = t.Label;
        var lineStyle = new JsonObject();
        if (!string.IsNullOrEmpty(t.Color)) lineStyle["color"] = t.Color;
        lineStyle["type"] = t.LineStyle;
        entry["lineStyle"] = lineStyle;
        if (!string.IsNullOrEmpty(t.Label))
        {
            entry["label"] = new JsonObject
            {
                ["formatter"] = t.Label,
                ["position"] = "insideEndTop"
            };
        }
        return entry;
    }

    private static JsonArray BuildMarkAreaEntry(ChartReferenceZoneInfo z)
    {
        var axisKey = z.Axis == ChartThresholdAxis.Horizontal ? "yAxis" : "xAxis";
        var fromEnd = new JsonObject();
        fromEnd[axisKey] = ValueToNode(z.From);
        if (!string.IsNullOrEmpty(z.Label)) fromEnd["name"] = z.Label;
        fromEnd["itemStyle"] = new JsonObject { ["color"] = z.Color };
        var toEnd = new JsonObject();
        toEnd[axisKey] = ValueToNode(z.To);
        return new JsonArray { fromEnd, toEnd };
    }

    private static JsonNode? ValueToNode(object value) => value switch
    {
        null => null,
        string s => JsonValue.Create(s),
        int i => JsonValue.Create(i),
        long l => JsonValue.Create(l),
        double d => JsonValue.Create(d),
        float f => JsonValue.Create(f),
        decimal m => JsonValue.Create(m),
        bool b => JsonValue.Create(b),
        _ => JsonValue.Create(value.ToString())
    };
}
