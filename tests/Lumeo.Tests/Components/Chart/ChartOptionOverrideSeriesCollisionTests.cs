using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Chart;

/// <summary>
/// Field report #464, finding 2: an <c>OptionOverride</c> dictionary that includes a
/// <c>"series"</c> entry used to land in <see cref="L.EChartOption.ExtensionData"/>
/// alongside the chart wrapper's own already-populated <c>Series</c> property.
/// <see cref="System.Text.Json"/>'s <c>[JsonExtensionData]</c> writer does not de-dupe
/// against a regular property of the same name, so the emitted JSON carried TWO
/// <c>"series"</c> keys. Confirmed independently: <c>JsonNode.Parse</c> on such text
/// throws <c>ArgumentException("An item with the same key has already been added.
/// Key: series")</c> — exactly the class of exception that, unguarded, would take a
/// page down; <c>JSON.parse</c> on the JS side instead silently keeps only the LAST
/// duplicate, discarding the properly generated series entirely.
///
/// <see cref="L.ChartHelper.ApplyOptionOverride"/> now clears whichever typed
/// <see cref="L.EChartOption"/> property serializes under a colliding override key
/// (most commonly <c>series</c>) before writing the override into
/// <see cref="L.EChartOption.ExtensionData"/>, so the override always wins outright for
/// that top-level key and the key is emitted exactly once — never duplicated.
/// </summary>
public class ChartOptionOverrideSeriesCollisionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        var module = _ctx.JSInterop.SetupModule("./_content/Lumeo.Charts/js/echarts-interop.js");
        module.Mode = Bunit.JSRuntimeMode.Loose;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.BarChart.ChartSeriesData> OneSeries() => new()
    {
        new() { Name = "Revenue", Values = new() { 10, 20, 30 } },
    };

    [Fact]
    public void OptionOverride_With_Series_Key_Renders_Without_Throwing_And_Series_Appears_Once()
    {
        var overrideSeries = new object[]
        {
            new Dictionary<string, object?> { ["type"] = "line", ["data"] = new[] { 1, 2, 3 } },
        };

        var cut = _ctx.Render<L.BarChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b", "c" })
            .Add(c => c.Series, OneSeries())
            .Add(c => c.OptionOverride, new Dictionary<string, object>
            {
                ["series"] = overrideSeries,
            }));

        var option = cut.FindComponent<L.Chart>().Instance.Option!;
        var json = option.ToJson();

        // Must be valid JSON with no duplicate top-level key — JsonNode.Parse throws
        // ArgumentException on a duplicate key, so a clean parse IS the assertion that
        // "series" was emitted exactly once.
        var node = JsonNode.Parse(json);
        Assert.NotNull(node);
        var obj = Assert.IsType<JsonObject>(node);
        Assert.True(obj.ContainsKey("series"));

        // Confirm there really is only one "series" occurrence in the raw text too —
        // belt-and-braces on top of the JsonNode.Parse guarantee above.
        Assert.Equal(1, CountOccurrences(json, "\"series\":"));
    }

    [Fact]
    public void OptionOverride_Series_Wins_Over_The_Generated_Series()
    {
        var cut = _ctx.Render<L.BarChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b", "c" })
            .Add(c => c.Series, OneSeries())
            .Add(c => c.OptionOverride, new Dictionary<string, object>
            {
                ["series"] = new object[]
                {
                    new Dictionary<string, object?> { ["type"] = "line", ["name"] = "Overridden" },
                },
            }));

        var option = cut.FindComponent<L.Chart>().Instance.Option!;
        using var doc = JsonDocument.Parse(option.ToJson());
        var series = doc.RootElement.GetProperty("series");

        Assert.Equal(1, series.GetArrayLength());
        Assert.Equal("line", series[0].GetProperty("type").GetString());
        Assert.Equal("Overridden", series[0].GetProperty("name").GetString());
    }

    [Fact]
    public void OptionOverride_Without_A_Colliding_Key_Still_Merges_Alongside_Generated_Series()
    {
        // Sanity check: the fix must not regress the common, documented case — an
        // override key the typed model has no dedicated parameter for at all.
        var cut = _ctx.Render<L.BarChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b", "c" })
            .Add(c => c.Series, OneSeries())
            .Add(c => c.OptionOverride, new Dictionary<string, object>
            {
                ["backgroundColor"] = "var(--color-card)",
            }));

        var option = cut.FindComponent<L.Chart>().Instance.Option!;
        using var doc = JsonDocument.Parse(option.ToJson());

        Assert.Equal("var(--color-card)", doc.RootElement.GetProperty("backgroundColor").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("series").GetArrayLength());
        Assert.Equal("bar", doc.RootElement.GetProperty("series")[0].GetProperty("type").GetString());
    }

    [Fact]
    public void ChartAccessibility_Build_Never_Throws_On_A_Duplicate_Series_Key()
    {
        // Direct unit test of the ChartAccessibility contract independent of the
        // OptionOverride fix above: even a hand-crafted (or future-regressed) JSON
        // string with a duplicate "series" key must degrade to "no table", never throw.
        var duplicateKeyJson = "{\"series\":[{\"type\":\"bar\",\"data\":[1,2,3]}],\"series\":{\"foo\":true}}";

        var table = L.ChartAccessibility.Build(duplicateKeyJson);

        Assert.Null(table);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
