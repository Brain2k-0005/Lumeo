using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DropdownMenu;

/// <summary>
/// DocFlow O1: "Dropdown submenu inside a scrollable menu is clipped (content has
/// overflow, submenu not portaled)." Repro attempt against current master. Evidence this
/// does NOT reproduce here: DropdownMenuContent already renders <c>overflow-visible</c>
/// (field report 4.4.1, fixed before 5.10.0) and DropdownMenuSubContent is
/// <c>position: fixed</c> — a fixed-positioned element is laid out against the viewport
/// regardless of any ancestor's overflow, so nothing in Lumeo's own DOM chain can clip it.
/// A consumer that wraps DropdownMenuContent in their OWN overflow:auto/max-height element
/// (needed because DropdownMenuContent has no built-in scroll-when-long behavior — a real,
/// separate gap, tracked as a follow-up, not this clipping bug) could still clip the
/// submenu if that wrapper establishes a CSS containing block (transform/filter/etc.) — that
/// is the consumer's own CSS, outside what a fixed-position child can defend against, and
/// isn't reproducible by changing Lumeo's markup alone.
/// </summary>
public class DropdownMenuSubmenuScrollClipRegressionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DropdownMenuSubmenuScrollClipRegressionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private RenderFragment ChildWithManyItemsAndSubmenu => b =>
    {
        b.OpenComponent<L.DropdownMenuTrigger>(0);
        b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Menu")));
        b.CloseComponent();
        b.OpenComponent<L.DropdownMenuContent>(2);
        b.AddAttribute(3, "ChildContent", (RenderFragment)(content =>
        {
            var seq = 0;
            for (var i = 0; i < 40; i++)
            {
                content.OpenComponent<L.DropdownMenuItem>(seq++);
                var label = $"Item {i}";
                content.AddAttribute(seq++, "ChildContent", (RenderFragment)(ic => ic.AddContent(0, label)));
                content.CloseComponent();
            }
            content.OpenComponent<L.DropdownMenuSub>(seq++);
            content.AddAttribute(seq++, "ChildContent", (RenderFragment)(sub =>
            {
                sub.OpenComponent<L.DropdownMenuSubTrigger>(0);
                sub.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "More")));
                sub.CloseComponent();
                sub.OpenComponent<L.DropdownMenuSubContent>(2);
                sub.AddAttribute(3, "ChildContent", (RenderFragment)(sc =>
                {
                    sc.OpenComponent<L.DropdownMenuItem>(0);
                    sc.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Sub Item")));
                    sc.CloseComponent();
                }));
                sub.CloseComponent();
            }));
            content.CloseComponent();
        }));
        b.CloseComponent();
    };

    [Fact]
    public void Long_Menu_With_Submenu_Content_Is_Overflow_Visible_Not_Clipped()
    {
        var cut = _ctx.Render<L.DropdownMenu>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.ChildContent, ChildWithManyItemsAndSubmenu));

        var contentClass = cut.Find("[data-slot='dropdown-menu-content']").GetAttribute("class") ?? "";
        Assert.Contains("overflow-visible", contentClass);
        Assert.DoesNotContain("overflow-hidden", contentClass);
    }

    [Fact]
    public void SubContent_Is_Viewport_Positioned_Fixed_So_No_Ancestor_Overflow_Can_Clip_It()
    {
        var cut = _ctx.Render<L.DropdownMenu>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.ChildContent, ChildWithManyItemsAndSubmenu));

        // Open the submenu via its trigger.
        cut.Find("button[aria-haspopup='menu']").Click();

        var subClass = cut.Find("[data-slot='dropdown-menu-sub-content']").GetAttribute("class") ?? "";
        Assert.Contains("fixed", subClass);
        Assert.Contains("overflow-visible", subClass);
    }
}
