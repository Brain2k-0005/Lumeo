using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Drawer;

/// <summary>
/// #381 round 12 (P1) — "Gate reconciliation until initial gesture setup
/// completes". <see cref="L.DrawerContent"/>'s OnAfterRenderAsync flips
/// _interopReady SYNCHRONOUSLY, before it ever awaits LockScroll,
/// SetupFocusTrap, or (eventually) RegisterGestureAsync — a realistic window
/// on Blazor Server for a parameter update to land and run
/// OnParametersSetAsync while that chain is still suspended. Round 11's
/// gesture-reconciliation fix (PreventClose toggling while open) read only
/// _interopReady as its "is it safe to touch the gesture yet" gate, so a
/// reconciliation landing in that exact window could register a gesture
/// BEFORE the original chain's own RegisterGestureAsync call ever ran — and
/// then the original chain, unaware, registered it AGAIN once unblocked.
/// registerDrawerSwipe (unlike registerDrawerSnap) had no de-dupe guard, so
/// the second call stacked a second set of listeners instead of replacing
/// the first.
///
/// These tests reproduce the race deterministically by BLOCKING the first
/// interop call (LockScroll) — mirroring OverlayExitAnimationRaceTests'
/// established technique for the exact same class of Blazor Server window —
/// then forcing a second OnParametersSetAsync pass before unblocking.
/// </summary>
public class DrawerGestureRegistrationRaceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly BlockingOpenInterop _interop = new();

    public DrawerGestureRegistrationRaceTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync()
    {
        _interop.Unblock(); // release any pending open-interop before teardown
        await _ctx.DisposeAsync();
    }

    /// <summary>Interop whose open chain (LockScroll, the first call
    /// DrawerContent's OnAfterRenderAsync makes) blocks until <see cref="Unblock"/> —
    /// the deterministic stand-in for a Server interop round-trip still in
    /// flight when a parameter update arrives.</summary>
    private sealed class BlockingOpenInterop : TrackingInteropService
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask LockScroll() => new(_gate.Task);
        public void Unblock() => _gate.TrySetResult();
    }

    private static RenderFragment DrawerContentFragment(bool preventClose) => b =>
    {
        b.OpenComponent<L.DrawerContent>(0);
        b.AddAttribute(1, "Side", L.Side.Bottom);
        b.AddAttribute(2, "PreventClose", preventClose);
        b.AddAttribute(3, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Body")));
        b.CloseComponent();
    };

    private IRenderedComponent<L.Drawer> RenderDrawer(bool preventClose) =>
        _ctx.Render<L.Drawer>(p => p
            .Add(d => d.Open, true)
            .Add(d => d.ChildContent, DrawerContentFragment(preventClose)));

    [Fact]
    public void ParametersSet_While_Open_Interop_Pending_Does_Not_Prematurely_Register()
    {
        var cut = RenderDrawer(preventClose: false);
        cut.WaitForState(() => cut.Markup.Contains("Body"));

        // Sanity: the open interop is genuinely stuck (LockScroll never
        // returned), so RegisterGestureAsync from OnAfterRenderAsync's own
        // chain has not run yet either.
        Assert.Empty(_interop.DrawerSwipeRegistrations);

        // Force a SECOND OnParametersSetAsync pass (an unrelated re-render,
        // same props) while LockScroll is still pending. Before the fix,
        // _interopReady is already true here (set synchronously before the
        // blocked await), so the round-11 reconciliation condition would
        // fire and register a gesture right now — ahead of, and later
        // duplicated by, the original chain.
        cut.Render(p => p
            .Add(d => d.Open, true)
            .Add(d => d.ChildContent, DrawerContentFragment(preventClose: false)));

        Assert.Empty(_interop.DrawerSwipeRegistrations);
        Assert.Empty(_interop.DrawerSwipeUnregistrations);
    }

    [Fact]
    public void Gesture_Registers_Exactly_Once_After_Interop_Unblocks_Despite_A_Concurrent_Reconcile()
    {
        var cut = RenderDrawer(preventClose: false);
        cut.WaitForState(() => cut.Markup.Contains("Body"));
        Assert.Empty(_interop.DrawerSwipeRegistrations);

        // Same concurrent parameter-set race as above.
        cut.Render(p => p
            .Add(d => d.Open, true)
            .Add(d => d.ChildContent, DrawerContentFragment(preventClose: false)));

        // Now let the original open-interop chain (LockScroll -> ... ->
        // RegisterGestureAsync) actually complete.
        _interop.Unblock();

        cut.WaitForAssertion(() =>
        {
            // Exactly ONE registration — not two, and no unregister was ever
            // needed in between (nothing was registered prematurely to tear
            // down). Before the fix this settled at 2 registrations with 0
            // unregistrations — a genuine leaked second listener set.
            var reg = Assert.Single(_interop.DrawerSwipeRegistrations);
            Assert.Equal("down", reg.Direction);
            Assert.Empty(_interop.DrawerSwipeUnregistrations);
        });
    }
}
