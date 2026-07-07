using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Lumeo;

/// <summary>
/// Builds a screen-reader-friendly data model from an ECharts option so the
/// canvas-rendered <see cref="Chart"/> can expose an accessible fallback — the
/// equivalent of shadcn/ui's <c>accessibilityLayer</c> for Recharts.
/// <para>
/// ECharts offers a native <c>aria.enabled</c> flag that emits a single verbose
/// <c>aria-label</c> string once the chart has initialised in JS. We deliberately
/// render our own visually-hidden <c>&lt;table&gt;</c> from the SAME series data
/// instead, because a real table is (a) navigable cell-by-cell by assistive tech
/// (row/column semantics, not one long sentence), (b) present in the DOM
/// server-side / pre-render before any JS runs, and (c) deterministic and unit
/// testable. The generated <see cref="ChartDataTable.Summary"/> doubles as the
/// focusable host's <c>aria-label</c>.
/// </para>
/// </summary>
public static class ChartAccessibility
{
    /// <summary>
    /// Default upper bound on the number of data rows the screen-reader table
    /// materialises. A big series (thousands of points) would otherwise emit one
    /// hidden <c>&lt;tr&gt;</c> per point — heavy DOM bloat that helps no assistive
    /// tech (a SR user cannot meaningfully tab through 5000 rows). Above the cap the
    /// table keeps the first <see cref="DefaultMaxAccessibilityRows"/> rows plus a
    /// single "… and N more data points" summary row; the caption / aria-label
    /// summary is computed from the FULL data so its totals stay accurate.
    /// </summary>
    public const int DefaultMaxAccessibilityRows = 50;

    /// <summary>A screen-reader data table projected from an ECharts option.</summary>
    /// <param name="Summary">One-line description used as the chart's <c>aria-label</c>.</param>
    /// <param name="ColumnHeaders">Header cells for the table's <c>&lt;thead&gt;</c>.
    /// The first entry labels the row-header column.</param>
    /// <param name="Rows">One row per category (cartesian) or data point, capped at
    /// the requested row limit.</param>
    /// <param name="TruncationNote">Non-null when <see cref="Rows"/> was capped: a
    /// human-readable "… and N more data points" note the host renders as a final
    /// spanning row. Null when the full data fit under the cap.</param>
    public sealed record ChartDataTable(
        string Summary,
        IReadOnlyList<string> ColumnHeaders,
        IReadOnlyList<ChartDataRow> Rows,
        string? TruncationNote = null);

    /// <summary>A single table row: a row-header cell plus its value cells.</summary>
    public sealed record ChartDataRow(string Header, IReadOnlyList<string> Cells);

    /// <summary>
    /// Projects the given ECharts option JSON into a <see cref="ChartDataTable"/>,
    /// or <c>null</c> when the option carries no usable series data (in which case
    /// the chart renders no accessibility table). Never throws — malformed JSON
    /// yields <c>null</c>.
    /// </summary>
    /// <param name="optionJson">The ECharts option JSON.</param>
    /// <param name="maxRows">Upper bound on materialised data rows (default
    /// <see cref="DefaultMaxAccessibilityRows"/>). <c>0</c> or negative means
    /// unlimited. When the projected data exceeds the cap the table keeps the first
    /// <paramref name="maxRows"/> rows and sets
    /// <see cref="ChartDataTable.TruncationNote"/>; the caption/summary totals are
    /// always computed from the complete data, so they remain accurate.</param>
    public static ChartDataTable? Build(string? optionJson, int maxRows = DefaultMaxAccessibilityRows)
    {
        if (string.IsNullOrWhiteSpace(optionJson))
            return null;

        JsonNode? root;
        try { root = JsonNode.Parse(optionJson); }
        catch { return null; }

        if (root is not JsonObject obj)
            return null;

        var series = ReadSeries(obj);
        if (series.Count == 0)
            return null;

        var categories = ReadCategories(obj);
        var typeLabel = ChartTypeLabel(series[0].Type);

        var table = categories.Count > 0
            ? BuildCartesian(typeLabel, categories, series)
            : BuildCategorical(typeLabel, series);

        return CapRows(table, maxRows);
    }

    // Caps the row list so a huge series doesn't emit thousands of hidden <tr>s.
    // The Summary was built from the FULL data (category / point counts), so it is
    // untouched — only the materialised Rows shrink, with the overflow announced by
    // a single TruncationNote row. maxRows <= 0 disables the cap entirely.
    private static ChartDataTable CapRows(ChartDataTable table, int maxRows)
    {
        if (maxRows <= 0 || table.Rows.Count <= maxRows)
            return table;

        var omitted = table.Rows.Count - maxRows;
        var kept = table.Rows.Take(maxRows).ToList();
        var note = $"… and {omitted} more data point{(omitted == 1 ? "" : "s")}";
        return table with { Rows = kept, TruncationNote = note };
    }

    private readonly record struct SeriesInfo(string Name, string? Type, IReadOnlyList<JsonNode?> Data);

