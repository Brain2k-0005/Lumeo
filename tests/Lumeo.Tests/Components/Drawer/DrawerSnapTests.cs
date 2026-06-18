using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Drawer;

/// <summary>
/// #218 — Drawer snap points + velocity dismiss + overlay-backdrop token.
/// The touch gesture itself (drag/flick/snap) is JS and browser-verified;
/// these cover the C# surface: the new parameters render, snap mode suppresses
/// the CSS slide-in (JS owns the transform), the backdrop uses the theme token,
/// and the velocity option has the documented default.
/// </summary>
public class DrawerSnapTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DrawerSnapTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderDrawer(
        bool open,
        double[]? snapPoints = null,
        int? activeSnap = null,
        EventCallback<int>? activeSnapChanged = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Drawer>(0);
            builder.AddAttribute(1, "Open", open);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DrawerContent>(0);
                if (snapPoints is not null) b.AddAttribute(1, "SnapPoints", snapPoints);
                if (activeSnap is not null) b.AddAttribute(2, "ActiveSnapPoint", activeSnap.Value);
                if (activeSnapChanged is not null) b.AddAttribute(3, "ActiveSnapPointChanged", activeSnapChanged.Value);
                b.AddAttribute(4, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Snap content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void SnapPoints_Drawer_Renders_Dialog()
    {
        var cut = RenderDrawer(open: true, snapPoints: new[] { 0.4, 0.75, 1.0 });
        Assert.NotEmpty(cut.FindAll("[role='dialog']"));
        Assert.Contains("Snap content", cut.Markup);
    }

    [Fact]
    public void SnapPoints_Suppress_SlideIn_Animation()
    {
        // Snap mode: JS owns the transform, so the CSS slide-in keyframe is dropped.
        var snap = RenderDrawer(open: true, snapPoints: new[] { 0.5, 1.0 });
        var snapPanel = snap.Find("[role='dialog']").GetAttribute("class") ?? "";
        Assert.DoesNotContain("animate-slide-in", snapPanel);
    }

    [Fact]
    public void NonSnap_Bottom_Drawer_Keeps_SlideIn_Animation()
    {
        // Control: without snap points the bottom drawer still slides in via CSS.
        var plain = RenderDrawer(open: true);
        var plainPanel = plain.Find("[role='dialog']").GetAttribute("class") ?? "";
        Assert.Contains("animate-slide-in-from-bottom", plainPanel);
    }

    [Fact]
    public void Backdrop_Uses_Overlay_Token_Not_Hardcoded_Black()
    {
        var cut = RenderDrawer(open: true, snapPoints: new[] { 0.5, 1.0 });
        var backdrop = cut.FindAll("div").First(d =>
        {
            var cls = d.GetAttribute("class") ?? "";
            return cls.Contains("fixed") && cls.Contains("inset-0") && cls.Contains("animate-fade-in");
        });
        var style = backdrop.GetAttribute("style") ?? "";
        Assert.Contains("overlay-backdrop", style);
        Assert.DoesNotContain("bg-black", backdrop.GetAttribute("class") ?? "");
    }

    [Fact]
    public void ActiveSnapPoint_TwoWay_Binding_Renders()
    {
        // Binding the index is accepted and the drawer renders (gesture-driven
        // changes are exercised in the browser).
        int? reported = null;
        var cb = EventCallback.Factory.Create<int>(_ctx, (int i) => reported = i);
        var cut = RenderDrawer(open: true, snapPoints: new[] { 0.4, 1.0 }, activeSnap: 0, activeSnapChanged: cb);
        Assert.NotEmpty(cut.FindAll("[role='dialog']"));
    }

    [Fact]
    public void GestureOptions_Velocity_Has_Default()
    {
        // Velocity/flick dismiss default documented as 0.4 px/ms.
        var opts = new L.Services.LumeoGestureOptions();
        Assert.Equal(0.4, opts.SwipeDismissVelocity);
    }
}
