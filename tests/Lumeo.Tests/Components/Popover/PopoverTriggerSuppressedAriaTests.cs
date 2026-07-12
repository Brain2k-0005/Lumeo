using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Popover;

/// <summary>
/// PR #356 round-7 (Codex P2) — with <see cref="L.PopoverTrigger.SuppressActivationKeys"/>
/// set, the wrapper's own <c>tabindex</c> drops to -1 because a focusable child inside
/// ChildContent already owns the real Tab stop (e.g. DatePicker's typeable input/button).
/// Before this fix, <c>aria-haspopup</c>/<c>aria-expanded</c>/<c>aria-controls</c> stayed
/// ONLY on that now-unfocusable wrapper, so a keyboard/SR user landing on the child never
/// heard the popup relationship or its expanded state. Mirrors the AsChild mechanism
/// (<see cref="PopoverTriggerAsChildTests"/>): a consumer that opts into
/// <c>ChildContentSlot</c> (instead of plain <c>ChildContent</c>) receives the same
/// <c>TriggerSlot</c> and can splat <c>slot.Attributes</c> onto its own focusable element.
/// </summary>
public class PopoverTriggerSuppressedAriaTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PopoverTriggerSuppressedAriaTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> Render(bool open, bool useChildContentSlot)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Popover>(0);
            builder.AddAttribute(1, "Open", open);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.PopoverTrigger>(0);
                b.AddAttribute(1, "SuppressActivationKeys", true);
                if (useChildContentSlot)
                {
                    b.AddAttribute(2, "ChildContentSlot", (RenderFragment<L.TriggerSlot>)(slot => t =>
                    {
                        t.OpenElement(0, "input");
                        t.AddMultipleAttributes(1, slot.Attributes);
                        t.CloseElement();
                    }));
                }
                else
                {
                    b.AddAttribute(2, "ChildContent", (RenderFragment)(t =>
                    {
                        t.OpenElement(0, "input");
                        t.CloseElement();
                    }));
                }
                b.CloseComponent();

                b.OpenComponent<L.PopoverContent>(1);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void SuppressActivationKeys_Drops_The_Wrapper_Out_Of_The_Tab_Order()
    {
        var cut = Render(open: false, useChildContentSlot: true);
        Assert.Equal("-1", cut.Find("div[role='button']").GetAttribute("tabindex"));
    }

    [Fact]
    public void ChildContentSlot_Puts_Aria_State_On_The_Actual_Focusable_Child()
    {
        var cut = Render(open: true, useChildContentSlot: true);

        var input = cut.Find("input");
        Assert.Equal("dialog", input.GetAttribute("aria-haspopup"));
        Assert.Equal("true", input.GetAttribute("aria-expanded"));
        Assert.False(string.IsNullOrEmpty(input.GetAttribute("aria-controls")));
    }

    [Fact]
    public void ChildContentSlot_Aria_Expanded_Reflects_Closed_State_Too()
    {
        var cut = Render(open: false, useChildContentSlot: true);

        var input = cut.Find("input");
        Assert.Equal("false", input.GetAttribute("aria-expanded"));
        Assert.Null(input.GetAttribute("aria-controls"));
    }

    [Fact]
    public void Plain_ChildContent_Leaves_The_Child_Without_Aria_State_Documented_Gap()
    {
        // Pins the boundary of the fix: a consumer that has NOT opted into
        // ChildContentSlot keeps the pre-existing wrapper-only behaviour (the
        // wrapper still carries the state — see the next test) rather than
        // silently gaining it on an element that never asked for the splat.
        var cut = Render(open: true, useChildContentSlot: false);

        var input = cut.Find("input");
        Assert.Null(input.GetAttribute("aria-haspopup"));
        Assert.Null(input.GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Wrapper_Still_Carries_Aria_State_Regardless_Of_ChildContentSlot()
    {
        // The wrapper's own attributes are unchanged/unremoved by this fix — the
        // fix is additive (hand the state to the child too), not a relocation.
        var cut = Render(open: true, useChildContentSlot: true);

        var wrapper = cut.Find("div[role='button']");
        Assert.Equal("dialog", wrapper.GetAttribute("aria-haspopup"));
        Assert.Equal("true", wrapper.GetAttribute("aria-expanded"));
    }

    /// <summary>
    /// PR #356 round-8 (Codex P2) — the slot handed to <c>ChildContentSlot</c> in this
    /// SuppressActivationKeys path used to be the SAME live <see cref="L.TriggerSlot"/>
    /// as the AsChild branch (whose own doc comment tells consumers to wire
    /// <c>slot.OnClick</c> onto their child). Doing that HERE — where the wrapper
    /// &lt;div&gt; still owns its own <c>@onclick="Toggle"</c> — would toggle once on
    /// the child and again when the click bubbles to the wrapper, so the popover would
    /// appear not to open (or immediately re-close). The suppressed slot's
    /// <c>OnClick</c>/<c>OnKeyDown</c> must be inert instead: even if a consumer follows
    /// the general guidance, nothing double-fires.
    /// </summary>
    [Fact]
    public void SuppressedSlot_OnClick_Has_No_Delegate()
    {
        L.TriggerSlot? captured = null;
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Popover>(0);
            builder.AddAttribute(1, "Open", false);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.PopoverTrigger>(0);
                b.AddAttribute(1, "SuppressActivationKeys", true);
                b.AddAttribute(2, "ChildContentSlot", (RenderFragment<L.TriggerSlot>)(slot => t =>
                {
                    captured = slot;
                    t.OpenElement(0, "input");
                    t.CloseElement();
                }));
                b.CloseComponent();

                b.OpenComponent<L.PopoverContent>(1);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.NotNull(captured);
        Assert.False(captured!.OnClick.HasDelegate);
        Assert.False(captured.OnKeyDown.HasDelegate);
        // Attributes are unaffected — same aria state as the live Slot would carry.
        Assert.True(captured.Attributes.ContainsKey("aria-haspopup"));
    }

    /// <summary>
    /// End-to-end version of the same guarantee, exercising the exact double-toggle
    /// scenario Codex flagged: a consumer wires <c>slot.OnClick</c> onto their own
    /// child (the general <c>ChildContentSlot</c> guidance) inside this
    /// SuppressActivationKeys path, where the wrapper &lt;div&gt; ALSO still owns its
    /// own <c>@onclick="Toggle"</c>. Blazor's <c>@onclick</c> bubbles (bUnit's renderer
    /// replicates this): clicking the child fires the child's own handler AND the
    /// bubbled wrapper handler. Before this fix, both were the SAME live
    /// <c>Toggle</c> — one toggle call from the child, one more from the bubble —
    /// so a single click fired <c>OpenChanged</c> TWICE (open, then immediately
    /// close again), and the popover appeared not to open. With the inert
    /// <c>SuppressedSlot.OnClick</c>, only the wrapper's bubbled handler fires:
    /// exactly ONE toggle per click.
    /// </summary>
    [Fact]
    public void SuppressedSlot_OnClick_Wired_Onto_Child_Toggles_Only_Once_Per_Click()
    {
        var toggleCount = 0;
        var openCb = EventCallback.Factory.Create<bool>(_ctx, (bool _) => toggleCount++);

        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Popover>(0);
            builder.AddAttribute(1, "Open", false);
            builder.AddAttribute(2, "OpenChanged", openCb);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.PopoverTrigger>(0);
                b.AddAttribute(1, "SuppressActivationKeys", true);
                b.AddAttribute(2, "ChildContentSlot", (RenderFragment<L.TriggerSlot>)(slot => t =>
                {
                    t.OpenElement(0, "input");
                    t.AddAttribute(1, "onclick", slot.OnClick); // the (mis)guided wiring
                    t.CloseElement();
                }));
                b.CloseComponent();

                b.OpenComponent<L.PopoverContent>(1);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.Find("input").Click();

        // Exactly one toggle (from the wrapper's own @onclick, via bubbling) — NOT
        // two (which a live slot.OnClick would add on top, double-toggling).
        Assert.Equal(1, toggleCount);
    }

    /// <summary>
    /// PR #356 round-9 (Codex P2) — <c>BuildAttrs()</c> copies <c>AdditionalAttributes</c>
    /// verbatim (author wins on collision) into <see cref="L.TriggerSlot.Attributes"/>, so
    /// a consumer-splatted <c>id</c> on the trigger used to flow into the SuppressedSlot's
    /// Attributes too. A consumer following the documented
    /// <c>@attributes="slot.Attributes"</c> ChildContentSlot guidance then put that SAME id
    /// on their own focusable child — while the wrapper &lt;div&gt; ALSO renders it via
    /// <c>EffectiveTriggerId</c> — producing two elements with the identical id, breaking
    /// <c>aria-controls</c>/<c>getElementById</c> resolution. The wrapper must be the sole
    /// id owner; the suppressed slot's Attributes must exclude "id".
    /// </summary>
    [Fact]
    public void SuppressedSlot_Attributes_Does_Not_Duplicate_The_Wrapper_Id()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Popover>(0);
            builder.AddAttribute(1, "Open", false);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.PopoverTrigger>(0);
                b.AddAttribute(1, "SuppressActivationKeys", true);
                b.AddAttribute(2, "id", "my-trigger"); // consumer-splatted id -> AdditionalAttributes
                b.AddAttribute(3, "ChildContentSlot", (RenderFragment<L.TriggerSlot>)(slot => t =>
                {
                    t.OpenElement(0, "input");
                    t.AddMultipleAttributes(1, slot.Attributes);
                    t.CloseElement();
                }));
                b.CloseComponent();

                b.OpenComponent<L.PopoverContent>(1);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // Exactly one element in the DOM carries the id — the wrapper.
        var withId = cut.FindAll("#my-trigger");
        Assert.Single(withId);
        Assert.Equal("div", withId[0].TagName.ToLowerInvariant());

        // The child received the rest of the slot's ARIA state, but not the id.
        var input = cut.Find("input");
        Assert.Null(input.GetAttribute("id"));
        Assert.Equal("dialog", input.GetAttribute("aria-haspopup"));
    }
}
