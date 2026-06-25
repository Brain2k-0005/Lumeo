using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.SplitButton;

/// <summary>
/// Regression for triage #121 (keyboard-a11y): the chevron half's expanded-state
/// ARIA must live on the FOCUSABLE chevron <c>&lt;button&gt;</c>, not on a separate
/// non-focusable <c>&lt;div role="button"&gt;</c> wrapper a screen reader never
/// lands on.
///
/// The fix switches the chevron's <c>DropdownMenuTrigger</c> to <b>AsChild</b>, which
/// folds the trigger's <c>aria-haspopup</c> / <c>aria-expanded</c> / <c>aria-controls</c>
/// / <c>data-state</c> + toggle onclick onto the single chevron button and removes the
/// wrapper entirely.
///
/// Without the fix (the old non-asChild path) these tests FAIL:
///   • there IS a <c>div[role='button']</c> wrapper, and
///   • the chevron <c>&lt;button&gt;</c> carries no <c>aria-expanded</c> /
///     <c>aria-haspopup</c> (those sit on the wrapper instead),
/// so the open/closed state is never exposed on the focused element.
/// </summary>
public class SplitButtonChevronAriaTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SplitButtonChevronAriaTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment MenuWithItem(string label) => builder =>
    {
        builder.OpenComponent<Lumeo.DropdownMenuItem>(0);
        builder.AddAttribute(1, "ChildContent",
            (RenderFragment)(b => b.AddContent(0, label)));
        builder.CloseComponent();
    };

    private IRenderedComponent<Lumeo.SplitButton> RenderSplit()
        => _ctx.Render<Lumeo.SplitButton>(p =>
        {
            p.Add(b => b.Text, "Save");
            p.Add(b => b.MenuContent, MenuWithItem("Save and exit"));
        });

    // The expanded-state ARIA must ride on a real <button> (the focusable element),
    // with no non-focusable role=button wrapper carrying it instead.
    [Fact]
    public void Chevron_Expanded_Aria_Lives_On_The_Focusable_Button_Not_A_Wrapper()
    {
        var cut = RenderSplit();

        // No separate non-focusable wrapper element owns the trigger semantics.
        Assert.Empty(cut.FindAll("div[role='button']"));

        // The chevron half is itself a <button> advertising the menu popup with a
        // collapsed expanded-state — i.e. the ARIA is on the focusable element.
        var chevron = cut.Find("button[aria-haspopup='menu']");
        Assert.Equal("false", chevron.GetAttribute("aria-expanded"));
    }

    // Toggling the menu must flip aria-expanded ON THE BUTTON the user focuses,
    // so assistive tech actually hears the open/closed transition.
    [Fact]
    public void Chevron_Button_AriaExpanded_Reflects_Open_State()
    {
        var cut = RenderSplit();

        var chevron = cut.Find("button[aria-haspopup='menu']");
        Assert.Equal("false", chevron.GetAttribute("aria-expanded"));

        // The toggle onclick is folded onto the chevron button itself (AsChild),
        // so clicking it opens the menu — no wrapper indirection needed.
        chevron.Click();

        // Re-find: the button is re-rendered on open.
        chevron = cut.Find("button[aria-haspopup='menu']");
        Assert.Equal("true", chevron.GetAttribute("aria-expanded"));

        // And aria-controls on that same focusable button points at the open menu.
        var menu = cut.Find("[role='menu']");
        Assert.Equal(menu.Id, chevron.GetAttribute("aria-controls"));
    }
}
