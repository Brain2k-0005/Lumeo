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

    // --- BarChart: no decal (charts-design-2 pass removed it — see BarChart.razor) --
    //
    // The original charts-design pass (PR #394) added a per-series decal to grouped/
    // stacked bar series; the charts-design-2 pass removed it for bar specifically
    // (kept for Pie/Donut, tested below) because the EvilCharts-aligned reference this
    // pass matches is explicit that bars are solid, pattern-free. The tests below
    // replace the old "decal appears/is skipped at the right count" assertions with
    // "decal never appears, at any count" — locking in the new behaviour rather than
    // just deleting coverage.

    [Fact]
    public void BarChart_Single_Series_Has_No_Decal()
    {
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
    public void BarChart_Grouped_Multi_Series_Never_Gets_Decal()
    {
        // Was: "gets a distinct decal per series". Bar no longer emits decal at all —
        // series read apart by palette colour + legend swatch alone, same as every
        // other Lumeo surface that distinguishes rows/segments by colour.
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
        for (var i = 0; i < 3; i++)
        {
            var hasItemStyle = series[i].TryGetProperty("itemStyle", out var style);
            var hasDecal = hasItemStyle && style.ValueKind == JsonValueKind.Object && style.TryGetProperty("decal", out _);
            Assert.False(hasDecal, $"Series {i} of 3 must not have a decal — bar no longer applies one.");
        }
    }

    [Fact]
    public void BarChart_Beyond_MaxDecalItems_Series_Gets_No_Decal()
    {
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
    public void BarChart_Two_Series_Boundary_Never_Gets_Decal()
    {
        // Was: "2 is the smallest count with something to differentiate — must get
        // decal." Bar no longer applies decal at any count, including this boundary.
        var cut = _ctx.Render<L.BarChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b" })
            .Add(c => c.Series, new List<L.BarChart.ChartSeriesData>
            {
                new() { Name = "A", Values = new() { 1, 2 } },
                new() { Name = "B", Values = new() { 2, 3 } },
            }));

        var series = SeriesArray(cut);
        var itemStyle0 = series[0].TryGetProperty("itemStyle", out var style0) ? style0 : default;
        var itemStyle1 = series[1].TryGetProperty("itemStyle", out var style1) ? style1 : default;
        Assert.False(itemStyle0.ValueKind == JsonValueKind.Object && itemStyle0.TryGetProperty("decal", out _));
        Assert.False(itemStyle1.ValueKind == JsonValueKind.Object && itemStyle1.TryGetProperty("decal", out _));
    }

    [Fact]
    public void BarChart_Stacked_Outer_Segment_Gets_Token_Driven_BorderRadius_No_Decal()
    {
        // Regression guard: the stacked branch sets ItemStyle.BorderRadiusCorners per
        // series (outer segment rounded, inner segments flat) — that must still work,
        // now via "var(--radius-sm)" tokens (see BarChart.razor) — HALF of --radius,
        // not the full token — and no decal is layered on top anymore.
        var cut = _ctx.Render<L.BarChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b" })
            .Add(c => c.Stacked, true)
            .Add(c => c.Series, new List<L.BarChart.ChartSeriesData>
            {
                new() { Name = "A", Values = new() { 1, 2 } },
                new() { Name = "B", Values = new() { 2, 3 } },
            }));

        var series = SeriesArray(cut);
        var itemStyle0 = series[0].GetProperty("itemStyle"); // inner (bottom) segment — flat
        var itemStyle1 = series[1].GetProperty("itemStyle"); // outer (top) segment — rounded

        var flatCorners = itemStyle0.GetProperty("borderRadius");
        Assert.Equal(4, flatCorners.GetArrayLength());
        foreach (var corner in flatCorners.EnumerateArray())
            Assert.Equal(0, corner.GetInt32());

        var outerCorners = itemStyle1.GetProperty("borderRadius");
        Assert.Equal(4, outerCorners.GetArrayLength());
        // Top-left/top-right carry the "var(--radius-sm)" token (resolved client-side
        // by the chart interop's resolveCssVars pass, not baked into the option JSON
        // at this layer) — bottom-left/bottom-right stay flat.
        Assert.Equal("var(--radius-sm)", outerCorners[0].GetString());
        Assert.Equal("var(--radius-sm)", outerCorners[1].GetString());
        Assert.Equal(0, outerCorners[2].GetInt32());
        Assert.Equal(0, outerCorners[3].GetInt32());

        Assert.False(itemStyle1.TryGetProperty("decal", out _), "Stacked bar must not get a decal anymore.");
    }

    [Fact]
    public void BarChart_Grouped_Vertical_Common_Case_Gets_Explicit_Token_Driven_TopRadius()
    {
        // Geometry-gap pass: the common case (grouped/single-series vertical, not
        // stacked, not horizontal) previously emitted NO series-level ItemStyle at
        // all, relying entirely on the shared theme's default bar.itemStyle.borderRadius
        // falling through — which worked for rendering, but made it impossible to flip
        // the rounded end for a negative value (see the negative-value tests below).
        // Now explicit, same var(--radius-sm) token as every other bar-family corner.
        var cut = _ctx.Render<L.BarChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b", "c" })
            .Add(c => c.Series, new List<L.BarChart.ChartSeriesData>
            {
                new() { Name = "This year", Values = new() { 1, 2, 3 } },
                new() { Name = "Last year", Values = new() { 2, 3, 4 } },
            }));

        var series = SeriesArray(cut);
        foreach (var s in series.EnumerateArray())
        {
            var corners = s.GetProperty("itemStyle").GetProperty("borderRadius");
            Assert.Equal(4, corners.GetArrayLength());
            Assert.Equal("var(--radius-sm)", corners[0].GetString());
            Assert.Equal("var(--radius-sm)", corners[1].GetString());
            Assert.Equal(0, corners[2].GetInt32());
            Assert.Equal(0, corners[3].GetInt32());
            // Plain array-of-numbers data — no negative values, so no per-point
            // itemStyle wrapping (see the negative-value test below).
            var data = s.GetProperty("data");
            Assert.Equal(JsonValueKind.Number, data[0].ValueKind);
        }
    }

    [Fact]
    public void BarChart_Horizontal_NonStacked_Gets_Token_Driven_RightRadius()
    {
        var cut = _ctx.Render<L.BarChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b" })
            .Add(c => c.Horizontal, true)
            .Add(c => c.Series, new List<L.BarChart.ChartSeriesData>
            {
                new() { Name = "Only", Values = new() { 1, 2 } },
            }));

        var series = SeriesArray(cut);
        var corners = series[0].GetProperty("itemStyle").GetProperty("borderRadius");
        Assert.Equal(4, corners.GetArrayLength());
        Assert.Equal(0, corners[0].GetInt32());
        Assert.Equal("var(--radius-sm)", corners[1].GetString());
        Assert.Equal("var(--radius-sm)", corners[2].GetString());
        Assert.Equal(0, corners[3].GetInt32());
    }

    // --- Negative-value corner flip (ChartHelper.BarCorners / BuildBarData) --------

    [Fact]
    public void BarChart_Vertical_Negative_Value_Rounds_Bottom_Not_Top()
    {
        // A negative bar hangs BELOW the zero baseline — its own far tip is the
        // bottom, so the rounded corners must flip there instead of staying at the
        // top (which would round the end touching the baseline/axis instead of the
        // bar's own tip).
        var cut = _ctx.Render<L.BarChart>(p => p
            .Add(c => c.Categories, new List<string> { "profit", "loss" })
            .Add(c => c.Series, new List<L.BarChart.ChartSeriesData>
            {
                new() { Name = "Net", Values = new() { 5, -3 } },
            }));

        var series = SeriesArray(cut);
        var data = series[0].GetProperty("data");
        Assert.Equal(2, data.GetArrayLength());

        // Positive point: still a plain object with the forward (top-rounded) corners.
        var positive = data[0];
        Assert.Equal(5, positive.GetProperty("value").GetDouble());
        var positiveCorners = positive.GetProperty("itemStyle").GetProperty("borderRadius");
        Assert.Equal("var(--radius-sm)", positiveCorners[0].GetString());
        Assert.Equal("var(--radius-sm)", positiveCorners[1].GetString());
        Assert.Equal(0, positiveCorners[2].GetInt32());
        Assert.Equal(0, positiveCorners[3].GetInt32());

        // Negative point: corners rotated 180° — bottom rounds, top stays flat.
        var negative = data[1];
        Assert.Equal(-3, negative.GetProperty("value").GetDouble());
        var negativeCorners = negative.GetProperty("itemStyle").GetProperty("borderRadius");
        Assert.Equal(0, negativeCorners[0].GetInt32());
        Assert.Equal(0, negativeCorners[1].GetInt32());
        Assert.Equal("var(--radius-sm)", negativeCorners[2].GetString());
        Assert.Equal("var(--radius-sm)", negativeCorners[3].GetString());
    }

    [Fact]
    public void BarChart_Horizontal_Negative_Value_Rounds_Left_Not_Right()
    {
        var cut = _ctx.Render<L.BarChart>(p => p
            .Add(c => c.Categories, new List<string> { "in", "out" })
            .Add(c => c.Horizontal, true)
            .Add(c => c.Series, new List<L.BarChart.ChartSeriesData>
            {
                new() { Name = "Flow", Values = new() { 4, -6 } },
            }));

        var series = SeriesArray(cut);
        var data = series[0].GetProperty("data");

        var positiveCorners = data[0].GetProperty("itemStyle").GetProperty("borderRadius");
        Assert.Equal(0, positiveCorners[0].GetInt32());
        Assert.Equal("var(--radius-sm)", positiveCorners[1].GetString());
        Assert.Equal("var(--radius-sm)", positiveCorners[2].GetString());
        Assert.Equal(0, positiveCorners[3].GetInt32());

        var negativeCorners = data[1].GetProperty("itemStyle").GetProperty("borderRadius");
        Assert.Equal("var(--radius-sm)", negativeCorners[0].GetString());
        Assert.Equal(0, negativeCorners[1].GetInt32());
        Assert.Equal(0, negativeCorners[2].GetInt32());
        Assert.Equal("var(--radius-sm)", negativeCorners[3].GetString());
    }

    [Fact]
    public void BarChart_AllPositive_Series_Keeps_Plain_Numeric_Data_Array()
    {
        // No negative values anywhere -> Data stays the flat List<double> wire shape
        // (no per-point itemStyle wrapping needed or emitted) — zero behaviour change
        // for the overwhelmingly common all-positive dataset.
        var cut = _ctx.Render<L.BarChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b", "c" })
            .Add(c => c.Series, new List<L.BarChart.ChartSeriesData>
            {
                new() { Name = "Only", Values = new() { 0, 2, 3 } }, // 0 counts as non-negative
            }));

        var series = SeriesArray(cut);
        var data = series[0].GetProperty("data");
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        foreach (var point in data.EnumerateArray())
            Assert.Equal(JsonValueKind.Number, point.ValueKind);
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
