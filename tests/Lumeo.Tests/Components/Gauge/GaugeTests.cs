using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Gauge;

public class GaugeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public GaugeTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_SVG_For_Radial_Variant()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 50));

        Assert.NotNull(cut.Find("svg"));
    }

    [Fact]
    public void Has_Meter_Role()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 40));

        Assert.NotNull(cut.Find("[role='meter']"));
    }

    [Fact]
    public void ARIA_Attributes_Are_Set_Correctly()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 75)
            .Add(g => g.Min, 0)
            .Add(g => g.Max, 100));

        var el = cut.Find("[role='meter']");
        Assert.Equal("75", el.GetAttribute("aria-valuenow"));
        Assert.Equal("0", el.GetAttribute("aria-valuemin"));
        Assert.Equal("100", el.GetAttribute("aria-valuemax"));
    }

    [Fact]
    public void ShowValue_True_Shows_Percentage()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 50)
            .Add(g => g.ShowValue, true));

        Assert.Contains("50%", cut.Markup);
    }

    [Fact]
    public void ShowValue_False_Does_Not_Render_Label_Span()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 50)
            .Add(g => g.ShowValue, false));

        // No visible span element should be rendered for the label
        Assert.Empty(cut.FindAll("span"));
    }

    [Fact]
    public void Custom_Label_Overrides_Percentage()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 80)
            .Add(g => g.Label, "CPU")
            .Add(g => g.ShowValue, true));

        Assert.Contains("CPU", cut.Markup);
    }

    [Fact]
    public void Custom_Class_Is_Applied()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 50)
            .Add(g => g.Class, "my-gauge"));

        Assert.Contains("my-gauge", cut.Markup);
    }

    [Fact]
    public void Linear_Variant_Does_Not_Render_Circle()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 60)
            .Add(g => g.Variant, Lumeo.Gauge.GaugeVariant.Linear));

        Assert.Empty(cut.FindAll("circle"));
    }

    [Fact]
    public void Linear_Variant_Renders_Rect_Elements()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 60)
            .Add(g => g.Variant, Lumeo.Gauge.GaugeVariant.Linear));

        Assert.NotEmpty(cut.FindAll("rect"));
    }

    [Fact]
    public void Radial_Variant_Renders_Circle_Elements()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 60)
            .Add(g => g.Variant, Lumeo.Gauge.GaugeVariant.Radial));

        Assert.NotEmpty(cut.FindAll("circle"));
    }

    [Fact]
    public void Arc_Variant_Has_Meter_Role()
    {
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 30)
            .Add(g => g.Variant, Lumeo.Gauge.GaugeVariant.Arc));

        Assert.NotNull(cut.Find("[role='meter']"));
    }

    [Fact]
    public void Arc_Value_Label_Is_Vertically_Centered_In_The_Arc_Band()
    {
        // The Arc band spans y in [Cy-Radius, Cy]; its vertical midpoint is
        // (Size+StrokeWidth)/4. The label is a `flex items-center` box of height
        // SvgHeight, so the bottom pad must be SvgHeight-(Size+StrokeWidth)/2 to
        // land the text centre exactly on that midpoint. Regression guard: the old
        // Size*0.3 pad (36px at the default size) pushed the value up near the apex.
        const int size = 120, stroke = 8;
        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 30)
            .Add(g => g.Variant, Lumeo.Gauge.GaugeVariant.Arc)
            .Add(g => g.Size, size)
            .Add(g => g.StrokeWidth, stroke));

        var svgHeight = (int)System.Math.Ceiling(size / 2.0 + stroke * 1.5); // 72
        var expectedPad = svgHeight - (size + stroke) / 2.0;                 // 8

        var label = cut.FindAll("span").Single(s => s.TextContent.Contains("30%"));
        var style = label.GetAttribute("style") ?? "";

        Assert.Contains(
            $"padding-bottom: {expectedPad.ToString(System.Globalization.CultureInfo.InvariantCulture)}px",
            style);
        // Must NOT regress to the old off-centre pad that scaled with Size.
        Assert.DoesNotContain($"padding-bottom: {(int)(size * 0.3)}px", style);
    }

    [Fact]
    public void Value_Affects_Stroke_Dasharray()
    {
        var cut25 = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 25)
            .Add(g => g.Variant, Lumeo.Gauge.GaugeVariant.Radial));

        var cut75 = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 75)
            .Add(g => g.Variant, Lumeo.Gauge.GaugeVariant.Radial));

        // Find the value arc (second circle)
        var circles25 = cut25.FindAll("circle");
        var circles75 = cut75.FindAll("circle");

        Assert.Equal(2, circles25.Count);
        Assert.Equal(2, circles75.Count);

        var valueDash25 = circles25[1].GetAttribute("stroke-dasharray");
        var valueDash75 = circles75[1].GetAttribute("stroke-dasharray");

        Assert.NotEqual(valueDash25, valueDash75);
    }

    [Fact]
    public void Threshold_Color_Applied_For_Value_Above_Threshold()
    {
        var thresholds = new List<Lumeo.Gauge.GaugeThreshold>
        {
            new(0, "var(--color-success)"),
            new(70, "var(--color-destructive)")
        };

        var cut = _ctx.Render<Lumeo.Gauge>(p => p
            .Add(g => g.Value, 90)
            .Add(g => g.Thresholds, thresholds));

        Assert.Contains("var(--color-destructive)", cut.Markup);
    }
}
