using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Collapsible;

/// <summary>
/// Behavior/a11y coverage for the Collapsible disclosure primitive.
/// Exercises the stateful disclosure contract:
///   - the trigger (<c>role=button</c>) flips <c>aria-expanded</c> true/false on click,
///   - the content region (<c>role=region</c>) flips <c>aria-hidden</c> in lock-step,
///   - keyboard activation (Enter / Space) toggles like a native button,
///   - controlled mode delegates state to the parent via <c>OpenChanged</c> and
///     does NOT self-mutate, while uncontrolled mode self-manages.
/// Assertions key off ARIA state rather than animation CSS classes so they stay
/// robust to styling churn.
/// </summary>
public class CollapsibleBehaviorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CollapsibleBehaviorTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const string TriggerText = "Toggle me";
    private const string BodyText = "Disclosure body";

    /// <param name="open">initial Open</param>
    /// <param name="onOpenChanged">when non-null the component is treated as controlled</param>
    private IRenderedComponent<L.Collapsible> RenderCollapsible(
        bool open,
        EventCallback<bool>? onOpenChanged = null)
    {
        return _ctx.Render<L.Collapsible>(p =>
        {
            p.Add(c => c.Open, open);
            if (onOpenChanged is { } cb)
                p.Add(c => c.OpenChanged, cb);
            p.Add(c => c.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.CollapsibleTrigger>(0);
                b.AddAttribute(1, "ChildContent",
                    (RenderFragment)(t => t.AddContent(0, TriggerText)));
                b.CloseComponent();

                b.OpenComponent<L.CollapsibleContent>(2);
                b.AddAttribute(3, "ChildContent",
                    (RenderFragment)(c => c.AddContent(0, BodyText)));
                b.CloseComponent();
            }));
        });
    }

    private static IElement Trigger(IRenderedComponent<L.Collapsible> cut)
        => cut.Find("[role='button']");

    private static IElement ContentRegion(IRenderedComponent<L.Collapsible> cut)
        => cut.Find("[role='region']");

    [Fact]
    public void ClosedByDefault_TriggerExpandedFalse_ContentHidden()
    {
        var cut = RenderCollapsible(open: false);

        Assert.Equal("false", Trigger(cut).GetAttribute("aria-expanded"));
        Assert.Equal("true", ContentRegion(cut).GetAttribute("aria-hidden"));
    }

    [Fact]
    public void ClickingTrigger_FlipsAriaExpandedAndAriaHidden_Open()
    {
        var cut = RenderCollapsible(open: false);

        Trigger(cut).Click();

        // Uncontrolled: state is self-managed, so ARIA flips immediately.
        Assert.Equal("true", Trigger(cut).GetAttribute("aria-expanded"));
        Assert.Equal("false", ContentRegion(cut).GetAttribute("aria-hidden"));
    }

    [Fact]
    public void ClickingTrigger_TogglesBackToClosed_OnSecondClick()
    {
        var cut = RenderCollapsible(open: false);

        Trigger(cut).Click();
        Assert.Equal("true", Trigger(cut).GetAttribute("aria-expanded"));

        Trigger(cut).Click();
        Assert.Equal("false", Trigger(cut).GetAttribute("aria-expanded"));
        Assert.Equal("true", ContentRegion(cut).GetAttribute("aria-hidden"));
    }

    [Fact]
    public void EnterKey_TogglesDisclosure_LikeNativeButton()
    {
        var cut = RenderCollapsible(open: false);

        Trigger(cut).KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("true", Trigger(cut).GetAttribute("aria-expanded"));
        Assert.Equal("false", ContentRegion(cut).GetAttribute("aria-hidden"));
    }

    [Fact]
    public void SpaceKey_TogglesDisclosure_LikeNativeButton()
    {
        var cut = RenderCollapsible(open: true);

        Trigger(cut).KeyDown(new KeyboardEventArgs { Key = " " });

        Assert.Equal("false", Trigger(cut).GetAttribute("aria-expanded"));
        Assert.Equal("true", ContentRegion(cut).GetAttribute("aria-hidden"));
    }

    [Fact]
    public void ControlledMode_RaisesOpenChanged_WithoutSelfMutating()
    {
        bool? raised = null;
        var cb = EventCallback.Factory.Create<bool>(this, v => raised = v);

        var cut = RenderCollapsible(open: false, onOpenChanged: cb);

        Trigger(cut).Click();

        // The callback fires with the requested next value...
        Assert.True(raised);
        // ...but because the parent (test) never pushed Open=true back down,
        // the controlled component must NOT desync by flipping its own state.
        Assert.Equal("false", Trigger(cut).GetAttribute("aria-expanded"));
        Assert.Equal("true", ContentRegion(cut).GetAttribute("aria-hidden"));
    }

    [Fact]
    public void ControlledMode_ParentDrivenOpen_FlipsAriaState()
    {
        bool open = true;
        var cb = EventCallback.Factory.Create<bool>(this, v => open = v);

        var cut = RenderCollapsible(open: open, onOpenChanged: cb);
        Assert.Equal("true", Trigger(cut).GetAttribute("aria-expanded"));

        // Simulate a controlled parent reacting to OpenChanged by re-rendering
        // with the new value.
        Trigger(cut).Click();
        cut.Render(p => p.Add(c => c.Open, open));

        Assert.Equal("false", Trigger(cut).GetAttribute("aria-expanded"));
        Assert.Equal("true", ContentRegion(cut).GetAttribute("aria-hidden"));
    }

    [Fact]
    public void TriggerAndContent_AreWiredViaAriaControlsAndLabelledby()
    {
        var cut = RenderCollapsible(open: true);

        var trigger = Trigger(cut);
        var content = ContentRegion(cut);

        // The trigger points at the content id, and the content points back at
        // the trigger id — the disclosure association assistive tech relies on.
        Assert.Equal(content.GetAttribute("id"), trigger.GetAttribute("aria-controls"));
        Assert.Equal(trigger.GetAttribute("id"), content.GetAttribute("aria-labelledby"));
    }
}
