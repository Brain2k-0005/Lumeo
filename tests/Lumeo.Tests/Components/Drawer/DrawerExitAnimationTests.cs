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

    private IRenderedComponent<L.Drawer> RenderDrawer(
        bool isOpen, L.DrawerContent.DrawerAnimation anim, L.Side side = L.Side.Bottom)
    {
        return _ctx.Render<L.Drawer>(p => p
            .Add(d => d.Open, isOpen)
            .Add(d => d.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.DrawerContent>(0);
                b.AddAttribute(1, "Side", side);
                b.AddAttribute(2, "Animation", anim);
                b.AddAttribute(3, "PlayExitAnimation", true);
                b.AddAttribute(4, "ChildContent",
                    (RenderFragment)(inner => inner.AddContent(0, "Drawer body")));
                b.CloseComponent();
            })));
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
}
