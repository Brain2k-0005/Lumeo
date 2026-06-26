using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Collapsible;

/// <summary>
/// Regression coverage for #246: an uncontrolled Collapsible (no OpenChanged
/// binding) used to store its open/closed state by writing back into the
/// [Parameter] Open. Blazor re-applies [Parameter] values from the parent on
/// every render, so any unrelated parent re-render that pushed the original
/// Open value down silently reset the disclosure — the user's expanded panel
/// snapped shut. State must live in a private backing field that parent
/// re-renders cannot clobber.
/// </summary>
public class CollapsibleUncontrolledStateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CollapsibleUncontrolledStateTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const string TriggerText = "Toggle me";
    private const string BodyText = "Disclosure body";

    private static RenderFragment Children() => b =>
    {
        b.OpenComponent<L.CollapsibleTrigger>(0);
        b.AddAttribute(1, "ChildContent",
            (RenderFragment)(t => t.AddContent(0, TriggerText)));
        b.CloseComponent();

        b.OpenComponent<L.CollapsibleContent>(2);
        b.AddAttribute(3, "ChildContent",
            (RenderFragment)(c => c.AddContent(0, BodyText)));
        b.CloseComponent();
    };

    private IRenderedComponent<L.Collapsible> RenderUncontrolled(bool open)
        => _ctx.Render<L.Collapsible>(p =>
        {
            p.Add(c => c.Open, open);
            p.Add(c => c.ChildContent, Children());
        });

    private static IElement Trigger(IRenderedComponent<L.Collapsible> cut)
        => cut.Find("[role='button']");

    private static IElement ContentRegion(IRenderedComponent<L.Collapsible> cut)
        => cut.Find("[role='region']");

    [Fact]
    public void Uncontrolled_OpenState_SurvivesParentReRenderWithSameOpenParam()
    {
        // Uncontrolled (no OpenChanged), initially closed.
        var cut = RenderUncontrolled(open: false);
        Assert.Equal("false", Trigger(cut).GetAttribute("aria-expanded"));

        // User opens it.
        Trigger(cut).Click();
        Assert.Equal("true", Trigger(cut).GetAttribute("aria-expanded"));
        Assert.Equal("false", ContentRegion(cut).GetAttribute("aria-hidden"));

        // An unrelated parent re-render re-applies the SAME original Open=false.
        // Before the fix this reset the [Parameter]-stored state and collapsed
        // the panel; after the fix the private backing field is preserved.
        cut.Render(p =>
        {
            p.Add(c => c.Open, false);
            p.Add(c => c.ChildContent, Children());
        });

        Assert.Equal("true", Trigger(cut).GetAttribute("aria-expanded"));
        Assert.Equal("false", ContentRegion(cut).GetAttribute("aria-hidden"));
    }

    [Fact]
    public void Uncontrolled_RespectsImperativeOpenParamChange()
    {
        // Guard: the fix must NOT freeze the component to its first value.
        // A caller that flips the Open parameter (without two-way binding)
        // should still drive the disclosure.
        var cut = RenderUncontrolled(open: false);
        Assert.Equal("false", Trigger(cut).GetAttribute("aria-expanded"));

        cut.Render(p =>
        {
            p.Add(c => c.Open, true);
            p.Add(c => c.ChildContent, Children());
        });

        Assert.Equal("true", Trigger(cut).GetAttribute("aria-expanded"));
        Assert.Equal("false", ContentRegion(cut).GetAttribute("aria-hidden"));
    }
}
