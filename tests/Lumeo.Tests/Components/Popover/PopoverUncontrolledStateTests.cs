using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Popover;

/// <summary>
/// battle-wave2 #183 (state-on-data-change) — an uncontrolled Popover (no
/// OpenChanged binding) used to store its open/closed state by writing back into
/// the [Parameter] Open. Blazor re-applies [Parameter] values from the parent on
/// every render, so any unrelated parent re-render that pushed the original Open
/// value down silently reset the popover — the surface the user opened snapped
/// shut. State must live in a private backing field (EffectiveOpen) that parent
/// re-renders cannot clobber (precedent #3 / Collapsible #246).
/// </summary>
public class PopoverUncontrolledStateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PopoverUncontrolledStateTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const string TriggerText = "Toggle";
    private const string BodyText = "Popover body";

    private static RenderFragment Children() => b =>
    {
        b.OpenComponent<L.PopoverTrigger>(0);
        b.AddAttribute(1, "ChildContent",
            (RenderFragment)(t => t.AddContent(0, TriggerText)));
        b.CloseComponent();

        b.OpenComponent<L.PopoverContent>(2);
        b.AddAttribute(3, "ChildContent",
            (RenderFragment)(c => c.AddContent(0, BodyText)));
        b.CloseComponent();
    };

    // Uncontrolled: Open supplied as a one-way value, NO OpenChanged delegate.
    private IRenderedComponent<L.Popover> RenderUncontrolled(bool open)
        => _ctx.Render<L.Popover>(p =>
        {
            p.Add(c => c.Open, open);
            p.Add(c => c.ChildContent, Children());
        });

    private static IElement Trigger(IRenderedComponent<L.Popover> cut)
        => cut.Find("[role='button']");

    [Fact]
    public void Uncontrolled_OpenState_SurvivesParentReRenderWithSameOpenParam()
    {
        // Uncontrolled (no OpenChanged), initially closed.
        var cut = RenderUncontrolled(open: false);
        Assert.Equal("false", Trigger(cut).GetAttribute("aria-expanded"));
        Assert.DoesNotContain(BodyText, cut.Markup);

        // User opens it via the trigger.
        Trigger(cut).Click();
        Assert.Equal("true", Trigger(cut).GetAttribute("aria-expanded"));
        Assert.Contains(BodyText, cut.Markup);

        // An unrelated parent re-render re-applies the SAME original Open=false.
        // Before the fix this reset the [Parameter]-stored state and closed the
        // popover; after the fix the private backing field is preserved.
        cut.Render(p =>
        {
            p.Add(c => c.Open, false);
            p.Add(c => c.ChildContent, Children());
        });

        Assert.Equal("true", Trigger(cut).GetAttribute("aria-expanded"));
        Assert.Contains(BodyText, cut.Markup);
    }

    [Fact]
    public void Uncontrolled_RespectsImperativeOpenParamChange()
    {
        // Guard: the fix must NOT freeze the component to its first value.
        // A caller that flips the Open parameter (without two-way binding)
        // should still drive the popover.
        var cut = RenderUncontrolled(open: false);
        Assert.Equal("false", Trigger(cut).GetAttribute("aria-expanded"));

        cut.Render(p =>
        {
            p.Add(c => c.Open, true);
            p.Add(c => c.ChildContent, Children());
        });

        Assert.Equal("true", Trigger(cut).GetAttribute("aria-expanded"));
        Assert.Contains(BodyText, cut.Markup);
    }
}
