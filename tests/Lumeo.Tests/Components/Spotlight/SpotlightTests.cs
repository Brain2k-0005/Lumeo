using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Spotlight;

public class SpotlightTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SpotlightTests()
    {
        _ctx.AddLumeoServices();
        var motionModule = _ctx.JSInterop.SetupModule("./_content/Lumeo.Motion/js/motion.js");
        motionModule.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Div_With_Spotlight_Class()
    {
        var cut = _ctx.Render<Lumeo.Spotlight>();

        Assert.Contains("lumeo-spotlight", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Renders_ChildContent()
    {
        var cut = _ctx.Render<Lumeo.Spotlight>(p => p
            .AddChildContent("<p>Content</p>"));

        Assert.NotNull(cut.Find("p"));
    }

    [Fact]
    public void Custom_Color_Sets_CSS_Variable()
    {
        var cut = _ctx.Render<Lumeo.Spotlight>(p => p
            .Add(s => s.Color, "oklch(0.7 0.2 200)"));

        Assert.Contains("--lumeo-spotlight-color: oklch(0.7 0.2 200)", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void Custom_Radius_Sets_CSS_Variable()
    {
        var cut = _ctx.Render<Lumeo.Spotlight>(p => p
            .Add(s => s.Radius, 400));

        Assert.Contains("--lumeo-spotlight-radius: 400px", cut.Find("div").GetAttribute("style"));
    }
}
