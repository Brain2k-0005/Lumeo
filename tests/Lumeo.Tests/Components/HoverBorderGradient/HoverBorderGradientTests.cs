using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.HoverBorderGradient;

public class HoverBorderGradientTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public HoverBorderGradientTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_root_div_with_class()
    {
        var cut = _ctx.Render<Lumeo.HoverBorderGradient>();
        Assert.Contains("lumeo-hover-border-gradient", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Renders_child_content()
    {
        var cut = _ctx.Render<Lumeo.HoverBorderGradient>(p => p
            .AddChildContent("<span data-testid='c'>Button</span>"));
        Assert.NotNull(cut.Find("[data-testid='c']"));
    }

    [Fact]
    public void Border_radius_applied_to_style()
    {
        var cut = _ctx.Render<Lumeo.HoverBorderGradient>(p => p
            .Add(c => c.BorderRadius, 16));
        Assert.Contains("border-radius:16px", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void Custom_class_appended()
    {
        var cut = _ctx.Render<Lumeo.HoverBorderGradient>(p => p
            .Add(c => c.Class, "hbg-x"));
        Assert.Contains("hbg-x", cut.Find("div").GetAttribute("class"));
    }
}
