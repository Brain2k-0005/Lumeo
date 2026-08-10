using System.Text.Json;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Chart;

/// <summary>
/// Regression tests for the charts-design "form, not colour" pass (feat/charts-design,
/// PR #394). The owner kept shadcn's chart palette unchanged (chart-1 is achromatic,
/// chart-4/chart-5 sit ~29° apart in hue), so bar/pie series and line/area series get a
/// colour-independent way to read apart: a per-series/per-slice decal (pattern fill) for
/// categorical bar and pie families, and a per-series marker-symbol/dash/stroke-weight
/// cycle for line and area.
///
/// <see cref="L.ChartHelper.GetDecal"/> and <see cref="L.ChartHelper.ApplyLineForm"/> are
/// internal, so these tests exercise them indirectly through the actual wrapper
/// components' rendered <c>EChartOption.ToJson()</c> output — the real render path, not a
/// re-implementation — mirroring <see cref="AreaChartGradientTests"/>.
/// </summary>
public class ChartFormDifferentiationTests : IAsyncLifetime
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

    private static JsonElement SeriesArray(IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> cut)
    {
        var option = cut.FindComponent<L.Chart>().Instance.Option!;
        using var doc = JsonDocument.Parse(option.ToJson());
        return doc.RootElement.GetProperty("series").Clone();
    }

    // --- BarChart: per-series decal, scoped to multi-series ------------------------

    [Fact]
    public void BarChart_Single_Series_Has_No_Decal()
    {
        // Nothing to differentiate against — decal must not appear at all.
        var cut = _ctx.Render<L.BarChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b", "c" })
            .Add(c => c.Series, new List<L.BarChart.ChartSeriesData>
            {
                new() { Name = "Revenue", Values = new() { 1, 2, 3 } },
            }));

        var series = SeriesArray(cut);
        Assert.Equal(1, series.GetArrayLength());
        var itemStyle0 = series[0].TryGetProperty("itemStyle", out var style) ? style : default;
        Assert.False(itemStyle0.ValueKind == JsonValueKind.Object && itemStyle0.TryGetProperty("decal", out _),
            "Single-series bar chart must not emit a decal — there is nothing to differentiate.");
    }

    [Fact]
    public void BarChart_Grouped_Multi_Series_Gets_Distinct_Decal_Per_Series()
    {
        var cut = _ctx.Render<L.BarChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b", "c" })
            .Add(c => c.Series, new List<L.BarChart.ChartSeriesData>
            {
                new() { Name = "This year", Values = new() { 1, 2, 3 } },
                new() { Name = "Last year", Values = new() { 2, 3, 4 } },
                new() { Name = "Forecast", Values = new() { 3, 4, 5 } },
            }));

        var series = SeriesArray(cut);
        Assert.Equal(3, series.GetArrayLength());

        var decal0 = series[0].GetProperty("itemStyle").GetProperty("decal");
        var decal1 = series[1].GetProperty("itemStyle").GetProperty("decal");
        var decal2 = series[2].GetProperty("itemStyle").GetProperty("decal");

        // Every decal derives colour from a token, never a raw hex (project rule).
        Assert.Equal("var(--color-border)", decal0.GetProperty("color").GetString());
        Assert.Equal("var(--color-border)", decal1.GetProperty("color").GetString());
        Assert.Equal("var(--color-border)", decal2.GetProperty("color").GetString());

        // Exact recipe cycle: index 0 = diagonal stripes (rect), index 1 = dots (circle),
        // index 2 = fine grid (rect, but a different dash cadence than index 0 — still a
        // genuinely different rendered pattern, see ChartHelper.DecalRecipes).
        Assert.Equal("rect", decal0.GetProperty("symbol").GetString());
        Assert.Equal("circle", decal1.GetProperty("symbol").GetString());
        Assert.Equal("rect", decal2.GetProperty("symbol").GetString());
        Assert.NotEqual(decal0.GetProperty("dashArrayY").GetRawText(), decal2.GetProperty("dashArrayY").GetRawText());
    }

    [Fact]
    public void BarChart_Beyond_MaxDecalItems_Series_Gets_No_Decal()
    {
        // 9 series exceeds ChartHelper.MaxDecalItems (8) — decal must be skipped
        // entirely rather than silently repeating the 5-recipe cycle.
        var series9 = Enumerable.Range(0, 9)
            .Select(i => new L.BarChart.ChartSeriesData { Name = $"S{i}", Values = new() { i, i + 1 } })
            .ToList();
        var cut = _ctx.Render<L.BarChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b" })
            .Add(c => c.Series, series9));

        var series = SeriesArray(cut);
        Assert.Equal(9, series.GetArrayLength());
        for (var i = 0; i < 9; i++)
        {
            var hasItemStyle = series[i].TryGetProperty("itemStyle", out var style);
            var hasDecal = hasItemStyle && style.TryGetProperty("decal", out _);
            Assert.False(hasDecal, $"Series {i} of 9 must not have a decal — count exceeds MaxDecalItems.");
        }
    }

    [Fact]
    public void BarChart_Two_Series_Boundary_Gets_Decal()
    {
        // 2 is the smallest count with something to differentiate — must get decal.
        var cut = _ctx.Render<L.BarChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b" })
            .Add(c => c.Series, new List<L.BarChart.ChartSeriesData>
            {
                new() { Name = "A", Values = new() { 1, 2 } },
                new() { Name = "B", Values = new() { 2, 3 } },
            }));

        var series = SeriesArray(cut);
        Assert.True(series[0].GetProperty("itemStyle").TryGetProperty("decal", out _));
        Assert.True(series[1].GetProperty("itemStyle").TryGetProperty("decal", out _));
    }

    [Fact]
    public void BarChart_Stacked_Preserves_BorderRadius_And_Adds_Decal()
    {
        // Regression guard: the stacked branch already sets ItemStyle.BorderRadiusCorners
        // per series — decal must layer ON TOP via ??=, not replace that ItemStyle object.
        var cut = _ctx.Render<L.BarChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b" })
            .Add(c => c.Stacked, true)
            .Add(c => c.Series, new List<L.BarChart.ChartSeriesData>
            {
                new() { Name = "A", Values = new() { 1, 2 } },
                new() { Name = "B", Values = new() { 2, 3 } },
            }));

        var series = SeriesArray(cut);
        var itemStyle1 = series[1].GetProperty("itemStyle"); // outer (top) segment
        Assert.True(itemStyle1.TryGetProperty("borderRadius", out _), "Stacked outer segment must still get rounded corners.");
        Assert.True(itemStyle1.TryGetProperty("decal", out _), "Stacked series must still get a decal.");
    }

    // --- PieChart / DonutChart: per-slice decal, scoped to slice count -------------

    [Fact]
    public void PieChart_Small_Slice_Count_Gets_Decal_Per_Slice()
    {
        var cut = _ctx.Render<L.PieChart>(p => p
            .Add(c => c.Data, new List<L.PieChart.PieChartData>
            {
                new() { Name = "A", Value = 10 },
                new() { Name = "B", Value = 20 },
                new() { Name = "C", Value = 30 },
            }));

        var series = SeriesArray(cut);
        var data = series[0].GetProperty("data");
        Assert.Equal(3, data.GetArrayLength());
        for (var i = 0; i < 3; i++)
            Assert.True(data[i].GetProperty("itemStyle").TryGetProperty("decal", out _), $"Slice {i} must have a decal.");
    }

    [Fact]
    public void PieChart_Beyond_MaxDecalItems_Slices_Get_No_Decal()
    {
        // 12 slices exceeds the cap (8) — every slice must be decal-free rather than
        // cluttering an already-hard-to-read many-slice pie with texture.
        var data12 = Enumerable.Range(0, 12).Select(i => new L.PieChart.PieChartData { Name = $"S{i}", Value = i + 1 }).ToList();
        var cut = _ctx.Render<L.PieChart>(p => p.Add(c => c.Data, data12));

        var series = SeriesArray(cut);
        var data = series[0].GetProperty("data");
        for (var i = 0; i < 12; i++)
        {
            var hasItemStyle = data[i].TryGetProperty("itemStyle", out var style);
            var hasDecal = hasItemStyle && style.ValueKind == JsonValueKind.Object && style.TryGetProperty("decal", out _);
            Assert.False(hasDecal, $"Slice {i} of 12 must not have a decal — count exceeds MaxDecalItems.");
        }
    }

    [Fact]
    public void PieChart_Single_Slice_Has_No_Decal()
    {
        var cut = _ctx.Render<L.PieChart>(p => p
            .Add(c => c.Data, new List<L.PieChart.PieChartData> { new() { Name = "Only", Value = 100 } }));

        var series = SeriesArray(cut);
        var data = series[0].GetProperty("data");
        var hasItemStyle = data[0].TryGetProperty("itemStyle", out var style);
        Assert.False(hasItemStyle && style.ValueKind == JsonValueKind.Object && style.TryGetProperty("decal", out _));
    }

    [Fact]
    public void DonutChart_Small_Slice_Count_Gets_Decal_Per_Slice()
    {
        var cut = _ctx.Render<L.DonutChart>(p => p
            .Add(c => c.Data, new List<L.DonutChart.DonutChartData>
            {
                new() { Name = "A", Value = 10 },
                new() { Name = "B", Value = 20 },
            }));

        var series = SeriesArray(cut);
        var data = series[0].GetProperty("data");
        Assert.True(data[0].GetProperty("itemStyle").TryGetProperty("decal", out _));
        Assert.True(data[1].GetProperty("itemStyle").TryGetProperty("decal", out _));
    }

    // --- LineChart / AreaChart: per-series symbol + dash + stroke weight -----------

    [Fact]
    public void LineChart_Single_Series_Gets_Circle_Symbol_And_Bolder_Stroke()
    {
        // The achromatic-series answer for pure line charts: series 0 (the common
        // single-series case, and the one that inherits the achromatic chart-1 token)
        // gets a heavier stroke than the theme default (2) asserting its presence via
        // weight rather than colour.
        var cut = _ctx.Render<L.LineChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b", "c" })
            .Add(c => c.Series, new List<L.LineChart.ChartSeriesData>
            {
                new() { Name = "Only", Values = new() { 1, 2, 3 } },
            }));

        var series = SeriesArray(cut);
        Assert.Equal("circle", series[0].GetProperty("symbol").GetString());
        Assert.Equal(3, series[0].GetProperty("lineStyle").GetProperty("width").GetInt32());
        // No dash on the sole series — solid, matching the theme default type.
        Assert.False(series[0].GetProperty("lineStyle").TryGetProperty("type", out _));
    }

    [Fact]
    public void LineChart_Multi_Series_Cycles_Symbol_And_Dash()
    {
        var cut = _ctx.Render<L.LineChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b", "c" })
            .Add(c => c.Series, new List<L.LineChart.ChartSeriesData>
            {
                new() { Name = "First", Values = new() { 1, 2, 3 } },
                new() { Name = "Second", Values = new() { 2, 3, 4 } },
                new() { Name = "Third", Values = new() { 3, 4, 5 } },
            }));

        var series = SeriesArray(cut);
        Assert.Equal("circle", series[0].GetProperty("symbol").GetString());
        Assert.Equal("diamond", series[1].GetProperty("symbol").GetString());
        Assert.Equal("triangle", series[2].GetProperty("symbol").GetString());

        Assert.Equal(3, series[0].GetProperty("lineStyle").GetProperty("width").GetInt32());
        Assert.Equal("dashed", series[1].GetProperty("lineStyle").GetProperty("type").GetString());
        Assert.Equal(2, series[1].GetProperty("lineStyle").GetProperty("width").GetInt32());
        Assert.Equal("dotted", series[2].GetProperty("lineStyle").GetProperty("type").GetString());
    }

    [Fact]
    public void AreaChart_Multi_Series_Cycles_Symbol_While_Keeping_Gradient_AreaStyle()
    {
        // Regression guard: the line-form cycle (symbol/lineStyle) must not clobber the
        // existing per-series gradient AreaStyle set earlier in AreaChart.razor.
        var cut = _ctx.Render<L.AreaChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b", "c" })
            .Add(c => c.Series, new List<L.AreaChart.ChartSeriesData>
            {
                new() { Name = "First", Values = new() { 1, 2, 3 } },
                new() { Name = "Second", Values = new() { 2, 3, 4 } },
            }));

        var series = SeriesArray(cut);
        Assert.Equal("circle", series[0].GetProperty("symbol").GetString());
        Assert.Equal("diamond", series[1].GetProperty("symbol").GetString());
        // AreaStyle gradient from the pre-existing BuildGradient() path must still be there.
        Assert.Equal("linear", series[0].GetProperty("areaStyle").GetProperty("color").GetProperty("type").GetString());
        Assert.Equal("linear", series[1].GetProperty("areaStyle").GetProperty("color").GetProperty("type").GetString());
    }
}
