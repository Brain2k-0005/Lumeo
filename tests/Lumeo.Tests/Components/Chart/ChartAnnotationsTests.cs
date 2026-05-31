using System.Text.Json.Nodes;
using Xunit;
using Lumeo;

namespace Lumeo.Tests.Components.Chart;

/// <summary>
/// Tests for the JSON merge that wires declarative <see cref="ChartThreshold"/> /
/// <see cref="ChartReferenceZone"/> children into the chart option. Drives
/// <see cref="ChartAnnotationMerger"/> directly so we don't depend on the live ECharts
/// JS bridge.
/// </summary>
public class ChartAnnotationsTests
{
    private const string BaseJsonOneSeries = """
        { "series": [ { "type": "line", "data": [1, 2, 3] } ] }
        """;

    private const string BaseJsonNoSeries = """{ "title": { "text": "Empty" } }""";

    private static IReadOnlyCollection<ChartThresholdInfo> Thresholds(params ChartThresholdInfo[] items) => items;
    private static IReadOnlyCollection<ChartReferenceZoneInfo> Zones(params ChartReferenceZoneInfo[] items) => items;

    [Fact]
    public void No_Annotations_Returns_Base_Json_Unchanged()
    {
        var result = ChartAnnotationMerger.Merge(BaseJsonOneSeries, Thresholds(), Zones());
        Assert.Equal(BaseJsonOneSeries, result);
    }

    [Fact]
    public void Threshold_Merges_Horizontal_YAxis_Entry()
    {
        var result = ChartAnnotationMerger.Merge(
            BaseJsonOneSeries,
            Thresholds(new ChartThresholdInfo("t1", 2.5, ChartThresholdAxis.Horizontal, "Target", "#22c55e", "dashed")),
            Zones());

        var data = JsonNode.Parse(result)!["series"]![0]!["markLine"]!["data"]!.AsArray();
        Assert.Single(data);
        var entry = data[0]!;
        Assert.Equal(2.5, entry["yAxis"]!.GetValue<double>());
        Assert.Equal("Target", entry["name"]!.GetValue<string>());
        Assert.Equal("#22c55e", entry["lineStyle"]!["color"]!.GetValue<string>());
        Assert.Equal("dashed", entry["lineStyle"]!["type"]!.GetValue<string>());
        Assert.Equal("Target", entry["label"]!["formatter"]!.GetValue<string>());
    }

    [Fact]
    public void Vertical_Threshold_Uses_XAxis_Key_And_String_Value()
    {
        var result = ChartAnnotationMerger.Merge(
            BaseJsonOneSeries,
            Thresholds(new ChartThresholdInfo("t1", "B", ChartThresholdAxis.Vertical, null, null, "solid")),
            Zones());

        var entry = JsonNode.Parse(result)!["series"]![0]!["markLine"]!["data"]![0]!;
        Assert.Equal("B", entry["xAxis"]!.GetValue<string>());
        Assert.Null(entry["yAxis"]);
        Assert.Null(entry["name"]);  // null label suppressed
        Assert.Null(entry["label"]); // ditto for the formatter object
        Assert.Equal("solid", entry["lineStyle"]!["type"]!.GetValue<string>());
        Assert.Null(entry["lineStyle"]!["color"]); // null color suppressed
    }

