using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ContextMenu;

public class ContextMenuTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ContextMenuTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderContextMenu(bool isOpen, EventCallback<bool>? isOpenChanged = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ContextMenu>(0);
            builder.AddAttribute(1, "IsOpen", isOpen);
            if (isOpenChanged.HasValue)
                builder.AddAttribute(2, "IsOpenChanged", isOpenChanged.Value);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ContextMenuTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Right-click here")));
                b.CloseComponent();

                b.OpenComponent<L.ContextMenuContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.ContextMenuItem>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(item => item.AddContent(0, "Menu Item 1")));
                    inner.CloseComponent();

                    inner.OpenComponent<L.ContextMenuItem>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(item => item.AddContent(0, "Menu Item 2")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // --- Open / Close ---

    [Fact]
    public void ContextMenuContent_Not_Rendered_When_Closed()
    {
        var cut = RenderContextMenu(isOpen: false);
        Assert.DoesNotContain("Menu Item 1", cut.Markup);
    }

    [Fact]
    public void ContextMenuContent_Rendered_When_Open()
    {
        var cut = RenderContextMenu(isOpen: true);
        Assert.Contains("Menu Item 1", cut.Markup);
        Assert.Contains("Menu Item 2", cut.Markup);
    }

    // --- Right-click trigger ---

    [Fact]
    public void Right_Click_On_Trigger_Opens_ContextMenu()
    {
        bool? openedValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => openedValue = v);
        var cut = RenderContextMenu(isOpen: false, isOpenChanged: callback);

        // ContextMenuTrigger renders as a div; find it by its inline-flex class
        // When closed, aria-expanded is omitted, so find by class instead
        var triggerDivs = cut.FindAll("div.inline-flex");
        Assert.NotEmpty(triggerDivs);
        triggerDivs[0].ContextMenu(new MouseEventArgs { ClientX = 100, ClientY = 200 });

        Assert.True(openedValue);
    }

    // --- Trigger aria-expanded ---
    // Blazor renders bool false as absent (null) and bool true as "" (HTML boolean attribute)

    [Fact]
    public void ContextMenuTrigger_Has_No_Aria_Expanded_When_Closed()
    {
        var cut = RenderContextMenu(isOpen: false);
        // When IsOpen is false, Blazor omits the aria-expanded attribute entirely
        var elements = cut.FindAll("[aria-expanded]");
        Assert.Empty(elements);
    }

    [Fact]
    public void ContextMenuTrigger_Has_Aria_Expanded_When_Open()
    {
        var cut = RenderContextMenu(isOpen: true);
        // When IsOpen is true, Blazor renders aria-expanded as "" (HTML boolean presence)
        var trigger = cut.FindAll("[aria-expanded]").FirstOrDefault();
        Assert.NotNull(trigger);
    }

    // --- Item selection closes menu ---

    [Fact]
    public void Clicking_ContextMenuItem_Fires_OnClick_And_Closes_Menu()
    {
        bool itemClicked = false;
        bool? closedValue = null;
        var menuCallback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => closedValue = v);
        var itemCallback = EventCallback.Factory.Create(_ctx, () => itemClicked = true);

        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ContextMenu>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "IsOpenChanged", menuCallback);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ContextMenuContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.ContextMenuItem>(0);
                    inner.AddAttribute(1, "OnClick", itemCallback);
                    inner.AddAttribute(2, "ChildContent", (RenderFragment)(item => item.AddContent(0, "Click Me")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        try { cut.Find("button").Click(); } catch (ArgumentException) { }
        Assert.True(itemClicked);
        Assert.False(closedValue);
    }

    // --- Disabled item ---

    [Fact]
    public void Disabled_ContextMenuItem_Has_Disabled_Attribute()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ContextMenu>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ContextMenuContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.ContextMenuItem>(0);
                    inner.AddAttribute(1, "Disabled", true);
                    inner.AddAttribute(2, "ChildContent", (RenderFragment)(item => item.AddContent(0, "Disabled Item")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var button = cut.Find("button[disabled]");
        Assert.NotNull(button);
    }

    // --- Escape key ---

    [Fact]
    public void Escape_Key_On_ContextMenuContent_Fires_Close()
    {
        bool? closedValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => closedValue = v);
        var cut = RenderContextMenu(isOpen: true, isOpenChanged: callback);

        var contentDiv = cut.FindAll("div").FirstOrDefault(d => d.GetAttribute("tabindex") == "-1");
        Assert.NotNull(contentDiv);
        contentDiv!.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(closedValue);
    }

    // --- Position style ---

    [Fact]
    public void ContextMenuContent_Has_Fixed_Position_Style_When_Open()
    {
        var cut = RenderContextMenu(isOpen: true);
        var elements = cut.FindAll("[style]");
        Assert.True(elements.Any(e =>
        {
            var style = e.GetAttribute("style") ?? "";
            return style.Contains("position: fixed");
        }));
    }

    // --- Custom class ---

    [Fact]
    public void Custom_Class_Forwarded_On_ContextMenuContent()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ContextMenu>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ContextMenuContent>(0);
                b.AddAttribute(1, "Class", "my-context-class");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var elements = cut.FindAll("[class]");
        Assert.True(elements.Any(e => (e.GetAttribute("class") ?? "").Contains("my-context-class")));
    }

    // --- Label and Separator ---

    [Fact]
    public void ContextMenuLabel_Renders_Content()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ContextMenu>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ContextMenuContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.ContextMenuLabel>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(label => label.AddContent(0, "Context Label")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("Context Label", cut.Markup);
    }

    [Fact]
    public void ContextMenuSeparator_Renders()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ContextMenu>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ContextMenuContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.ContextMenuSeparator>(0);
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // ContextMenuSeparator uses role="none" and has bg-border class
        var elements = cut.FindAll("[class]");
        Assert.True(elements.Any(e =>
        {
            var cls = e.GetAttribute("class") ?? "";
            return cls.Contains("bg-border") && cls.Contains("h-px");
        }));
    }

    // --- Group ---

    [Fact]
    public void ContextMenuGroup_Renders_With_Role_Group()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ContextMenu>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ContextMenuContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.ContextMenuGroup>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(g => g.AddContent(0, "Group content")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.NotEmpty(cut.FindAll("[role='group']"));
        Assert.Contains("Group content", cut.Markup);
    }
}
