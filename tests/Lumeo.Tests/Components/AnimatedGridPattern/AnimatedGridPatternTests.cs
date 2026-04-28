using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.AnimatedGridPattern;

public class AnimatedGridPatternTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AnimatedGridPatternTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_root_div_with_class()
    {
        var cut = _ctx.Render<Lumeo.AnimatedGridPattern>();
        Assert.Contains("lumeo-animated-grid", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Renders_svg_element()
    {
        var cut = _ctx.Render<Lumeo.AnimatedGridPattern>();
        Assert.NotNull(cut.Find("svg"));
    }

    [Fact]
    public void Custom_class_appended()
    {
        var cut = _ctx.Render<Lumeo.AnimatedGridPattern>(p => p
            .Add(c => c.Class, "bg-grid"));
        Assert.Contains("bg-grid", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Aria_hidden_set()
    {
        var cut = _ctx.Render<Lumeo.AnimatedGridPattern>();
        Assert.Equal("true", cut.Find("div").GetAttribute("aria-hidden"));
    }
}
