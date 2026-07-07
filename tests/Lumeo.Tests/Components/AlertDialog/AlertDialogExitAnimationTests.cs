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
}
