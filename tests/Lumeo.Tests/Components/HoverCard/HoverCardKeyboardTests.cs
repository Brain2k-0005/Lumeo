using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.HoverCard;

/// <summary>
/// Wave 4 composition audit — HoverCard's keyboard-parity path (Tab-focus opens,
/// blur closes) is already covered by HoverCardFocusTests. This file fills the
/// one remaining neededTests gap: the tap-to-pin path (@onclick
/// RequestTogglePin) that keeps the card open on touch/click devices even after
/// focus moves elsewhere — distinct from the hover/focus auto-close path.
/// No Escape handler exists in HoverCard/HoverCardContent/HoverCardTrigger
/// (verified by source inspection); focus-out already closes it, which is the
/// documented, intentional behaviour, not a missing affordance.
/// </summary>
public class HoverCardKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public HoverCardKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.HoverCard> RenderCard()
    {
        return _ctx.Render<L.HoverCard>(builder =>
        {
            builder.Add(c => c.OpenDelay, 0);
            builder.Add(c => c.CloseDelay, 0);
            builder.Add(c => c.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.HoverCardTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Profile")));
                b.CloseComponent();

                b.OpenComponent<L.HoverCardContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Card body")));
                b.CloseComponent();
            }));
        });
    }

    private static IElement Trigger(IRenderedComponent<L.HoverCard> cut) =>
        cut.FindAll("div").First(d => (d.GetAttribute("class") ?? "").Contains("inline-flex"));

    [Fact]
    public void Tap_Pinning_Keeps_The_Card_Open_After_Focus_Leaves()
    {
        var cut = RenderCard();

        // Open via focus, then pin via click (the touch/tap path).
        Trigger(cut).FocusIn();
        cut.WaitForAssertion(() => Assert.Contains("Card body", cut.Markup));
        Trigger(cut).Click();

        // Without pinning, FocusOut would close the card (HoverCardFocusTests).
        // Pinned, it must survive focus leaving the trigger.
        Trigger(cut).FocusOut();

        Assert.Contains("Card body", cut.Markup);
    }

    [Fact]
    public void Unpinning_Via_A_Second_Tap_Allows_FocusOut_To_Close_Again()
    {
        var cut = RenderCard();

        Trigger(cut).FocusIn();
        cut.WaitForAssertion(() => Assert.Contains("Card body", cut.Markup));
        Trigger(cut).Click(); // pin
        Trigger(cut).Click(); // unpin

        // Unpinning while still "hovered" (no FocusOut since) collapses the card
        // itself (RequestTogglePin's own auto-collapse when not EffectiveOpen via
        // hover) — assert the card is gone once unpinned.
        cut.WaitForAssertion(() => Assert.DoesNotContain("Card body", cut.Markup));
    }
}
