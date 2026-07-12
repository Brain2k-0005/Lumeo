using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sheet;

/// <summary>
/// Round-9 finding-2 — exit-window inertness for Sheet. During the slide-out the
/// panel stays mounted (via the <c>_exiting</c> latch) so the animation can play, but
/// its focus trap + scroll lock + swipe gesture were already torn down by Cleanup().
/// The still-painted panel must therefore be made inert: pointer-events-none + the
/// <c>inert</c> attribute — otherwise a stray click / second tap / Tab lands on a
/// closing sheet. The BACKDROP, by contrast, KEEPS pointer-events-auto so it goes on
/// shielding the page underneath until it unmounts (Radix keeps a modal overlay
/// blocking until close completes); its dismiss handler is already a no-op while
/// exiting (gates on Context.IsOpen). Mirrors the proven DropdownMenuContent exit
/// pattern on the panel and sits alongside the #185 aria-modal drop.
/// </summary>
public class SheetExitInertnessTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SheetExitInertnessTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // playExit is not a Sheet parameter — SheetContent always animates its close
    // (ExitDurationMs keys off Animation). Animation=None is the immediate-unmount
    // opt-out, exercised by the ghost test below.
    private IRenderedComponent<L.Sheet> RenderSheet(bool open, L.SheetContent.SheetAnimation anim = L.SheetContent.SheetAnimation.Slide)
    {
        return _ctx.Render<L.Sheet>(p => p
            .Add(s => s.Open, open)
            .Add(s => s.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.SheetContent>(0);
                b.AddAttribute(1, "Side", L.Side.Right);
                b.AddAttribute(2, "Animation", anim);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Body")));
                b.CloseComponent();
            })));
    }

    [Fact]
    public void Open_Panel_Is_HitTestable_Not_Inert()
    {
        var cut = RenderSheet(open: true);
        var panel = cut.Find("[role='dialog']");
        Assert.Null(panel.GetAttribute("inert"));
        Assert.Contains("pointer-events-auto", panel.GetAttribute("class") ?? "");
        Assert.DoesNotContain("pointer-events-none", panel.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Exiting_Panel_Is_Inert_And_Backdrop_Keeps_Blocking()
    {
        var cut = RenderSheet(open: true);
        cut.Render(p => p.Add(s => s.Open, false));

        cut.WaitForAssertion(() =>
        {
            var panel = cut.Find("[role='dialog']");
            // sanity: we are in the exit window (panel kept mounted, sliding out).
            Assert.Contains("animate-slide-out-to-right", panel.GetAttribute("class") ?? "");
            Assert.Contains("pointer-events-none", panel.GetAttribute("class") ?? "");
            Assert.DoesNotContain("pointer-events-auto", panel.GetAttribute("class") ?? "");
            Assert.Equal("true", panel.GetAttribute("inert"));
            // The fading scrim keeps blocking — it shields the page until unmount.
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
        var cut = RenderSheet(open: true);
        cut.Render(p => p.Add(s => s.Open, false));

        // The exit window opens synchronously on the close render — assert and click
        // in the same beat. A WaitForAssertion here can burn past the 280/320ms
        // fallback timers under CI starvation, unmounting the backdrop before the
        // click (fourth overlay-exit flap of this shape; family doctrine: transient
        // exit-state asserts are synchronous on the committed close render).
        Assert.Contains("animate-slide-out-to-right", cut.Find("[role='dialog']").GetAttribute("class") ?? "");

        cut.Find(".animate-fade-out").Click();
        var panel = cut.Find("[role='dialog']");
        Assert.Equal("false", panel.GetAttribute("aria-modal"));
        Assert.Contains("animate-slide-out-to-right", panel.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Exit_Optout_Produces_No_Inert_Ghost()
    {
        // Animation=None has no exit phase → immediate unmount, so there is no
        // lingering inert / pointer-events-none panel.
        var cut = RenderSheet(open: true, anim: L.SheetContent.SheetAnimation.None);
        cut.Render(p => p.Add(s => s.Open, false));

        Assert.Empty(cut.FindAll("[role='dialog']"));
        Assert.Empty(cut.FindAll("[inert]"));
    }
}
