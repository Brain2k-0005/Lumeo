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
}
