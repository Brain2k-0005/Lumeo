using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.HoverCard;

/// <summary>
/// #220 — HoverCard opens on keyboard focus and closes on blur (Radix parity).
/// The trigger wrapper handles focusin/focusout so focusing its inner link/button
/// shows the card and tabbing away hides it.
/// </summary>
public class HoverCardFocusTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public HoverCardFocusTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.HoverCard> RenderCard(EventCallback<bool>? openChanged = null)
    {
        return _ctx.Render<L.HoverCard>(builder =>
        {
            builder.Add(c => c.OpenDelay, 0);
            builder.Add(c => c.CloseDelay, 0);
            if (openChanged.HasValue)
                builder.Add(c => c.OpenChanged, openChanged.Value);
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

    // The trigger is the inline-flex wrapper div. When closed, aria-expanded is
    // omitted by Blazor (bound bool false), so we select by the trigger class.
    private static IElement Trigger(IRenderedComponent<L.HoverCard> cut) =>
        cut.FindAll("div").First(d => (d.GetAttribute("class") ?? "").Contains("inline-flex"));

    [Fact]
    public void FocusIn_Opens_The_Card()
    {
        bool? opened = null;
        var cb = EventCallback.Factory.Create<bool>(_ctx, (bool v) => opened = v);
        var cut = RenderCard(cb);

        Assert.DoesNotContain("Card body", cut.Markup);

        Trigger(cut).FocusIn();

        cut.WaitForAssertion(() => Assert.True(opened));
        cut.WaitForAssertion(() => Assert.Contains("Card body", cut.Markup));
    }

    [Fact]
    public void FocusOut_Closes_The_Open_Card()
    {
        bool? lastValue = null;
        var cb = EventCallback.Factory.Create<bool>(_ctx, (bool v) => lastValue = v);
        var cut = RenderCard(cb);

        Trigger(cut).FocusIn();
        cut.WaitForAssertion(() => Assert.Contains("Card body", cut.Markup));

        Trigger(cut).FocusOut();

        cut.WaitForAssertion(() => Assert.False(lastValue));
        cut.WaitForAssertion(() => Assert.DoesNotContain("Card body", cut.Markup));
    }
}
