using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native;

public class NativeCandlestickChartTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        var module = _ctx.JSInterop.SetupModule("./_content/Lumeo.Charts/js/chart-interop.js");
        module.Mode = Bunit.JSRuntimeMode.Loose;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static async Task<string> HoverAsync(Bunit.IRenderedComponent<L.NativeCandlestickChart> cut, int index)
    {
        var surface = cut.FindComponent<L.ChartInteractionSurface>();
        await surface.InvokeAsync(() => surface.Instance.OnChartPointerIndex(index));
        return cut.Find(".lumeo-chart-tooltip-host").TextContent;
    }

    // Data order is [open, close, low, high] — matches the legacy ECharts wrapper's
    // own documented convention (CandlestickChartPage.razor's API table), preserved
    // exactly rather than silently reordered.
    [Fact]
    public void Up_Candle_Close_GreaterOrEqual_Open_Uses_The_Up_Color()
    {
        var cut = _ctx.Render<L.NativeCandlestickChart>(p => p
            .Add(b => b.Categories, new List<string> { "Day1" })
            .Add(b => b.Data, new List<double[]> { new[] { 100.0, 110.0, 95.0, 115.0 } }));

        var body = cut.Find("svg rect");
        Assert.Equal("var(--color-success)", body.GetAttribute("fill"));
    }

    [Fact]
    public void Down_Candle_Close_Less_Than_Open_Uses_The_Down_Color()
    {
        var cut = _ctx.Render<L.NativeCandlestickChart>(p => p
            .Add(b => b.Categories, new List<string> { "Day1" })
            .Add(b => b.Data, new List<double[]> { new[] { 110.0, 100.0, 95.0, 115.0 } }));

        var body = cut.Find("svg rect");
        Assert.Equal("var(--color-destructive)", body.GetAttribute("fill"));
    }

    [Fact]
    public void Doji_Open_Equals_Close_Still_Renders_A_Visible_Body_Not_A_Zero_Height_Rect()
    {
        // A real, documented case (task instructions): open == close must not
        // collapse to an invisible 0-height rect — it renders as a thin (>=1px) line.
        var cut = _ctx.Render<L.NativeCandlestickChart>(p => p
            .Add(b => b.Categories, new List<string> { "Doji" })
            .Add(b => b.Data, new List<double[]> { new[] { 100.0, 100.0, 90.0, 110.0 } }));

        var body = cut.Find("svg rect");
        var height = double.Parse(body.GetAttribute("height")!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(height >= 1, $"Expected a non-zero (>=1px) body height for a doji candle, got {height}");
    }

    [Fact]
    public async Task Wick_Spans_From_The_High_To_The_Low_Not_The_Open_Close_Range()
    {
        var cut = _ctx.Render<L.NativeCandlestickChart>(p => p
            .Add(b => b.Categories, new List<string> { "Day1" })
            .Add(b => b.Data, new List<double[]> { new[] { 100.0, 105.0, 80.0, 130.0 } }));

        var tooltip = await HoverAsync(cut, 0);
        Assert.Contains("High 130", tooltip);
        Assert.Contains("Low 80", tooltip);
        Assert.Contains("Open 100", tooltip);
        Assert.Contains("Close 105", tooltip);
    }

    [Fact]
    public async Task Change_Percent_Is_Computed_From_Open_To_Close()
    {
        // (110/100 - 1) * 100 = +10%
        var cut = _ctx.Render<L.NativeCandlestickChart>(p => p
            .Add(b => b.Categories, new List<string> { "Day1" })
            .Add(b => b.Data, new List<double[]> { new[] { 100.0, 110.0, 95.0, 115.0 } }));

        var tooltip = await HoverAsync(cut, 0);
        Assert.Contains("+10%", tooltip);
    }

    [Fact]
    public void Custom_Colors_Override_The_Default_Semantic_Up_Down_Tokens()
    {
        var cut = _ctx.Render<L.NativeCandlestickChart>(p => p
            .Add(b => b.Categories, new List<string> { "Day1" })
            .Add(b => b.Data, new List<double[]> { new[] { 100.0, 110.0, 95.0, 115.0 } })
            .Add(b => b.Colors, new List<string> { "#10b981", "#ef4444" }));

        var body = cut.Find("svg rect");
        Assert.Equal("#10b981", body.GetAttribute("fill"));
    }

    [Fact]
    public void DataZoom_Renders_A_Zoom_Slider_Only_When_Requested()
    {
        var categories = new List<string> { "A", "B", "C" };
        var data = categories.Select(_ => new[] { 100.0, 105.0, 95.0, 110.0 }).ToList();

        var without = _ctx.Render<L.NativeCandlestickChart>(p => p.Add(b => b.Categories, categories).Add(b => b.Data, data));
        Assert.Empty(without.FindAll(".lumeo-chart-zoom-slider"));

        var with = _ctx.Render<L.NativeCandlestickChart>(p => p.Add(b => b.Categories, categories).Add(b => b.Data, data).Add(b => b.DataZoom, true));
        Assert.Single(with.FindAll(".lumeo-chart-zoom-slider"));
    }

    [Fact]
    public void Accessibility_Table_Has_An_OHLC_Column_Per_Category()
    {
        var cut = _ctx.Render<L.NativeCandlestickChart>(p => p
            .Add(b => b.Categories, new List<string> { "Day1" })
            .Add(b => b.Data, new List<double[]> { new[] { 100.0, 110.0, 95.0, 115.0 } }));

        var headers = cut.FindAll("table.sr-only thead th").Select(h => h.TextContent).ToList();
        Assert.Equal(new[] { "Category", "Open", "High", "Low", "Close" }, headers);
    }

    [Fact]
    public void Focus_Host_Keyboard_Navigation_Reveals_The_Next_Candle_Via_ArrowRight()
    {
        var cut = _ctx.Render<L.NativeCandlestickChart>(p => p
            .Add(b => b.Categories, new List<string> { "Day1", "Day2" })
            .Add(b => b.Data, new List<double[]>
            {
                new[] { 100.0, 110.0, 95.0, 115.0 },
                new[] { 110.0, 90.0, 85.0, 120.0 },
            }));

        var host = cut.Find("rect[tabindex]");
        cut.InvokeAsync(() => host.KeyDown("ArrowRight"));

        var tooltip = cut.Find(".lumeo-chart-tooltip-host").TextContent;
        Assert.Contains("Day1", tooltip);
    }
}
