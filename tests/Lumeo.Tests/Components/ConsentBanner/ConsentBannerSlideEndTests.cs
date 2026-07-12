using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo.Services;
using Microsoft.Extensions.DependencyInjection;
using L = Lumeo;

namespace Lumeo.Tests.Components.ConsentBanner;

/// <summary>
/// Held-fill guard coverage for the banner's root <c>AnimationClass</c>
/// (default <c>animate-slide-in-from-bottom</c>): the class carries
/// <c>animation-fill-mode: both</c>, which never resolves on its own — the
/// same "Toast bug class" pattern already fixed for Sheet/Drawer/Dialog/
/// AlertDialog via <c>Interop.AttachOverlaySlideEnd</c>. Without the fix, a
/// transform-animating class stays live on the banner root forever,
/// permanently establishing a containing block for any position:fixed
/// descendant.
/// </summary>
public class ConsentBannerSlideEndTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ConsentBannerSlideEndTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<ConsentService>();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement ButtonWithText(IRenderedComponent<L.ConsentBanner> cut, string text)
        => cut.FindAll("button").Single(b => b.TextContent.Trim() == text);

    [Fact]
    public void Default_slide_in_animation_wires_AttachOverlaySlideEnd_on_the_banner_root()
    {
        var cut = _ctx.Render<L.ConsentBanner>();

        cut.WaitForAssertion(() => cut.Find("[role='region']"));
        var bannerId = cut.Find("[role='region']").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(bannerId));

        cut.WaitForAssertion(() =>
            Assert.Contains(_ctx.JSInterop.Invocations,
                i => i.Identifier == "attachOverlaySlideEnd"
                     && (i.Arguments[0] as string) == bannerId));
    }

    [Fact]
    public void Non_slide_AnimationClass_never_calls_AttachOverlaySlideEnd()
    {
        // A fade-only (or custom) AnimationClass has no transform to leave stuck —
        // and attachOverlaySlideEnd unconditionally stamps animation:none when it
        // finds no matching slide/zoom animation, which would cut a fade-in short.
        // The wiring must be gated so it never fires for these classes.
        var cut = _ctx.Render<L.ConsentBanner>(p => p
            .Add(b => b.AnimationClass, "animate-fade-in"));

        cut.WaitForAssertion(() => cut.Find("[role='region']"));

        Assert.DoesNotContain(_ctx.JSInterop.Invocations,
            i => i.Identifier == "attachOverlaySlideEnd");
    }

    [Fact]
    public void Dismissing_And_Reprompting_Rewires_AttachOverlaySlideEnd_For_The_New_Instance()
    {
        var svc = _ctx.Services.GetRequiredService<ConsentService>();
        var cut = _ctx.Render<L.ConsentBanner>();

        cut.WaitForAssertion(() => cut.Find("[role='region']"));
        var firstId = cut.Find("[role='region']").GetAttribute("id");
        cut.WaitForAssertion(() =>
            Assert.Contains(_ctx.JSInterop.Invocations,
                i => i.Identifier == "attachOverlaySlideEnd" && (i.Arguments[0] as string) == firstId));

        // Dismiss (unmounts the banner div — _slideEndWired resets), then wipe the
        // stored decision (e.g. a "change preferences" / version-bump re-prompt) —
        // HasDecided flips back to false and the SAME component instance remounts
        // the banner div, replaying the entrance animation, so the interop must be
        // re-armed rather than permanently skipped after the first wire.
        ButtonWithText(cut, "Accept all").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='region']")));

        cut.InvokeAsync(() => svc.ResetAsync());

        cut.WaitForAssertion(() => cut.Find("[role='region']"));
        var secondId = cut.Find("[role='region']").GetAttribute("id");
        Assert.Equal(firstId, secondId); // same stable id — LumeoIds.New is a field on the live component instance
        cut.WaitForAssertion(() =>
            Assert.Equal(2, _ctx.JSInterop.Invocations.Count(
                i => i.Identifier == "attachOverlaySlideEnd" && (i.Arguments[0] as string) == secondId)));
    }
}
