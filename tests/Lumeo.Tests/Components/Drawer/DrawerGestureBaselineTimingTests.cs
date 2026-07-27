using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Drawer;

/// <summary>
/// #381 round 14 (P2) — "Defer snap baseline capture until the new side
/// renders". OnParametersSetAsync's gesture reconciliation used to unregister
/// + re-register the instant it detected a mismatch (e.g. a Side flip on an
/// open snap drawer) — synchronously, INSIDE OnParametersSetAsync.
/// registerDrawerSnap's baseline capture (restBaseRaw) reads the CURRENT DOM
/// via getComputedStyle when there is no inline override, so if that read
/// happens before the render containing the NEW side's CSS classes commits,
/// it captures the stale side's classes.
///
/// bUnit's render dispatch turned out NOT to block the calling thread on a
/// stuck OnParametersSetAsync task (mirroring real Blazor's non-blocking
/// render model — verified empirically before writing this), so "does the
/// render call hang" cannot distinguish the old and new code. What DOES
/// distinguish them is WHICH RENDER PASS is current at the moment the
/// interop call fires: the fix moves that call from OnParametersSetAsync
/// (runs BEFORE the render reflecting the new Side commits) to
/// OnAfterRenderAsync (runs only AFTER a render commits, by definition).
/// This is captured directly by reading the rendered panel's class list
/// FROM INSIDE the interop call itself — before the fix this reads the
/// stale (Bottom) PositionClasses; after the fix it always reads the
/// current (Top) ones.
/// </summary>
public class DrawerGestureBaselineTimingTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly RecordingRegisterInterop _interop = new();

    public DrawerGestureBaselineTimingTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    /// <summary>Interop whose RegisterDrawerSwipe call (registerDrawerSnap's
    /// default-interface implementation routes through it too) invokes a
    /// caller-supplied probe SYNCHRONOUSLY, before doing anything else — lets a
    /// test observe exactly what the component's rendered state is at the
    /// instant the interop call fires, the same moment the real JS interop
    /// would read the live DOM for its own baseline capture.</summary>
    private sealed class RecordingRegisterInterop : TrackingInteropService
    {
        public Action<string>? OnRegisterCalled;

        public override ValueTask RegisterDrawerSwipe(string elementId, string direction, Func<Task> handler)
        {
            OnRegisterCalled?.Invoke(direction);
            return base.RegisterDrawerSwipe(elementId, direction, handler);
        }
    }

    private static RenderFragment SnapDrawerContentFragment(L.Side side) => b =>
    {
        b.OpenComponent<L.DrawerContent>(0);
        b.AddAttribute(1, "Side", side);
        b.AddAttribute(2, "SnapPoints", new double[] { 0.4, 1.0 });
        b.AddAttribute(3, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Body")));
        b.CloseComponent();
    };

    private IRenderedComponent<L.Drawer> RenderSnapDrawer(L.Side side) =>
        _ctx.Render<L.Drawer>(p => p
            .Add(d => d.Open, true)
            .Add(d => d.ChildContent, SnapDrawerContentFragment(side)));

    [Fact]
    public void Reregistering_After_A_Side_Change_Reads_The_Panel_After_The_New_Side_Has_Rendered()
    {
        var cut = RenderSnapDrawer(L.Side.Bottom);
        Assert.Single(_interop.DrawerSwipeRegistrations);

        // Capture the panel's OWN rendered class list at the exact instant the
        // re-registration's interop call fires — the same moment the real JS
        // baseline capture would read the live DOM.
        string? classAtRegisterTime = null;
        _interop.OnRegisterCalled = _ => classAtRegisterTime = cut.Find("[role='dialog']").GetAttribute("class");

        cut.Render(p => p
            .Add(d => d.Open, true)
            .Add(d => d.ChildContent, SnapDrawerContentFragment(L.Side.Top)));

        Assert.NotNull(classAtRegisterTime);
        // Side.Top's PositionClasses include "top-0" and NOT "bottom-0" (see
        // PositionClasses' own switch); the pre-fix code fired this interop
        // call before Blazor committed the render containing these classes,
        // so it observed the STILL-BOTTOM-ANCHORED panel instead.
        Assert.Contains("top-0", classAtRegisterTime);
        Assert.DoesNotContain("bottom-0", classAtRegisterTime);
    }
}

