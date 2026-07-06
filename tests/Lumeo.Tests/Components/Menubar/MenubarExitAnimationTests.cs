using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Menubar;

/// <summary>
/// Wave 1 (B11 exit parity). On close MenubarContent stays mounted with
/// data-state="closed" and its zoom-out exit class for the exit window, then
/// unmounts. The bUnit unmount is driven by the DelayedDispatch fallback timer.
/// </summary>
public class MenubarExitAnimationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MenubarExitAnimationTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderMenubar()
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Menubar>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.MenubarMenu>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(menu =>
                {
                    menu.OpenComponent<L.MenubarTrigger>(0);
                    menu.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "File")));
                    menu.CloseComponent();

                    menu.OpenComponent<L.MenubarContent>(1);
                    menu.AddAttribute(2, "ChildContent", (RenderFragment)(content => content.AddContent(0, "New File")));
                    menu.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    [Fact]
    public void Closing_Keeps_Content_Mounted_With_DataState_Closed_And_ZoomOut()
    {
        var cut = RenderMenubar();
        cut.Find("button").Click();
        Assert.Equal("open", cut.Find("[role='menu']").GetAttribute("data-state"));

        // Close by re-clicking the trigger. The close render commits synchronously in
        // bUnit; assert the exit state directly (no poll) so the ~250ms fallback
        // unmount can't race a delayed first poll.
        cut.Find("button").Click();

        var menu = cut.Find("[role='menu']");
        Assert.Equal("closed", menu.GetAttribute("data-state"));
        Assert.Contains("animate-zoom-out", menu.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Exit_Eventually_Unmounts_The_Content()
    {
        var cut = RenderMenubar();
        cut.Find("button").Click();
        cut.Find("button").Click();

        cut.WaitForAssertion(
            () => Assert.Empty(cut.FindAll("[role='menu']")),
            timeout: TimeSpan.FromSeconds(5));
    }
}
