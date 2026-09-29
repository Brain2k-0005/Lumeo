using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.NavigationMenu;

/// <summary>
/// Overlay-disposal-race regression (5.12.2) — see
/// <see cref="Lumeo.Tests.Components.Tooltip.TooltipDisposalRaceTests"/> for the full
/// writeup. NavigationMenuViewport goes through the same shared
/// <see cref="Lumeo.Services.OverlayExitAnimator"/> exit latch as Tooltip.
/// </summary>
public class NavigationMenuViewportDisposalRaceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public NavigationMenuViewportDisposalRaceTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.NavigationMenu> RenderNav(string? value)
        => _ctx.Render<L.NavigationMenu>(p =>
        {
            p.Add(m => m.Value, value);
            p.Add(m => m.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.NavigationMenuList>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(list =>
                {
                    list.OpenComponent<L.NavigationMenuItem>(0);
                    list.AddAttribute(1, "Value", "a");
                    list.AddAttribute(2, "ChildContent", (RenderFragment)(item =>
                    {
                        item.OpenComponent<L.NavigationMenuTrigger>(0);
                        item.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Alpha")));
                        item.CloseComponent();
                    }));
                    list.CloseComponent();
                }));
                b.CloseComponent();

                b.OpenComponent<L.NavigationMenuViewport>(2);
                b.CloseComponent();
            }));
        });

    [Fact]
    public void AttachOverlayExitEnd_ObjectDisposedException_Does_Not_Escape_The_Renderer()
    {
        var module = _ctx.SetupComponentsModule();
        module.SetupVoid("attachOverlayExitEnd", _ => true)
            .SetException(new ObjectDisposedException("DotNetObjectReference`1[[Lumeo.NavigationMenuViewport]]"));

        // Value seeds an open item so the viewport (gated on ActiveItemId != null)
        // renders open immediately.
        var cut = RenderNav("a");
        Assert.NotEmpty(cut.FindAll("[data-slot='navigation-menu-viewport']"));

        var ex = Record.Exception(() => cut.Render(p => p.Add(m => m.Value, (string?)null)));
        Assert.Null(ex);
    }
}
