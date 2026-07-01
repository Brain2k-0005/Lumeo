using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.HoverCard;

/// <summary>
/// battle-wave2 #81 (state-on-data-change) — HoverCard stored its live open state
/// directly in the <c>Open</c> [Parameter]. Two consequences:
///   (a) hover-opened state was reverted by any unrelated parent re-render that
///       re-pushed the original <c>Open</c> literal, and
///   (b) a consumer-supplied <c>Open=true</c> was overwritten by hover-leave.
/// The fix keeps live open-state in a private backing field (<c>_open</c>) that is
/// reseeded from <c>Open</c> only when the parent genuinely changes it, mirroring
/// the Dialog controlled/uncontrolled split. A same-value re-render no longer
/// clobbers the user's hover, while a real parent change still wins.
/// </summary>
public class HoverCardControlledStateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public HoverCardControlledStateTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // The trigger + content body. OpenDelay 0 so the hover open commits
    // synchronously for the assertion. Open is intentionally NEVER supplied here —
    // this is the uncontrolled scenario.
    private static RenderFragment Body => b =>
    {
        b.OpenComponent<L.HoverCardTrigger>(0);
        b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Profile")));
        b.CloseComponent();

        b.OpenComponent<L.HoverCardContent>(2);
        b.AddAttribute(3, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Card body")));
        b.CloseComponent();
    };

    private IRenderedComponent<L.HoverCard> RenderUncontrolled(int closeDelay = 0)
        => _ctx.Render<L.HoverCard>(p => p
            .Add(c => c.OpenDelay, 0)
            .Add(c => c.CloseDelay, closeDelay)
            .Add(c => c.ChildContent, Body));

    // The trigger is the inline-flex wrapper div (aria-expanded is omitted by
    // Blazor when the bound bool is false, so select by the trigger class).
    private static IElement Trigger(IRenderedComponent<L.HoverCard> cut) =>
        cut.FindAll("div").First(d => (d.GetAttribute("class") ?? "").Contains("inline-flex"));

    [Fact]
    public void HoverOpened_Survives_An_Unrelated_Parent_Rerender()
    {
        // Uncontrolled (no OpenChanged bound, no Open supplied). Hover opens the card.
        var cut = RenderUncontrolled();
        Assert.DoesNotContain("Card body", cut.Markup);

        Trigger(cut).MouseEnter();
        cut.WaitForAssertion(() => Assert.Contains("Card body", cut.Markup));

        // An unrelated parent re-render: change an UNRELATED parameter (CloseDelay)
        // without ever supplying Open. Without the fix the hover state lived in the
        // Open [Parameter] and this re-render reverted it back to the false default;
        // with the fix the live open state is in the backing field and survives.
        cut.Render(p => p
            .Add(c => c.OpenDelay, 0)
            .Add(c => c.CloseDelay, 999)
            .Add(c => c.ChildContent, Body));

        Assert.Contains("Card body", cut.Markup);
    }

    [Fact]
    public void Parent_That_Genuinely_Changes_Open_Still_Wins()
    {
        // Guards against the fix over-correcting: a real parent change of Open must
        // still drive the rendered state. Open=false initially, then Open=true.
        var cut = _ctx.Render<L.HoverCard>(p => p
            .Add(c => c.Open, false)
            .Add(c => c.ChildContent, Body));
        Assert.DoesNotContain("Card body", cut.Markup);

        cut.Render(p => p
            .Add(c => c.Open, true)
            .Add(c => c.ChildContent, Body));

        Assert.Contains("Card body", cut.Markup);
    }
}
