using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Popover;

/// <summary>
/// battle-wave2 keyboard-a11y regressions for Popover, asserting the JSInterop
/// MECHANISM (bUnit cannot move real DOM focus) via the recorded invocations on
/// the real <see cref="ComponentInteropService"/> over bUnit's loose JSInterop —
/// the same approach as <see cref="PopoverRepositionTests"/>.
///
/// #87 — Closing the popover always stole focus back to the trigger wrapper via an
///       unconditional <c>focusElementById(WrapperId)</c>, even on a programmatic /
///       external close when the user's focus was already elsewhere. The fix routes
///       focus restoration through the non-modal <c>saveFocus</c>/<c>restoreFocus</c>
///       pair (shared with Select/DropdownMenu/ContextMenu): save on open, restore
///       on close — and <c>restoreFocus</c> no-ops when the saved element is gone,
///       so it never force-focuses the wrapper.
///
/// #88 — The fallback (non-AsChild) <c>div[role=button]</c> trigger never suppressed
///       Space's default, so Space opened the popover AND scrolled the page. The fix
///       registers a JS-side key-selective preventDefault for <c>" "</c> on the
///       trigger (Collapsible/Accordion/Tabs idiom) — NOT <c>@onkeydown:preventDefault</c>,
///       which would also trap Tab.
/// </summary>
public class PopoverKeyboardA11yTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PopoverKeyboardA11yTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- #88: non-AsChild trigger + content ---
    private static RenderFragment FallbackTrigger => b =>
    {
        b.OpenComponent<L.PopoverTrigger>(0);
        b.AddAttribute(1, "AsChild", false);
        b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Toggle")));
        b.CloseComponent();

        b.OpenComponent<L.PopoverContent>(2);
        b.AddAttribute(3, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Popover content")));
        b.CloseComponent();
    };

    private IReadOnlyList<PreventDefaultKeyRule>? RuleSetFor(string idPrefix) =>
        _ctx.JSInterop.Invocations
            .Where(i => i.Identifier == "registerPreventDefaultKeys"
                        && i.Arguments[0] is string id && id.StartsWith(idPrefix))
            .Select(i => i.Arguments[1] as IReadOnlyList<PreventDefaultKeyRule>)
            .LastOrDefault();

    [Fact]
    public void Fallback_Trigger_Registers_Space_PreventDefault()
    {
        var cut = _ctx.Render<L.Popover>(p => p.Add(x => x.ChildContent, FallbackTrigger));

        // The role=button div has no native key synthesis, so Space scrolled the
        // page. Without the fix no registerPreventDefaultKeys is invoked for the
        // trigger; with it, a Space (" ") rule is registered on the trigger id.
        cut.WaitForAssertion(() =>
        {
            var rules = RuleSetFor("popover-trigger-");
            Assert.NotNull(rules);
            Assert.Contains(rules!, r => r.Key == " ");
        });
    }

    [Fact]
    public void AsChild_Trigger_Does_Not_Register_PreventDefault_Keys()
    {
        // AsChild renders NO role=button div — the cooperating child (e.g. a real
        // <button>) handles Space natively, so there is nothing to suppress. Guard
        // that the Space-suppression is scoped to the fallback branch only.
        var cut = _ctx.Render<L.Popover>(p => p.Add(x => x.ChildContent, b =>
        {
            b.OpenComponent<L.PopoverTrigger>(0);
            b.AddAttribute(1, "AsChild", true);
            b.AddAttribute(2, "ChildContent", (RenderFragment)(t =>
            {
                t.OpenComponent<L.Button>(0);
                t.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Open")));
                t.CloseComponent();
            }));
            b.CloseComponent();
        }));

        Assert.Null(RuleSetFor("popover-trigger-"));
    }

    // --- #87: focus restore on close ---
    private static RenderFragment FocusChildren => b =>
    {
        b.OpenComponent<L.PopoverTrigger>(0);
        b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Toggle")));
        b.CloseComponent();

        b.OpenComponent<L.PopoverContent>(2);
        b.AddAttribute(3, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Popover content")));
        b.CloseComponent();
    };

    private int CountInvocations(string identifier) =>
        _ctx.JSInterop.Invocations.Count(i => i.Identifier == identifier);

    private bool FocusElementCalledWithWrapperId() =>
        _ctx.JSInterop.Invocations.Any(i =>
            i.Identifier == "focusElementById"
            && i.Arguments[0] is string id
            && id.StartsWith("popover-")
            && !id.StartsWith("popover-content-")
            && !id.StartsWith("popover-trigger-"));

    [Fact]
    public void Open_Saves_Focus_And_Close_Restores_It_Via_RestoreFocus()
    {
        var cut = _ctx.Render<L.Popover>(p => p
            .Add(x => x.Open, true)
            .Add(x => x.ChildContent, FocusChildren));

        // On open the popover stashes the trigger's focus (saveFocus) before moving
        // focus into the content. Without the fix saveFocus is never called.
        cut.WaitForAssertion(() => Assert.True(CountInvocations("saveFocus") >= 1));

        Assert.Equal(0, CountInvocations("restoreFocus"));

        // Close the popover.
        cut.Render(p => p.Add(x => x.Open, false));

        // Close restores focus through restoreFocus (the no-op-when-gone WCAG 2.4.3
        // path) instead of an unconditional focusElementById(WrapperId).
        cut.WaitForAssertion(() => Assert.True(CountInvocations("restoreFocus") >= 1));
    }

    [Fact]
    public void Close_Does_Not_Force_Focus_The_Wrapper()
    {
        var cut = _ctx.Render<L.Popover>(p => p
            .Add(x => x.Open, true)
            .Add(x => x.ChildContent, FocusChildren));

        cut.WaitForAssertion(() => Assert.True(CountInvocations("saveFocus") >= 1));

        cut.Render(p => p.Add(x => x.Open, false));
        cut.WaitForState(() => !cut.Markup.Contains("Popover content"));

        // The wrapper id (a non-content, non-trigger popover-* id) must never be
        // force-focused on close. Before the fix Cleanup() called
        // focusElementById(WrapperId) unconditionally.
        Assert.False(FocusElementCalledWithWrapperId());
    }
}
