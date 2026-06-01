using System.Text.Json;
using Xunit;
using Lumeo;

namespace Lumeo.Tests.Components.Chart;

/// <summary>
/// Regression tests for #153 — DonutChart rendered like a PieChart because the
/// EChartSeries.RadiusList / CenterList properties serialized under their C#
/// names (camelCase: <c>radiusList</c> / <c>centerList</c>) instead of the
/// ECharts-expected <c>radius</c> / <c>center</c>. ECharts ignored both, so
/// inner-radius / centre-offset settings were silently dropped on the floor.
///
/// The fix routes both through computed <c>*Serialized</c> properties carrying
/// <c>[JsonPropertyName("radius"/"center")]</c>. These tests pin the JSON
/// keys + shapes so a future refactor can't silently regress the donut again.
/// </summary>
public class EChartSeriesRadiusSerializationTests
{
    private static string ToJson(EChartSeries s) => JsonSerializer.Serialize(s, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    });

    [Fact]
    public void RadiusList_Serializes_As_Radius_Array()
    {
        var s = new EChartSeries
        {
            Type = "pie",
            RadiusList = new List<string> { "50%", "70%" }
        };

        using var doc = JsonDocument.Parse(ToJson(s));
        var root = doc.RootElement;

        // Must produce { radius: ["50%", "70%"] } — the actual ECharts shape.
        Assert.True(root.TryGetProperty("radius", out var radius), "Expected 'radius' key");
        Assert.Equal(JsonValueKind.Array, radius.ValueKind);
        Assert.Equal(2, radius.GetArrayLength());
        Assert.Equal("50%", radius[0].GetString());
        Assert.Equal("70%", radius[1].GetString());

        // Must NOT leak the C# property name through.
        Assert.False(root.TryGetProperty("radiusList", out _), "Should not emit 'radiusList'");
    }

    [Fact]
    public void Radius_Single_String_Serializes_Through_Same_Key()
    {
        // The single-string form (e.g. plain pie at "70%") must keep working too —
        // the *Serialized fallback picks the string when no list is set.
        var s = new EChartSeries { Type = "pie", Radius = "70%" };
        using var doc = JsonDocument.Parse(ToJson(s));
        var radius = doc.RootElement.GetProperty("radius");
        Assert.Equal(JsonValueKind.String, radius.ValueKind);
        Assert.Equal("70%", radius.GetString());
    }

    [Fact]
    public void RadiusList_Takes_Precedence_Over_Radius_When_Both_Set()
    {
        // Defensive: if a consumer sets both, the array wins (donut shape over plain pie).
        var s = new EChartSeries
        {
            Type = "pie",
            Radius = "70%",
            RadiusList = new List<string> { "50%", "70%" }
        };
        using var doc = JsonDocument.Parse(ToJson(s));
        var radius = doc.RootElement.GetProperty("radius");
        Assert.Equal(JsonValueKind.Array, radius.ValueKind);
    }

    [Fact]
    public void CenterList_Serializes_As_Center_Array()
    {
        var s = new EChartSeries
        {
            Type = "pie",
            CenterList = new List<string> { "50%", "50%" }
        };

        using var doc = JsonDocument.Parse(ToJson(s));
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("center", out var center), "Expected 'center' key");
        Assert.Equal(JsonValueKind.Array, center.ValueKind);
        Assert.False(root.TryGetProperty("centerList", out _), "Should not emit 'centerList'");
    }

    [Fact]
    public void DonutChart_Built_Series_Json_Has_Inner_Outer_Radius()
    {
        // End-to-end shape: a DonutChart-style series must JSON-render with both
        // inner and outer radii inside the `radius` array.
        var s = new EChartSeries
        {
            Type = "pie",
            RadiusList = new List<string> { "50%", "70%" },
            CenterList = new List<string> { "50%", "50%" },
        };

        var json = ToJson(s);
        // Quick string-level sanity that the right shape is in the output.
        Assert.Contains("\"radius\":[\"50%\",\"70%\"]", json);
        Assert.Contains("\"center\":[\"50%\",\"50%\"]", json);
        Assert.DoesNotContain("\"radiusList\"", json);
        Assert.DoesNotContain("\"centerList\"", json);
    }

    [Fact]
    public void Neither_Radius_Nor_RadiusList_Set_Omits_Key()
    {
        // When nothing is set the computed property is null and the serializer drops it
        // — chart still renders with ECharts defaults instead of an explicit empty value.
        var s = new EChartSeries { Type = "line" };
        using var doc = JsonDocument.Parse(ToJson(s));
        Assert.False(doc.RootElement.TryGetProperty("radius", out _));
        Assert.False(doc.RootElement.TryGetProperty("center", out _));
    }
}
