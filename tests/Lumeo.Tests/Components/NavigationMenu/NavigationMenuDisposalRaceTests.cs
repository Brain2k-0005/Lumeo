using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.NavigationMenu;

/// <summary>
/// Overlay-disposal-race regression (5.12.2) — see
/// <see cref="Lumeo.Tests.Components.Tooltip.TooltipDisposalRaceTests"/> for the full
/// writeup. NavigationMenuContent goes through the same shared
/// <see cref="Lumeo.Services.OverlayExitAnimator"/> exit latch as Tooltip.
/// </summary>
public class NavigationMenuDisposalRaceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public NavigationMenuDisposalRaceTests() => _ctx.AddLumeoServices();
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
    public void AttachOverlayExitEnd_ObjectDisposedException_Does_Not_Escape_The_Renderer()
    {
        var module = _ctx.SetupComponentsModule();
        module.SetupVoid("attachOverlayExitEnd", _ => true)
            .SetException(new ObjectDisposedException("DotNetObjectReference`1[[Lumeo.NavigationMenuContent]]"));

        var cut = RenderNav();
        cut.Find("button").Click();
        Assert.Equal("open", cut.Find("[role='menu']").GetAttribute("data-state"));

        var ex = Record.Exception(() => cut.Find("button").Click());
        Assert.Null(ex);
    }
}
