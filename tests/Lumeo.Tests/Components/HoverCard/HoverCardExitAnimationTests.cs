using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.HoverCard;

/// <summary>
/// Wave 1 (B11 exit parity). On close HoverCardContent stays mounted with
/// data-state="closed" and its zoom-out exit class for the exit window, then
/// unmounts. The bUnit unmount is driven by the DelayedDispatch fallback timer.
/// </summary>
public class HoverCardExitAnimationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly RenderFragment _child;

    public HoverCardExitAnimationTests()
    {
        _ctx.AddLumeoServices();
        _child = b =>
        {
            b.OpenComponent<L.HoverCardTrigger>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "@user")));
            b.CloseComponent();
            b.OpenComponent<L.HoverCardContent>(2);
            b.AddAttribute(3, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Card body")));
            b.CloseComponent();
        };
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.HoverCard> RenderCard(bool open)
        => _ctx.Render<L.HoverCard>(p => p.Add(c => c.Open, open).Add(c => c.ChildContent, _child));

    // The content is the unique w-64 popover surface (trigger is inline-flex).
    private IElement Content(IRenderedComponent<L.HoverCard> cut) => cut.Find(".w-64");

    [Fact]
    public void Open_Content_Carries_DataState_Open()
    {
        var cut = RenderCard(open: true);
        Assert.Equal("open", Content(cut).GetAttribute("data-state"));
    }

    [Fact]
    public void Closing_Keeps_Content_Mounted_With_DataState_Closed_And_ZoomOut()
    {
        var cut = RenderCard(open: true);
        Assert.Contains("Card body", cut.Markup);

        cut.Render(p => p.Add(c => c.Open, false).Add(c => c.ChildContent, _child));

        // The close render commits synchronously in bUnit; assert the exit state
        // directly (no poll) so the ~250ms fallback unmount can't race a delayed poll.
        var content = Content(cut);
        Assert.Equal("closed", content.GetAttribute("data-state"));
        Assert.Contains("animate-zoom-out", content.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Exit_Eventually_Unmounts_The_Content()
    {
        var cut = RenderCard(open: true);
        cut.Render(p => p.Add(c => c.Open, false).Add(c => c.ChildContent, _child));

        cut.WaitForAssertion(
            () => Assert.DoesNotContain("Card body", cut.Markup),
            timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Exiting_Surface_Is_Inert_PointerEventsNone()
    {
        // P2 (exit-window inertness): the fading card must be pointer-events-none so a
        // mouseenter can't cancel/reopen the close while it animates out.
        var cut = RenderCard(open: true);
        Assert.DoesNotContain("pointer-events-none", Content(cut).GetAttribute("class") ?? "");

        cut.Render(p => p.Add(c => c.Open, false).Add(c => c.ChildContent, _child));

        var content = Content(cut);
        Assert.Equal("closed", content.GetAttribute("data-state"));
        Assert.Contains("pointer-events-none", content.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Mouseenter_On_Exiting_Card_Does_Not_Reopen_It()
    {
        // P2: hovering the fading card must NOT resurrect it. The handler is gated on
        // the exit latch, so even a mouseenter that reaches the exiting surface (bUnit
        // ignores pointer-events, so it always does) is a no-op and the card completes
        // its exit + unmounts. Pre-fix the inline @onmouseenter called RequestOpen,
        // which cancelled the exit and re-opened the card (stranded open forever).
        //
        // OpenDelay=0 makes the (pre-fix) reopen fire near-immediately — well inside the
        // ~250ms exit window — so a regressed gate deterministically reopens the card and
        // this test's WaitForAssertion(unmount) times out. With the gate it never schedules.
        var cut = _ctx.Render<L.HoverCard>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.OpenDelay, 0)
            .Add(c => c.CloseDelay, 0)
            .Add(c => c.ChildContent, _child));
        cut.Render(p => p
            .Add(c => c.Open, false)
            .Add(c => c.OpenDelay, 0)
            .Add(c => c.CloseDelay, 0)
            .Add(c => c.ChildContent, _child));

        var content = Content(cut);
        Assert.Equal("closed", content.GetAttribute("data-state"));
        // Fire mouseenter on the exiting card.
        content.MouseEnter(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // The hover did not reopen it: the card completes its exit and unmounts. (A
        // regressed gate would reopen within ~0ms and this would time out.)
        cut.WaitForAssertion(
            () => Assert.DoesNotContain("Card body", cut.Markup),
            timeout: TimeSpan.FromSeconds(5));
    }
}
