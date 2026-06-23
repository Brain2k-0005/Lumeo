using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Skeleton;

/// <summary>
/// Behaviour / accessibility tier for <see cref="L.Skeleton"/>. The existing
/// SkeletonTests cover the animation-class matrix and class merging; this file
/// focuses on the screen-reader contract (role / aria-busy / aria-label) and the
/// distinct markup the Wave animation branch emits (keyframes + gradient +
/// inline animation style), neither of which is exercised elsewhere.
/// </summary>
public class SkeletonBehaviorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SkeletonBehaviorTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- Accessibility contract (default / Pulse branch) ---

    [Fact]
    public void Exposes_Status_Role_And_AriaBusy_For_AssistiveTech()
    {
        var cut = _ctx.Render<L.Skeleton>();

        var div = cut.Find("div");
        Assert.Equal("status", div.GetAttribute("role"));
        Assert.Equal("true", div.GetAttribute("aria-busy"));
    }

    [Fact]
    public void Defaults_AriaLabel_To_Loading_When_Not_Specified()
    {
        var cut = _ctx.Render<L.Skeleton>();

        var div = cut.Find("div");
        Assert.Equal("Loading", div.GetAttribute("aria-label"));
    }

    [Fact]
    public void Uses_Provided_AriaLabel_For_Localized_Announcement()
    {
        var cut = _ctx.Render<L.Skeleton>(p => p
            .Add(s => s.AriaLabel, "Lädt Inhalte"));

        var div = cut.Find("div");
        Assert.Equal("Lädt Inhalte", div.GetAttribute("aria-label"));
    }

    [Fact]
    public void Empty_AriaLabel_Falls_Back_To_Loading_Default()
    {
        var cut = _ctx.Render<L.Skeleton>(p => p
            .Add(s => s.AriaLabel, ""));

        var div = cut.Find("div");
        Assert.Equal("Loading", div.GetAttribute("aria-label"));
    }

    // --- Wave branch: accessibility contract is preserved AND distinct markup is emitted ---

    [Fact]
    public void Wave_Preserves_AccessibilityContract_And_Emits_Gradient_Animation()
    {
        var cut = _ctx.Render<L.Skeleton>(p => p
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

    // --- None branch: still announces, just without an animation utility class ---

    [Fact]
    public void None_Animation_Still_Announces_As_Busy_Status()
    {
        var cut = _ctx.Render<L.Skeleton>(p => p
            .Add(s => s.Animation, L.Skeleton.SkeletonAnimation.None));

        var div = cut.Find("div");
        Assert.Equal("status", div.GetAttribute("role"));
        Assert.Equal("true", div.GetAttribute("aria-busy"));

        var cls = div.GetAttribute("class") ?? "";
        Assert.DoesNotContain("animate-pulse", cls);
        // Base placeholder styling is retained even with animation disabled.
        Assert.Contains("rounded-md", cls);
        Assert.Contains("bg-primary/10", cls);
    }
}
