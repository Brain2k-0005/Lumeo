using System.Linq;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Drawer;

/// <summary>
/// #346 finding D — vaul's <c>shouldScaleBackground</c>. <see cref="L.Drawer.ScaleBackground"/>
/// (default <c>false</c>) toggles <c>data-lumeo-drawer-scaled</c> on the app's own
/// <c>[data-lumeo-drawer-wrapper]</c> element (marked by the CONSUMER, never rendered by
/// Lumeo) via <see cref="IComponentInteropService.SetDrawerBackgroundScaled"/>, so
/// lumeo.css can scale/round/translate it while a BOTTOM drawer is open — vaul only
/// scales the background for a bottom sheet, so Top/Left/Right never trigger it even
/// with ScaleBackground true.
///
/// Cascaded from Drawer to DrawerContent by NAME (LumeoDrawerScaleBackground) rather
/// than folded into DrawerContext, so the change doesn't alter that record's already
/// -shipped constructor/Deconstruct shape (see Drawer.razor's own remarks).
/// </summary>
public class DrawerScaleBackgroundTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DrawerScaleBackgroundTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.Drawer> RenderDrawer(bool open, bool scaleBackground, L.Side side = L.Side.Bottom)
    {
        return _ctx.Render<L.Drawer>(p => p
            .Add(d => d.Open, open)
            .Add(d => d.ScaleBackground, scaleBackground)
            .AddChildContent<L.DrawerContent>(cp => cp
                .Add(c => c.Side, side)
                .AddChildContent("Drawer body")));
    }

    [Fact]
    public void Default_Parameter_Value_Is_False()
    {
        Assert.False(new L.Drawer().ScaleBackground);
    }

    [Fact]
    public void ScaleBackground_False_Never_Calls_The_Interop()
    {
        RenderDrawer(open: true, scaleBackground: false);
        Assert.Empty(_interop.DrawerBackgroundScaleCalls);
    }

    [Fact]
    public void ScaleBackground_True_On_A_Bottom_Drawer_Scales_On_Open()
    {
        RenderDrawer(open: true, scaleBackground: true);
        Assert.True(Assert.Single(_interop.DrawerBackgroundScaleCalls));
    }

    [Fact]
    public void Closing_A_Scaled_Bottom_Drawer_Un_Scales_The_Wrapper()
    {
        var cut = RenderDrawer(open: true, scaleBackground: true);
        Assert.Single(_interop.DrawerBackgroundScaleCalls); // [true]

        cut.Render(p => p.Add(d => d.Open, false));

        Assert.Equal(new[] { true, false }, _interop.DrawerBackgroundScaleCalls);
    }

    [Theory]
    [InlineData(L.Side.Top)]
    [InlineData(L.Side.Left)]
    [InlineData(L.Side.Right)]
    public void ScaleBackground_True_On_A_NonBottom_Drawer_Never_Calls_The_Interop(L.Side side)
    {
        // vaul's shouldScaleBackground only applies to a bottom sheet.
        RenderDrawer(open: true, scaleBackground: true, side);
        Assert.Empty(_interop.DrawerBackgroundScaleCalls);
    }

    [Fact]
    public void Disposing_A_Scaled_Open_Drawer_Un_Scales_The_Wrapper()
    {
        var cut = _ctx.Render<ConditionalRoot>(p => p
            .Add(x => x.Show, true)
            .AddChildContent(DrawerFragment(open: true, scaleBackground: true)));

        Assert.Single(_interop.DrawerBackgroundScaleCalls); // [true]

        cut.Render(p => p.Add(x => x.Show, false));

        Assert.Equal(new[] { true, false }, _interop.DrawerBackgroundScaleCalls);
    }

    // Fix round 1 (task review) — components.js's setDrawerBackgroundScaled is
    // now ref-counted (mirrors scrollLockCount) because two simultaneously-open
    // bottom Drawers with ScaleBackground=true used to fight over the single
    // [data-lumeo-drawer-wrapper] element: whichever closed FIRST un-scaled it
    // while the second was still open. bUnit has no real JS runtime, so this
    // can't exercise the ref-count arithmetic itself — but the ref-count's
    // correctness DEPENDS ENTIRELY on each component instance issuing exactly
    // one `false` call per `true` it issued (never more, never fewer, and
    // never a `false` it didn't earn). That per-instance balance is exactly
    // what's bUnit-testable: two independent Drawer roots, opened together,
    // closed independently — each instance's own true/false pair must be
    // present and the total count must stay balanced throughout, regardless
    // of which drawer closes first.
    [Fact]
    public void Two_Simultaneously_Open_Scaled_Drawers_Each_Issue_A_Balanced_Call_Pair()
    {
        var cutA = RenderDrawer(open: true, scaleBackground: true);
        Assert.Equal(new[] { true }, _interop.DrawerBackgroundScaleCalls);

        var cutB = RenderDrawer(open: true, scaleBackground: true);
        Assert.Equal(new[] { true, true }, _interop.DrawerBackgroundScaleCalls);

        // First-opened closes first — its own call pair must balance without
        // touching B's still-open state (B calls no `false` here).
        cutA.Render(p => p.Add(d => d.Open, false));
        Assert.Equal(new[] { true, true, false }, _interop.DrawerBackgroundScaleCalls);

        cutB.Render(p => p.Add(d => d.Open, false));
        Assert.Equal(new[] { true, true, false, false }, _interop.DrawerBackgroundScaleCalls);

        // Every `true` has exactly one matching `false` — the invariant the
        // JS-side ref-count (0->1 sets the attribute, 1->0 removes it) relies
        // on to never un-scale a wrapper a still-open drawer needs scaled.
        Assert.Equal(
            _interop.DrawerBackgroundScaleCalls.Count(v => v),
            _interop.DrawerBackgroundScaleCalls.Count(v => !v));
    }

    [Fact]
    public void Two_Simultaneously_Open_Scaled_Drawers_Balance_Regardless_Of_Close_Order()
    {
        var cutA = RenderDrawer(open: true, scaleBackground: true);
        var cutB = RenderDrawer(open: true, scaleBackground: true);
        Assert.Equal(new[] { true, true }, _interop.DrawerBackgroundScaleCalls);

        // Last-opened closes first this time.
        cutB.Render(p => p.Add(d => d.Open, false));
        Assert.Equal(new[] { true, true, false }, _interop.DrawerBackgroundScaleCalls);

        cutA.Render(p => p.Add(d => d.Open, false));
        Assert.Equal(new[] { true, true, false, false }, _interop.DrawerBackgroundScaleCalls);
    }

    private static RenderFragment DrawerFragment(bool open, bool scaleBackground) => builder =>
    {
        builder.OpenComponent<L.Drawer>(0);
        builder.AddAttribute(1, "Open", open);
        builder.AddAttribute(2, "ScaleBackground", scaleBackground);
        builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
        {
            b.OpenComponent<L.DrawerContent>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Body")));
            b.CloseComponent();
        }));
        builder.CloseComponent();
    };
}
