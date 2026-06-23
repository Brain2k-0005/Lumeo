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
/// The chevron half wraps a Lumeo <c>Button</c> inside a NON-asChild
/// <c>DropdownMenuTrigger</c>, which renders a <c>&lt;div role="button"&gt;</c>
/// carrying the live <c>@onclick</c> toggle plus the menu ARIA (aria-haspopup,
/// aria-expanded, aria-controls). The inner chevron <c>&lt;button&gt;</c> has no
/// toggle handler of its own — in a real browser the click bubbles to the trigger
/// div; bUnit does NOT bubble, so these tests click the <c>role="button"</c>
/// trigger element (the one that owns the handler + aria-expanded). The menu
/// content (<c>role="menu"</c>) is conditionally rendered only while open.
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

    // The chevron half's clickable trigger: the role="button" wrapper that owns
    // the @onclick toggle AND aria-expanded (distinct from the inner chevron
    // <button>, which has no toggle handler of its own and does not bubble in bUnit).
    private static AngleSharp.Dom.IElement Trigger(IRenderedComponent<Lumeo.SplitButton> cut)
        => cut.Find("[role='button']");

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

        Assert.Empty(cut.FindAll("[role='menu']"));
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
