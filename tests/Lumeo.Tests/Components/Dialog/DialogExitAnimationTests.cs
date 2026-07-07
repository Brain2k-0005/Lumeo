using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Dialog;

/// <summary>
/// Exit-animation duration coupling for Dialog: backdrop (animate-fade-out 0.15s)
/// and panel (animate-zoom-out 0.15s) use the same duration after the fade-out
/// keyframe was made symmetric with fade-in. No inline override is needed —
/// these tests pin that assumption so a future CSS drift is caught here.
/// </summary>
public class DialogExitAnimationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DialogExitAnimationTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // playExit: null → omit the attribute entirely (exercises the COMPONENT
    // default, which is now true); true/false → set it explicitly.
    private IRenderedComponent<L.Dialog> RenderDialog(bool isOpen, bool? playExit = true)
    {
        return _ctx.Render<L.Dialog>(p => p
            .Add(d => d.Open, isOpen)
            .Add(d => d.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.DialogContent>(0);
                if (playExit is bool pe) b.AddAttribute(1, "PlayExitAnimation", pe);
                b.AddAttribute(2, "ChildContent",
                    (RenderFragment)(inner => inner.AddContent(0, "Body")));
                b.CloseComponent();
            })));
    }

    /// <summary>
    /// Parity fix: a declarative Dialog that does NOT set PlayExitAnimation now
    /// animates its close by default (the param defaults to true) — the panel
    /// stays mounted carrying animate-zoom-out instead of vanishing instantly,
    /// matching the declarative Sheet and the shadcn close choreography.
    /// </summary>
    [Fact]
    public void Declarative_Close_Animates_By_Default()
    {
        var cut = RenderDialog(isOpen: true, playExit: null);
        cut.Render(p => p.Add(d => d.Open, false));

        cut.WaitForAssertion(() =>
        {
            var panel = cut.Find("[role='dialog']");
            Assert.Contains("animate-zoom-out", panel.GetAttribute("class") ?? "");
        });
        // Still mounted during the exit window (not vanished).
        Assert.Contains("Body", cut.Markup);
    }

    /// <summary>
    /// The param remains an opt-OUT: PlayExitAnimation=false unmounts the panel
    /// immediately on close (no exit phase) — for tests/consumers wanting instant.
    /// </summary>
    [Fact]
    public void PlayExitAnimation_False_Unmounts_Immediately()
    {
        var cut = RenderDialog(isOpen: true, playExit: false);
        cut.Render(p => p.Add(d => d.Open, false));

        Assert.Empty(cut.FindAll("[role='dialog']"));
    }

    /// <summary>
    /// On close the backdrop carries animate-fade-out with no inline
    /// duration override — CSS 0.15s already matches zoom-out 0.15s.
    /// </summary>
    [Fact]
    public void Exit_Backdrop_Has_No_Duration_Override()
    {
        var cut = RenderDialog(isOpen: true);
        cut.Render(p => p.Add(d => d.Open, false));

        cut.WaitForAssertion(() =>
        {
            var backdrop = cut.Find(".animate-fade-out");
            var style = backdrop.GetAttribute("style") ?? "";
            Assert.DoesNotContain("animation-duration", style);
        });
    }

    /// <summary>
    /// Panel carries animate-zoom-out while exiting (not animate-fade-out),
    /// confirming the two elements use independent keyframes at equal duration.
    /// </summary>
    [Fact]
    public void Exit_Panel_Carries_ZoomOut_Class()
    {
        var cut = RenderDialog(isOpen: true);
        cut.Render(p => p.Add(d => d.Open, false));

        cut.WaitForAssertion(() =>
        {
            var panel = cut.Find("[role='dialog']");
            Assert.Contains("animate-zoom-out", panel.GetAttribute("class") ?? "");
        });
    }

    /// <summary>
    /// Round-9 finding-2: while OPEN the panel is hit-testable — pointer-events-auto,
    /// no <c>inert</c>. This is the contrast baseline for the exit assertion below.
    /// </summary>
    [Fact]
    public void Open_Panel_Is_HitTestable_Not_Inert()
    {
        var cut = RenderDialog(isOpen: true);
        var panel = cut.Find("[role='dialog']");
        Assert.Null(panel.GetAttribute("inert"));
        Assert.Contains("pointer-events-auto", panel.GetAttribute("class") ?? "");
        Assert.DoesNotContain("pointer-events-none", panel.GetAttribute("class") ?? "");
    }

    /// <summary>
    /// Round-9 finding-2: during the exit window the still-mounted panel (kept for
    /// the zoom-out) had its focus trap + scroll lock already torn down, leaving a
    /// clickable/tabbable ghost. It must carry pointer-events-none + inert. The
    /// BACKDROP, however, KEEPS pointer-events-auto: a modal scrim goes on shielding
    /// the page underneath until it unmounts (Radix keeps the overlay blocking until
    /// close completes), so a fast double-click during the fade lands on the scrim,
    /// not the page below. Mirrors the DropdownMenuContent inertness on the panel.
    /// </summary>
    [Fact]
    public void Exiting_Panel_Is_Inert_And_Backdrop_Keeps_Blocking()
    {
        var cut = RenderDialog(isOpen: true);
        cut.Render(p => p.Add(d => d.Open, false));

        cut.WaitForAssertion(() =>
        {
            var panel = cut.Find("[role='dialog']");
            // sanity: we are in the exit window (panel kept mounted, zooming out).
            Assert.Contains("animate-zoom-out", panel.GetAttribute("class") ?? "");
            Assert.Contains("pointer-events-none", panel.GetAttribute("class") ?? "");
            Assert.DoesNotContain("pointer-events-auto", panel.GetAttribute("class") ?? "");
            Assert.Equal("true", panel.GetAttribute("inert"));
            // The fading scrim still swallows clicks (shields the page until unmount).
            var backdrop = cut.Find(".animate-fade-out");
            Assert.Contains("pointer-events-auto", backdrop.GetAttribute("class") ?? "");
            Assert.DoesNotContain("pointer-events-none", backdrop.GetAttribute("class") ?? "");
        });
    }

    /// <summary>
    /// Round-Codex P2: clicking the still-blocking backdrop DURING the exit window
    /// neither throws nor re-invokes the dismiss/reopen path — HandleBackdropClick
    /// gates on Context.IsOpen (already false), so the scrim swallows the click as a
    /// no-op. The panel stays mounted for the zoom-out (no reopen), then the exit
    /// completes as normal.
    /// </summary>
    [Fact]
    public void Clicking_Exiting_Backdrop_Is_A_NoOp_Not_A_Reopen()
    {
        var cut = RenderDialog(isOpen: true);
        cut.Render(p => p.Add(d => d.Open, false));

        cut.WaitForAssertion(() =>
            Assert.Contains("animate-zoom-out", cut.Find("[role='dialog']").GetAttribute("class") ?? ""));

        var backdrop = cut.Find(".animate-fade-out");
        // No throw, and aria-modal stays false (not re-promoted to an open modal).
        backdrop.Click();
        var panel = cut.Find("[role='dialog']");
        Assert.Equal("false", panel.GetAttribute("aria-modal"));
        Assert.Contains("animate-zoom-out", panel.GetAttribute("class") ?? "");
    }

    /// <summary>
    /// The opt-out is unaffected: PlayExitAnimation=false unmounts on close, so
    /// there is no lingering inert / pointer-events-none ghost at all.
    /// </summary>
    [Fact]
    public void Exit_Optout_Produces_No_Inert_Ghost()
    {
        var cut = RenderDialog(isOpen: true, playExit: false);
        cut.Render(p => p.Add(d => d.Open, false));

        Assert.Empty(cut.FindAll("[role='dialog']"));
        Assert.Empty(cut.FindAll("[inert]"));
    }
}
