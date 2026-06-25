using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sidebar;

/// <summary>
/// Battle-test #102 (medium, lifecycle) — the MobileSheet viewport subscription was
/// firstRender-gated:
/// <code>if (firstRender &amp;&amp; MobileSheet &amp;&amp; !_viewportSubscribed)</code>.
/// MobileSheet is an opt-in [Parameter] that a consumer can flip false→true AFTER the
/// first render. When that happened, the <c>Responsive.ViewportChanged</c> subscription
/// (and the <c>EnsureInitialisedAsync</c> kick) was never reached, so a later md-boundary
/// crossing produced no cascade re-render — the off-canvas sheet treatment was dead.
///
/// The fix drops the firstRender condition: the provider subscribes whenever
/// <c>MobileSheet</c> is true and not yet subscribed (on ANY render), staying idempotent
/// via <c>_viewportSubscribed</c>, and symmetrically unsubscribes when MobileSheet flips
/// back to false.
///
/// Observable: <c>SidebarComponent</c> renders the off-canvas sheet (class contains
/// <c>absolute</c> + a scrim) only once the provider has reacted to a mobile viewport.
/// A viewport change only reaches the provider if its <c>OnViewportChanged</c> handler is
/// subscribed — so these tests fail against the firstRender-gated implementation (the
/// post-mount viewport flip never re-renders the cascade) and pass with the fix.
/// </summary>
public class SidebarMobileSheetSubscriptionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SidebarMobileSheetSubscriptionTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private ResponsiveService Responsive => _ctx.Services.GetRequiredService<ResponsiveService>();

    // Fires Responsive.ViewportChanged with the given width (height is irrelevant here).
    private void SetViewport(double width) => Responsive.OnViewportChange(width, 800);

    // Renders the provider (unit under test) as the typed root so we can re-render it with
    // cut.Render(p => p.Add(...)). A Push-variant SidebarComponent inside exposes the
    // mobile-sheet treatment as the aside's "absolute" positioning class.
    private IRenderedComponent<L.SidebarProvider> RenderProvider(bool mobileSheet)
    {
        return _ctx.Render<L.SidebarProvider>(p =>
        {
            p.Add(s => s.MobileSheet, mobileSheet);
            p.Add(s => s.Variant, L.SidebarProvider.SidebarVariant.Push);
            p.Add(s => s.IsCollapsed, false); // expanded -> open sheet shows scrim
            p.Add(s => s.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarComponent>(0);
                b.AddAttribute(1, "ChildContent",
                    (RenderFragment)(inner => inner.AddContent(0, "Nav")));
                b.CloseComponent();
            }));
        });
    }

    private static bool IsSheet(IRenderedComponent<L.SidebarProvider> cut)
    {
        // Mobile sheet (any variant on mobile) = absolutely-positioned slide-in panel.
        // Push variant on desktop is an inline rail (shrink-0 w-64, no "absolute").
        var cls = cut.Find("aside").GetAttribute("class") ?? "";
        return cls.Contains("absolute");
    }

    [Fact]
    public void MobileSheet_Toggled_On_After_First_Render_Subscribes_To_Viewport()
    {
        // Mount at a desktop viewport with MobileSheet OFF: no subscription on first render,
        // and Push renders as an inline rail.
        SetViewport(1280);
        var cut = RenderProvider(mobileSheet: false);
        Assert.False(IsSheet(cut));

        // The consumer flips MobileSheet ON *after* the first render. With the firstRender
        // gate this re-render never reached the subscription block; with the fix it
        // subscribes now. Still on desktop, so the rendered geometry is unchanged.
        cut.Render(p => p.Add(s => s.MobileSheet, true));
        Assert.False(IsSheet(cut));

        // Now cross the md boundary into mobile. The provider only re-renders the cascade
        // in response to this if its OnViewportChanged handler is subscribed.
        SetViewport(400);

        // Fix: subscribed -> cascade re-rendered -> off-canvas sheet (absolute) is shown.
        // Bug: not subscribed -> the viewport event is ignored -> aside stays an inline rail.
        cut.WaitForAssertion(() => Assert.True(IsSheet(cut)));
    }

    [Fact]
    public void MobileSheet_On_From_Start_Reacts_To_Md_Boundary_Crossing()
    {
        // Regression guard for the normal path: MobileSheet on from mount must still react
        // to a desktop->mobile crossing (this worked pre-fix and must keep working).
        SetViewport(1280);
        var cut = RenderProvider(mobileSheet: true);
        Assert.False(IsSheet(cut)); // desktop -> inline rail

        SetViewport(400);

        cut.WaitForAssertion(() => Assert.True(IsSheet(cut)));
    }
}
