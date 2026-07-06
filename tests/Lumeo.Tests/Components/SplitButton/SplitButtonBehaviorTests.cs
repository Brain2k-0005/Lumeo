using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Lumeo.Tests.Components.SplitButton;

/// <summary>
/// Behaviour/a11y tests for the two-half <see cref="Lumeo.SplitButton"/>: a primary
/// action button plus a chevron half that opens a <c>DropdownMenu</c>.
///
/// The chevron half wraps a Lumeo <c>Button</c> inside an <b>AsChild</b>
/// <c>DropdownMenuTrigger</c> (triage #121). AsChild folds the menu ARIA
/// (<c>aria-haspopup</c>, <c>aria-expanded</c>, <c>aria-controls</c>,
/// <c>data-state</c>) plus the live toggle <c>@onclick</c> onto the SINGLE
/// focusable chevron <c>&lt;button&gt;</c> — there is no separate
/// <c>&lt;div role="button"&gt;</c> wrapper, so a screen reader landing on the
/// focused button now hears the open/closed state. These tests therefore click /
/// assert against the chevron button itself (the one carrying
/// <c>aria-haspopup='menu'</c>). The menu content (<c>role="menu"</c>) is
/// conditionally rendered only while open.
///
/// JSInterop runs LOOSE (the positioning / click-outside / focus calls fired in
/// DropdownMenuContent.OnAfterRenderAsync are recorded, not asserted here), so the
/// open state is observed purely through rendered ARIA + menu items.
/// </summary>
public class SplitButtonBehaviorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SplitButtonBehaviorTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // A menu body with a single item, so opening the menu surfaces a role="menuitem".
    private static RenderFragment MenuWithItem(string label) => builder =>
    {
        builder.OpenComponent<Lumeo.DropdownMenuItem>(0);
        builder.AddAttribute(1, "ChildContent",
            (RenderFragment)(b => b.AddContent(0, label)));
        builder.CloseComponent();
    };

    private IRenderedComponent<Lumeo.SplitButton> RenderSplit(
        Action<MouseEventArgs>? onClick = null,
        string menuItemLabel = "Save and exit")
        => _ctx.Render<Lumeo.SplitButton>(p =>
        {
            p.Add(b => b.Text, "Save");
            if (onClick is not null)
                p.Add(b => b.OnClick, onClick);
            p.Add(b => b.MenuContent, MenuWithItem(menuItemLabel));
        });

    // The chevron half's clickable trigger: with AsChild (triage #121) the menu's
    // toggle @onclick and aria-expanded/aria-haspopup/aria-controls fold onto the
    // single chevron <button> itself — there is no role="button" wrapper. The
    // primary action button has no aria-haspopup, so this selector unambiguously
    // picks the chevron half.
    private static AngleSharp.Dom.IElement Trigger(IRenderedComponent<Lumeo.SplitButton> cut)
        => cut.Find("button[aria-haspopup='menu']");

    [Fact]
    public void Menu_Is_Closed_Initially_With_Collapsed_Aria()
    {
        var cut = RenderSplit();

        // No menu rendered, and the trigger advertises a collapsed popup.
        Assert.Empty(cut.FindAll("[role='menu']"));
        Assert.Empty(cut.FindAll("[role='menuitem']"));

        var trigger = Trigger(cut);
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        Assert.Equal("menu", trigger.GetAttribute("aria-haspopup"));
    }

    [Fact]
    public void Chevron_Click_Opens_Menu_And_Sets_Expanded()
    {
        var cut = RenderSplit(menuItemLabel: "Duplicate");

        Trigger(cut).Click();

        // Menu now exists with the WAI-ARIA menu contract, and the item is visible.
        var menu = cut.Find("[role='menu']");
        Assert.Equal("vertical", menu.GetAttribute("aria-orientation"));
        Assert.Contains("Duplicate", cut.Markup);
        Assert.Single(cut.FindAll("[role='menuitem']"));

        // aria-expanded flips and aria-controls points at the rendered menu.
        var trigger = Trigger(cut);
        Assert.Equal("true", trigger.GetAttribute("aria-expanded"));
        Assert.Equal(menu.Id, trigger.GetAttribute("aria-controls"));
    }

    [Fact]
    public void Chevron_Click_Toggles_Menu_Closed_Again()
    {
        var cut = RenderSplit();

        Trigger(cut).Click();
        Assert.NotEmpty(cut.FindAll("[role='menu']"));

        // Re-find: the trigger element is re-rendered on open.
        Trigger(cut).Click();

        // Menu plays its zoom-out exit before unmounting (B11 parity) — poll for removal.
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='menu']")), timeout: TimeSpan.FromSeconds(5));
        Assert.Equal("false", Trigger(cut).GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Primary_Click_Fires_OnClick_Without_Opening_Menu()
    {
        var clicks = 0;
        var cut = RenderSplit(onClick: _ => clicks++);

        // Primary action half is the first <button> (chevron lives in the 2nd half).
        cut.FindAll("button")[0].Click();

        Assert.Equal(1, clicks);
        // The two halves are independent: a primary click must NOT open the menu.
        Assert.Empty(cut.FindAll("[role='menu']"));
        Assert.Equal("false", Trigger(cut).GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Chevron_Click_Does_Not_Fire_Primary_OnClick()
    {
        var clicks = 0;
        var cut = RenderSplit(onClick: _ => clicks++);

        Trigger(cut).Click();

        // Opening the menu is independent of the primary action.
        Assert.Equal(0, clicks);
        Assert.NotEmpty(cut.FindAll("[role='menu']"));
    }

    [Fact]
    public void Selecting_A_Menu_Item_Closes_The_Menu()
    {
        var cut = RenderSplit(menuItemLabel: "Save as draft");

        Trigger(cut).Click();
        var item = cut.Find("[role='menuitem']");
        Assert.Equal("Save as draft", item.TextContent.Trim());

        // DropdownMenuItem closes the menu after activation.
        item.Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[role='menu']"));
            Assert.Equal("false", Trigger(cut).GetAttribute("aria-expanded"));
        });
    }
}
