using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.MagicCard;

public class MagicCardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MagicCardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_root_div_with_class()
    {
        var cut = _ctx.Render<Lumeo.MagicCard>();
        Assert.Contains("lumeo-magic-card", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Renders_child_content()
    {
        var cut = _ctx.Render<Lumeo.MagicCard>(p => p
            .AddChildContent("<span data-testid='body'>Content</span>"));
        Assert.NotNull(cut.Find("[data-testid='body']"));
    }

    [Fact]
    public void SpotlightRadius_applied_as_css_var()
    {
        var cut = _ctx.Render<Lumeo.MagicCard>(p => p
            .Add(c => c.SpotlightRadius, 200));
        Assert.Contains("--lumeo-magic-radius:200px", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void Custom_class_appended()
    {
        var cut = _ctx.Render<Lumeo.MagicCard>(p => p
            .Add(c => c.Class, "magic-x"));
        Assert.Contains("magic-x", cut.Find("div").GetAttribute("class"));
    }
}
