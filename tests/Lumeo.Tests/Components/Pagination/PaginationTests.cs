using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Pagination;

public class PaginationTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public PaginationTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    // --- Pagination (nav wrapper) ---

    [Fact]
    public void Pagination_Renders_Nav_Element()
    {
        var cut = _ctx.Render<L.Pagination>(p => p.AddChildContent(""));
        Assert.NotNull(cut.Find("nav"));
    }

    [Fact]
    public void Pagination_Has_Role_Navigation()
    {
        var cut = _ctx.Render<L.Pagination>(p => p.AddChildContent(""));
        Assert.Equal("navigation", cut.Find("nav").GetAttribute("role"));
    }

    [Fact]
    public void Pagination_Has_Aria_Label_Pagination()
    {
        var cut = _ctx.Render<L.Pagination>(p => p.AddChildContent(""));
        Assert.Equal("pagination", cut.Find("nav").GetAttribute("aria-label"));
    }

    [Fact]
    public void Pagination_Has_Default_Classes()
    {
        var cut = _ctx.Render<L.Pagination>(p => p.AddChildContent(""));
        var cls = cut.Find("nav").GetAttribute("class") ?? "";
        Assert.Contains("flex", cls);
        Assert.Contains("justify-center", cls);
    }

    [Fact]
    public void Pagination_Custom_Class_Appended()
    {
        var cut = _ctx.Render<L.Pagination>(p => p
            .Add(c => c.Class, "my-pagination")
            .AddChildContent(""));
        var cls = cut.Find("nav").GetAttribute("class") ?? "";
        Assert.Contains("my-pagination", cls);
    }

    [Fact]
    public void Pagination_Renders_ChildContent()
    {
        var cut = _ctx.Render<L.Pagination>(p => p.AddChildContent("pagination content"));
        Assert.Contains("pagination content", cut.Markup);
    }

    [Fact]
    public void Pagination_Additional_Attributes_Forwarded()
    {
        var cut = _ctx.Render<L.Pagination>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "my-nav" })
            .AddChildContent(""));
        Assert.Equal("my-nav", cut.Find("nav").GetAttribute("data-testid"));
    }

    // --- PaginationContent ---

    [Fact]
    public void PaginationContent_Renders_Ul_Element()
    {
        var cut = _ctx.Render<L.PaginationContent>(p => p.AddChildContent(""));
        Assert.NotNull(cut.Find("ul"));
    }

    [Fact]
    public void PaginationContent_Has_Default_Classes()
    {
        var cut = _ctx.Render<L.PaginationContent>(p => p.AddChildContent(""));
        var cls = cut.Find("ul").GetAttribute("class") ?? "";
        Assert.Contains("flex", cls);
        Assert.Contains("flex-row", cls);
        Assert.Contains("items-center", cls);
        Assert.Contains("gap-1", cls);
    }

    [Fact]
    public void PaginationContent_Renders_ChildContent()
    {
        var cut = _ctx.Render<L.PaginationContent>(p => p.AddChildContent("list content"));
        Assert.Contains("list content", cut.Markup);
    }

    [Fact]
    public void PaginationContent_Custom_Class_Appended()
    {
        var cut = _ctx.Render<L.PaginationContent>(p => p
            .Add(c => c.Class, "my-content")
            .AddChildContent(""));
        var cls = cut.Find("ul").GetAttribute("class") ?? "";
        Assert.Contains("my-content", cls);
    }

    // --- PaginationItem ---

    [Fact]
    public void PaginationItem_Renders_Li_With_Button()
    {
        var cut = _ctx.Render<L.PaginationItem>(p => p.AddChildContent("1"));
        Assert.NotNull(cut.Find("li"));
        Assert.NotNull(cut.Find("button"));
    }

    [Fact]
    public void PaginationItem_Renders_ChildContent()
    {
        var cut = _ctx.Render<L.PaginationItem>(p => p.AddChildContent("1"));
        Assert.Contains("1", cut.Find("button").TextContent);
    }

    [Fact]
    public void PaginationItem_Not_Active_Has_Hover_Classes()
    {
        var cut = _ctx.Render<L.PaginationItem>(p => p
            .Add(c => c.IsActive, false)
            .AddChildContent("1"));
        var cls = cut.Find("button").GetAttribute("class") ?? "";
        Assert.Contains("hover:bg-accent", cls);
    }

    [Fact]
    public void PaginationItem_Active_Has_Border_And_Background()
    {
        var cut = _ctx.Render<L.PaginationItem>(p => p
            .Add(c => c.IsActive, true)
            .AddChildContent("1"));
        var cls = cut.Find("button").GetAttribute("class") ?? "";
        Assert.Contains("border", cls);
        Assert.Contains("bg-background", cls);
        Assert.Contains("shadow-sm", cls);
    }

    [Fact]
    public void PaginationItem_OnClick_Fires_When_Clicked()
    {
        var clicked = false;
        var cut = _ctx.Render<L.PaginationItem>(p => p
            .Add(c => c.OnClick, EventCallback.Factory.Create(_ctx, () => clicked = true))
            .AddChildContent("1"));

        cut.Find("button").Click();
        Assert.True(clicked);
    }

    [Fact]
    public void PaginationItem_Custom_Class_Appended()
    {
        var cut = _ctx.Render<L.PaginationItem>(p => p
            .Add(c => c.Class, "page-item-custom")
            .AddChildContent("1"));
        var cls = cut.Find("button").GetAttribute("class") ?? "";
        Assert.Contains("page-item-custom", cls);
    }

    // --- PaginationPrevious ---

    [Fact]
    public void PaginationPrevious_Renders_Li_With_Button()
    {
        var cut = _ctx.Render<L.PaginationPrevious>();
        Assert.NotNull(cut.Find("li"));
        Assert.NotNull(cut.Find("button"));
    }

    [Fact]
    public void PaginationPrevious_Contains_Previous_Text()
    {
        var cut = _ctx.Render<L.PaginationPrevious>();
        Assert.Contains("Previous", cut.Find("button").TextContent);
    }

    [Fact]
    public void PaginationPrevious_OnClick_Fires_When_Clicked()
    {
        var clicked = false;
        var cut = _ctx.Render<L.PaginationPrevious>(p => p
            .Add(c => c.OnClick, EventCallback.Factory.Create(_ctx, () => clicked = true)));

        cut.Find("button").Click();
        Assert.True(clicked);
    }

    [Fact]
    public void PaginationPrevious_Disabled_Attribute_Set()
    {
        var cut = _ctx.Render<L.PaginationPrevious>(p => p
            .Add(c => c.Disabled, true));

        Assert.NotNull(cut.Find("button[disabled]"));
    }

    [Fact]
    public void PaginationPrevious_Not_Disabled_By_Default()
    {
        var cut = _ctx.Render<L.PaginationPrevious>();
        Assert.Null(cut.Find("button").GetAttribute("disabled"));
    }

    [Fact]
    public void PaginationPrevious_Custom_Class_Appended()
    {
        var cut = _ctx.Render<L.PaginationPrevious>(p => p
            .Add(c => c.Class, "prev-custom"));
        var cls = cut.Find("button").GetAttribute("class") ?? "";
        Assert.Contains("prev-custom", cls);
    }

    // --- PaginationNext ---

    [Fact]
    public void PaginationNext_Renders_Li_With_Button()
    {
        var cut = _ctx.Render<L.PaginationNext>();
        Assert.NotNull(cut.Find("li"));
        Assert.NotNull(cut.Find("button"));
    }

    [Fact]
    public void PaginationNext_Contains_Next_Text()
    {
        var cut = _ctx.Render<L.PaginationNext>();
        Assert.Contains("Next", cut.Find("button").TextContent);
    }

    [Fact]
    public void PaginationNext_OnClick_Fires_When_Clicked()
    {
        var clicked = false;
        var cut = _ctx.Render<L.PaginationNext>(p => p
            .Add(c => c.OnClick, EventCallback.Factory.Create(_ctx, () => clicked = true)));

        cut.Find("button").Click();
        Assert.True(clicked);
    }

    [Fact]
    public void PaginationNext_Disabled_Attribute_Set()
    {
        var cut = _ctx.Render<L.PaginationNext>(p => p
            .Add(c => c.Disabled, true));

        Assert.NotNull(cut.Find("button[disabled]"));
    }

    [Fact]
    public void PaginationNext_Not_Disabled_By_Default()
    {
        var cut = _ctx.Render<L.PaginationNext>();
        Assert.Null(cut.Find("button").GetAttribute("disabled"));
    }

    [Fact]
    public void PaginationNext_Custom_Class_Appended()
    {
        var cut = _ctx.Render<L.PaginationNext>(p => p
            .Add(c => c.Class, "next-custom"));
        var cls = cut.Find("button").GetAttribute("class") ?? "";
        Assert.Contains("next-custom", cls);
    }

    // --- PaginationEllipsis ---

    [Fact]
    public void PaginationEllipsis_Renders_Li_With_Span()
    {
        var cut = _ctx.Render<L.PaginationEllipsis>();
        Assert.NotNull(cut.Find("li"));
        Assert.NotNull(cut.Find("span"));
    }

    [Fact]
    public void PaginationEllipsis_Has_Sr_Only_Text()
    {
        var cut = _ctx.Render<L.PaginationEllipsis>();
        Assert.Contains("More pages", cut.Markup);
        Assert.Contains("sr-only", cut.Markup);
    }

    [Fact]
    public void PaginationEllipsis_Custom_Class_Appended()
    {
        var cut = _ctx.Render<L.PaginationEllipsis>(p => p
            .Add(c => c.Class, "ellipsis-custom"));
        // The outer span wrapping both icon and sr-only text
        var outerSpan = cut.Find("li > span");
        var cls = outerSpan.GetAttribute("class") ?? "";
        Assert.Contains("ellipsis-custom", cls);
    }

    // --- Full Pagination Structure ---

    [Fact]
    public void Full_Pagination_Structure_Renders_Correctly()
    {
        var currentPage = 2;
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Pagination>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.PaginationContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(ul =>
                {
                    ul.OpenComponent<L.PaginationPrevious>(0);
                    ul.AddAttribute(1, "Disabled", currentPage <= 1);
                    ul.CloseComponent();

                    ul.OpenComponent<L.PaginationItem>(1);
                    ul.AddAttribute(2, "IsActive", true);
                    ul.AddAttribute(3, "ChildContent", (RenderFragment)(c => c.AddContent(0, "2")));
                    ul.CloseComponent();

                    ul.OpenComponent<L.PaginationEllipsis>(2);
                    ul.CloseComponent();

                    ul.OpenComponent<L.PaginationNext>(3);
                    ul.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.NotNull(cut.Find("nav"));
        Assert.NotNull(cut.Find("ul"));
        Assert.Contains("Previous", cut.Markup);
        Assert.Contains("Next", cut.Markup);
        Assert.Contains("More pages", cut.Markup);
    }
}
