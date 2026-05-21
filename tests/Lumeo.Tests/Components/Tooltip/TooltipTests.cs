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

    // 3.0 — Tooltip switched from always-in-DOM + CSS group-hover to mounted-on-open
    // via position: fixed + IComponentInteropService.PositionFixed for collision flip.
    // TooltipContent renders nothing until the trigger opens the tooltip.

    private IRenderedComponent<IComponent> RenderTooltip(L.Side side = L.Side.Top)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Tooltip>(0);
            builder.AddAttribute(1, "ShowDelay", 0);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TooltipTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Hover me")));
                b.CloseComponent();

                b.OpenComponent<L.TooltipContent>(2);
                b.AddAttribute(3, "Side", side);
                b.AddAttribute(4, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Tooltip text")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    private static void OpenTooltip(IRenderedComponent<IComponent> cut)
    {
        cut.Find("div").TriggerEvent("onmouseenter", new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
    }

    // --- Closed by default ---

    [Fact]
    public void TooltipContent_Not_Rendered_When_Closed()
    {
        var cut = RenderTooltip();
        Assert.Empty(cut.FindAll("[role='tooltip']"));
    }

    [Fact]
    public void TooltipTrigger_Shows_Text()
    {
        var cut = RenderTooltip();
        Assert.Contains("Hover me", cut.Markup);
    }

    // --- Opens on mouseenter ---

    [Fact]
    public void Tooltip_Mounts_Content_On_MouseEnter()
    {
        var cut = RenderTooltip();
        OpenTooltip(cut);
        Assert.NotEmpty(cut.FindAll("[role='tooltip']"));
        Assert.Contains("Tooltip text", cut.Markup);
    }

    [Fact]
    public void TooltipContent_Has_Fixed_Position_When_Open()
    {
        var cut = RenderTooltip();
        OpenTooltip(cut);
        var tooltip = cut.Find("[role='tooltip']");
        var cls = tooltip.GetAttribute("class") ?? "";
        Assert.Contains("fixed", cls);
    }

    [Fact]
    public void TooltipContent_Visible_When_Open()
    {
        var cut = RenderTooltip();
        OpenTooltip(cut);
        var tooltip = cut.Find("[role='tooltip']");
        var cls = tooltip.GetAttribute("class") ?? "";
        Assert.Contains("visible", cls);
        Assert.Contains("opacity-100", cls);
    }

    // --- Wrapper / Trigger ---

    [Fact]
    public void Tooltip_Wrapper_Has_Relative_Inline_Flex_Class()
    {
        var cut = RenderTooltip();
        var elements = cut.FindAll("[class]");
        Assert.Contains(elements, e => (e.GetAttribute("class") ?? "").Contains("relative inline-flex"));
    }

    [Fact]
    public void TooltipTrigger_Has_Inline_Flex_Class()
    {
        var cut = RenderTooltip();
        var elements = cut.FindAll("[class]");
        Assert.Contains(elements, e => (e.GetAttribute("class") ?? "").StartsWith("inline-flex"));
    }

    // --- Custom class forwarded ---

    [Fact]
    public void Custom_Class_Forwarded_On_TooltipContent()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Tooltip>(0);
            builder.AddAttribute(1, "ShowDelay", 0);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TooltipTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Hover me")));
                b.CloseComponent();

                b.OpenComponent<L.TooltipContent>(2);
                b.AddAttribute(3, "Class", "my-tooltip-class");
                b.AddAttribute(4, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Text")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.Find("div").TriggerEvent("onmouseenter", new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        var tooltip = cut.Find("[role='tooltip']");
        Assert.Contains("my-tooltip-class", tooltip.GetAttribute("class"));
    }

    // --- AdditionalAttributes forwarded ---

    [Fact]
    public void Additional_Attributes_Forwarded_On_TooltipContent()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Tooltip>(0);
            builder.AddAttribute(1, "ShowDelay", 0);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TooltipTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Hover me")));
                b.CloseComponent();

                b.OpenComponent<L.TooltipContent>(2);
                b.AddAttribute(3, "AdditionalAttributes", new Dictionary<string, object>
                {
                    ["data-testid"] = "my-tooltip"
                });
                b.AddAttribute(4, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Text")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.Find("div").TriggerEvent("onmouseenter", new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        var tooltip = cut.Find("[role='tooltip']");
        Assert.Equal("my-tooltip", tooltip.GetAttribute("data-testid"));
    }
}
