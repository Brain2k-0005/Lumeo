using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.RingProgress;

public class RingProgressTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public RingProgressTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_SVG_Element()
    {
        var cut = _ctx.Render<Lumeo.RingProgress>(p => p
            .Add(r => r.Value, 50));

        Assert.NotNull(cut.Find("svg"));
    }

    [Fact]
    public void Has_Progressbar_Role()
    {
        var cut = _ctx.Render<Lumeo.RingProgress>(p => p
            .Add(r => r.Value, 60));

        Assert.NotNull(cut.Find("[role='progressbar']"));
    }

    [Fact]
    public void ARIA_Attributes_Are_Correct()
    {
        var cut = _ctx.Render<Lumeo.RingProgress>(p => p
            .Add(r => r.Value, 42));

        var el = cut.Find("[role='progressbar']");
        Assert.Equal("42", el.GetAttribute("aria-valuenow"));
        Assert.Equal("0", el.GetAttribute("aria-valuemin"));
        Assert.Equal("100", el.GetAttribute("aria-valuemax"));
    }

    [Fact]
    public void ShowLabel_Displays_Percentage()
    {
        var cut = _ctx.Render<Lumeo.RingProgress>(p => p
            .Add(r => r.Value, 75)
            .Add(r => r.ShowLabel, true));

        Assert.Contains("75%", cut.Markup);
    }

    [Fact]
    public void ShowLabel_False_Hides_Text()
    {
        var cut = _ctx.Render<Lumeo.RingProgress>(p => p
            .Add(r => r.Value, 75)
            .Add(r => r.ShowLabel, false));

        Assert.DoesNotContain("75%", cut.Markup);
    }

    [Fact]
    public void Custom_Class_Is_Applied()
    {
        var cut = _ctx.Render<Lumeo.RingProgress>(p => p
            .Add(r => r.Value, 50)
            .Add(r => r.Class, "my-ring"));

        Assert.Contains("my-ring", cut.Markup);
    }

    [Fact]
    public void Renders_Two_Circles()
    {
        var cut = _ctx.Render<Lumeo.RingProgress>(p => p
            .Add(r => r.Value, 50));

        var circles = cut.FindAll("circle");
        Assert.Equal(2, circles.Count);
    }

    [Fact]
    public void Value_Affects_Dashoffset()
    {
        var cut0 = _ctx.Render<Lumeo.RingProgress>(p => p
            .Add(r => r.Value, 0));
        var cut100 = _ctx.Render<Lumeo.RingProgress>(p => p
            .Add(r => r.Value, 100));

        var valueCircle0 = cut0.FindAll("circle")[1];
        var valueCircle100 = cut100.FindAll("circle")[1];

        var offset0 = valueCircle0.GetAttribute("stroke-dashoffset");
        var offset100 = valueCircle100.GetAttribute("stroke-dashoffset");

        Assert.NotEqual(offset0, offset100);
    }

    [Fact]
    public void RoundedCaps_True_Sets_Round_Linecap()
    {
        var cut = _ctx.Render<Lumeo.RingProgress>(p => p
            .Add(r => r.Value, 50)
            .Add(r => r.RoundedCaps, true));

        var valueCircle = cut.FindAll("circle")[1];
        Assert.Equal("round", valueCircle.GetAttribute("stroke-linecap"));
    }

    [Fact]
    public void RoundedCaps_False_Sets_Butt_Linecap()
    {
        var cut = _ctx.Render<Lumeo.RingProgress>(p => p
            .Add(r => r.Value, 50)
            .Add(r => r.RoundedCaps, false));

        var valueCircle = cut.FindAll("circle")[1];
        Assert.Equal("butt", valueCircle.GetAttribute("stroke-linecap"));
    }

    [Fact]
    public void LabelContent_RenderFragment_Is_Rendered()
    {
        var cut = _ctx.Render<Lumeo.RingProgress>(p => p
            .Add(r => r.Value, 50)
            .Add(r => r.LabelContent, b => b.AddMarkupContent(0, "<span>custom</span>")));

        Assert.Contains("custom", cut.Markup);
    }

    [Fact]
    public void Container_Has_Correct_Size_Style()
    {
        var cut = _ctx.Render<Lumeo.RingProgress>(p => p
            .Add(r => r.Value, 50)
            .Add(r => r.Size, 80));

        var container = cut.Find("[role='progressbar']");
        var style = container.GetAttribute("style") ?? "";
        Assert.Contains("80px", style);
    }
}
