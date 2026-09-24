using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DropdownMenu;

/// <summary>
/// #520: DropdownMenuContent gets a built-in max-height + scroll on an INNER viewport
/// element (Radix's <c>--radix-dropdown-menu-content-available-height</c> parity), so a
/// long menu no longer needs a consumer-supplied wrapper — which was the exact thing that
/// could re-introduce clipping of the position:fixed submenu (see
/// <see cref="DropdownMenuSubmenuScrollClipRegressionTests"/>). The outer content element
/// stays <c>overflow-visible</c> so the submenu keeps escaping it; only the new inner
/// viewport div scrolls.
/// </summary>
public class DropdownMenuScrollViewportTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DropdownMenuScrollViewportTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private RenderFragment ChildWithManyItemsAndSubmenu(string? maxHeight = null) => b =>
    {
        b.OpenComponent<L.DropdownMenuTrigger>(0);
        b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Menu")));
        b.CloseComponent();
        b.OpenComponent<L.DropdownMenuContent>(2);
        if (maxHeight is not null) b.AddAttribute(3, "MaxHeight", maxHeight);
        b.AddAttribute(4, "ChildContent", (RenderFragment)(content =>
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
                sub.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "More (Item 35 submenu)")));
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
    public void Inner_Viewport_Has_Overflow_Y_Auto()
    {
        var cut = _ctx.Render<L.DropdownMenu>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.ChildContent, ChildWithManyItemsAndSubmenu()));

        var viewport = cut.Find("[data-slot='dropdown-menu-viewport']");
        Assert.Contains("overflow-y-auto", viewport.ClassName);
    }

    [Fact]
    public void Outer_Content_Stays_Overflow_Visible_With_The_New_Inner_Viewport()
    {
        var cut = _ctx.Render<L.DropdownMenu>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.ChildContent, ChildWithManyItemsAndSubmenu()));

        var contentClass = cut.Find("[data-slot='dropdown-menu-content']").GetAttribute("class") ?? "";
        Assert.Contains("overflow-visible", contentClass);
        Assert.DoesNotContain("overflow-hidden", contentClass);
    }

    [Fact]
    public void Default_Viewport_Max_Height_Uses_The_Live_Available_Height_Var_With_24rem_Fallback()
    {
        var cut = _ctx.Render<L.DropdownMenu>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.ChildContent, ChildWithManyItemsAndSubmenu()));

        var style = cut.Find("[data-slot='dropdown-menu-viewport']").GetAttribute("style") ?? "";
        Assert.Contains("max-height: var(--lumeo-dropdown-available-height, 24rem)", style);
    }

    [Fact]
    public void MaxHeight_Parameter_Overrides_The_Viewport_Cap()
    {
        var cut = _ctx.Render<L.DropdownMenu>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.ChildContent, ChildWithManyItemsAndSubmenu("20rem")));

        var style = cut.Find("[data-slot='dropdown-menu-viewport']").GetAttribute("style") ?? "";
        Assert.Contains("max-height: 20rem", style);
        Assert.DoesNotContain("--lumeo-dropdown-available-height", style);
    }

    [Fact]
    public void All_40_Items_Render_Inside_The_Scrollable_Viewport()
    {
        var cut = _ctx.Render<L.DropdownMenu>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.ChildContent, ChildWithManyItemsAndSubmenu()));

        var viewport = cut.Find("[data-slot='dropdown-menu-viewport']");
        for (var i = 0; i < 40; i++)
            Assert.Contains($"Item {i}", viewport.InnerHtml);
    }

    [Fact]
    public void Submenu_Opened_From_An_Item_Deep_In_The_Scrollable_List_Stays_Fixed_And_Unclipped()
    {
        var cut = _ctx.Render<L.DropdownMenu>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.ChildContent, ChildWithManyItemsAndSubmenu()));

        cut.Find("button[aria-haspopup='menu']").Click();

        var subContent = cut.Find("[data-slot='dropdown-menu-sub-content']");
        var subClass = subContent.GetAttribute("class") ?? "";
        Assert.Contains("fixed", subClass);
        Assert.Contains("overflow-visible", subClass);
    }
}
