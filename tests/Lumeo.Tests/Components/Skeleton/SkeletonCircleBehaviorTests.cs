using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Skeleton;

/// <summary>
/// Accessibility tier for <see cref="L.SkeletonCircle"/>. SkeletonCircle is a
/// loading placeholder just like <see cref="L.Skeleton"/> and must expose the
/// same screen-reader contract (role=status / aria-busy / aria-label) so that
/// an avatar/icon placeholder is announced as busy rather than silently omitted.
/// These mirror SkeletonBehaviorTests and reproduce battle-wave3 finding #55.
/// </summary>
public class SkeletonCircleBehaviorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SkeletonCircleBehaviorTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- Accessibility contract (default / Pulse branch) ---

    [Fact]
    public void Exposes_Status_Role_And_AriaBusy_For_AssistiveTech()
    {
        var cut = _ctx.Render<L.SkeletonCircle>();

        var div = cut.Find("div");
        Assert.Equal("status", div.GetAttribute("role"));
        Assert.Equal("true", div.GetAttribute("aria-busy"));
    }

    [Fact]
    public void Defaults_AriaLabel_To_Loading_When_Not_Specified()
    {
        var cut = _ctx.Render<L.SkeletonCircle>();

        var div = cut.Find("div");
        Assert.Equal("Loading", div.GetAttribute("aria-label"));
    }

    [Fact]
    public void Uses_Provided_AriaLabel_For_Localized_Announcement()
    {
        var cut = _ctx.Render<L.SkeletonCircle>(p => p
            .Add(s => s.AriaLabel, "Lädt Avatar"));

        var div = cut.Find("div");
        Assert.Equal("Lädt Avatar", div.GetAttribute("aria-label"));
    }

    [Fact]
    public void Empty_AriaLabel_Falls_Back_To_Loading_Default()
    {
        var cut = _ctx.Render<L.SkeletonCircle>(p => p
            .Add(s => s.AriaLabel, ""));

        var div = cut.Find("div");
        Assert.Equal("Loading", div.GetAttribute("aria-label"));
    }

    // --- Wave branch: accessibility contract is preserved on the distinct gradient markup ---

    [Fact]
    public void Wave_Preserves_AccessibilityContract_And_Emits_Gradient_Animation()
    {
        var cut = _ctx.Render<L.SkeletonCircle>(p => p
            .Add(s => s.Animation, L.Skeleton.SkeletonAnimation.Wave)
            .Add(s => s.AriaLabel, "Wird geladen"));

        var div = cut.Find("div");

        // Same screen-reader contract regardless of which animation branch renders.
        Assert.Equal("status", div.GetAttribute("role"));
        Assert.Equal("true", div.GetAttribute("aria-busy"));
        Assert.Equal("Wird geladen", div.GetAttribute("aria-label"));

        // Wave uses a moving gradient driven by an inline animation referencing the
        // skeleton-wave keyframes (instead of the Tailwind animate-pulse utility).
        var cls = div.GetAttribute("class") ?? "";
        Assert.Contains("bg-gradient-to-r", cls);

        var style = div.GetAttribute("style") ?? "";
        Assert.Contains("skeleton-wave", style);
    }
}
