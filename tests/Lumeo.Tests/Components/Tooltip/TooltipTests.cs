using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Tooltip;

public class TooltipTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TooltipTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Tooltip is CSS-only (group/group-hover). Content is always in the DOM but visually hidden.

    private IRenderedComponent<IComponent> RenderTooltip(L.TooltipContent.TooltipSide side = L.TooltipContent.TooltipSide.Top)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Tooltip>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TooltipTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Hover me")));
                b.CloseComponent();

                b.OpenComponent<L.TooltipContent>(0);
                b.AddAttribute(1, "Side", side);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Tooltip text")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // --- Always rendered (CSS-only) ---

    [Fact]
    public void TooltipContent_Always_Rendered_In_DOM()
    {
        var cut = RenderTooltip();
        // TooltipContent is always in the DOM with invisible class; visibility toggled via CSS group-hover
        Assert.NotEmpty(cut.FindAll("[role='tooltip']"));
    }

    [Fact]
    public void TooltipContent_Shows_Text()
    {
        var cut = RenderTooltip();
        Assert.Contains("Tooltip text", cut.Markup);
    }

    [Fact]
    public void TooltipTrigger_Shows_Text()
    {
        var cut = RenderTooltip();
        Assert.Contains("Hover me", cut.Markup);
    }

    // --- CSS classes for visibility ---

    [Fact]
    public void TooltipContent_Has_Invisible_Class_By_Default()
    {
        var cut = RenderTooltip();
        var tooltip = cut.Find("[role='tooltip']");
        var cls = tooltip.GetAttribute("class") ?? "";
        Assert.Contains("invisible", cls);
    }

    [Fact]
    public void TooltipContent_Has_Group_Hover_Visible_Class()
    {
        var cut = RenderTooltip();
        var tooltip = cut.Find("[role='tooltip']");
        var cls = tooltip.GetAttribute("class") ?? "";
        Assert.Contains("group-hover:visible", cls);
    }

    // --- Side variants ---

    [Fact]
    public void TooltipContent_Top_Side_Has_Bottom_Full_Class()
    {
        var cut = RenderTooltip(side: L.TooltipContent.TooltipSide.Top);
        var tooltip = cut.Find("[role='tooltip']");
        var cls = tooltip.GetAttribute("class") ?? "";
        Assert.Contains("bottom-full", cls);
    }

    [Fact]
    public void TooltipContent_Bottom_Side_Has_Top_Full_Class()
    {
        var cut = RenderTooltip(side: L.TooltipContent.TooltipSide.Bottom);
        var tooltip = cut.Find("[role='tooltip']");
        var cls = tooltip.GetAttribute("class") ?? "";
        Assert.Contains("top-full", cls);
    }

    [Fact]
    public void TooltipContent_Left_Side_Has_Right_Full_Class()
    {
        var cut = RenderTooltip(side: L.TooltipContent.TooltipSide.Left);
        var tooltip = cut.Find("[role='tooltip']");
        var cls = tooltip.GetAttribute("class") ?? "";
        Assert.Contains("right-full", cls);
    }

    [Fact]
    public void TooltipContent_Right_Side_Has_Left_Full_Class()
    {
        var cut = RenderTooltip(side: L.TooltipContent.TooltipSide.Right);
        var tooltip = cut.Find("[role='tooltip']");
        var cls = tooltip.GetAttribute("class") ?? "";
        Assert.Contains("left-full", cls);
    }

    // --- Custom class ---

    [Fact]
    public void Custom_Class_Forwarded_On_TooltipContent()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Tooltip>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TooltipContent>(0);
                b.AddAttribute(1, "Class", "my-tooltip-class");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Text")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var tooltip = cut.Find("[role='tooltip']");
        Assert.Contains("my-tooltip-class", tooltip.GetAttribute("class"));
    }

    // --- Wrapper has group class ---

    [Fact]
    public void Tooltip_Wrapper_Has_Group_Class()
    {
        var cut = RenderTooltip();
        // Tooltip wraps in a div with class "group relative inline-flex"
        var elements = cut.FindAll("[class]");
        Assert.True(elements.Any(e => (e.GetAttribute("class") ?? "").Contains("group")));
    }

    // --- TooltipTrigger class ---

    [Fact]
    public void TooltipTrigger_Has_Inline_Flex_Class()
    {
        var cut = RenderTooltip();
        var elements = cut.FindAll("[class]");
        // TooltipTrigger renders a div with class "inline-flex"
        Assert.True(elements.Any(e => (e.GetAttribute("class") ?? "").StartsWith("inline-flex")));
    }

    // --- AdditionalAttributes forwarded ---

    [Fact]
    public void Additional_Attributes_Forwarded_On_TooltipContent()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Tooltip>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TooltipContent>(0);
                b.AddAttribute(1, "AdditionalAttributes", new Dictionary<string, object>
                {
                    ["data-testid"] = "my-tooltip"
                });
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Text")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var tooltip = cut.Find("[role='tooltip']");
        Assert.Equal("my-tooltip", tooltip.GetAttribute("data-testid"));
    }
}
