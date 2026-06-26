using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.HoverCard;

/// <summary>
/// G34b — HoverCardTrigger AsChild folds the hover/focus open-close handlers and
/// aria-expanded onto a cooperating child (a Lumeo Button) so the pair renders as a
/// single element. This also exercises the Button's slot mouse/focus forwarding
/// (the Button consumes OnMouseEnter/OnFocusIn from the cascaded TriggerSlot).
/// </summary>
public class HoverCardAsChildTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public HoverCardAsChildTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.HoverCard> RenderAsChild(EventCallback<bool>? openChanged = null)
        => _ctx.Render<L.HoverCard>(builder =>
        {
            builder.Add(c => c.OpenDelay, 0);
            builder.Add(c => c.CloseDelay, 0);
            if (openChanged.HasValue) builder.Add(c => c.OpenChanged, openChanged.Value);
            builder.Add(c => c.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.HoverCardTrigger>(0);
                b.AddAttribute(1, "AsChild", true);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.Button>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Profile")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();

                b.OpenComponent<L.HoverCardContent>(3);
                b.AddAttribute(4, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Card body")));
                b.CloseComponent();
            }));
        });

    [Fact]
    public void AsChild_Folds_The_Trigger_Onto_The_Button_Without_A_Wrapper()
    {
        var cut = RenderAsChild();

        // The Button itself is the trigger: aria-expanded is folded onto the <button>,
        // and there is no separate inline-flex trigger wrapper div around it.
        var button = cut.Find("button");
        Assert.Equal("false", button.GetAttribute("aria-expanded"));
        Assert.DoesNotContain(cut.FindAll("div"),
            d => (d.GetAttribute("class") ?? "").Split(' ').Contains("inline-flex"));
    }

    [Fact]
    public void AsChild_Button_MouseEnter_Opens_The_Card()
    {
        // Proves the Button forwards the slot's OnMouseEnter (the splatted event
        // handler is actually wired) — without it the card would never open.
        bool? opened = null;
        var cb = EventCallback.Factory.Create<bool>(_ctx, (bool v) => opened = v);
        var cut = RenderAsChild(cb);

        Assert.DoesNotContain("Card body", cut.Markup);

        cut.Find("button").MouseEnter();

        cut.WaitForAssertion(() => Assert.True(opened));
        cut.WaitForAssertion(() => Assert.Contains("Card body", cut.Markup));
    }

    [Fact]
    public void AsChild_Button_FocusIn_Opens_The_Card()
    {
        bool? opened = null;
        var cb = EventCallback.Factory.Create<bool>(_ctx, (bool v) => opened = v);
        var cut = RenderAsChild(cb);

        cut.Find("button").FocusIn();

        cut.WaitForAssertion(() => Assert.True(opened));
    }

    [Fact]
    public void AsChild_Button_MouseLeave_Closes_The_Open_Card()
    {
        bool? lastValue = null;
        var cb = EventCallback.Factory.Create<bool>(_ctx, (bool v) => lastValue = v);
        var cut = RenderAsChild(cb);

        cut.Find("button").MouseEnter();
        cut.WaitForAssertion(() => Assert.Contains("Card body", cut.Markup));

        cut.Find("button").MouseLeave();

        cut.WaitForAssertion(() => Assert.False(lastValue));
    }
}