    private static List<SeriesInfo> ReadSeries(JsonObject obj)
    {
        var result = new List<SeriesInfo>();

        // ECharts accepts "series" as EITHER an array OR a single object
        // ("series": { "type": "pie", "data": [...] }). Treat the object form as a
        // one-element sequence so the SR table + caption are built for it too —
        // previously the object shape yielded nothing and the a11y layer silently
        // vanished (round-9 finding).
        IEnumerable<JsonNode?> nodes = obj["series"] switch
        {
            JsonArray arr => arr,
            JsonObject single => new JsonNode?[] { single },
            _ => Array.Empty<JsonNode?>(),
        };

        var index = 0;
        foreach (var node in nodes)
        {
            index++;
            if (node is not JsonObject s)
                continue;
            var name = (s["name"] as JsonValue)?.ToString();
            var type = (s["type"] as JsonValue)?.ToString();
            var data = s["data"] as JsonArray;
            result.Add(new SeriesInfo(
                string.IsNullOrEmpty(name) ? $"Series {index}" : name!,
                type,
                data?.ToList() ?? new List<JsonNode?>()));
        }
        return result;
    }

    // Category labels come from the first cartesian axis (x then y) that carries a
    // "data" string array — covers both vertical (xAxis) and horizontal (yAxis) bars.
    private static List<string> ReadCategories(JsonObject obj)
    {
        foreach (var key in new[] { "xAxis", "yAxis" })
        {
            var data = obj[key] switch
            {
                JsonArray arr => (arr.FirstOrDefault() as JsonObject)?["data"] as JsonArray,
                JsonObject axis => axis["data"] as JsonArray,
                _ => null,
            };
            if (data is { Count: > 0 })
                return data.Select(FormatValue).ToList();
        }
        return new List<string>();
    }

    private static ChartDataTable BuildCartesian(
        string typeLabel, List<string> categories, List<SeriesInfo> series)
    {
        var headers = new List<string> { "Category" };
        headers.AddRange(series.Select(s => s.Name));

        var rows = new List<ChartDataRow>(categories.Count);
        for (var c = 0; c < categories.Count; c++)
        {
            var cells = new List<string>(series.Count);
            foreach (var s in series)
                cells.Add(c < s.Data.Count ? FormatValue(s.Data[c]) : "");
            rows.Add(new ChartDataRow(categories[c], cells));
        }

        var names = string.Join(", ", series.Select(s => s.Name));
        var summary = series.Count == 1
            ? $"{typeLabel} with {categories.Count} categories: {names}."
            : $"{typeLabel} with {series.Count} series ({names}) across {categories.Count} categories.";
        return new ChartDataTable(summary, headers, rows);
    }

    // No cartesian axis (pie / funnel / radar / scatter …). One series → Name/Value
    // rows; several → Series/Item/Value long form so every point is still reachable.
    private static ChartDataTable BuildCategorical(string typeLabel, List<SeriesInfo> series)
    {
        int points = series.Sum(s => s.Data.Count);

        if (series.Count == 1)
        {
            var single = series[0];
            var headers = new List<string> { "Name", "Value" };
            var rows = new List<ChartDataRow>(single.Data.Count);
            var i = 0;
            foreach (var item in single.Data)
            {
                i++;
                rows.Add(new ChartDataRow(ItemName(item, i), new[] { ItemValue(item) }));
            }
            return new ChartDataTable(
                $"{typeLabel} with {single.Data.Count} data points.", headers, rows);
        }

        var multiHeaders = new List<string> { "Series", "Item", "Value" };
        var multiRows = new List<ChartDataRow>();
        foreach (var s in series)
        {
            var i = 0;
            foreach (var item in s.Data)
            {
                i++;
                multiRows.Add(new ChartDataRow(s.Name, new[] { ItemName(item, i), ItemValue(item) }));
            }
        }
        var names = string.Join(", ", series.Select(s => s.Name));
        return new ChartDataTable(
            $"{typeLabel} with {series.Count} series ({names}) and {points} data points.",
            multiHeaders, multiRows);
    }

    private static string ItemName(JsonNode? item, int ordinal) =>
        item is JsonObject o && o["name"] is JsonValue n && !string.IsNullOrEmpty(n.ToString())
            ? n.ToString()
            : ordinal.ToString(CultureInfo.InvariantCulture);

    private static string ItemValue(JsonNode? item) =>
        item is JsonObject o && o["value"] is { } v ? FormatValue(v) : FormatValue(item);

    private static string FormatValue(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return "";
            case JsonArray arr:
                return string.Join(", ", arr.Select(FormatValue));
            case JsonObject o:
                if (o["value"] is { } v) return FormatValue(v);
                if (o["name"] is { } n) return n.ToString();
                return "";
            case JsonValue val:
                if (val.TryGetValue<double>(out var d))
                    return d.ToString(CultureInfo.InvariantCulture);
                if (val.TryGetValue<bool>(out var b))
                    return b ? "true" : "false";
                return val.ToString();
            default:
                return node.ToString();
        }
    }

    private static string ChartTypeLabel(string? type) => (type?.ToLowerInvariant()) switch
    {
        "bar" => "Bar chart",
        "line" => "Line chart",
        "pie" => "Pie chart",
        "scatter" or "effectscatter" => "Scatter chart",
        "radar" => "Radar chart",
        "candlestick" => "Candlestick chart",
        "heatmap" => "Heatmap",
        "funnel" => "Funnel chart",
        "gauge" => "Gauge chart",
        "sankey" => "Sankey diagram",
        "treemap" => "Treemap",
        "sunburst" => "Sunburst chart",
        "boxplot" => "Box plot",
        "graph" => "Graph",
        "tree" => "Tree diagram",
        _ => "Chart",
    };
}
