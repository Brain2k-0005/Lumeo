using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Menubar;

/// <summary>
/// Regression tests for stale menu ids leaking into Menubar._menuIds when a
/// MenubarMenu is conditionally removed. A leaked id corrupts the roving
/// tabindex (MenuIds[0] no longer maps to a mounted trigger) and breaks
/// arrow-key navigation across the menubar.
/// </summary>
public class MenubarStaleMenuIdTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MenubarStaleMenuIdTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    /// <summary>
    /// A single MenubarMenu whose trigger renders <paramref name="label"/>.
    /// </summary>
    private static RenderFragment Menu(string label)
    {
        return menuFrag =>
        {
            menuFrag.OpenComponent<L.MenubarMenu>(0);
            menuFrag.AddAttribute(1, "ChildContent", (RenderFragment)(menu =>
            {
                menu.OpenComponent<L.MenubarTrigger>(0);
                menu.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, label)));
                menu.CloseComponent();
            }));
            menuFrag.CloseComponent();
        };
    }

    /// <summary>
    /// Renders a Menubar whose first ("File") menu lives inside a
    /// <see cref="ConditionalRoot"/> bound to the outer root's Show parameter, so
    /// re-rendering the root with Show=false unmounts (and disposes) only the
    /// first menu. The second ("Edit") menu is always mounted.
    /// The outer ConditionalRoot is itself always shown (Show drives the nested one).
    /// </summary>
    private IRenderedComponent<ConditionalRoot> RenderTwoMenus()
    {
        return _ctx.Render<ConditionalRoot>(p => p
            .Add(x => x.Show, true)
            .AddChildContent(host =>
            {
                // The host renders a Menubar; the inner ConditionalRoot's Show is
                // bound to a CascadingValue<bool> we flip via the root's Show below.
                host.OpenComponent<L.Menubar>(0);
                host.AddAttribute(1, "ChildContent", (RenderFragment)(bar =>
                {
                    bar.OpenComponent<MenubarMenuVisibilityProbe>(0);
                    bar.AddAttribute(1, "FirstMenu", Menu("File"));
                    bar.AddAttribute(2, "SecondMenu", Menu("Edit"));
                    bar.CloseComponent();
                }));
                host.CloseComponent();
            }));
    }

    [Fact]
    public void Removing_First_Menu_Promotes_Survivor_To_Roving_Tabindex()
    {
        var cut = RenderTwoMenus();
        var probe = cut.FindComponent<MenubarMenuVisibilityProbe>();

        // Sanity: both triggers present, "File" (first registered) owns the tab stop.
        var fileBtn = cut.FindAll("button").Single(b => b.TextContent.Contains("File"));
        Assert.Equal("0", fileBtn.GetAttribute("tabindex"));
        var editBefore = cut.FindAll("button").Single(b => b.TextContent.Contains("Edit"));
        Assert.Equal("-1", editBefore.GetAttribute("tabindex"));

        // Remove the first menu. Its id must be unregistered on dispose so that
        // MenuIds[0] now points at the surviving "Edit" menu.
        probe.Render(p => p.Add(x => x.ShowFirst, false));

        var buttons = cut.FindAll("button");
        Assert.DoesNotContain(buttons, b => b.TextContent.Contains("File"));

        // The survivor must now be the roving tab stop (tabindex=0). Without the
        // dispose/unregister fix the stale "File" id stays at MenuIds[0], so the
        // surviving "Edit" trigger keeps tabindex=-1 and arrow nav is broken.
        var editAfter = buttons.Single(b => b.TextContent.Contains("Edit"));
        Assert.Equal("0", editAfter.GetAttribute("tabindex"));
    }

    [Fact]
    public void Removing_Open_Menu_Closes_It()
    {
        var cut = RenderTwoMenus();
        var probe = cut.FindComponent<MenubarMenuVisibilityProbe>();

        // Open the first ("File") menu.
        var fileBtn = cut.FindAll("button").Single(b => b.TextContent.Contains("File"));
        fileBtn.Click();
        Assert.Equal("true", cut.FindAll("button")
            .Single(b => b.TextContent.Contains("File"))
            .GetAttribute("aria-expanded"));

        // Removing the currently-open menu must clear _openMenuId so no phantom
        // menu stays "open" and the survivor reclaims the roving tab stop.
        probe.Render(p => p.Add(x => x.ShowFirst, false));

        var editAfter = cut.FindAll("button").Single(b => b.TextContent.Contains("Edit"));
        Assert.Equal("false", editAfter.GetAttribute("aria-expanded"));
        Assert.Equal("0", editAfter.GetAttribute("tabindex"));
    }

    /// <summary>
    /// Test host that renders a removable FirstMenu (gated by <see cref="ShowFirst"/>)
    /// and an always-present SecondMenu, both as direct children of the surrounding
    /// Menubar so they register/unregister against the same MenubarContext.
    /// </summary>
    private sealed class MenubarMenuVisibilityProbe : ComponentBase
    {
        [Parameter] public bool ShowFirst { get; set; } = true;
        [Parameter] public RenderFragment FirstMenu { get; set; } = default!;
        [Parameter] public RenderFragment SecondMenu { get; set; } = default!;

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            if (ShowFirst)
            {
                builder.AddContent(0, FirstMenu);
            }
            builder.AddContent(1, SecondMenu);
        }
    }
}
