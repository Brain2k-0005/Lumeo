using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.AlertDialog;

/// <summary>
/// Exit-animation duration coupling for AlertDialog: backdrop (animate-fade-out
/// 0.15s) and panel (animate-zoom-out 0.15s) are equal after the fade-out
/// keyframe was made symmetric with fade-in. These tests pin that assumption.
/// </summary>
public class AlertDialogExitAnimationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AlertDialogExitAnimationTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // playExit: null → omit the attribute (exercises the COMPONENT default, now
    // true); true/false → set it explicitly.
    private IRenderedComponent<L.AlertDialog> RenderAlertDialog(bool isOpen, bool? playExit = true)
    {
        return _ctx.Render<L.AlertDialog>(p => p
            .Add(a => a.Open, isOpen)
            .Add(a => a.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.AlertDialogContent>(0);
                if (playExit is bool pe) b.AddAttribute(1, "PlayExitAnimation", pe);
                b.AddAttribute(2, "ChildContent",
                    (RenderFragment)(inner => inner.AddContent(0, "Alert body")));
                b.CloseComponent();
            })));
    }

    /// <summary>
    /// Parity fix: a declarative AlertDialog that does NOT set PlayExitAnimation
    /// now animates its close by default (the param defaults to true) — the panel
    /// stays mounted carrying animate-zoom-out instead of vanishing instantly.
    /// </summary>
    [Fact]
    public void Declarative_Close_Animates_By_Default()
    {
        var cut = RenderAlertDialog(isOpen: true, playExit: null);
        cut.Render(p => p.Add(a => a.Open, false));

        cut.WaitForAssertion(() =>
        {
            var panel = cut.Find("[role='alertdialog']");
            Assert.Contains("animate-zoom-out", panel.GetAttribute("class") ?? "");
        });
        Assert.Contains("Alert body", cut.Markup);
    }

    /// <summary>
    /// The param remains an opt-OUT: PlayExitAnimation=false unmounts immediately
    /// on close (no exit phase).
    /// </summary>
    [Fact]
    public void PlayExitAnimation_False_Unmounts_Immediately()
    {
        var cut = RenderAlertDialog(isOpen: true, playExit: false);
        cut.Render(p => p.Add(a => a.Open, false));

        Assert.Empty(cut.FindAll("[role='alertdialog']"));
    }

    /// <summary>
    /// Backdrop carries animate-fade-out with no inline duration override —
    /// CSS 0.15s already matches the panel's animate-zoom-out 0.15s.
    /// </summary>
    [Fact]
    public void Exit_Backdrop_Has_No_Duration_Override()
    {
        var cut = RenderAlertDialog(isOpen: true);
        cut.Render(p => p.Add(a => a.Open, false));

        cut.WaitForAssertion(() =>
        {
            var backdrop = cut.Find(".animate-fade-out");
            var style = backdrop.GetAttribute("style") ?? "";
            Assert.DoesNotContain("animation-duration", style);
        });
    }

    /// <summary>
    /// Panel carries animate-zoom-out while exiting, confirming the two
    /// elements use independent keyframes at equal CSS duration.
    /// </summary>
    [Fact]
    public void Exit_Panel_Carries_ZoomOut_Class()
    {
        var cut = RenderAlertDialog(isOpen: true);
        cut.Render(p => p.Add(a => a.Open, false));

        cut.WaitForAssertion(() =>
        {
            var panel = cut.Find("[role='alertdialog']");
            Assert.Contains("animate-zoom-out", panel.GetAttribute("class") ?? "");
        });
    }

    /// <summary>
    /// Round-9 finding-2 baseline: while OPEN the panel is hit-testable —
    /// pointer-events-auto, no <c>inert</c>.
    /// </summary>
    [Fact]
    public void Open_Panel_Is_HitTestable_Not_Inert()
    {
        var cut = RenderAlertDialog(isOpen: true);
        var panel = cut.Find("[role='alertdialog']");
        Assert.Null(panel.GetAttribute("inert"));
        Assert.Contains("pointer-events-auto", panel.GetAttribute("class") ?? "");
        Assert.DoesNotContain("pointer-events-none", panel.GetAttribute("class") ?? "");
    }

    /// <summary>
    /// Round-9 finding-2: the exiting panel (kept mounted for the zoom-out, focus
    /// trap + scroll lock already gone) carries pointer-events-none + inert. The
    /// BACKDROP, however, KEEPS pointer-events-auto so it goes on shielding the page
    /// beneath until it unmounts (Radix keeps a modal overlay blocking until close
    /// completes). AlertDialog's backdrop has no click-to-dismiss handler, so it
    /// simply swallows any stray click during the fade — nothing to re-trigger.
    /// </summary>
    [Fact]
    public void Exiting_Panel_Is_Inert_And_Backdrop_Keeps_Blocking()
    {
        var cut = RenderAlertDialog(isOpen: true);
        cut.Render(p => p.Add(a => a.Open, false));

        cut.WaitForAssertion(() =>
        {
            var panel = cut.Find("[role='alertdialog']");
            Assert.Contains("animate-zoom-out", panel.GetAttribute("class") ?? "");
            Assert.Contains("pointer-events-none", panel.GetAttribute("class") ?? "");
            Assert.DoesNotContain("pointer-events-auto", panel.GetAttribute("class") ?? "");
            Assert.Equal("true", panel.GetAttribute("inert"));
            var backdrop = cut.Find(".animate-fade-out");
            Assert.Contains("pointer-events-auto", backdrop.GetAttribute("class") ?? "");
            Assert.DoesNotContain("pointer-events-none", backdrop.GetAttribute("class") ?? "");
        });
    }

    /// <summary>
    /// Round-Codex P2: the alertdialog backdrop carries NO click-to-dismiss handler
    /// at all (an alert must be answered explicitly), so there is nothing a stray
    /// click during the exit window could re-invoke — dispatching a click at it
    /// raises bUnit's missing-handler guard, which is exactly the proof that the
    /// backdrop cannot reopen or re-dismiss the panel while it fades.
    /// </summary>
    [Fact]
    public void Exiting_Backdrop_Has_No_Dismiss_Handler_To_ReInvoke()
    {
        var cut = RenderAlertDialog(isOpen: true);
        cut.Render(p => p.Add(a => a.Open, false));

        cut.WaitForAssertion(() =>
            Assert.Contains("animate-zoom-out", cut.Find("[role='alertdialog']").GetAttribute("class") ?? ""));

        // No @onclick is wired on the scrim → bUnit refuses to dispatch one.
        var backdrop = cut.Find(".animate-fade-out");
        Assert.Throws<Bunit.MissingEventHandlerException>(() => backdrop.Click());
        // And it is still the blocking scrim (shields the page until unmount).
        Assert.Contains("pointer-events-auto", backdrop.GetAttribute("class") ?? "");
    }

    /// <summary>
    /// Opt-out unaffected: PlayExitAnimation=false unmounts on close, leaving no
    /// inert / pointer-events-none ghost.
    /// </summary>
    [Fact]
    public void Exit_Optout_Produces_No_Inert_Ghost()
    {
        var cut = RenderAlertDialog(isOpen: true, playExit: false);
        cut.Render(p => p.Add(a => a.Open, false));

        Assert.Empty(cut.FindAll("[role='alertdialog']"));
        Assert.Empty(cut.FindAll("[inert]"));
    }
}
