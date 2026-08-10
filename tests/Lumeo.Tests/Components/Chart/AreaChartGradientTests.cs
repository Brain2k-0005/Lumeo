using System.Text.Json;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Chart;

/// <summary>
/// Regression tests for the AreaChart real-gradient fill. Historically
/// <c>Gradient</c> only changed the area <c>opacity</c> (0.3 vs 0.7); there was no
/// way to get a true brand-colour gradient. <c>GradientFill</c> / <c>GradientStops</c>
/// map to an ECharts <c>areaStyle.color</c> linear-gradient object.
///
/// The charts-design pass ("EvilCharts level") made the DEFAULT (<c>Gradient=true</c>,
/// <c>GradientFill=false</c>) route through the SAME gradient builder too — scaled to a
/// moderate 0.45 top opacity via <c>AreaStyle.Opacity</c>, distinct from the bolder,
/// full-opacity-top look <c>GradientFill</c> opts into. <c>Gradient=false</c> is the one
/// remaining "existing charts are unchanged" escape hatch: flat 0.7 opacity, no gradient
/// object at all — see <see cref="Default_Gradient_False_Is_Opacity_Only_No_Gradient"/>.
/// </summary>
public class AreaChartGradientTests : IAsyncLifetime
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

    private static List<L.AreaChart.ChartSeriesData> OneSeries() => new()
    {
        new() { Name = "Visits", Values = new() { 10, 20, 30 } },
    };

    // Serialize the option the AreaChart handed to its inner Chart and return the
    // first series' areaStyle element (the real render path, not a re-implementation).
    private JsonElement FirstAreaStyle(IRenderedComponent<L.AreaChart> cut)
    {
        var option = cut.FindComponent<L.Chart>().Instance.Option!;
        using var doc = JsonDocument.Parse(option.ToJson());
        var areaStyle = doc.RootElement
            .GetProperty("series")[0]
            .GetProperty("areaStyle");
        return areaStyle.Clone();
    }

    [Fact]
    public void Default_Fill_Is_A_Moderate_Gradient_To_Transparent()
    {
        // charts-design pass: the DEFAULT (Gradient=true, GradientFill=false) now
        // emits the SAME per-series vertical gradient as the explicit GradientFill
        // opt-in, scaled down to a moderate 0.45 top opacity via AreaStyle.Opacity —
        // the restrained everyday look instead of the old flat 0.3-opacity rectangle.
        var cut = _ctx.Render<L.AreaChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b", "c" })
            .Add(c => c.Series, OneSeries()));

        var areaStyle = FirstAreaStyle(cut);
        Assert.True(areaStyle.TryGetProperty("opacity", out var opacity));
        Assert.Equal(0.45, opacity.GetDouble());
        Assert.True(areaStyle.TryGetProperty("color", out var color), "Default fill must emit a gradient color object");
        Assert.Equal("linear", color.GetProperty("type").GetString());
        var stops = color.GetProperty("colorStops");
        Assert.Equal("var(--color-chart-1)", stops[0].GetProperty("color").GetString());
        Assert.Equal("transparent", stops[1].GetProperty("color").GetString());
    }

    [Fact]
    public void Default_Gradient_False_Is_Opacity_Only_No_Gradient()
    {
        // Gradient=false is the explicit "solid, no gradient" alternative — the one
        // remaining flat translucent fill, unchanged by the charts-design pass.
        var cut = _ctx.Render<L.AreaChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b", "c" })
            .Add(c => c.Series, OneSeries())
            .Add(c => c.Gradient, false));

        var areaStyle = FirstAreaStyle(cut);
        Assert.True(areaStyle.TryGetProperty("opacity", out var opacity));
        Assert.Equal(0.7, opacity.GetDouble());
        Assert.False(areaStyle.TryGetProperty("color", out _), "Gradient=false fill must not emit a gradient color object");
    }

    [Fact]
    public void GradientFill_Emits_Vertical_Linear_Gradient_To_Transparent()
    {
        var cut = _ctx.Render<L.AreaChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b", "c" })
            .Add(c => c.Series, OneSeries())
            .Add(c => c.GradientFill, true));

        var areaStyle = FirstAreaStyle(cut);
        Assert.True(areaStyle.TryGetProperty("color", out var color), "Expected areaStyle.color gradient object");
        Assert.Equal("linear", color.GetProperty("type").GetString());
        // Vertical: top (y=0) → bottom (y2=1).
        Assert.Equal(0, color.GetProperty("y").GetDouble());
        Assert.Equal(1, color.GetProperty("y2").GetDouble());

        var stops = color.GetProperty("colorStops");
        Assert.Equal(2, stops.GetArrayLength());
        Assert.Equal(0, stops[0].GetProperty("offset").GetDouble());
        // First series with no palette → chart-1 theme token at the top.
        Assert.Equal("var(--color-chart-1)", stops[0].GetProperty("color").GetString());
        Assert.Equal(1, stops[1].GetProperty("offset").GetDouble());
        Assert.Equal("transparent", stops[1].GetProperty("color").GetString());

        // Pure gradient fill — no leftover flat opacity.
        Assert.False(areaStyle.TryGetProperty("opacity", out _));
    }

    [Fact]
    public void GradientFill_Uses_Provided_Palette_Colour_At_Top_Stop()
    {
        var cut = _ctx.Render<L.AreaChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b", "c" })
            .Add(c => c.Series, OneSeries())
            .Add(c => c.GradientFill, true)
            .Add(c => c.Colors, new List<string> { "#ff0000" }));

        var areaStyle = FirstAreaStyle(cut);
        var top = areaStyle.GetProperty("color").GetProperty("colorStops")[0];
        Assert.Equal("#ff0000", top.GetProperty("color").GetString());
    }

    [Fact]
    public void GradientStops_Override_Is_Applied_And_Implies_GradientFill()
    {
        // Custom stops must flow through verbatim even without setting GradientFill.
        var cut = _ctx.Render<L.AreaChart>(p => p
            .Add(c => c.Categories, new List<string> { "a", "b", "c" })
            .Add(c => c.Series, OneSeries())
            .Add(c => c.GradientStops, new List<L.EChartGradientColorStop>
            {
                new(0, "var(--color-primary)"),
                new(0.5, "#00ff00"),
                new(1, "transparent"),
            }));

        var areaStyle = FirstAreaStyle(cut);
        var stops = areaStyle.GetProperty("color").GetProperty("colorStops");
        Assert.Equal(3, stops.GetArrayLength());
        Assert.Equal("var(--color-primary)", stops[0].GetProperty("color").GetString());
        Assert.Equal(0.5, stops[1].GetProperty("offset").GetDouble());
        Assert.Equal("#00ff00", stops[1].GetProperty("color").GetString());
    }
}
