using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Menubar;

/// <summary>
/// #520 follow-up: MenubarContent shares DropdownMenuContent's overflow-visible /
/// fixed-submenu pattern, so it gets the same built-in scrollable inner viewport
/// (see <see cref="Lumeo.Tests.Components.DropdownMenu.DropdownMenuScrollViewportTests"/>
/// for the full rationale).
/// </summary>
public class MenubarScrollViewportTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MenubarScrollViewportTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderOpenMenu(string? maxHeight = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Menubar>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.MenubarMenu>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(menu =>
                {
                    menu.OpenComponent<L.MenubarTrigger>(0);
                    menu.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "File")));
                    menu.CloseComponent();

                    menu.OpenComponent<L.MenubarContent>(1);
                    if (maxHeight is not null) menu.AddAttribute(2, "MaxHeight", maxHeight);
                    menu.AddAttribute(3, "ChildContent", (RenderFragment)(content =>
                    {
                        content.OpenComponent<L.MenubarItem>(0);
                        content.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "New File")));
                        content.CloseComponent();
                    }));
                    menu.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Inner_Viewport_Has_Overflow_Y_Auto()
    {
        var cut = RenderOpenMenu();
        cut.Find("button").Click();

        var viewport = cut.Find("[data-slot='menubar-viewport']");
        Assert.Contains("overflow-y-auto", viewport.ClassName);
    }

    [Fact]
    public void Default_Viewport_Max_Height_Uses_The_Live_Available_Height_Var_With_24rem_Fallback()
    {
        var cut = RenderOpenMenu();
        cut.Find("button").Click();

        var style = cut.Find("[data-slot='menubar-viewport']").GetAttribute("style") ?? "";
        Assert.Contains("max-height: var(--lumeo-dropdown-available-height, 24rem)", style);
    }

    [Fact]
    public void MaxHeight_Parameter_Overrides_The_Viewport_Cap()
    {
        var cut = RenderOpenMenu(maxHeight: "18rem");
        cut.Find("button").Click();

        var style = cut.Find("[data-slot='menubar-viewport']").GetAttribute("style") ?? "";
        Assert.Contains("max-height: 18rem", style);
    }

    [Fact]
    public void Menu_Item_Still_Renders_Inside_The_New_Viewport()
    {
        var cut = RenderOpenMenu();
        cut.Find("button").Click();
        Assert.Contains("New File", cut.Find("[data-slot='menubar-viewport']").InnerHtml);
    }
}
