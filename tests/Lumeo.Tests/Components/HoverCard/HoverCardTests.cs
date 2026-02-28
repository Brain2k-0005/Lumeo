using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.HoverCard;

public class HoverCardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public HoverCardTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderHoverCard(bool isOpen, EventCallback<bool>? isOpenChanged = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.HoverCard>(0);
            builder.AddAttribute(1, "IsOpen", isOpen);
            if (isOpenChanged.HasValue)
                builder.AddAttribute(2, "IsOpenChanged", isOpenChanged.Value);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.HoverCardTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Hover trigger")));
                b.CloseComponent();

                b.OpenComponent<L.HoverCardContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.AddContent(0, "HoverCard content");
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // --- Open / Close ---

    [Fact]
    public void HoverCardContent_Not_Rendered_When_Closed()
    {
        var cut = RenderHoverCard(isOpen: false);
        Assert.DoesNotContain("HoverCard content", cut.Markup);
    }

    [Fact]
    public void HoverCardContent_Rendered_When_Open()
    {
        var cut = RenderHoverCard(isOpen: true);
        Assert.Contains("HoverCard content", cut.Markup);
    }

    // --- Trigger aria-expanded ---
    // Blazor renders bool false as absent (null) and bool true as "" (HTML boolean attribute)

    [Fact]
    public void HoverCardTrigger_Has_No_Aria_Expanded_When_Closed()
    {
        var cut = RenderHoverCard(isOpen: false);
        // HoverCardTrigger has aria-expanded="@Context.IsOpen"; when false, attribute is omitted
        var triggerDivs = cut.FindAll("[aria-expanded]");
        Assert.Empty(triggerDivs);
    }

    [Fact]
    public void HoverCardTrigger_Has_Aria_Expanded_When_Open()
    {
        var cut = RenderHoverCard(isOpen: true);
        // When true, Blazor renders aria-expanded as "" (HTML boolean attribute presence)
        var trigger = cut.FindAll("[aria-expanded]").FirstOrDefault();
        Assert.NotNull(trigger);
    }

    // --- Mouse events on trigger ---

    [Fact]
    public void HoverCardTrigger_Renders_With_Inline_Flex_Class()
    {
        var cut = RenderHoverCard(isOpen: false);
        // HoverCardTrigger renders a div with class "inline-flex"
        var elements = cut.FindAll("[class]");
        Assert.True(elements.Any(e => (e.GetAttribute("class") ?? "").StartsWith("inline-flex")));
    }

    // --- HoverCard wrapper ---

    [Fact]
    public void HoverCard_Wrapper_Has_Relative_Inline_Block_Class()
    {
        var cut = RenderHoverCard(isOpen: false);
        var elements = cut.FindAll("[class]");
        Assert.True(elements.Any(e =>
        {
            var cls = e.GetAttribute("class") ?? "";
            return cls.Contains("relative") && cls.Contains("inline-block");
        }));
    }

    // --- HoverCardContent default classes ---

    [Fact]
    public void HoverCardContent_Has_Default_Classes_When_Open()
    {
        var cut = RenderHoverCard(isOpen: true);
        var elements = cut.FindAll("[class]");
        Assert.True(elements.Any(e =>
        {
            var cls = e.GetAttribute("class") ?? "";
            return cls.Contains("bg-popover") && cls.Contains("border-border");
        }));
    }

    // --- Custom class on HoverCardContent ---

    [Fact]
    public void Custom_Class_Forwarded_On_HoverCardContent()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.HoverCard>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.HoverCardContent>(0);
                b.AddAttribute(1, "Class", "my-hovercard-class");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var elements = cut.FindAll("[class]");
        Assert.True(elements.Any(e => (e.GetAttribute("class") ?? "").Contains("my-hovercard-class")));
    }

    // --- Open/Close delay parameters ---

    [Fact]
    public void HoverCard_Accepts_OpenDelay_And_CloseDelay_Parameters()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.HoverCard>(0);
            builder.AddAttribute(1, "IsOpen", false);
            builder.AddAttribute(2, "OpenDelay", 100);
            builder.AddAttribute(3, "CloseDelay", 150);
            builder.AddAttribute(4, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.HoverCardTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Trigger")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("Trigger", cut.Markup);
    }
}
