using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ContextMenu;

/// <summary>
/// Overlay-disposal-race regression (5.12.2) — see
/// <see cref="Lumeo.Tests.Components.Tooltip.TooltipDisposalRaceTests"/> for the full
/// writeup. ContextMenuSubContent goes through the same shared
/// <see cref="Lumeo.Services.OverlayExitAnimator"/> exit latch as Tooltip.
/// </summary>
public class ContextMenuSubDisposalRaceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ContextMenuSubDisposalRaceTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private RenderFragment Child => b =>
    {
        b.OpenComponent<L.ContextMenuContent>(0);
        b.AddAttribute(1, "ChildContent", (RenderFragment)(content =>
        {
            content.OpenComponent<L.ContextMenuSub>(0);
            content.AddAttribute(1, "ChildContent", (RenderFragment)(sub =>
            {
                sub.OpenComponent<L.ContextMenuSubTrigger>(0);
                sub.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "More")));
                sub.CloseComponent();
                sub.OpenComponent<L.ContextMenuSubContent>(2);
                sub.AddAttribute(3, "ChildContent", (RenderFragment)(sc =>
                {
                    sc.OpenComponent<L.ContextMenuItem>(0);
                    sc.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Sub Item")));
                    sc.CloseComponent();
                }));
                sub.CloseComponent();
            }));
            content.CloseComponent();
        }));
        b.CloseComponent();
    };

    private IRenderedComponent<L.ContextMenu> RenderMenu(bool open)
        => _ctx.Render<L.ContextMenu>(p => p.Add(m => m.Open, open).Add(m => m.ChildContent, Child));

    [Fact]
    public void AttachOverlayExitEnd_ObjectDisposedException_Does_Not_Escape_The_Renderer()
    {
        var module = _ctx.SetupComponentsModule();
        module.SetupVoid("attachOverlayExitEnd", _ => true)
            .SetException(new ObjectDisposedException("DotNetObjectReference`1[[Lumeo.ContextMenuSubContent]]"));

        var cut = RenderMenu(open: true);
        cut.Find("button[aria-haspopup='menu']").Click();
        Assert.Equal("open", cut.Find("[id*='sub-content']").GetAttribute("data-state"));

        // Closing the root cascades the close to the open submenu — its exit latch
        // wires attachOverlayExitEnd through the same OverlayExitAnimator.
        var ex = Record.Exception(() => cut.Render(p => p.Add(m => m.Open, false).Add(m => m.ChildContent, Child)));
        Assert.Null(ex);
    }
}
