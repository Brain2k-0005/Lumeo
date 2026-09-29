using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ContextMenu;

/// <summary>
/// Overlay-disposal-race regression (5.12.2) — see
/// <see cref="Lumeo.Tests.Components.Tooltip.TooltipDisposalRaceTests"/> for the full
/// writeup. ContextMenuContent goes through the same shared
/// <see cref="Lumeo.Services.OverlayExitAnimator"/> exit latch as Tooltip; disposal
/// racing an in-flight OnAfterRenderAsync await used to reach
/// <c>attachOverlayExitEnd</c> with a disposed <c>DotNetObjectReference</c>, which
/// killed the circuit.
/// </summary>
public class ContextMenuDisposalRaceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly RenderFragment _child;

    public ContextMenuDisposalRaceTests()
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
    public void AttachOverlayExitEnd_ObjectDisposedException_Does_Not_Escape_The_Renderer()
    {
        var module = _ctx.SetupComponentsModule();
        module.SetupVoid("attachOverlayExitEnd", _ => true)
            .SetException(new ObjectDisposedException("DotNetObjectReference`1[[Lumeo.ContextMenuContent]]"));

        var cut = RenderMenu(open: true);
        Assert.NotEmpty(cut.FindAll("[role='menu']"));

        var ex = Record.Exception(() => cut.Render(p => p.Add(m => m.Open, false).Add(m => m.ChildContent, _child)));
        Assert.Null(ex);
    }
}