/// <summary>
/// #381 round 14 (P2) — "Reconcile parameters changed during gesture
/// registration". RegisterGestureAsync's own branch condition (UsesSnapPoints
/// / !PreventClose) is read once, before ITS OWN internal interop await, and
/// never re-checked after resuming — a parameter change landing while THAT
/// specific await is in flight (distinct from, and not covered by, the
/// earlier LockScroll-wait window _initialGestureSetupComplete already
/// gates) commits a registration that is already stale the instant it lands,
/// and nothing catches it if the parameters don't change again afterward.
/// Reproduced by blocking the FIRST RegisterDrawerSwipe call (the initial
/// open sequence's own) and flipping PreventClose while it is in flight.
/// </summary>
public class DrawerGestureMidRegistrationReconcileTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly BlockingRegisterInterop _interop = new();

    public DrawerGestureMidRegistrationReconcileTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync()
    {
        _interop.Unblock();
        await _ctx.DisposeAsync();
    }

    private sealed class BlockingRegisterInterop : TrackingInteropService
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Unblock() => _gate.TrySetResult();

        public override async ValueTask RegisterDrawerSwipe(string elementId, string direction, Func<Task> handler)
        {
            await _gate.Task;
            await base.RegisterDrawerSwipe(elementId, direction, handler);
        }
    }

    private static RenderFragment NonSnapDrawerContentFragment(bool preventClose) => b =>
    {
        b.OpenComponent<L.DrawerContent>(0);
        b.AddAttribute(1, "Side", L.Side.Bottom);
        b.AddAttribute(2, "PreventClose", preventClose);
        b.AddAttribute(3, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Body")));
        b.CloseComponent();
    };

    [Fact]
    public void PreventClose_Flipping_While_The_Initial_Registration_Is_In_Flight_Self_Corrects()
    {
        // Open with PreventClose=false — RegisterGestureAsync's OWN
        // RegisterDrawerSwipe call is now stuck on the gate (armed from the
        // very first call, unlike DrawerGestureRegistrationRaceTests which
        // blocks LockScroll — an EARLIER window than this one).
        var cut = _ctx.Render<L.Drawer>(p => p
            .Add(d => d.Open, true)
            .Add(d => d.ChildContent, NonSnapDrawerContentFragment(preventClose: false)));

        // PreventClose flips to true WHILE that registration call is still
        // in flight — _initialGestureSetupComplete is still false here, so
        // OnParametersSetAsync's own reconciliation gate correctly skips
        // (this is the round-12 window, working as intended); the question
        // is whether anything ELSE catches the mismatch once the in-flight
        // call finally lands.
        cut.Render(p => p
            .Add(d => d.Open, true)
            .Add(d => d.ChildContent, NonSnapDrawerContentFragment(preventClose: true)));

        Assert.Empty(_interop.DrawerSwipeRegistrations); // still stuck, nothing landed yet

        // Let the original (now-stale) registration attempt complete.
        _interop.Unblock();

        cut.WaitForAssertion(() =>
        {
            // The self-correction loop must have noticed the mismatch
            // immediately after the stale registration landed and torn it
            // back down — a swipe-dismissible listener must not survive on
            // a drawer whose CURRENT PreventClose is true.
            Assert.NotEmpty(_interop.DrawerSwipeUnregistrations);
        });

        // No dangling registration count greater than what got unregistered —
        // i.e. no listener is left live and dismissible.
        Assert.Equal(_interop.DrawerSwipeRegistrations.Count, _interop.DrawerSwipeUnregistrations.Count);
    }

    [Fact]
    public void PreventClose_Staying_False_While_Registration_Is_In_Flight_Still_Registers_Once_Unblocked()
    {
        // Control: no parameter change during the in-flight window — the
        // self-correction loop must be a true no-op (GestureNeedsReconcile
        // stays false), not accidentally churn a perfectly valid registration.
        var cut = _ctx.Render<L.Drawer>(p => p
            .Add(d => d.Open, true)
            .Add(d => d.ChildContent, NonSnapDrawerContentFragment(preventClose: false)));

        _interop.Unblock();

        cut.WaitForAssertion(() => Assert.Single(_interop.DrawerSwipeRegistrations));
        Assert.Empty(_interop.DrawerSwipeUnregistrations);
    }
}
