using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Progress;

public class ProgressAnimatedTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ProgressAnimatedTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Animation_Defaults_To_None_And_Omits_Stripe_Class()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 50));

        Assert.DoesNotContain("lumeo-progress-stripes", cut.Markup);
    }

    [Fact]
    public void Animation_Stripe_Adds_Stripe_Class_To_Indicator()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 50)
            .Add(b => b.Animation, Lumeo.Progress.ProgressAnimation.Stripe));

        Assert.Contains("lumeo-progress-stripes", cut.Markup);
    }

    [Fact]
    public void Animation_Stripe_Coexists_With_Variant()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 75)
            .Add(b => b.Variant, Lumeo.Progress.ProgressVariant.Success)
            .Add(b => b.Animation, Lumeo.Progress.ProgressAnimation.Stripe));

        Assert.Contains("lumeo-progress-stripes", cut.Markup);
        Assert.Contains("bg-success", cut.Markup);
    }
}
