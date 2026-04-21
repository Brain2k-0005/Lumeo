using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Checkbox;

public class CheckboxAnimatedTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CheckboxAnimatedTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Animated_False_Is_Default_And_Omits_Animated_Class()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p.Add(c => c.Checked, true));

        Assert.DoesNotContain("lumeo-checkbox-animated", cut.Markup);
    }

    [Fact]
    public void Animated_True_Checked_Adds_Animated_Class()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(c => c.Animated, true)
            .Add(c => c.Checked, true));

        Assert.Contains("lumeo-checkbox-animated", cut.Markup);
    }

    [Fact]
    public void Animated_True_Unchecked_Does_Not_Add_Animated_Class()
    {
        // Only animate on check — an un-checked animated checkbox stays neutral.
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(c => c.Animated, true)
            .Add(c => c.Checked, false));

        Assert.DoesNotContain("lumeo-checkbox-animated", cut.Markup);
    }
}
