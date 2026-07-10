using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Tooltip;

/// <summary>
/// Wave 1 (B11 exit parity). On close TooltipContent stays mounted with
/// data-state="closed" and its zoom-out exit class for the exit window, then
/// unmounts. The bUnit unmount is driven by the DelayedDispatch fallback timer.
/// </summary>
public class TooltipExitAnimationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TooltipExitAnimationTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderTooltip()
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Tooltip>(0);
            builder.AddAttribute(1, "ShowDelay", 0);
            builder.AddAttribute(2, "HideDelay", 0);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TooltipTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Hover me")));
                b.CloseComponent();
                b.OpenComponent<L.TooltipContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Tooltip text")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    private static MouseEventArgs Mouse => new();

    [Fact]
    public void Open_Content_Carries_DataState_Open()
    {
        var cut = RenderTooltip();
        cut.Find("div").MouseEnter(Mouse);
        var tip = cut.Find("[role='tooltip']");
        Assert.Equal("open", tip.GetAttribute("data-state"));
        Assert.Contains("animate-fade-in", tip.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Closing_Keeps_Content_Mounted_With_DataState_Closed_And_ZoomOut()
    {
        var cut = RenderTooltip();
        cut.Find("div").MouseEnter(Mouse);
        Assert.NotEmpty(cut.FindAll("[role='tooltip']"));

        cut.Find("div").MouseLeave(Mouse);

        // HideDelay=0 → the close render commits synchronously; assert the exit state
        // directly (no poll) so the ~250ms fallback unmount can't race a delayed poll.
        var tip = cut.Find("[role='tooltip']");
        Assert.Equal("closed", tip.GetAttribute("data-state"));
        var cls = tip.GetAttribute("class") ?? "";
        Assert.Contains("animate-zoom-out", cls);
        // Exit state stays visible so the animation is seen (not opacity-0 invisible).
        Assert.Contains("visible", cls);
    }

    [Fact]
    public void Exit_Eventually_Unmounts_The_Content()
    {
        var cut = RenderTooltip();
        cut.Find("div").MouseEnter(Mouse);
        cut.Find("div").MouseLeave(Mouse);

        // Stable end-state poll; inherits the 10 s module ceiling (TestContextExtensions)
        // so a starved CI thread pool delaying the fallback-timer dispatch can't trip it.
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='tooltip']")));
    }

    [Fact]
    public void Exiting_Surface_Is_Inert_PointerEventsNone()
    {
        // P2 (exit-window inertness): the fading tooltip must be pointer-events-none so
        // a mouseenter on the exiting box can't influence state — parity with the menu
        // surfaces and the Radix pointer-events-none tooltip contract.
        var cut = RenderTooltip();
        cut.Find("div").MouseEnter(Mouse);
        Assert.DoesNotContain("pointer-events-none", cut.Find("[role='tooltip']").GetAttribute("class") ?? "");

        cut.Find("div").MouseLeave(Mouse);

        var tip = cut.Find("[role='tooltip']");
        Assert.Equal("closed", tip.GetAttribute("data-state"));
        Assert.Contains("pointer-events-none", tip.GetAttribute("class") ?? "");
    }
}
