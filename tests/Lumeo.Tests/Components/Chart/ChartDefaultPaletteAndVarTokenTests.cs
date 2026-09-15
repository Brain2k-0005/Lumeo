using System.Text.Json;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Chart;

/// <summary>
/// Field report #464, findings 1 and 3, verified against CURRENT source (5.10.0-in-
/// progress, master past the charts-design passes).
///
/// Finding 1 ("AreaChart draws an invisible line when no Colors are passed"): with no
/// <c>Colors</c>/<c>ColorPalette</c>, <see cref="L.AreaChart"/> must NOT emit an empty
/// top-level <c>color</c> array (which would suppress ECharts' own theme palette and
/// leave every series with no resolvable colour) — it must omit <c>color</c> entirely so
/// the registered "lumeo" theme's <c>color: [chart-1..chart-5]</c> array applies, exactly
/// like <see cref="L.BarChart"/>/<see cref="L.LineChart"/> already do. Already true in
/// current source (<see cref="AreaChart_Omits_Top_Level_Color_When_Unset"/> passes without
/// any code change) — this pins it as a regression guard. The AREA FILL side of this
/// finding was already covered by <see cref="AreaChartGradientTests"/>
/// (<c>var(--color-chart-1)</c> at the gradient's top stop).
///
/// Finding 3 ("var(--token) as a colour evaluates to 0 — affects every colour, not just
/// decals"): the C# side must pass a <c>var(--x)</c> colour string through to the emitted
/// JSON completely verbatim — the actual resolution (and the historical "probed as a
/// length, got 0" bug) lives entirely in the JS interop's <c>resolveCssVarValue</c>
/// (src/Lumeo.Charts/wwwroot/js/echarts-interop.js), which already special-cases
/// <c>--color*</c> variables to resolve as a colour, never a length (see the comment on
/// that function). These tests pin the C#-side half of the contract: nothing between a
/// consumer's <c>Colors</c>/<c>ColorPalette</c> parameter and the emitted option JSON may
/// mangle a var() token before it ever reaches that JS resolver.
/// </summary>
public class ChartDefaultPaletteAndVarTokenTests : IAsyncLifetime
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

    private static List<L.AreaChart.ChartSeriesData> OneAreaSeries() => new()
    {
        new() { Name = "Visits", Values = new() { 10, 20, 30 } },
    };

    private static List<L.BarChart.ChartSeriesData> OneBarSeries() => new()
    {
        new() { Name = "Revenue", Values = new() { 10, 20, 30 } },
    };

    [Fact]
    public void AreaChart_Omits_Top_Level_Color_When_Unset()
    {
        var cut = _ctx.Render<L.AreaChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b", "c" })
            .Add(c => c.Series, OneAreaSeries()));

        var option = cut.FindComponent<L.Chart>().Instance.Option!;
        using var doc = JsonDocument.Parse(option.ToJson());

        // Absent, not an empty array — an empty `color:[]` would suppress ECharts' own
        // theme-registered palette and leave the series with nothing to resolve to
        // (the "invisible line" symptom); omitting the key lets the theme's
        // color:[chart-1..chart-5] array (registered in echarts-interop.js's
        // buildLumeoTheme) apply, same as every other chart wrapper.
        Assert.False(doc.RootElement.TryGetProperty("color", out _));
    }

    [Fact]
    public void AreaChart_Single_Series_LineStyle_Never_Carries_An_Explicit_Invisible_Color()
    {
        var cut = _ctx.Render<L.AreaChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b", "c" })
            .Add(c => c.Series, OneAreaSeries()));

        var option = cut.FindComponent<L.Chart>().Instance.Option!;
        using var doc = JsonDocument.Parse(option.ToJson());
        var series = doc.RootElement.GetProperty("series")[0];

        // ApplyLineForm sets a bolder stroke (width 3) for series 0 but must never pin
        // an explicit lineStyle.color — that would override the auto-assigned theme
        // palette colour with nothing resolvable (empty string / "0" / "transparent").
        if (series.TryGetProperty("lineStyle", out var lineStyle))
        {
            Assert.False(lineStyle.TryGetProperty("color", out var color)
                && (color.GetString() is "" or "0" or "transparent"));
        }
    }

    [Theory]
    [InlineData("var(--color-chart-2)")]
    [InlineData("var(--color-chart-2, #ff0000)")]
    public void BarChart_Colors_VarToken_Passes_Through_To_Json_Verbatim(string token)
    {
        var cut = _ctx.Render<L.BarChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b", "c" })
            .Add(c => c.Series, OneBarSeries())
            .Add(c => c.Colors, new List<string> { token }));

        var option = cut.FindComponent<L.Chart>().Instance.Option!;
        using var doc = JsonDocument.Parse(option.ToJson());

        var color = doc.RootElement.GetProperty("color");
        Assert.Equal(1, color.GetArrayLength());
        Assert.Equal(token, color[0].GetString());
        Assert.NotEqual("0", color[0].GetString());
    }

    [Fact]
    public void PieChart_Colors_VarToken_Passes_Through_To_Json_Verbatim()
    {
        var cut = _ctx.Render<L.PieChart>(p => p
            .Add(c => c.Data, new List<L.PieChart.PieChartData>
            {
                new() { Name = "A", Value = 10 },
                new() { Name = "B", Value = 20 },
            })
            .Add(c => c.Colors, new List<string> { "var(--color-chart-3)", "var(--color-chart-4)" }));

        var option = cut.FindComponent<L.Chart>().Instance.Option!;
        using var doc = JsonDocument.Parse(option.ToJson());

        var color = doc.RootElement.GetProperty("color");
        Assert.Equal("var(--color-chart-3)", color[0].GetString());
        Assert.Equal("var(--color-chart-4)", color[1].GetString());
    }

    // ── Finding 4 ("the theme paints all pie/donut segments the same colour") ──
    //
    // The bug and its fix are entirely JS-side: the previous pie.itemStyle.color
    // THEME CALLBACK read params.color to decide each slice's base colour, but a live
    // probe against the real `echarts` package (SSR renderer, no DOM needed) confirmed
    // params.color is pinned to the series' single default entry for every call once a
    // callback is registered — params.dataIndex varies correctly, params.color does not.
    // The fix (src/Lumeo.Charts/wwwroot/js/echarts-interop.js: applyPieItemGradients,
    // called before every setOption) computes each slice's gradient explicitly per data
    // item instead — see tests/js/echarts-interop-theme.test.mjs for the full coverage
    // (default palette cycling, a consumer's Colors/ColorPalette array taking precedence
    // and wrapping, var(--token) palette entries, preserving an existing decal, and
    // leaving an already-explicit item colour alone) plus the removed-callback pin.
    //
    // This bUnit test pins the C# side of the JS/C# contract applyPieItemGradients
    // relies on: PieChart/DonutChart must keep emitting `color` omitted (so the JS
    // fallback branch — the theme's own chart-1..5 tokens — applies) and each slice as
    // a plain {name,value,...} data object the JS gradient assignment can attach
    // `itemStyle.color` to without clobbering the decal it may already carry.
    [Fact]
    public void PieChart_Four_Slices_No_Colors_Emits_Shape_ApplyPieItemGradients_Can_Colour_Distinctly()
    {
        var cut = _ctx.Render<L.PieChart>(p => p
            .Add(c => c.Data, new List<L.PieChart.PieChartData>
            {
                new() { Name = "A", Value = 10 },
                new() { Name = "B", Value = 20 },
                new() { Name = "C", Value = 30 },
                new() { Name = "D", Value = 40 },
            }));

        var option = cut.FindComponent<L.Chart>().Instance.Option!;
        using var doc = JsonDocument.Parse(option.ToJson());

        // No explicit color array — the JS fallback (theme chart-1..5) governs.
        Assert.False(doc.RootElement.TryGetProperty("color", out _));

        var data = doc.RootElement.GetProperty("series")[0].GetProperty("data");
        Assert.Equal(4, data.GetArrayLength());
        var names = new List<string?>();
        foreach (var item in data.EnumerateArray())
        {
            names.Add(item.GetProperty("name").GetString());
            // None of the C# wrappers set a per-item colour themselves — that's the JS
            // fix's job — but the decal (form differentiation) may legitimately be present.
            Assert.False(item.TryGetProperty("itemStyle", out var itemStyle)
                && itemStyle.TryGetProperty("color", out _));
        }
        Assert.Equal(new[] { "A", "B", "C", "D" }, names);
    }
}
