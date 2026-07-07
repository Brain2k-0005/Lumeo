using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.DropdownButton;

/// <summary>
/// Keyboard / a11y regression coverage for the battle-test findings #113 and #114
/// against <see cref="L.DropdownButton"/>.
///
/// #113 — the menu ARIA (role=button + aria-haspopup / aria-expanded / aria-controls)
/// rides on the DropdownMenuTrigger wrapper, but the focusable element used to be the
/// inner native &lt;button&gt; which carried NONE of it, so a screen reader announced a
/// bare "button". DropdownButton now makes the ARIA-bearing wrapper the single tab stop
/// (<c>tabindex=0</c>) and drops the inner button out of the tab order
/// (<c>tabindex=-1</c>).
///
/// #114 — the trigger had no keydown handler, so the menu-button pattern's keyboard
/// entry point (Enter/Space to toggle, ArrowDown/ArrowUp to open) was missing; only the
/// already-open menu handled arrows. DropdownButton now wires an <c>onkeydown</c> on the
/// trigger that opens the menu.
///
/// These mirror <see cref="DropdownButtonBehaviorTests"/>: same loose-but-tracked
/// interop, the same two-item MenuContent, and role/attribute assertions against the
/// public surface (per the keyboard-a11y test rules, bUnit cannot move real DOM focus,
/// so these assert the OBSERVABLE markup — tabindex + aria-expanded + the role=menu
/// popup — never document.activeElement).
/// </summary>
public class DropdownButtonKeyboardAriaTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DropdownButtonKeyboardAriaTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.DropdownButton> RenderButton(bool disabled = false)
        => _ctx.Render<L.DropdownButton>(p => p
            .Add(b => b.Text, "Actions")
            .Add(b => b.Disabled, disabled)
            .Add(b => b.MenuContent, (RenderFragment)(menu =>
            {
                menu.OpenComponent<L.DropdownMenuItem>(0);
                menu.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Edit")));
                menu.CloseComponent();

                menu.OpenComponent<L.DropdownMenuItem>(2);
                menu.AddAttribute(3, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Delete")));
                menu.CloseComponent();
            })));

    // ---- #113: the ARIA-bearing element is the single tab stop ----

    [Fact]
    public void Trigger_Wrapper_Carries_The_Menu_Aria_And_Is_Tabbable()
    {
        var cut = RenderButton();

        // The wrapper holds the menu semantics...
        var trigger = cut.Find("[role='button']");
        Assert.Equal("menu", trigger.GetAttribute("aria-haspopup"));
        Assert.False(string.IsNullOrEmpty(trigger.GetAttribute("aria-controls")));

        // ...and is the focusable tab stop (regression: was null before the fix).
        Assert.Equal("0", trigger.GetAttribute("tabindex"));
    }

    [Fact]
    public void Inner_Button_Is_Removed_From_The_Tab_Order()
    {
        var cut = RenderButton();

        // The inner native <button> must NOT be a second tab stop — the focusable
        // host is the ARIA-bearing wrapper, so the bare button is tabindex=-1.
        var button = cut.Find("button");
        Assert.Equal("-1", button.GetAttribute("tabindex"));
    }

    [Fact]
    public void Disabled_Trigger_Wrapper_Leaves_The_Tab_Order()
    {
        var cut = RenderButton(disabled: true);

        Assert.Equal("-1", cut.Find("[role='button']").GetAttribute("tabindex"));
    }

    // ---- #114: keyboard entry point opens the menu ----

    [Fact]
    public void ArrowDown_On_Closed_Trigger_Opens_The_Menu()
    {
        var cut = RenderButton();

        // Closed to start: no menu, aria-expanded false.
        Assert.Empty(cut.FindAll("[role='menu']"));
        Assert.Equal("false", cut.Find("[role='button']").GetAttribute("aria-expanded"));

        // ArrowDown on the trigger must open the menu (menu-button pattern).
        cut.Find("[role='button']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.NotEmpty(cut.FindAll("[role='menu']"));
        Assert.Equal("true", cut.Find("[role='button']").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void ArrowUp_On_Closed_Trigger_Opens_The_Menu()
    {
        var cut = RenderButton();

        cut.Find("[role='button']").KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });

        Assert.NotEmpty(cut.FindAll("[role='menu']"));
        Assert.Equal("true", cut.Find("[role='button']").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Enter_On_Closed_Trigger_Opens_The_Menu()
    {
        var cut = RenderButton();

        cut.Find("[role='button']").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.NotEmpty(cut.FindAll("[role='menu']"));
        Assert.Equal("true", cut.Find("[role='button']").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Space_On_Open_Trigger_Closes_The_Menu()
    {
        var cut = RenderButton();
        cut.Find("[role='button']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.NotEmpty(cut.FindAll("[role='menu']"));

        // Space toggles, so it closes an already-open menu.
        cut.Find("[role='button']").KeyDown(new KeyboardEventArgs { Key = " " });

        // aria-expanded flips SYNCHRONOUSLY on the trigger the moment the menu closes —
        // the exit animation only delays the menu's DOM removal, not the trigger state.
        // Assert it before polling for the (animation-delayed) unmount.
        Assert.Equal("false", cut.Find("[role='button']").GetAttribute("aria-expanded"));

        // Menu then plays its zoom-out exit before unmounting (B11 parity) — poll for removal.
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='menu']")), timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ArrowDown_On_Disabled_Trigger_Stays_Closed()
    {
        var cut = RenderButton(disabled: true);

        cut.Find("[role='button']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.Empty(cut.FindAll("[role='menu']"));
        Assert.Equal("false", cut.Find("[role='button']").GetAttribute("aria-expanded"));
    }
}
