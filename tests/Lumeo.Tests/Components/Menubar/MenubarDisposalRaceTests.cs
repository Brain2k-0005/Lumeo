using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Menubar;

/// <summary>
/// Overlay-disposal-race regression (5.12.2) — see
/// <see cref="Lumeo.Tests.Components.Tooltip.TooltipDisposalRaceTests"/> for the full
/// writeup. MenubarContent goes through the same shared
/// <see cref="Lumeo.Services.OverlayExitAnimator"/> exit latch as Tooltip.
/// </summary>
public class MenubarDisposalRaceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MenubarDisposalRaceTests() => _ctx.AddLumeoServices();
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
    public void AttachOverlayExitEnd_ObjectDisposedException_Does_Not_Escape_The_Renderer()
    {
        var module = _ctx.SetupComponentsModule();
        module.SetupVoid("attachOverlayExitEnd", _ => true)
            .SetException(new ObjectDisposedException("DotNetObjectReference`1[[Lumeo.MenubarContent]]"));

        var cut = RenderMenubar();
        cut.Find("button").Click();
        Assert.Equal("open", cut.Find("[role='menu']").GetAttribute("data-state"));

        var ex = Record.Exception(() => cut.Find("button").Click());
        Assert.Null(ex);
    }
}
