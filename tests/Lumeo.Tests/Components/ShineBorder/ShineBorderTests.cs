using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.ShineBorder;

public class ShineBorderTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ShineBorderTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_root_with_class()
    {
        var cut = _ctx.Render<Lumeo.ShineBorder>();
        Assert.Contains("lumeo-shine-border-wrap", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Renders_child_content()
    {
        var cut = _ctx.Render<Lumeo.ShineBorder>(p => p
            .AddChildContent("<p data-testid='c'>Card content</p>"));
        Assert.NotNull(cut.Find("[data-testid='c']"));
    }

    [Fact]
    public void Duration_applied_as_css_var()
    {
        var cut = _ctx.Render<Lumeo.ShineBorder>(p => p
            .Add(c => c.DurationMs, 5000));
        Assert.Contains("--lumeo-shine-duration:5000ms", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void Custom_class_appended()
    {
        var cut = _ctx.Render<Lumeo.ShineBorder>(p => p
            .Add(c => c.Class, "shine-x"));
        Assert.Contains("shine-x", cut.Find("div").GetAttribute("class"));
    }
}
