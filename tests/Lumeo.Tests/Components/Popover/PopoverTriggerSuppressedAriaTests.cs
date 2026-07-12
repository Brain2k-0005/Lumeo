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
}
