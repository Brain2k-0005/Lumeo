using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Drawer;

/// <summary>
/// Drawer state-on-data-change regressions (battle-wave2 #76 + #77):
///
/// #76 — Changing <see cref="L.DrawerContent.Side"/> (or flipping the app/scoped
///   RTL direction) WHILE a non-snap drawer stays open must re-register the
///   swipe-to-dismiss gesture for the NEW visual edge. Before the fix
///   OnParametersSetAsync only re-registered on a snap-mode flip
///   (<c>_registeredWithSnap != UsesSnapPoints</c>), which never trips for a
///   Left↔Right swap (both are non-snap), so the gesture stayed wired to the OLD
///   edge and the drawer could only be dismissed by dragging the wrong way.
///
/// #77 — Changing the <see cref="L.DrawerContent.SnapPoints"/> fraction values
///   (same count, still non-empty) while a snap drawer stays open must re-seed
///   the JS resting geometry. Before the fix the same-mode gate was a no-op for a
///   fraction-only edit, so JS kept the stale snap offsets.
///
/// Both fixes record the registered direction + snap-point array and re-register
/// when the live parameters diverge. The TrackingInteropService records every
/// RegisterDrawerSwipe (snap registrations route through it too) and every
/// unregistration, so a re-register is observable without a real DOM gesture.
/// </summary>
public class DrawerGestureReconcileTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DrawerGestureReconcileTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment DrawerContentFragment(L.Side side, double[]? snapPoints) => b =>
    {
        b.OpenComponent<L.DrawerContent>(0);
        b.AddAttribute(1, "Side", side);
        if (snapPoints is not null) b.AddAttribute(2, "SnapPoints", snapPoints);
        b.AddAttribute(3, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Body")));
        b.CloseComponent();
    };

    private IRenderedComponent<L.Drawer> RenderDrawer(bool open, L.Side side, double[]? snapPoints = null)
    {
        return _ctx.Render<L.Drawer>(p => p
            .Add(d => d.Open, open)
            .Add(d => d.ChildContent, DrawerContentFragment(side, snapPoints)));
    }

    // ---- #76: a Side change while open re-wires the swipe to the new edge ----

    [Fact]
    public void Changing_Side_While_Open_ReRegisters_Swipe_For_New_Edge()
    {
        // Open a Left drawer — swipe gesture wired to the "left" edge.
        var cut = RenderDrawer(open: true, side: L.Side.Left);
        var first = Assert.Single(_interop.DrawerSwipeRegistrations);
        Assert.Equal("left", first.Direction);

        // Flip to the Right side while the drawer STAYS open (Open is still true).
        cut.Render(p => p
            .Add(d => d.Open, true)
            .Add(d => d.ChildContent, DrawerContentFragment(L.Side.Right, snapPoints: null)));

        // The fix re-registers in OnParametersSetAsync: the old edge is torn down
        // and a fresh gesture is wired to the new "right" edge. Without the fix the
        // same-mode gate never fires and DrawerSwipeRegistrations still has only
        // the stale "left" entry.
        Assert.NotEmpty(_interop.DrawerSwipeUnregistrations);
        Assert.Equal("right", _interop.DrawerSwipeRegistrations[^1].Direction);
    }

    [Fact]
    public void Keeping_Same_Side_While_Open_Does_Not_ReRegister_Swipe()
    {
        // Control: an unrelated re-render that does NOT change Side must not churn
        // the gesture (no extra register / unregister).
        var cut = RenderDrawer(open: true, side: L.Side.Left);
        Assert.Single(_interop.DrawerSwipeRegistrations);
        Assert.Empty(_interop.DrawerSwipeUnregistrations);

        cut.Render(p => p
            .Add(d => d.Open, true)
            .Add(d => d.ChildContent, DrawerContentFragment(L.Side.Left, snapPoints: null)));

        Assert.Single(_interop.DrawerSwipeRegistrations);
        Assert.Empty(_interop.DrawerSwipeUnregistrations);
    }

    // ---- #77: a SnapPoints fraction edit while open re-seeds the geometry ----

    [Fact]
    public void Changing_SnapPoints_Fractions_While_Open_ReRegisters_Snap()
    {
        // Open a snap drawer (Bottom) with one set of resting fractions.
        var cut = RenderDrawer(open: true, side: L.Side.Bottom, snapPoints: new[] { 0.4, 1.0 });
        Assert.Single(_interop.DrawerSwipeRegistrations);
        Assert.Empty(_interop.DrawerSwipeUnregistrations);

        // Edit the fractions (same count, still non-empty) while the drawer stays
        // open — only the resting geometry changed, the mode did not.
        cut.Render(p => p
            .Add(d => d.Open, true)
            .Add(d => d.ChildContent, DrawerContentFragment(L.Side.Bottom, snapPoints: new[] { 0.6, 1.0 })));

        // The fix detects the snap-array divergence and re-registers so JS gets the
        // new offsets. Without it the same-mode + same-count edit is ignored and no
        // second registration / unregistration is ever issued.
        Assert.NotEmpty(_interop.DrawerSwipeUnregistrations);
        Assert.True(_interop.DrawerSwipeRegistrations.Count >= 2);
    }

    [Fact]
    public void Keeping_Same_SnapPoints_While_Open_Does_Not_ReRegister_Snap()
    {
        // Control: re-supplying the SAME fractions (sequence-equal) must not churn
        // the snap gesture.
        var cut = RenderDrawer(open: true, side: L.Side.Bottom, snapPoints: new[] { 0.4, 1.0 });
        Assert.Single(_interop.DrawerSwipeRegistrations);
        Assert.Empty(_interop.DrawerSwipeUnregistrations);

        cut.Render(p => p
            .Add(d => d.Open, true)
            .Add(d => d.ChildContent, DrawerContentFragment(L.Side.Bottom, snapPoints: new[] { 0.4, 1.0 })));

        Assert.Single(_interop.DrawerSwipeRegistrations);
        Assert.Empty(_interop.DrawerSwipeUnregistrations);
    }
}