    [Fact]
    public void Multiple_Thresholds_All_Append_In_Order()
    {
        var result = ChartAnnotationMerger.Merge(
            BaseJsonOneSeries,
            Thresholds(
                new ChartThresholdInfo("t1", 100, ChartThresholdAxis.Horizontal, "Target", null, "dashed"),
                new ChartThresholdInfo("t2", 50,  ChartThresholdAxis.Horizontal, "SLA",    null, "dotted")),
            Zones());

        var data = JsonNode.Parse(result)!["series"]![0]!["markLine"]!["data"]!.AsArray();
        Assert.Equal(2, data.Count);
        Assert.Equal("Target", data[0]!["name"]!.GetValue<string>());
        Assert.Equal("SLA",    data[1]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Reference_Zone_Merges_Pair_With_Color_And_Label()
    {
        var result = ChartAnnotationMerger.Merge(
            BaseJsonOneSeries,
            Thresholds(),
            Zones(new ChartReferenceZoneInfo("z1", 80, 120, ChartThresholdAxis.Horizontal, "Healthy", "#22c55e22")));

        var data = JsonNode.Parse(result)!["series"]![0]!["markArea"]!["data"]!.AsArray();
        Assert.Single(data);
        var pair = data[0]!.AsArray();
        Assert.Equal(2, pair.Count);
        Assert.Equal(80, pair[0]!["yAxis"]!.GetValue<int>());
        Assert.Equal(120, pair[1]!["yAxis"]!.GetValue<int>());
        Assert.Equal("#22c55e22", pair[0]!["itemStyle"]!["color"]!.GetValue<string>());
        Assert.Equal("Healthy", pair[0]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Threshold_And_Zone_Compose_On_Same_Series()
    {
        var result = ChartAnnotationMerger.Merge(
            BaseJsonOneSeries,
            Thresholds(new ChartThresholdInfo("t1", 90, ChartThresholdAxis.Horizontal, "Target", "#000", "dashed")),
            Zones(new ChartReferenceZoneInfo("z1", 80, 100, ChartThresholdAxis.Horizontal, null, "#22c55e22")));

        var series0 = JsonNode.Parse(result)!["series"]![0]!;
        Assert.Single(series0["markLine"]!["data"]!.AsArray());
        Assert.Single(series0["markArea"]!["data"]!.AsArray());
    }

    [Fact]
    public void Existing_MarkLine_Data_Is_Preserved_And_Appended_To()
    {
        // If the consumer already shipped markLine via raw EChartOption, our cascade
        // adds to it rather than overwriting.
        var baseJson = """
            { "series": [ { "type": "line", "markLine": { "data": [ { "yAxis": 10, "name": "Pre" } ] } } ] }
            """;
        var result = ChartAnnotationMerger.Merge(
            baseJson,
            Thresholds(new ChartThresholdInfo("t1", 20, ChartThresholdAxis.Horizontal, "Added", null, "dashed")),
            Zones());

        var data = JsonNode.Parse(result)!["series"]![0]!["markLine"]!["data"]!.AsArray();
        Assert.Equal(2, data.Count);
        Assert.Equal("Pre",   data[0]!["name"]!.GetValue<string>());
        Assert.Equal("Added", data[1]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Empty_Series_Returns_Base_Json_Unchanged()
    {
        // An option with no series — annotations have nothing to attach to. Returning the
        // unmodified base is the correct fall-through (per the "never break the chart" rule).
        var result = ChartAnnotationMerger.Merge(
            BaseJsonNoSeries,
            Thresholds(new ChartThresholdInfo("t1", 1, ChartThresholdAxis.Horizontal, null, null, "dashed")),
            Zones());

        Assert.Equal(BaseJsonNoSeries, result);
    }

    [Fact]
    public void Malformed_Base_Json_Returns_Input_Unchanged()
    {
        var bad = "not json";
        var result = ChartAnnotationMerger.Merge(
            bad,
            Thresholds(new ChartThresholdInfo("t1", 1, ChartThresholdAxis.Horizontal, null, null, "dashed")),
            Zones());

        Assert.Equal(bad, result);
    }

    [Fact]
    public void Annotations_Context_Registers_And_Unregisters_Items()
    {
        var changeCount = 0;
        var ctx = new ChartAnnotationsContext(() => changeCount++);
        var t1 = new ChartThresholdInfo("t1", 1, ChartThresholdAxis.Horizontal, null, null, "dashed");
        var z1 = new ChartReferenceZoneInfo("z1", 1, 2, ChartThresholdAxis.Horizontal, null, "#000");

        ctx.RegisterThreshold(t1);
        ctx.RegisterZone(z1);
        Assert.Single(ctx.Thresholds);
        Assert.Single(ctx.Zones);
        Assert.Equal(2, changeCount);

        // Re-registering the same value should NOT fire OnChanged again — guards against
        // a render loop where OnParametersSet calls register on every parameter set.
        ctx.RegisterThreshold(t1);
        Assert.Equal(2, changeCount);

        ctx.UnregisterThreshold("t1");
        ctx.UnregisterZone("z1");
        Assert.Empty(ctx.Thresholds);
        Assert.Empty(ctx.Zones);
        Assert.Equal(4, changeCount);
    }
}
