using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Sparkline;

public class SparklineTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SparklineTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Svg_With_ViewBox()
    {
        var cut = _ctx.Render<Lumeo.Sparkline>(p => p
            .Add(s => s.Values, new double[] { 1, 2, 3, 4 })
            .Add(s => s.Height, 32));

        var svg = cut.Find("svg");
        Assert.Equal("0 0 100 32", svg.GetAttribute("viewBox"));
        Assert.Equal("img", svg.GetAttribute("role"));
    }

    [Fact]
    public void Values_Null_Renders_Baseline_Rect()
    {
        var cut = _ctx.Render<Lumeo.Sparkline>();

        Assert.Empty(cut.FindAll("polyline"));
        Assert.Empty(cut.FindAll("polygon"));
        var rects = cut.FindAll("rect");
        Assert.Single(rects);
    }

    [Fact]
    public void Line_Type_Emits_Polyline_With_Expected_Point_Count()
    {
        var data = new double[] { 1, 3, 2, 5, 4 };
        var cut = _ctx.Render<Lumeo.Sparkline>(p => p
            .Add(s => s.Values, data)
            .Add(s => s.Type, Lumeo.Sparkline.SparkType.Line));

        var poly = cut.Find("polyline");
        var points = poly.GetAttribute("points") ?? string.Empty;
        var parts = points.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(data.Length, parts.Length);
    }

    [Fact]
    public void Area_Type_Emits_Polygon()
    {
        var cut = _ctx.Render<Lumeo.Sparkline>(p => p
            .Add(s => s.Values, new double[] { 1, 2, 3, 4 })
            .Add(s => s.Type, Lumeo.Sparkline.SparkType.Area));

        Assert.NotNull(cut.Find("polygon"));
    }

    [Fact]
    public void Bars_Type_Emits_N_Rects()
    {
        var data = new double[] { 1, 2, 3, 4, 5 };
        var cut = _ctx.Render<Lumeo.Sparkline>(p => p
            .Add(s => s.Values, data)
            .Add(s => s.Type, Lumeo.Sparkline.SparkType.Bars));

        var rects = cut.FindAll("rect");
        Assert.Equal(data.Length, rects.Count);
    }

    [Fact]
    public void ShowLast_Emits_Circle()
    {
        var cut = _ctx.Render<Lumeo.Sparkline>(p => p
            .Add(s => s.Values, new double[] { 1, 2, 3 })
            .Add(s => s.ShowLast, true));

        Assert.NotNull(cut.Find("circle"));
    }

    // Regression (battle-wave3 #57, edge-data): a single data point used to render an
    // invisible Line/Area sparkline — the polyline had a single coordinate ("50,16") so
    // no line segment was drawn, and the Area polygon collapsed to zero width ("50,32
    // 50,16 50,32"). The fix emits a flat full-width line for n == 1.
    [Fact]
    public void Line_SinglePoint_Emits_Visible_Flat_Polyline()
    {
        var cut = _ctx.Render<Lumeo.Sparkline>(p => p
            .Add(s => s.Values, new double[] { 5 })
            .Add(s => s.Type, Lumeo.Sparkline.SparkType.Line));

        var poly = cut.Find("polyline");
        var points = poly.GetAttribute("points") ?? string.Empty;
        var parts = points.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // One coordinate pair = no visible segment; the fix yields >= 2 points.
        Assert.True(parts.Length >= 2, $"Expected a visible line (>=2 points) but got '{points}'");
    }

    [Fact]
    public void Area_SinglePoint_Emits_NonDegenerate_Polygon()
    {
        var cut = _ctx.Render<Lumeo.Sparkline>(p => p
            .Add(s => s.Values, new double[] { 5 })
            .Add(s => s.Type, Lumeo.Sparkline.SparkType.Area));

        var poly = cut.Find("polygon");
        var points = poly.GetAttribute("points") ?? string.Empty;
        var distinctX = points
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(pt => pt.Split(',')[0])
            .Distinct()
            .Count();
        // Before the fix every point shared x=50 (zero-width, invisible polygon).
        Assert.True(distinctX >= 2, $"Expected the area polygon to span width but got '{points}'");
    }

    [Fact]
    public void Custom_Color_Propagates_To_Stroke()
    {
        var cut = _ctx.Render<Lumeo.Sparkline>(p => p
            .Add(s => s.Values, new double[] { 1, 2, 3 })
            .Add(s => s.Color, "#ff0000"));

        var poly = cut.Find("polyline");
        Assert.Equal("#ff0000", poly.GetAttribute("stroke"));
    }
}
