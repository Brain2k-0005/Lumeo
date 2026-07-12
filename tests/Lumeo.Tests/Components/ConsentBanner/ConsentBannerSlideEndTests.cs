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

    // Regression for the "consumer id clobbers the generated one" bug: @attributes
    // used to be splatted AFTER the explicit id="@_bannerId" attribute, and Blazor
    // applies same-named attributes in source order — so a caller-supplied "id" in
    // AdditionalAttributes silently won, AttachOverlaySlideEnd(_bannerId) then
    // targeted a DOM id that didn't exist, and the held-fill guard never fired.
    [Fact]
    public void Consumer_supplied_id_via_AdditionalAttributes_does_not_break_slide_end_wiring()
    {
        var cut = _ctx.Render<L.ConsentBanner>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["id"] = "consumer-chosen-id" }));

        cut.WaitForAssertion(() => cut.Find("[role='region']"));
        var renderedId = cut.Find("[role='region']").GetAttribute("id");

        // The generated _bannerId must win over the consumer-supplied "id" — it's
        // load-bearing for the interop call below, not just cosmetic.
        Assert.NotEqual("consumer-chosen-id", renderedId);
        Assert.False(string.IsNullOrEmpty(renderedId));

        cut.WaitForAssertion(() =>
            Assert.Contains(_ctx.JSInterop.Invocations,
                i => i.Identifier == "attachOverlaySlideEnd"
                     && (i.Arguments[0] as string) == renderedId));
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

    // Regression coverage for the "wire before the element is mounted" bug: on the
    // very first OnAfterRenderAsync call, `_ready` flips false->true and
    // StateHasChanged() only QUEUES the follow-up render — it does not block until
    // that render commits. The buggy code fell straight through to the held-fill
    // guard in the SAME synchronous continuation and called AttachOverlaySlideEnd
    // against a `role="region"` element that had not been rendered yet (the render
    // that ran just before this OnAfterRenderAsync call still had `_ready == false`,
    // since it flips true INSIDE this very call). A JSInterop-invocation assertion
    // alone can't catch this — bUnit's mock records the call regardless of whether
    // a real element exists — so this test captures the component's OWN rendered
    // markup at the exact moment the interop call fires and asserts the target
    // element was already present, i.e. wiring happened strictly after the render
    // that mounts the banner, never in the same pass that flips `_ready`.
    //
    // Mounting via a host with the banner initially OFF (see ConsentBannerHost
    // below) — rather than `_ctx.Render<L.ConsentBanner>()` directly — matters for
    // the test's own plumbing, not the product: bUnit runs this component's whole
    // async lifecycle (EnsureLoadedAsync -> StateHasChanged -> the follow-up
    // render -> OnAfterRenderAsync again) SYNCHRONOUSLY inside the initial
    // Render() call, because every interop call here resolves an
    // already-completed task with no real yield. Rendering ConsentBanner
    // straight away would mean the interop's `Cut` handle — assigned from
    // Render()'s RETURN VALUE — isn't set until AFTER that whole cascade (and
    // any premature attach) has already run, making the check a false negative
    // regardless of whether the product bug is present. The host lets the test
    // grab a live, queryable handle on an earlier, trivially-synchronous render
    // (banner off — nothing async happens) and only THEN flip the banner on via
    // cut.Render(...), so `Cut` is already wired before ConsentBanner's own
    // lifecycle — and any interop call it makes — can fire.
    private sealed class ConsentBannerHost : Microsoft.AspNetCore.Components.ComponentBase
    {
        [Microsoft.AspNetCore.Components.Parameter] public bool ShowBanner { get; set; }
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            if (ShowBanner)
            {
                builder.OpenComponent<L.ConsentBanner>(0);
                builder.CloseComponent();
            }
        }
    }

    private sealed class MarkupCheckingInteropService : TrackingInteropService
    {
        // Assigned by the test BEFORE the banner is ever mounted (see the
        // class-level comment above) — always populated by the time any
        // AttachOverlaySlideEnd call can fire.
        public IRenderedComponent<ConsentBannerHost>? Cut { get; set; }
        public List<bool> ElementPresentAtInvocation { get; } = new();

        public override ValueTask AttachOverlaySlideEnd(string elementId)
        {
            var present = Cut is not null &&
                Cut.FindAll("[role='region']").Any(e => e.GetAttribute("id") == elementId);
            ElementPresentAtInvocation.Add(present);
            return base.AttachOverlaySlideEnd(elementId);
        }
    }

    [Fact]
    public void AttachOverlaySlideEnd_only_fires_once_the_banner_element_is_actually_mounted()
    {
        var interop = new MarkupCheckingInteropService();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => interop);

        var cut = _ctx.Render<ConsentBannerHost>(p => p.Add(h => h.ShowBanner, false));
        interop.Cut = cut;

        cut.Render(p => p.Add(h => h.ShowBanner, true));

        cut.WaitForAssertion(() => Assert.NotEmpty(interop.ElementPresentAtInvocation));

        // Every recorded call must have found the element already mounted — a
        // premature (same-pass) attach would record `false` here.
        Assert.All(interop.ElementPresentAtInvocation, Assert.True);
        // Exactly one wire-up for the single mount (no double-fire, no silent skip).
        Assert.Single(interop.ElementPresentAtInvocation);
    }
}
