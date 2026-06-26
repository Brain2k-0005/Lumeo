using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Menubar;

/// <summary>
/// ARIA menu roles (WAI-ARIA menubar pattern): the trigger is a menuitem that
/// owns a popup menu (aria-haspopup + aria-expanded reflecting open state); the
/// items inside the content are menuitems with roving tabindex.
/// </summary>
public class MenubarRolesTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public MenubarRolesTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderMenubar()
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
                    menu.AddAttribute(2, "ChildContent", (RenderFragment)(content =>
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
    public void Trigger_Is_A_Menuitem_That_Owns_A_Popup_Menu()
    {
        var cut = RenderMenubar();
        var trigger = cut.Find("button");

        Assert.Equal("menuitem", trigger.GetAttribute("role"));
        Assert.Equal("menu", trigger.GetAttribute("aria-haspopup"));
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Open_Trigger_Reflects_Expanded_And_Items_Are_Menuitems()
    {
        var cut = RenderMenubar();
        cut.Find("button").Click();

        var trigger = cut.Find("button");
        Assert.Equal("true", trigger.GetAttribute("aria-expanded"));

        var item = cut.Find("button[role='menuitem'][tabindex='-1']");
        Assert.Equal("New File", item.TextContent.Trim());
    }
}
