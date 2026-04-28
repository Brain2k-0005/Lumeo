using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.RetroGrid;

public class RetroGridTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public RetroGridTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_root_with_class()
    {
        var cut = _ctx.Render<Lumeo.RetroGrid>();
        Assert.Contains("lumeo-retro-grid", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Applies_angle_css_variable()
    {
        var cut = _ctx.Render<Lumeo.RetroGrid>(p => p
            .Add(c => c.Angle, 45));
        Assert.Contains("--lumeo-grid-angle:45deg", cut.Markup);
    }

    [Fact]
    public void Renders_child_content()
    {
        var cut = _ctx.Render<Lumeo.RetroGrid>(p => p
            .AddChildContent("<h1>Hero</h1>"));
        Assert.NotNull(cut.Find("h1"));
    }

    [Fact]
    public void Custom_class_appended()
    {
        var cut = _ctx.Render<Lumeo.RetroGrid>(p => p
            .Add(c => c.Class, "custom-rg"));
        Assert.Contains("custom-rg", cut.Find("div").GetAttribute("class"));
    }
}
