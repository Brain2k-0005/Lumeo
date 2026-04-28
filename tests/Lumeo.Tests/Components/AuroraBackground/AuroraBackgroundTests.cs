using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.AuroraBackground;

public class AuroraBackgroundTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AuroraBackgroundTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_root_with_aurora_class()
    {
        var cut = _ctx.Render<Lumeo.AuroraBackground>();
        Assert.Contains("lumeo-aurora", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Renders_three_layers()
    {
        var cut = _ctx.Render<Lumeo.AuroraBackground>();
        Assert.Equal(3, cut.FindAll(".lumeo-aurora-layer").Count);
    }

    [Fact]
    public void Renders_child_content()
    {
        var cut = _ctx.Render<Lumeo.AuroraBackground>(p => p
            .AddChildContent("<p data-testid='hero'>Hello</p>"));
        Assert.NotNull(cut.Find("[data-testid='hero']"));
    }

    [Fact]
    public void Custom_class_appended()
    {
        var cut = _ctx.Render<Lumeo.AuroraBackground>(p => p
            .Add(c => c.Class, "my-aurora"));
        Assert.Contains("my-aurora", cut.Find("div").GetAttribute("class"));
    }
}
