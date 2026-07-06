using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ContextMenu;

/// <summary>
/// Wave 4 — ContextMenuContent joins the B11 exit-animation family (deferred from
/// wave 1 because it has its own inline-positioned markup). On close it stays
/// mounted with data-state="closed" and the zoom-out exit class for the exit
/// window, then unmounts — instead of vanishing instantly. Unmount is driven by
/// the DelayedDispatch fallback timer (no JS/animationend in bUnit).
/// </summary>
public class ContextMenuExitAnimationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly RenderFragment _child;

    public ContextMenuExitAnimationTests()
    {
        _ctx.AddLumeoServices();
        _child = b =>
        {
            b.OpenComponent<L.ContextMenuContent>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(c => c.AddContent(0, "items")));
            b.CloseComponent();
        };
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.ContextMenu> RenderMenu(bool open)
        => _ctx.Render<L.ContextMenu>(p => p.Add(m => m.Open, open).Add(m => m.ChildContent, _child));

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

        // Synchronous close render: still mounted, now advertising the closed state
        // + zoom-out exit class (the exit window has not elapsed). Asserted directly
        // so the ~250ms fallback unmount cannot race a delayed poll.
        var menu = cut.Find("[role='menu']");
        Assert.Equal("closed", menu.GetAttribute("data-state"));
        Assert.Contains("animate-zoom-out", menu.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Exit_Eventually_Unmounts_The_Content()
    {
        var cut = RenderMenu(open: true);
        cut.Render(p => p.Add(m => m.Open, false).Add(m => m.ChildContent, _child));

        cut.WaitForAssertion(
            () => Assert.Empty(cut.FindAll("[role='menu']")),
            timeout: TimeSpan.FromSeconds(5));
    }
}
