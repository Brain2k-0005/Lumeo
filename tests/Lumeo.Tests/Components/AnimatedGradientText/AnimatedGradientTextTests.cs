using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.AnimatedGradientText;

public class AnimatedGradientTextTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AnimatedGradientTextTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Span_With_GradientText_Class()
    {
        var cut = _ctx.Render<Lumeo.AnimatedGradientText>();

        Assert.Contains("lumeo-animated-gradient-text", cut.Find("span").GetAttribute("class"));
    }

    [Fact]
    public void Custom_Gradient_Sets_CSS_Variable()
    {
        var cut = _ctx.Render<Lumeo.AnimatedGradientText>(p => p
            .Add(a => a.Gradient, "red, blue, red"));

        Assert.Contains("--lumeo-agt-gradient: red, blue, red", cut.Find("span").GetAttribute("style"));
    }

    [Fact]
    public void AnimationDuration_Sets_CSS_Variable()
    {
        var cut = _ctx.Render<Lumeo.AnimatedGradientText>(p => p
            .Add(a => a.AnimationDuration, "6s"));

        Assert.Contains("--lumeo-agt-duration: 6s", cut.Find("span").GetAttribute("style"));
    }

    [Fact]
    public void Renders_ChildContent()
    {
        var cut = _ctx.Render<Lumeo.AnimatedGradientText>(p => p
            .AddChildContent("Hello World"));

        Assert.Contains("Hello World", cut.Markup);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.AnimatedGradientText>(p => p
            .Add(a => a.Class, "text-4xl"));

        Assert.Contains("text-4xl", cut.Find("span").GetAttribute("class"));
    }
}
