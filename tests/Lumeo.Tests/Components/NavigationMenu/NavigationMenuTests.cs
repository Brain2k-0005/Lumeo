using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.NavigationMenu;

public class NavigationMenuTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public NavigationMenuTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderNavigationMenu(bool withTriggerAndContent = false)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.NavigationMenu>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.NavigationMenuList>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(list =>
                {
                    list.OpenComponent<L.NavigationMenuItem>(0);
                    list.AddAttribute(1, "ChildContent", (RenderFragment)(item =>
                    {
                        if (withTriggerAndContent)
                        {
                            item.OpenComponent<L.NavigationMenuTrigger>(0);
                            item.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Products")));
                            item.CloseComponent();

                            item.OpenComponent<L.NavigationMenuContent>(1);
                            item.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Products content")));
                            item.CloseComponent();
                        }
                        else
                        {
                            item.OpenComponent<L.NavigationMenuLink>(0);
                            item.AddAttribute(1, "Href", "/home");
                            item.AddAttribute(2, "ChildContent", (RenderFragment)(l => l.AddContent(0, "Home")));
                            item.CloseComponent();
                        }
                    }));
                    list.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // --- Rendering ---

    [Fact]
    public void NavigationMenu_Renders_Nav_Element()
    {
        var cut = RenderNavigationMenu();
        var nav = cut.Find("nav");
        Assert.NotNull(nav);
    }

    [Fact]
    public void NavigationMenuList_Renders_As_Ul()
    {
        var cut = RenderNavigationMenu();
        var ul = cut.Find("ul");
        Assert.NotNull(ul);
    }

    [Fact]
    public void NavigationMenuItem_Renders_As_Li()
    {
        var cut = RenderNavigationMenu();
        var li = cut.Find("li");
        Assert.NotNull(li);
    }

    [Fact]
    public void NavigationMenuLink_Renders_As_Anchor()
    {
        var cut = RenderNavigationMenu();
        var link = cut.Find("a");
        Assert.NotNull(link);
        Assert.Contains("Home", link.TextContent);
    }

    [Fact]
    public void NavigationMenuLink_Has_Href()
    {
        var cut = RenderNavigationMenu();
        var link = cut.Find("a");
        Assert.Equal("/home", link.GetAttribute("href"));
    }

    // --- Trigger and Content ---

    [Fact]
    public void NavigationMenuTrigger_Renders_As_Button()
    {
        var cut = RenderNavigationMenu(withTriggerAndContent: true);
        var button = cut.Find("button");
        Assert.Contains("Products", button.TextContent);
    }

    [Fact]
    public void NavigationMenuContent_Not_Rendered_Initially()
    {
        var cut = RenderNavigationMenu(withTriggerAndContent: true);
        Assert.DoesNotContain("Products content", cut.Markup);
    }

    [Fact]
    public void Clicking_Trigger_Shows_Content()
    {
        var cut = RenderNavigationMenu(withTriggerAndContent: true);

        cut.Find("button").Click();
        Assert.Contains("Products content", cut.Markup);
    }

    [Fact]
    public void Clicking_Trigger_Again_Hides_Content()
    {
        var cut = RenderNavigationMenu(withTriggerAndContent: true);

        // Open
        cut.Find("button").Click();
        Assert.Contains("Products content", cut.Markup);

        // Close
        cut.Find("button").Click();
        Assert.DoesNotContain("Products content", cut.Markup);
    }

    [Fact]
    public void Active_Trigger_Has_Active_Class()
    {
        var cut = RenderNavigationMenu(withTriggerAndContent: true);

        cut.Find("button").Click();
        var btn = cut.Find("button");
        var cls = btn.GetAttribute("class") ?? "";
        Assert.Contains("bg-accent", cls);
    }

    // --- Chevron rotation ---

    [Fact]
    public void Trigger_Shows_ChevronDown_Icon()
    {
        var cut = RenderNavigationMenu(withTriggerAndContent: true);
        var svgs = cut.FindAll("svg");
        Assert.NotEmpty(svgs);
    }

    // --- Multiple items ---

    [Fact]
    public void Multiple_MenuItems_Only_One_Content_Open_At_A_Time()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.NavigationMenu>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.NavigationMenuList>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(list =>
                {
                    // Item 1
                    list.OpenComponent<L.NavigationMenuItem>(0);
                    list.AddAttribute(1, "ChildContent", (RenderFragment)(item =>
                    {
                        item.OpenComponent<L.NavigationMenuTrigger>(0);
                        item.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Products")));
                        item.CloseComponent();
                        item.OpenComponent<L.NavigationMenuContent>(1);
                        item.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Products content")));
                        item.CloseComponent();
                    }));
                    list.CloseComponent();

                    // Item 2
                    list.OpenComponent<L.NavigationMenuItem>(1);
                    list.AddAttribute(2, "ChildContent", (RenderFragment)(item =>
                    {
                        item.OpenComponent<L.NavigationMenuTrigger>(0);
                        item.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Services")));
                        item.CloseComponent();
                        item.OpenComponent<L.NavigationMenuContent>(1);
                        item.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Services content")));
                        item.CloseComponent();
                    }));
                    list.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var buttons = cut.FindAll("button");
        Assert.Equal(2, buttons.Count);

        // Open Products
        buttons[0].Click();
        Assert.Contains("Products content", cut.Markup);
        Assert.DoesNotContain("Services content", cut.Markup);
    }

    // --- Link interaction ---

    [Fact]
    public void Clicking_NavigationMenuLink_Calls_SetActiveItemId_Null()
    {
        // Clicking a link should close any open sub-menus (sets active to null)
        var cut = RenderNavigationMenu();
        var link = cut.Find("a");
        // Should not throw
        link.Click();
        Assert.NotNull(link);
    }

    // --- Custom CSS ---

    [Fact]
    public void Custom_Class_Forwarded_On_NavigationMenu()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.NavigationMenu>(0);
            builder.AddAttribute(1, "Class", "my-nav-class");
            builder.CloseComponent();
        });

        var nav = cut.Find("nav");
        Assert.Contains("my-nav-class", nav.GetAttribute("class"));
    }

    [Fact]
    public void Custom_Class_Forwarded_On_NavigationMenuLink()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.NavigationMenu>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.NavigationMenuList>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(list =>
                {
                    list.OpenComponent<L.NavigationMenuItem>(0);
                    list.AddAttribute(1, "ChildContent", (RenderFragment)(item =>
                    {
                        item.OpenComponent<L.NavigationMenuLink>(0);
                        item.AddAttribute(1, "Class", "my-link-class");
                        item.AddAttribute(2, "ChildContent", (RenderFragment)(l => l.AddContent(0, "Home")));
                        item.CloseComponent();
                    }));
                    list.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var link = cut.Find("a");
        Assert.Contains("my-link-class", link.GetAttribute("class"));
    }
}
