using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.NavigationMenu;

/// <summary>
/// Wave 1 (B11 exit parity). On close NavigationMenuContent stays mounted with
/// data-state="closed" and its fade-out exit class for the exit window, then
/// unmounts. The bUnit unmount is driven by the DelayedDispatch fallback timer.
/// </summary>
public class NavigationMenuExitAnimationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public NavigationMenuExitAnimationTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderNav()
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.NavigationMenu>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.NavigationMenuList>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(list =>
                {
                    list.OpenComponent<L.NavigationMenuItem>(0);
                    list.AddAttribute(1, "ChildContent", (RenderFragment)(item =>
                    {
                        item.OpenComponent<L.NavigationMenuTrigger>(0);
                        item.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Products")));
                        item.CloseComponent();

                        item.OpenComponent<L.NavigationMenuContent>(1);
                        item.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Products content")));
                        item.CloseComponent();
                    }));
                    list.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    [Fact]
    public void Closing_Keeps_Content_Mounted_With_DataState_Closed_And_FadeOut()
    {
        var cut = RenderNav();
        cut.Find("button").Click();
        Assert.Equal("open", cut.Find("[role='menu']").GetAttribute("data-state"));

        // Close by re-clicking the trigger. The close render commits synchronously in
        // bUnit; assert the exit state directly (no poll) so the ~250ms fallback
        // unmount can't race a delayed first poll.
        cut.Find("button").Click();

        var menu = cut.Find("[role='menu']");
        Assert.Equal("closed", menu.GetAttribute("data-state"));
        Assert.Contains("animate-fade-out", menu.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Exiting_Content_Is_PointerEventsNone_And_Inert()
    {
        // Round-2 P2: round 1 missed NavigationMenuContent when it added the exit-window
        // pointer-events-none to the other overlays, and never gave it keyboard inertness.
        // The fading panel must be both pointer-inert (pointer-events-none) AND keyboard-
        // inert (`inert`), so its native links/buttons leave the tab order while it fades.
        var cut = RenderNav();
        cut.Find("button").Click();
        var open = cut.Find("[role='menu']");
        Assert.DoesNotContain("pointer-events-none", open.GetAttribute("class") ?? "");
        Assert.False(open.HasAttribute("inert"));

        cut.Find("button").Click(); // close → exit window

        var menu = cut.Find("[role='menu']");
        Assert.Equal("closed", menu.GetAttribute("data-state"));
        Assert.Contains("pointer-events-none", menu.GetAttribute("class") ?? "");
        Assert.True(menu.HasAttribute("inert"));
    }

    [Fact]
    public void Exit_Eventually_Unmounts_The_Content()
    {
        var cut = RenderNav();
        cut.Find("button").Click();
        cut.Find("button").Click();

        // Stable end-state poll; inherits the 10 s module ceiling (TestContextExtensions)
        // so a starved CI thread pool delaying the fallback-timer dispatch can't trip it.
        cut.WaitForAssertion(() => Assert.DoesNotContain("Products content", cut.Markup));
    }
}
