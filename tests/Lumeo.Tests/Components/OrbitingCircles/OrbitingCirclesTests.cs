using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.OrbitingCircles;

public class OrbitingCirclesTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public OrbitingCirclesTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_root_div_with_class()
    {
        var cut = _ctx.Render<Lumeo.OrbitingCircles>();
        Assert.Contains("lumeo-orbiting-circles", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Applies_size_via_style()
    {
        var cut = _ctx.Render<Lumeo.OrbitingCircles>(p => p
            .Add(c => c.Size, 300));
        Assert.Contains("width:300px", cut.Find("div").GetAttribute("style"));
        Assert.Contains("height:300px", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void Renders_child_content_in_center()
    {
        var cut = _ctx.Render<Lumeo.OrbitingCircles>(p => p
            .AddChildContent("<span data-testid='center'>Logo</span>"));
        Assert.NotNull(cut.Find("[data-testid='center']"));
    }

    [Fact]
    public void Custom_class_appended()
    {
        var cut = _ctx.Render<Lumeo.OrbitingCircles>(p => p
            .Add(c => c.Class, "custom-oc"));
        Assert.Contains("custom-oc", cut.Find("div").GetAttribute("class"));
    }
}
