using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Overlay;

/// <summary>
/// B11 regression — the exit animation of a service-opened overlay must NOT depend
/// on the open-interop chain (scroll lock + focus trap + slide-end + swipe) having
/// completed.
///
/// On Blazor WebAssembly that chain resolves in ~1 ms, so a human never dismisses
/// inside it and the docs demo always animated out. On Blazor Server every interop
/// call is a SignalR round-trip; the dispatcher can process a user's dismiss click
/// BETWEEN those awaits, while the content's post-interop "ready" flag is still
/// false. The old code keyed the slide/zoom-out off that flag, so those dismissals
/// removed the backdrop+panel in the very next render with no exit class — exactly
/// the "service sheets don't animate out on real apps" report that a WASM-only
/// retest could not reproduce.
///
/// These tests reproduce the race deterministically by BLOCKING the first interop
/// call (<see cref="TrackingInteropService.LockScroll"/>) so the open-interop chain
/// never completes, then dismissing. The exit animation must still play, and the
/// interop that DID get set up must still be torn down once the chain unblocks.
/// </summary>
public class OverlayExitAnimationRaceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly OverlayService _overlay = new();
    private readonly BlockingOpenInterop _interop = new();

    public OverlayExitAnimationRaceTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<OverlayService>(_ => _overlay);
        _ctx.Services.AddScoped<IOverlayService>(_ => _overlay);
        // Override the interop with one whose open chain blocks at LockScroll,
        // mirroring a Server round-trip that hasn't returned yet. Registered AFTER
        // AddLumeoServices so this binding wins for IComponentInteropService.
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync()
    {
        _interop.Unblock(); // release any pending open-interop before teardown
        await _ctx.DisposeAsync();
    }

    /// <summary>Interop whose open chain (LockScroll, the first call every overlay
    /// content makes) blocks until <see cref="Unblock"/> — the deterministic stand-in
    /// for a Server interop round-trip that is still in flight when the user dismisses.</summary>
    private sealed class BlockingOpenInterop : TrackingInteropService
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask LockScroll() => new(_gate.Task);
        public void Unblock() => _gate.TrySetResult();
    }

    private sealed class Body : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, "BODY");
    }

    private string OpenAndGetId(Func<Task<OverlayResult>> open)
    {
        OverlayInstance? shown = null;
        _overlay.OnShow += i => shown = i;
        _ = open();
        return shown!.Id;
    }

    // --- exit animation plays even though the open interop never completed --------

    [Fact]
    public async Task Sheet_dismissed_while_open_interop_pending_still_slides_out()
    {
        var cut = _ctx.Render<Lumeo.OverlayProvider>();
        var id = OpenAndGetId(() => _overlay.ShowSheetAsync<Body>(title: "S", side: Lumeo.Side.Right));
        cut.WaitForState(() => cut.Markup.Contains("BODY"));

        // Sanity: the open interop is genuinely stuck (LockScroll never returned).
        Assert.Empty(_interop.FocusTrapSetups);

        await cut.InvokeAsync(() => _overlay.Cancel(id));

        // Old code: exit gated on the post-interop flag → no class, panel gone.
        // Fixed: the slide-out latches from the open→closed render transition.
        Assert.Contains("animate-slide-out-to-right", cut.Markup);
        Assert.Contains("BODY", cut.Markup); // still mounted during the exit window
    }

    [Fact]
    public async Task Dialog_dismissed_while_open_interop_pending_still_zooms_out()
    {
        var cut = _ctx.Render<Lumeo.OverlayProvider>();
        var id = OpenAndGetId(() => _overlay.ShowDialogAsync<Body>(title: "D"));
        cut.WaitForState(() => cut.Markup.Contains("BODY"));
        Assert.Empty(_interop.FocusTrapSetups);

        await cut.InvokeAsync(() => _overlay.Cancel(id));

        Assert.Contains("animate-zoom-out", cut.Markup);
        Assert.Contains("animate-fade-out", cut.Markup); // backdrop fades in parallel
        Assert.Contains("BODY", cut.Markup);
    }

    [Fact]
    public async Task Drawer_dismissed_while_open_interop_pending_still_slides_out()
    {
        var cut = _ctx.Render<Lumeo.OverlayProvider>();
        var id = OpenAndGetId(() => _overlay.ShowDrawerAsync<Body>(title: "Dr"));
        cut.WaitForState(() => cut.Markup.Contains("BODY"));
        Assert.Empty(_interop.FocusTrapSetups);

        await cut.InvokeAsync(() => _overlay.Cancel(id));

        Assert.Contains("animate-slide-out-to-bottom", cut.Markup);
        Assert.Contains("BODY", cut.Markup);
    }

    [Fact]
    public async Task AlertDialog_dismissed_while_open_interop_pending_still_zooms_out()
    {
        var cut = _ctx.Render<Lumeo.OverlayProvider>();
        var id = OpenAndGetId(() => _overlay.ShowAlertDialogAsync(new AlertDialogOptions { Title = "Zoom alert" }));
        cut.WaitForState(() => cut.Markup.Contains("Zoom alert"));

        await cut.InvokeAsync(() => _overlay.Cancel(id));

        Assert.Contains("animate-zoom-out", cut.Markup);
        Assert.Contains("Zoom alert", cut.Markup);
    }

    // --- the X-button path (the consumer's exact dismiss) under a pending open -----

    [Fact]
    public async Task Sheet_X_button_dismiss_while_open_interop_pending_still_slides_out()
    {
        var cut = _ctx.Render<Lumeo.OverlayProvider>();
        _ = _overlay.ShowSheetAsync<Body>(title: "S", side: Lumeo.Side.Right);
        cut.WaitForState(() => cut.Markup.Contains("BODY"));

        // Click the shell's real X (lumeo-sheet-close) — the path the consumer
        // reported broken, routed through Context.TryDismiss → OpenChanged →
        // OverlayService.Cancel, all while LockScroll is still pending.
        var x = cut.Find("button.lumeo-sheet-close");
        await cut.InvokeAsync(() => x.Click());

        Assert.Contains("animate-slide-out-to-right", cut.Markup);
        Assert.Contains("BODY", cut.Markup);
    }

    // --- interop hygiene: a dismiss mid-setup still tears the trap back down -------

    [Fact]
    public async Task Sheet_dismissed_mid_setup_tears_down_interop_once_the_chain_unblocks()
    {
        var cut = _ctx.Render<Lumeo.OverlayProvider>();
        var id = OpenAndGetId(() => _overlay.ShowSheetAsync<Body>(title: "S", side: Lumeo.Side.Right));
        cut.WaitForState(() => cut.Markup.Contains("BODY"));

        // Dismiss while the open interop is still stuck at LockScroll.
        await cut.InvokeAsync(() => _overlay.Cancel(id));

        // Now let the open-interop chain complete. The content must notice it was
        // dismissed mid-setup and undo the focus trap it just installed — otherwise
        // a scroll lock / focus trap would leak onto a panel that's animating out
        // (the pre-fix behaviour left it bound forever with no teardown).
        _interop.Unblock();

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(_interop.FocusTrapSetups);   // setup did run once unblocked
            Assert.NotEmpty(_interop.FocusTrapRemovals); // and was torn back down
        });
    }
}
