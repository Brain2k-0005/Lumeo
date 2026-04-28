using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.BackgroundBeams;

public class BackgroundBeamsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BackgroundBeamsTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_root_with_class()
    {
        var cut = _ctx.Render<Lumeo.BackgroundBeams>();
        Assert.Contains("lumeo-background-beams", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Renders_svg_with_beams()
    {
        var cut = _ctx.Render<Lumeo.BackgroundBeams>(p => p
            .Add(c => c.Count, 5));
        Assert.Equal(5, cut.FindAll(".lumeo-beam").Count);
    }

    [Fact]
    public void Renders_child_content()
    {
        var cut = _ctx.Render<Lumeo.BackgroundBeams>(p => p
            .AddChildContent("<span data-testid='c'>Hero</span>"));
        Assert.NotNull(cut.Find("[data-testid='c']"));
    }

    [Fact]
    public void Custom_class_appended()
    {
        var cut = _ctx.Render<Lumeo.BackgroundBeams>(p => p
            .Add(c => c.Class, "hero-beams"));
        Assert.Contains("hero-beams", cut.Find("div").GetAttribute("class"));
    }
}
