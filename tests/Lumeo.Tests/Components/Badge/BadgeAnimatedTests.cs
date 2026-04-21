using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Badge;

public class BadgeAnimatedTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BadgeAnimatedTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Animated_False_Is_Default_And_Omits_Animated_Class()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p.AddChildContent("Hi"));

        Assert.DoesNotContain("lumeo-badge-animated", cut.Find("div").GetAttribute("class") ?? "");
    }

    [Fact]
    public void Animated_True_Adds_Animated_Class()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .Add(b => b.Animated, true)
            .AddChildContent("New"));

        Assert.Contains("lumeo-badge-animated", cut.Find("div").GetAttribute("class") ?? "");
    }

    [Fact]
    public void Animated_True_IsDot_Still_Renders_Simple_Div()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .Add(b => b.Animated, true)
            .Add(b => b.IsDot, true));

        var cls = cut.Find("div").GetAttribute("class") ?? "";
        Assert.Contains("lumeo-badge-animated", cls);
        Assert.Contains("rounded-full", cls);
    }
}
