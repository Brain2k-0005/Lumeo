using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.BottomNav;

public class BottomNavAnimatedTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BottomNavAnimatedTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void AnimatedIndicator_False_Is_Default_And_Omits_Animated_Class()
    {
        var cut = _ctx.Render<Lumeo.BottomNav>(p => p
            .AddChildContent<Lumeo.BottomNavItem>(item => item.Add(i => i.Label, "Home")));

        Assert.DoesNotContain("lumeo-bottom-nav-animated", cut.Markup);
    }

    [Fact]
    public void AnimatedIndicator_True_Cascades_To_Items()
    {
        var cut = _ctx.Render<Lumeo.BottomNav>(p => p
            .Add(b => b.AnimatedIndicator, true)
            .AddChildContent<Lumeo.BottomNavItem>(item => item.Add(i => i.Label, "Home"))
            .AddChildContent<Lumeo.BottomNavItem>(item => item.Add(i => i.Label, "Profile")));

        // The class should be present at least once per item
        var matches = System.Text.RegularExpressions.Regex.Matches(cut.Markup, "lumeo-bottom-nav-animated");
        Assert.True(matches.Count >= 2, $"Expected lumeo-bottom-nav-animated on each item; found {matches.Count}.");
    }

    [Fact]
    public void AnimatedIndicator_Does_Not_Break_Default_Item_Rendering()
    {
        var cut = _ctx.Render<Lumeo.BottomNav>(p => p
            .Add(b => b.AnimatedIndicator, true)
            .AddChildContent<Lumeo.BottomNavItem>(item => item.Add(i => i.Label, "Home")));

        // Existing base classes are still there.
        Assert.Contains("transition-colors", cut.Markup);
        Assert.Contains("Home", cut.Markup);
    }
}
