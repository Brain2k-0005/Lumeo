using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DropdownMenu;

/// <summary>
/// Wave 1 (B11 exit parity). On close DropdownMenuContent must stay mounted with
/// data-state="closed" and its zoom-out exit class for the exit window, then
/// unmount — instead of vanishing instantly. Mirrors the hardened Dialog/Sheet
/// exit contract; the unmount here is driven by the DelayedDispatch fallback
/// timer (no JS/animationend in bUnit), so the "gone" assertion polls.
/// </summary>
public class DropdownMenuExitAnimationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly RenderFragment _child;

    public DropdownMenuExitAnimationTests()
    {
        _ctx.AddLumeoServices();
        _child = b =>
        {
            b.OpenComponent<L.DropdownMenuTrigger>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Menu")));
            b.CloseComponent();
            b.OpenComponent<L.DropdownMenuContent>(2);
            b.AddAttribute(3, "ChildContent", (RenderFragment)(c => c.AddContent(0, "items")));
            b.CloseComponent();
        };
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.DropdownMenu> RenderMenu(bool open)
        => _ctx.Render<L.DropdownMenu>(p => p.Add(m => m.Open, open).Add(m => m.ChildContent, _child));

    [Fact]
    public void Open_Content_Carries_DataState_Open_And_Enter_Class()
    {
        var cut = RenderMenu(open: true);
        var menu = cut.Find("[role='menu']");
        Assert.Equal("open", menu.GetAttribute("data-state"));
        Assert.Contains("animate-fade-in", menu.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Closing_Keeps_Content_Mounted_With_DataState_Closed_And_ZoomOut()
    {
        var cut = RenderMenu(open: true);
        Assert.NotEmpty(cut.FindAll("[role='menu']"));

        cut.Render(p => p.Add(m => m.Open, false).Add(m => m.ChildContent, _child));

        // The close render commits synchronously in bUnit: the panel is still mounted,
        // now advertising the closed state + the zoom-out exit class (the exit window
        // has not elapsed). Asserted directly — not polled — so the ~250ms fallback
        // unmount can never race a delayed first poll.
        var menu = cut.Find("[role='menu']");
        Assert.Equal("closed", menu.GetAttribute("data-state"));
        Assert.Contains("animate-zoom-out", menu.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Exit_Eventually_Unmounts_The_Content()
    {
        var cut = RenderMenu(open: true);
        cut.Render(p => p.Add(m => m.Open, false).Add(m => m.ChildContent, _child));

        // Unmount is driven by the ~250ms fallback timer; poll a stable end state and
        // inherit the 10 s module ceiling (TestContextExtensions) so a starved CI thread
        // pool delaying the fallback-timer dispatch can't trip it.
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='menu']")));
    }

    [Fact]
    public void Exiting_Surface_Is_Inert_PointerEventsNone()
    {
        // P2 (exit-window inertness): the open surface must be hit-testable, but the
        // fading (data-state=closed, still-mounted) surface must be inert so a
        // double-click / second tap can't re-invoke a menu item mid-exit.
        var cut = RenderMenu(open: true);
        Assert.DoesNotContain("pointer-events-none", cut.Find("[role='menu']").GetAttribute("class") ?? "");

        cut.Render(p => p.Add(m => m.Open, false).Add(m => m.ChildContent, _child));

        var menu = cut.Find("[role='menu']");
        Assert.Equal("closed", menu.GetAttribute("data-state"));
        Assert.Contains("pointer-events-none", menu.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Reopen_During_Exit_Cancels_It_And_Restores_Enter_Class()
    {
        var cut = RenderMenu(open: true);
        cut.Render(p => p.Add(m => m.Open, false).Add(m => m.ChildContent, _child));
        // Synchronous: the exit class is committed by the close render (see above).
        Assert.Contains("animate-zoom-out", cut.Find("[role='menu']").GetAttribute("class") ?? "");

        // Re-open mid-exit: the latch is cleared and the panel paints the enter class
        // again (no frozen frame of the zoom-out).
        cut.Render(p => p.Add(m => m.Open, true).Add(m => m.ChildContent, _child));
        var menu = cut.Find("[role='menu']");
        Assert.Equal("open", menu.GetAttribute("data-state"));
        Assert.Contains("animate-fade-in", menu.GetAttribute("class") ?? "");
        Assert.DoesNotContain("animate-zoom-out", menu.GetAttribute("class") ?? "");
    }
}
