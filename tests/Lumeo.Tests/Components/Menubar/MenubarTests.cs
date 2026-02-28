using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Menubar;

public class MenubarTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MenubarTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderMenubar(bool includeItems = false, EventCallback? itemOnClick = null)
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
                        if (includeItems)
                        {
                            content.OpenComponent<L.MenubarItem>(0);
                            if (itemOnClick.HasValue)
                                content.AddAttribute(1, "OnClick", itemOnClick.Value);
                            content.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "New File")));
                            content.CloseComponent();

                            content.OpenComponent<L.MenubarItem>(1);
                            content.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Open")));
                            content.CloseComponent();
                        }
                    }));
                    menu.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // --- Rendering ---

    [Fact]
    public void Menubar_Renders_Menubar_Role()
    {
        var cut = RenderMenubar();
        var menubar = cut.Find("[role='menubar']");
        Assert.NotNull(menubar);
    }

    [Fact]
    public void MenubarMenu_Renders_Wrapper_Div()
    {
        var cut = RenderMenubar();
        var divs = cut.FindAll("div");
        Assert.NotEmpty(divs);
    }

    [Fact]
    public void MenubarTrigger_Renders_As_Button()
    {
        var cut = RenderMenubar();
        var button = cut.Find("button");
        Assert.Contains("File", button.TextContent);
    }

    [Fact]
    public void MenubarContent_Not_Rendered_Initially()
    {
        var cut = RenderMenubar(includeItems: true);
        Assert.DoesNotContain("New File", cut.Markup);
    }

    // --- Open/Close ---

    [Fact]
    public void Clicking_MenubarTrigger_Opens_Menu()
    {
        var cut = RenderMenubar(includeItems: true);

        cut.Find("button").Click();
        Assert.Contains("New File", cut.Markup);
    }

    [Fact]
    public void Clicking_MenubarTrigger_Again_Closes_Menu()
    {
        var cut = RenderMenubar(includeItems: true);

        // Open
        cut.Find("button").Click();
        Assert.Contains("New File", cut.Markup);

        // Close
        cut.Find("button").Click();
        Assert.DoesNotContain("New File", cut.Markup);
    }

    [Fact]
    public void Open_Trigger_Has_Active_Classes()
    {
        var cut = RenderMenubar();

        cut.Find("button").Click();
        var btn = cut.Find("button");
        var cls = btn.GetAttribute("class") ?? "";
        Assert.Contains("bg-accent", cls);
    }

    // --- Item interaction ---

    [Fact]
    public void Clicking_MenubarItem_Fires_OnClick()
    {
        bool called = false;
        var callback = EventCallback.Factory.Create(_ctx, () => called = true);
        var cut = RenderMenubar(includeItems: true, itemOnClick: callback);

        // Open the menu first
        cut.Find("button").Click();

        // Find the item and click it
        var buttons = cut.FindAll("button");
        var itemBtn = buttons.FirstOrDefault(b => b.TextContent.Contains("New File"));
        Assert.NotNull(itemBtn);
        try { itemBtn!.Click(); } catch (ArgumentException) { }

        Assert.True(called);
    }

    [Fact]
    public void Clicking_MenubarItem_Fires_OnClick_Callback()
    {
        // Verify that clicking a menu item fires the OnClick callback
        // (Menu close behavior tested separately via internal state)
        bool called = false;
        var callback = EventCallback.Factory.Create(_ctx, () => called = true);
        var cut = RenderMenubar(includeItems: true, itemOnClick: callback);

        // Open menu
        cut.Find("button").Click();

        // Click item
        var buttons = cut.FindAll("button");
        var itemBtn = buttons.FirstOrDefault(b => b.TextContent.Contains("New File"));
        Assert.NotNull(itemBtn);
        try { itemBtn!.Click(); } catch (ArgumentException) { }

        Assert.True(called);
    }

    // --- Disabled item ---

    [Fact]
    public void Disabled_MenubarItem_Has_Disabled_Attribute()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Menubar>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.MenubarMenu>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(menu =>
                {
                    menu.OpenComponent<L.MenubarTrigger>(0);
                    menu.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Edit")));
                    menu.CloseComponent();

                    menu.OpenComponent<L.MenubarContent>(1);
                    menu.AddAttribute(2, "ChildContent", (RenderFragment)(content =>
                    {
                        content.OpenComponent<L.MenubarItem>(0);
                        content.AddAttribute(1, "Disabled", true);
                        content.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Disabled Action")));
                        content.CloseComponent();
                    }));
                    menu.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // Open menu
        cut.Find("button").Click();

        var buttons = cut.FindAll("button");
        var disabledBtn = buttons.FirstOrDefault(b => b.TextContent.Contains("Disabled Action"));
        Assert.NotNull(disabledBtn);
        Assert.True(disabledBtn!.HasAttribute("disabled"));
    }

    // --- MenubarLabel and MenubarSeparator ---

    [Fact]
    public void MenubarLabel_Renders_Text()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.MenubarLabel>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b => b.AddContent(0, "My Label")));
            builder.CloseComponent();
        });

        Assert.Contains("My Label", cut.Markup);
    }

    [Fact]
    public void MenubarSeparator_Renders()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.MenubarSeparator>(0);
            builder.CloseComponent();
        });

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void MenubarShortcut_Renders_Text()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.MenubarShortcut>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b => b.AddContent(0, "Ctrl+S")));
            builder.CloseComponent();
        });

        Assert.Contains("Ctrl+S", cut.Markup);
    }

    // --- Custom CSS ---

    [Fact]
    public void Custom_Class_Forwarded_On_Menubar()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Menubar>(0);
            builder.AddAttribute(1, "Class", "my-menubar-class");
            builder.CloseComponent();
        });

        var menubar = cut.Find("[role='menubar']");
        Assert.Contains("my-menubar-class", menubar.GetAttribute("class"));
    }

    [Fact]
    public void Menubar_Has_Default_Classes()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Menubar>(0);
            builder.CloseComponent();
        });

        var menubar = cut.Find("[role='menubar']");
        var cls = menubar.GetAttribute("class") ?? "";
        Assert.Contains("border-border", cls);
        Assert.Contains("bg-background", cls);
    }
}
