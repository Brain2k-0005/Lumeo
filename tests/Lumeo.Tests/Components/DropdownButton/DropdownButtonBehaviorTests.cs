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
/// Behaviour / a11y coverage for <see cref="L.DropdownButton"/> — the split-style
/// trigger that wraps a <see cref="L.DropdownMenu"/> over a <c>role="menu"</c> popup.
///
/// DropdownButton owns no interaction logic of its own; it composes
/// DropdownMenuTrigger (the live <c>role="button"</c> with <c>aria-expanded</c>) and
/// DropdownMenuContent (the <c>role="menu"</c> that handles Escape / ArrowDown). These
/// tests therefore drive the public surface — the trigger click, the menu's keydown,
/// and the registered click-outside handler — and assert the ARIA contract plus the
/// open/closed state, rather than internal markup.
///
/// The default JSInterop is loose (calls recorded, not failed). For the keyboard-nav
/// assertions we swap in <see cref="TrackingInteropService"/> (last registration wins)
/// so ArrowDown can be driven via a configurable item count and the resulting
/// focus-by-index call can be observed, and so the click-outside handler can be
/// captured and invoked directly (the real one is wired by JS that does not run here).
/// </summary>
public class DropdownButtonBehaviorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DropdownButtonBehaviorTests()
    {
        _ctx.AddLumeoServices();
        // Route component interop through the tracker so menu keyboard-nav and
        // click-outside wiring can be observed without a real DOM.
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    /// <summary>
    /// Renders a DropdownButton with two real <see cref="L.DropdownMenuItem"/>s, so the
    /// open menu exposes <c>role="menu"</c> / <c>role="menuitem"</c> to assert against.
    /// </summary>
    private IRenderedComponent<L.DropdownButton> RenderButton()
        => _ctx.Render<L.DropdownButton>(p => p
            .Add(b => b.Text, "Actions")
            .Add(b => b.MenuContent, (RenderFragment)(menu =>
            {
                menu.OpenComponent<L.DropdownMenuItem>(0);
                menu.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Edit")));
                menu.CloseComponent();

                menu.OpenComponent<L.DropdownMenuItem>(2);
                menu.AddAttribute(3, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Delete")));
                menu.CloseComponent();
            })));

    [Fact]
    public void Closed_By_Default_Trigger_Reports_AriaExpanded_False_And_No_Menu()
    {
        var cut = RenderButton();

        var trigger = cut.Find("[role='button']");
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        // aria-haspopup advertises that activating the trigger opens a menu.
        Assert.Equal("menu", trigger.GetAttribute("aria-haspopup"));
        // The menu popup is not rendered while closed.
        Assert.Empty(cut.FindAll("[role='menu']"));
    }

    [Fact]
    public void Click_Opens_Menu_Sets_AriaExpanded_True_And_Exposes_Menu_Role()
    {
        var cut = RenderButton();

        cut.Find("[role='button']").Click();

        // Trigger now advertises the expanded popup it controls...
        var trigger = cut.Find("[role='button']");
        Assert.Equal("true", trigger.GetAttribute("aria-expanded"));

        // ...and that popup is the WAI-ARIA vertical menu with our two items.
        var menu = cut.Find("[role='menu']");
        Assert.Equal("vertical", menu.GetAttribute("aria-orientation"));
        Assert.Equal(trigger.GetAttribute("aria-controls"), menu.Id);
        Assert.Equal(2, cut.FindAll("[role='menuitem']").Count);
    }

    [Fact]
    public void Escape_On_Open_Menu_Closes_It_And_Resets_AriaExpanded()
    {
        var cut = RenderButton();
        cut.Find("[role='button']").Click();
        Assert.NotEmpty(cut.FindAll("[role='menu']"));

        // Escape is handled by the menu container's @onkeydown.
        cut.Find("[role='menu']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        // The menu now plays a zoom-out exit before unmounting (B11 parity), so poll
        // for its removal; aria-expanded flips synchronously on the trigger.
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='menu']")), timeout: TimeSpan.FromSeconds(5));
        Assert.Equal("false", cut.Find("[role='button']").GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task Outside_Click_Closes_The_Open_Menu()
    {
        var cut = RenderButton();
        cut.Find("[role='button']").Click();

        // The content registers a click-outside handler on open (excluding the
        // wrapper that holds the trigger). Invoke the captured handler to emulate
        // a click elsewhere on the page.
        cut.WaitForAssertion(() => Assert.Single(_interop.ClickOutsideRegistrations));
        await cut.InvokeAsync(() => _interop.ClickOutsideRegistrations[0].Handler());

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[role='menu']"));
            Assert.Equal("false", cut.Find("[role='button']").GetAttribute("aria-expanded"));
        });
    }

    [Fact]
    public void ArrowDown_On_Open_Menu_Moves_Roving_Focus_To_First_Item()
    {
        // Drive the keyboard-nav path: the menu queries the live item count via
        // interop, so stage two items, then assert ArrowDown focuses index 0.
        _interop.MenuItemCount = 2;
        var cut = RenderButton();
        cut.Find("[role='button']").Click();

        var menu = cut.Find("[role='menu']");
        menu.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        cut.WaitForAssertion(() =>
            Assert.Contains(_interop.FocusMenuItemCalls,
                c => c.Index == 0 && c.ContainerId == menu.Id));
        // Menu stays open during navigation.
        Assert.Equal("true", cut.Find("[role='button']").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Disabled_Trigger_Click_Keeps_Menu_Closed_And_AriaExpanded_False()
    {
        var cut = _ctx.Render<L.DropdownButton>(p => p
            .Add(b => b.Text, "Actions")
            .Add(b => b.Disabled, true)
            .Add(b => b.MenuContent, (RenderFragment)(menu =>
            {
                menu.OpenComponent<L.DropdownMenuItem>(0);
                menu.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Edit")));
                menu.CloseComponent();
            })));

        cut.Find("[role='button']").Click();

        Assert.Empty(cut.FindAll("[role='menu']"));
        Assert.Equal("false", cut.Find("[role='button']").GetAttribute("aria-expanded"));
    }
}
