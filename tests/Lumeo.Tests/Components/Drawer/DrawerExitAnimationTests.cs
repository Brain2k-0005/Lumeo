using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Drawer;

/// <summary>
/// Exit-animation duration coupling: backdrop must finish in sync with the panel
/// per animation type so neither runs solo over an un-obscured page.
/// </summary>
public class DrawerExitAnimationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DrawerExitAnimationTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // playExit: null → omit the attribute (exercises the COMPONENT default, now
    // true); true/false → set it explicitly.
    private IRenderedComponent<L.Drawer> RenderDrawer(
        bool isOpen, L.DrawerContent.DrawerAnimation anim, L.Side side = L.Side.Bottom,
        bool? playExit = true)
    {
        return _ctx.Render<L.Drawer>(p => p
            .Add(d => d.Open, isOpen)
            .Add(d => d.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.DrawerContent>(0);
                b.AddAttribute(1, "Side", side);
                b.AddAttribute(2, "Animation", anim);
                if (playExit is bool pe) b.AddAttribute(3, "PlayExitAnimation", pe);
                b.AddAttribute(4, "ChildContent",
                    (RenderFragment)(inner => inner.AddContent(0, "Drawer body")));
                b.CloseComponent();
            })));
    }

    /// <summary>
    /// Parity fix: a declarative Drawer that does NOT set PlayExitAnimation now
    /// slides out on close by default (the param defaults to true) — the panel
    /// stays mounted carrying animate-slide-out-to-bottom instead of vanishing.
    /// </summary>
    [Fact]
    public void Declarative_Close_Animates_By_Default()
    {
        var cut = RenderDrawer(isOpen: true, L.DrawerContent.DrawerAnimation.Slide,
            L.Side.Bottom, playExit: null);
        cut.Render(p => p.Add(d => d.Open, false));

        cut.WaitForAssertion(() =>
        {
            var panel = cut.Find("[role='dialog']");
            Assert.Contains("animate-slide-out-to-bottom", panel.GetAttribute("class") ?? "");
        });
        Assert.Contains("Drawer body", cut.Markup);
    }

    /// <summary>
    /// The param remains an opt-OUT: PlayExitAnimation=false unmounts immediately
    /// on close (no exit phase).
    /// </summary>
    [Fact]
    public void PlayExitAnimation_False_Unmounts_Immediately()
    {
        var cut = RenderDrawer(isOpen: true, L.DrawerContent.DrawerAnimation.Slide,
            L.Side.Bottom, playExit: false);
        cut.Render(p => p.Add(d => d.Open, false));

        Assert.Empty(cut.FindAll("[role='dialog']"));
    }

    /// <summary>
    /// Slide exit: backdrop must carry animation-duration:300ms to match the
    /// 0.3s slide-out keyframe on the panel.
    /// </summary>
    [Fact]
    public void Slide_Exit_Backdrop_Carries_300ms_Duration()
    {
        var cut = RenderDrawer(isOpen: true, L.DrawerContent.DrawerAnimation.Slide);
        cut.Render(p => p.Add(d => d.Open, false));

        cut.WaitForAssertion(() =>
        {
            var backdrop = cut.Find(".animate-fade-out");
            var style = backdrop.GetAttribute("style") ?? "";
            Assert.Contains("animation-duration:300ms", style);
        });
    }

    /// <summary>
    /// Fade exit: both backdrop and panel use animate-fade-out (0.15s each).
    /// No inline duration override should appear on the backdrop.
    /// </summary>
    [Fact]
    public void Fade_Exit_Backdrop_Has_No_Duration_Override()
    {
        var cut = RenderDrawer(isOpen: true, L.DrawerContent.DrawerAnimation.Fade);
        cut.Render(p => p.Add(d => d.Open, false));

        cut.WaitForAssertion(() =>
        {
            var backdrop = cut.Find(".animate-fade-out");
            var style = backdrop.GetAttribute("style") ?? "";
            Assert.DoesNotContain("animation-duration", style);
        });
    }

    /// <summary>
    /// Round-9 finding-2 baseline: while OPEN the drawer panel is hit-testable —
    /// pointer-events-auto, no <c>inert</c>.
    /// </summary>
    [Fact]
    public void Open_Panel_Is_HitTestable_Not_Inert()
    {
        var cut = RenderDrawer(isOpen: true, L.DrawerContent.DrawerAnimation.Slide);
        var panel = cut.Find("[role='dialog']");
        Assert.Null(panel.GetAttribute("inert"));
        Assert.Contains("pointer-events-auto", panel.GetAttribute("class") ?? "");
        Assert.DoesNotContain("pointer-events-none", panel.GetAttribute("class") ?? "");
    }

    /// <summary>
    /// Round-9 finding-2: the exiting drawer panel (kept mounted for the slide-out,
    /// focus trap + scroll lock + gesture already torn down) carries
    /// pointer-events-none + inert. The BACKDROP, however, KEEPS pointer-events-auto
    /// so it goes on shielding the page beneath until it unmounts (Radix keeps a modal
    /// overlay blocking until close completes); its dismiss handler is already a no-op
    /// while exiting (gates on Context.IsOpen), so it swallows the click harmlessly.
    /// </summary>
    [Fact]
    public void Exiting_Panel_Is_Inert_And_Backdrop_Keeps_Blocking()
    {
        var cut = RenderDrawer(isOpen: true, L.DrawerContent.DrawerAnimation.Slide, L.Side.Bottom);
        cut.Render(p => p.Add(d => d.Open, false));

        cut.WaitForAssertion(() =>
        {
            var panel = cut.Find("[role='dialog']");
            Assert.Contains("animate-slide-out-to-bottom", panel.GetAttribute("class") ?? "");
            Assert.Contains("pointer-events-none", panel.GetAttribute("class") ?? "");
            Assert.DoesNotContain("pointer-events-auto", panel.GetAttribute("class") ?? "");
            Assert.Equal("true", panel.GetAttribute("inert"));
            var backdrop = cut.Find(".animate-fade-out");
            Assert.Contains("pointer-events-auto", backdrop.GetAttribute("class") ?? "");
            Assert.DoesNotContain("pointer-events-none", backdrop.GetAttribute("class") ?? "");
        });
    }

    /// <summary>
    /// Round-Codex P2: clicking the still-blocking backdrop during the slide-out
    /// neither throws nor re-invokes dismiss/reopen — HandleBackdropClick gates on
    /// Context.IsOpen (already false), so the scrim swallows the click as a no-op and
    /// the panel stays mid-slide-out.
    /// </summary>
    [Fact]
    public void Clicking_Exiting_Backdrop_Is_A_NoOp_Not_A_Reopen()
    {
        var cut = RenderDrawer(isOpen: true, L.DrawerContent.DrawerAnimation.Slide, L.Side.Bottom);
        cut.Render(p => p.Add(d => d.Open, false));

        cut.WaitForAssertion(() =>
            Assert.Contains("animate-slide-out-to-bottom", cut.Find("[role='dialog']").GetAttribute("class") ?? ""));

        cut.Find(".animate-fade-out").Click();
        var panel = cut.Find("[role='dialog']");
        Assert.Equal("false", panel.GetAttribute("aria-modal"));
        Assert.Contains("animate-slide-out-to-bottom", panel.GetAttribute("class") ?? "");
    }

    /// <summary>
    /// Opt-out unaffected: PlayExitAnimation=false unmounts on close, leaving no
    /// inert / pointer-events-none ghost.
    /// </summary>
    [Fact]
    public void Exit_Optout_Produces_No_Inert_Ghost()
    {
        var cut = RenderDrawer(isOpen: true, L.DrawerContent.DrawerAnimation.Slide,
            L.Side.Bottom, playExit: false);
        cut.Render(p => p.Add(d => d.Open, false));

        Assert.Empty(cut.FindAll("[role='dialog']"));
        Assert.Empty(cut.FindAll("[inert]"));
    }
}
