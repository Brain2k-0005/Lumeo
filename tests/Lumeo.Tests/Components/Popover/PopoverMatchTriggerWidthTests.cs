using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Popover;

/// <summary>
/// #518: Popover content following the trigger's rendered width. <c>MatchTriggerWidth</c>
/// reuses the positioning JS's existing <c>matchWidth</c> plumbing (already exercised by
/// Select/Combobox/TreeSelect) instead of adding a separate ResizeObserver — this asserts
/// the parameter wires through to that same <c>positionFixed</c> argument, and that the
/// default (<c>false</c>) leaves the fixed <c>w-72</c> width untouched.
/// </summary>
public class PopoverMatchTriggerWidthTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PopoverMatchTriggerWidthTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.Popover> RenderOpen(bool matchTriggerWidth) =>
        _ctx.Render<L.Popover>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.ChildContent, Content(matchTriggerWidth)));

    private static RenderFragment Content(bool matchTriggerWidth) => b =>
    {
        b.OpenComponent<L.PopoverContent>(0);
        b.AddAttribute(1, "MatchTriggerWidth", matchTriggerWidth);
        b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Popover body")));
        b.CloseComponent();
    };

    private bool AnyPositionFixedCallHadMatchWidth(bool expected) =>
        _ctx.JSInterop.Invocations.Any(i =>
            i.Identifier == "positionFixed" && Equals(i.Arguments[3], expected));

    [Fact]
    public void Default_Does_Not_Match_Trigger_Width()
    {
        var cut = RenderOpen(matchTriggerWidth: false);
        var content = cut.Find("[data-slot='popover-content']");
        Assert.Contains("w-72", content.ClassName);
        Assert.DoesNotContain("w-auto", content.ClassName);
        Assert.True(AnyPositionFixedCallHadMatchWidth(false));
    }

    [Fact]
    public void MatchTriggerWidth_Drops_The_Fixed_Width_Class()
    {
        var cut = RenderOpen(matchTriggerWidth: true);
        var content = cut.Find("[data-slot='popover-content']");
        Assert.DoesNotContain("w-72", content.ClassName);
        Assert.Contains("w-auto", content.ClassName);
    }

    [Fact]
    public void MatchTriggerWidth_Passes_MatchWidth_True_To_PositionFixed()
    {
        RenderOpen(matchTriggerWidth: true);
        Assert.True(AnyPositionFixedCallHadMatchWidth(true));
    }

    [Fact]
    public void MatchTriggerWidth_False_Still_Registers_And_Renders_Content()
    {
        var cut = RenderOpen(matchTriggerWidth: false);
        Assert.Contains("Popover body", cut.Markup);
    }
}
