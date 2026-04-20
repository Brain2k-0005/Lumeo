using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Switch;

public class SwitchAnimatedTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SwitchAnimatedTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Animated_False_Is_Default_And_Omits_Spring_Class()
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p.Add(s => s.Checked, true));

        Assert.DoesNotContain("lumeo-switch-spring", cut.Markup);
    }

    [Fact]
    public void Animated_True_Adds_Spring_Class_To_Thumb()
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p
            .Add(s => s.Animated, true)
            .Add(s => s.Checked, true));

        Assert.Contains("lumeo-switch-spring", cut.Markup);
    }

    [Fact]
    public void Animated_True_Does_Not_Break_Checked_Toggle_Rendering()
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p
            .Add(s => s.Animated, true)
            .Add(s => s.Checked, true));

        var button = cut.Find("button[role='switch']");
        Assert.Equal("true", button.GetAttribute("aria-checked"));
    }
}
