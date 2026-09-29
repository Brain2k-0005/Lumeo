using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.HoverCard;

/// <summary>
/// Overlay-disposal-race regression (5.12.2) — see
/// <see cref="Lumeo.Tests.Components.Tooltip.TooltipDisposalRaceTests"/> for the full
/// writeup. HoverCardContent goes through the same shared
/// <see cref="Lumeo.Services.OverlayExitAnimator"/> exit latch as Tooltip.
/// </summary>
public class HoverCardDisposalRaceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly RenderFragment _child;

    public HoverCardDisposalRaceTests()
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

    [Fact]
    public void AttachOverlayExitEnd_ObjectDisposedException_Does_Not_Escape_The_Renderer()
    {
        var module = _ctx.SetupComponentsModule();
        module.SetupVoid("attachOverlayExitEnd", _ => true)
            .SetException(new ObjectDisposedException("DotNetObjectReference`1[[Lumeo.HoverCardContent]]"));

        var cut = RenderCard(open: true);
        Assert.Contains("Card body", cut.Markup);

        var ex = Record.Exception(() => cut.Render(p => p.Add(c => c.Open, false).Add(c => c.ChildContent, _child)));
        Assert.Null(ex);
    }
}
