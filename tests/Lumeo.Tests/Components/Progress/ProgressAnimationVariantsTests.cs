using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Progress;

/// <summary>
/// Tests for the new <see cref="Progress.ProgressAnimation"/> values
/// added in the motion-polish pass: Indeterminate and Glow.
/// </summary>
public class ProgressAnimationVariantsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ProgressAnimationVariantsTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Animation_Indeterminate_Adds_Indeterminate_Class()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Animation, Lumeo.Progress.ProgressAnimation.Indeterminate));

        Assert.Contains("lumeo-progress-indeterminate", cut.Markup);
    }

    [Fact]
    public void Animation_Indeterminate_Ignores_Value_And_Uses_Transparent_Fill()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 50)
            .Add(b => b.Animation, Lumeo.Progress.ProgressAnimation.Indeterminate));

        // The indicator must stretch to 100% width and have a transparent
        // background so only the sliding ::before shows.
        Assert.Contains("width: 100%", cut.Markup);
        Assert.Contains("background: transparent", cut.Markup);
    }

    [Fact]
    public void Animation_Glow_Adds_Glow_Class()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 70)
            .Add(b => b.Animation, Lumeo.Progress.ProgressAnimation.Glow));

        Assert.Contains("lumeo-progress-glow", cut.Markup);
    }

    [Fact]
    public void Animation_Glow_Respects_Value_Width()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 40)
            .Add(b => b.Animation, Lumeo.Progress.ProgressAnimation.Glow));

        // Glow must NOT hijack the fill width — value should still be applied.
        Assert.Contains("width: 40%", cut.Markup);
    }

    [Fact]
    public void Animation_Default_None_Omits_New_Classes()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p.Add(b => b.Value, 50));

        Assert.DoesNotContain("lumeo-progress-indeterminate", cut.Markup);
        Assert.DoesNotContain("lumeo-progress-glow", cut.Markup);
    }

    [Fact]
    public void Animation_Glow_Coexists_With_Variant_Color()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 60)
            .Add(b => b.Variant, Lumeo.Progress.ProgressVariant.Success)
            .Add(b => b.Animation, Lumeo.Progress.ProgressAnimation.Glow));

        Assert.Contains("lumeo-progress-glow", cut.Markup);
        Assert.Contains("bg-success", cut.Markup);
    }
}
