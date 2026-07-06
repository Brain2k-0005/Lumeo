using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sheet;

/// <summary>
/// #217 — Sheet gains an exit animation. On close the panel stays mounted with a
/// slide/fade-out animation class for the animation's duration, then unmounts,
/// instead of vanishing instantly. Animation=None keeps the immediate-unmount
/// behaviour.
/// </summary>
public class SheetExitAnimationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SheetExitAnimationTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.Sheet> RenderSheet(bool open, L.Side side, L.SheetContent.SheetAnimation anim)
    {
        return _ctx.Render<L.Sheet>(p => p
            .Add(s => s.Open, open)
            .Add(s => s.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.SheetContent>(0);
                b.AddAttribute(1, "Side", side);
                b.AddAttribute(2, "Animation", anim);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Body")));
                b.CloseComponent();
            })));
    }

    [Fact]
    public void Closing_Keeps_Panel_Mounted_With_SlideOut_Class()
    {
        var cut = RenderSheet(open: true, L.Side.Right, L.SheetContent.SheetAnimation.Slide);
        Assert.NotEmpty(cut.FindAll("[role='dialog']"));

        cut.Render(p => p.Add(s => s.Open, false));

        // During the exit phase the panel is still mounted but now carries the
        // slide-out class (right side → slide-out-to-right).
        cut.WaitForAssertion(() =>
        {
            var dialog = cut.Find("[role='dialog']");
            Assert.Contains("animate-slide-out-to-right", dialog.GetAttribute("class") ?? "");
        });
    }

    [Fact]
    public void Fade_Animation_Uses_FadeOut_On_Close()
    {
        var cut = RenderSheet(open: true, L.Side.Right, L.SheetContent.SheetAnimation.Fade);
        cut.Render(p => p.Add(s => s.Open, false));

        cut.WaitForAssertion(() =>
        {
            var dialog = cut.Find("[role='dialog']");
            Assert.Contains("animate-fade-out", dialog.GetAttribute("class") ?? "");
        });
    }

    [Fact]
    public void Exit_Animation_Eventually_Unmounts_The_Panel()
    {
        var cut = RenderSheet(open: true, L.Side.Bottom, L.SheetContent.SheetAnimation.Slide);
        cut.Render(p => p.Add(s => s.Open, false));

        // After the exit animation window the panel is removed from the DOM. The unmount
        // is driven by a real ~280 ms animation timer (DelayedDispatch), so this is a poll,
        // not a fixed sleep: WaitForAssertion returns the instant the panel unmounts
        // (typically well under 300 ms). The ceiling is deliberately generous so a starved
        // thread pool under parallel test load — which can delay the timer callback's
        // dispatch — cannot trip it; it does not widen any real wait on the happy path.
        cut.WaitForAssertion(
            () => Assert.Empty(cut.FindAll("[role='dialog']")),
            timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Animation_None_Unmounts_Immediately_On_Close()
    {
        var cut = RenderSheet(open: true, L.Side.Right, L.SheetContent.SheetAnimation.None);
        cut.Render(p => p.Add(s => s.Open, false));

        // No exit phase for Animation=None — the panel is gone on the next render.
        Assert.Empty(cut.FindAll("[role='dialog']"));
    }

    /// <summary>
    /// Slide exit: backdrop must carry animation-duration:300ms so it finishes
    /// in sync with the 0.3s slide-out panel. A future CSS duration drift will
    /// fail here before it reaches the browser.
    /// </summary>
    [Fact]
    public void Slide_Exit_Backdrop_Carries_300ms_Duration()
    {
        var cut = RenderSheet(open: true, L.Side.Right, L.SheetContent.SheetAnimation.Slide);
        cut.Render(p => p.Add(s => s.Open, false));

        cut.WaitForAssertion(() =>
        {
            // The backdrop is the sibling div with animate-fade-out.
            var backdrop = cut.Find(".animate-fade-out");
            var style = backdrop.GetAttribute("style") ?? "";
            Assert.Contains("animation-duration:300ms", style);
        });
    }

    /// <summary>
    /// Fade exit: backdrop uses animate-fade-out (0.15s) to match the panel's
    /// own animate-fade-out — no inline duration override needed.
    /// </summary>
    [Fact]
    public void Fade_Exit_Backdrop_Has_No_Duration_Override()
    {
        var cut = RenderSheet(open: true, L.Side.Right, L.SheetContent.SheetAnimation.Fade);
        cut.Render(p => p.Add(s => s.Open, false));

        cut.WaitForAssertion(() =>
        {
            var backdrop = cut.Find(".animate-fade-out");
            var style = backdrop.GetAttribute("style") ?? "";
            Assert.DoesNotContain("animation-duration", style);
        });
    }
}
