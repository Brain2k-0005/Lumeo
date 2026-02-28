using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Popover;

public class PopoverTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PopoverTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderPopover(bool isOpen, EventCallback<bool>? isOpenChanged = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Popover>(0);
            builder.AddAttribute(1, "IsOpen", isOpen);
            if (isOpenChanged.HasValue)
                builder.AddAttribute(2, "IsOpenChanged", isOpenChanged.Value);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.PopoverTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Toggle")));
                b.CloseComponent();

                b.OpenComponent<L.PopoverContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.AddContent(0, "Popover content");
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // --- Open / Close ---

    [Fact]
    public void PopoverContent_Not_Rendered_When_Closed()
    {
        var cut = RenderPopover(isOpen: false);
        // PopoverContent wraps in a div without special role;
        // check that popover content text is absent
        Assert.DoesNotContain("Popover content", cut.Markup);
    }

    [Fact]
    public void PopoverContent_Rendered_When_Open()
    {
        var cut = RenderPopover(isOpen: true);
        Assert.Contains("Popover content", cut.Markup);
    }

    // --- Trigger toggle ---

    [Fact]
    public void Trigger_Click_Opens_Popover()
    {
        bool? openedValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => openedValue = v);
        var cut = RenderPopover(isOpen: false, isOpenChanged: callback);

        cut.Find("[role='button']").Click();
        Assert.True(openedValue);
    }

    [Fact]
    public void Trigger_Click_Closes_Popover_When_Open()
    {
        bool? closedValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => closedValue = v);
        var cut = RenderPopover(isOpen: true, isOpenChanged: callback);

        cut.Find("[role='button']").Click();
        Assert.False(closedValue);
    }

    // --- Trigger aria-expanded ---
    // Blazor renders bool false as absent (null) and bool true as "" (HTML boolean attribute)

    [Fact]
    public void Trigger_Has_Aria_Expanded_False_When_Closed()
    {
        var cut = RenderPopover(isOpen: false);
        var trigger = cut.Find("[role='button']");
        // When false, Blazor omits the attribute entirely
        Assert.Null(trigger.GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Trigger_Has_Aria_Expanded_True_When_Open()
    {
        var cut = RenderPopover(isOpen: true);
        var trigger = cut.Find("[role='button']");
        // When true, Blazor renders aria-expanded as "" (HTML boolean attribute presence)
        Assert.NotNull(trigger.GetAttribute("aria-expanded"));
    }

    // --- Custom Class on PopoverContent ---

    [Fact]
    public void Custom_Class_Forwarded_On_PopoverContent()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Popover>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.PopoverContent>(0);
                b.AddAttribute(1, "Class", "my-popover-class");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var elements = cut.FindAll("[class]");
        Assert.True(elements.Any(e => (e.GetAttribute("class") ?? "").Contains("my-popover-class")));
    }

    // --- PopoverContent default classes ---

    [Fact]
    public void PopoverContent_Has_Default_Classes_When_Open()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Popover>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.PopoverContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var elements = cut.FindAll("[class]");
        Assert.True(elements.Any(e =>
        {
            var cls = e.GetAttribute("class") ?? "";
            return cls.Contains("bg-popover") && cls.Contains("border-border");
        }));
    }

    // --- Escape key ---

    [Fact]
    public void Escape_Key_On_PopoverContent_Fires_Close()
    {
        bool? closedValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => closedValue = v);

        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Popover>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "IsOpenChanged", callback);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.PopoverContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // Find the content div (tabindex="-1") and fire keydown Escape
        var contentDiv = cut.FindAll("div").FirstOrDefault(d => d.GetAttribute("tabindex") == "-1");
        Assert.NotNull(contentDiv);
        contentDiv!.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        Assert.False(closedValue);
    }
}
