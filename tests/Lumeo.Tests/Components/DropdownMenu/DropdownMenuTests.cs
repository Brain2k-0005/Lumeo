using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DropdownMenu;

public class DropdownMenuTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DropdownMenuTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderDropdownMenu(bool isOpen, EventCallback<bool>? isOpenChanged = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DropdownMenu>(0);
            builder.AddAttribute(1, "IsOpen", isOpen);
            if (isOpenChanged.HasValue)
                builder.AddAttribute(2, "IsOpenChanged", isOpenChanged.Value);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DropdownMenuTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Open Menu")));
                b.CloseComponent();

                b.OpenComponent<L.DropdownMenuContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.DropdownMenuItem>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(item => item.AddContent(0, "Item 1")));
                    inner.CloseComponent();

                    inner.OpenComponent<L.DropdownMenuItem>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(item => item.AddContent(0, "Item 2")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // --- Open / Close ---

    [Fact]
    public void DropdownMenuContent_Not_Rendered_When_Closed()
    {
        var cut = RenderDropdownMenu(isOpen: false);
        Assert.DoesNotContain("Item 1", cut.Markup);
    }

    [Fact]
    public void DropdownMenuContent_Rendered_When_Open()
    {
        var cut = RenderDropdownMenu(isOpen: true);
        Assert.Contains("Item 1", cut.Markup);
        Assert.Contains("Item 2", cut.Markup);
    }

    // --- Trigger toggle ---

    [Fact]
    public void Trigger_Click_Opens_DropdownMenu()
    {
        bool? openedValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => openedValue = v);
        var cut = RenderDropdownMenu(isOpen: false, isOpenChanged: callback);

        cut.Find("[role='button']").Click();
        Assert.True(openedValue);
    }

    [Fact]
    public void Trigger_Click_Closes_DropdownMenu_When_Open()
    {
        bool? closedValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => closedValue = v);
        var cut = RenderDropdownMenu(isOpen: true, isOpenChanged: callback);

        cut.Find("[role='button']").Click();
        Assert.False(closedValue);
    }

    // --- Trigger aria-expanded ---
    // Blazor renders bool false as absent (null) and bool true as "" (HTML boolean attribute)

    [Fact]
    public void Trigger_Has_Aria_Expanded_False_When_Closed()
    {
        var cut = RenderDropdownMenu(isOpen: false);
        var trigger = cut.Find("[role='button']");
        // When false, Blazor omits the attribute entirely
        Assert.Null(trigger.GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Trigger_Has_Aria_Expanded_True_When_Open()
    {
        var cut = RenderDropdownMenu(isOpen: true);
        var trigger = cut.Find("[role='button']");
        // When true, Blazor renders aria-expanded as "" (HTML boolean attribute presence)
        Assert.NotNull(trigger.GetAttribute("aria-expanded"));
    }

    // --- Item selection closes menu ---

    [Fact]
    public void Clicking_DropdownMenuItem_Fires_OnClick_And_Closes_Menu()
    {
        bool itemClicked = false;
        bool? closedValue = null;
        var menuCallback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => closedValue = v);
        var itemCallback = EventCallback.Factory.Create(_ctx, () => itemClicked = true);

        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DropdownMenu>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "IsOpenChanged", menuCallback);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DropdownMenuContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.DropdownMenuItem>(0);
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
    public void Disabled_DropdownMenuItem_Has_Disabled_Attribute()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DropdownMenu>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DropdownMenuContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.DropdownMenuItem>(0);
                    inner.AddAttribute(1, "Disabled", true);
                    inner.AddAttribute(2, "ChildContent", (RenderFragment)(item => item.AddContent(0, "Disabled")));
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
    public void Escape_Key_On_DropdownMenuContent_Fires_Close()
    {
        bool? closedValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => closedValue = v);
        var cut = RenderDropdownMenu(isOpen: true, isOpenChanged: callback);

        var contentDiv = cut.FindAll("div").FirstOrDefault(d => d.GetAttribute("tabindex") == "-1");
        Assert.NotNull(contentDiv);
        contentDiv!.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        Assert.False(closedValue);
    }

    // --- Custom class ---

    [Fact]
    public void Custom_Class_Forwarded_On_DropdownMenuContent()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DropdownMenu>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DropdownMenuContent>(0);
                b.AddAttribute(1, "Class", "my-dropdown-class");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var elements = cut.FindAll("[class]");
        Assert.True(elements.Any(e => (e.GetAttribute("class") ?? "").Contains("my-dropdown-class")));
    }

    // --- Label and Separator ---

    [Fact]
    public void DropdownMenuLabel_Renders_Content()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DropdownMenu>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DropdownMenuContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.DropdownMenuLabel>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(label => label.AddContent(0, "Section Label")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("Section Label", cut.Markup);
    }

    [Fact]
    public void DropdownMenuSeparator_Renders()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DropdownMenu>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DropdownMenuContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.DropdownMenuSeparator>(0);
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // DropdownMenuSeparator uses role="none" and has bg-border class
        var elements = cut.FindAll("[class]");
        Assert.True(elements.Any(e =>
        {
            var cls = e.GetAttribute("class") ?? "";
            return cls.Contains("bg-border") && cls.Contains("h-px");
        }));
    }
}
