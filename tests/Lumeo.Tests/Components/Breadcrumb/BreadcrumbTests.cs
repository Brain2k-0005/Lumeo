using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Breadcrumb;

public class BreadcrumbTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public BreadcrumbTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    // Breadcrumb (nav) tests

    [Fact]
    public void Breadcrumb_Renders_Nav_Element()
    {
        var cut = _ctx.Render<L.Breadcrumb>();

        Assert.NotNull(cut.Find("nav"));
    }

    [Fact]
    public void Breadcrumb_Has_Aria_Label()
    {
        var cut = _ctx.Render<L.Breadcrumb>();

        Assert.Equal("breadcrumb", cut.Find("nav").GetAttribute("aria-label"));
    }

    [Fact]
    public void Breadcrumb_Renders_Child_Content()
    {
        var cut = _ctx.Render<L.Breadcrumb>(p => p
            .AddChildContent("<ol><li>Home</li></ol>"));

        Assert.Contains("Home", cut.Markup);
    }

    [Fact]
    public void Breadcrumb_Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.Breadcrumb>(p => p
            .Add(b => b.Class, "my-breadcrumb-class"));

        var cls = cut.Find("nav").GetAttribute("class");
        Assert.Contains("my-breadcrumb-class", cls);
    }

    [Fact]
    public void Breadcrumb_Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.Breadcrumb>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "breadcrumb-nav"
            }));

        Assert.Equal("breadcrumb-nav", cut.Find("nav").GetAttribute("data-testid"));
    }

    // BreadcrumbList tests

    [Fact]
    public void BreadcrumbList_Renders_Ol_Element()
    {
        var cut = _ctx.Render<L.BreadcrumbList>();

        Assert.NotNull(cut.Find("ol"));
    }

    [Fact]
    public void BreadcrumbList_Has_Base_Classes()
    {
        var cut = _ctx.Render<L.BreadcrumbList>();

        var cls = cut.Find("ol").GetAttribute("class");
        Assert.Contains("flex", cls);
        Assert.Contains("flex-wrap", cls);
        Assert.Contains("items-center", cls);
        Assert.Contains("text-sm", cls);
        Assert.Contains("text-muted-foreground", cls);
    }

    [Fact]
    public void BreadcrumbList_Renders_Child_Content()
    {
        var cut = _ctx.Render<L.BreadcrumbList>(p => p
            .AddChildContent("<li>Item</li>"));

        Assert.Contains("Item", cut.Markup);
    }

    [Fact]
    public void BreadcrumbList_Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.BreadcrumbList>(p => p
            .Add(b => b.Class, "my-list-class"));

        var cls = cut.Find("ol").GetAttribute("class");
        Assert.Contains("my-list-class", cls);
        Assert.Contains("flex", cls);
    }

    [Fact]
    public void BreadcrumbList_Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.BreadcrumbList>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "breadcrumb-list"
            }));

        Assert.Equal("breadcrumb-list", cut.Find("ol").GetAttribute("data-testid"));
    }

    // BreadcrumbItem tests

    [Fact]
    public void BreadcrumbItem_Renders_Li_Element()
    {
        var cut = _ctx.Render<L.BreadcrumbItem>(p => p
            .AddChildContent("Home"));

        Assert.NotNull(cut.Find("li"));
    }

    [Fact]
    public void BreadcrumbItem_Has_Base_Classes()
    {
        var cut = _ctx.Render<L.BreadcrumbItem>(p => p
            .AddChildContent("Home"));

        var cls = cut.Find("li").GetAttribute("class");
        Assert.Contains("inline-flex", cls);
        Assert.Contains("items-center", cls);
        Assert.Contains("gap-1.5", cls);
    }

    [Fact]
    public void BreadcrumbItem_Renders_Child_Content()
    {
        var cut = _ctx.Render<L.BreadcrumbItem>(p => p
            .AddChildContent("Home"));

        Assert.Contains("Home", cut.Find("li").TextContent);
    }

    [Fact]
    public void BreadcrumbItem_Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.BreadcrumbItem>(p => p
            .Add(b => b.Class, "my-item-class")
            .AddChildContent("Home"));

        var cls = cut.Find("li").GetAttribute("class");
        Assert.Contains("my-item-class", cls);
        Assert.Contains("inline-flex", cls);
    }

    [Fact]
    public void BreadcrumbItem_Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.BreadcrumbItem>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "breadcrumb-item"
            })
            .AddChildContent("Home"));

        Assert.Equal("breadcrumb-item", cut.Find("li").GetAttribute("data-testid"));
    }

    // BreadcrumbLink tests

    [Fact]
    public void BreadcrumbLink_Renders_Anchor_Element()
    {
        var cut = _ctx.Render<L.BreadcrumbLink>(p => p
            .AddChildContent("Home"));

        Assert.NotNull(cut.Find("a"));
    }

    [Fact]
    public void BreadcrumbLink_Has_Default_Href()
    {
        var cut = _ctx.Render<L.BreadcrumbLink>(p => p
            .AddChildContent("Home"));

        Assert.Equal("#", cut.Find("a").GetAttribute("href"));
    }

    [Fact]
    public void BreadcrumbLink_Sets_Custom_Href()
    {
        var cut = _ctx.Render<L.BreadcrumbLink>(p => p
            .Add(b => b.Href, "/home")
            .AddChildContent("Home"));

        Assert.Equal("/home", cut.Find("a").GetAttribute("href"));
    }

    [Fact]
    public void BreadcrumbLink_Has_Base_Classes()
    {
        var cut = _ctx.Render<L.BreadcrumbLink>(p => p
            .AddChildContent("Home"));

        var cls = cut.Find("a").GetAttribute("class");
        Assert.Contains("transition-colors", cls);
        Assert.Contains("hover:text-foreground", cls);
    }

    [Fact]
    public void BreadcrumbLink_Renders_Child_Content()
    {
        var cut = _ctx.Render<L.BreadcrumbLink>(p => p
            .AddChildContent("Home"));

        Assert.Contains("Home", cut.Find("a").TextContent);
    }

    [Fact]
    public void BreadcrumbLink_Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.BreadcrumbLink>(p => p
            .Add(b => b.Class, "my-link-class")
            .AddChildContent("Home"));

        var cls = cut.Find("a").GetAttribute("class");
        Assert.Contains("my-link-class", cls);
        Assert.Contains("transition-colors", cls);
    }

    [Fact]
    public void BreadcrumbLink_Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.BreadcrumbLink>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "breadcrumb-link"
            })
            .AddChildContent("Home"));

        Assert.Equal("breadcrumb-link", cut.Find("a").GetAttribute("data-testid"));
    }

    // BreadcrumbPage tests

    [Fact]
    public void BreadcrumbPage_Renders_Span_Element()
    {
        var cut = _ctx.Render<L.BreadcrumbPage>(p => p
            .AddChildContent("Current Page"));

        Assert.NotNull(cut.Find("span"));
    }

    [Fact]
    public void BreadcrumbPage_Has_Aria_Attributes()
    {
        var cut = _ctx.Render<L.BreadcrumbPage>(p => p
            .AddChildContent("Current Page"));

        var span = cut.Find("span");
        Assert.Equal("link", span.GetAttribute("role"));
        Assert.Equal("true", span.GetAttribute("aria-disabled"));
        Assert.Equal("page", span.GetAttribute("aria-current"));
    }

    [Fact]
    public void BreadcrumbPage_Has_Base_Classes()
    {
        var cut = _ctx.Render<L.BreadcrumbPage>(p => p
            .AddChildContent("Current Page"));

        var cls = cut.Find("span").GetAttribute("class");
        Assert.Contains("font-normal", cls);
        Assert.Contains("text-foreground", cls);
    }

    [Fact]
    public void BreadcrumbPage_Renders_Child_Content()
    {
        var cut = _ctx.Render<L.BreadcrumbPage>(p => p
            .AddChildContent("Current Page"));

        Assert.Contains("Current Page", cut.Find("span").TextContent);
    }

    [Fact]
    public void BreadcrumbPage_Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.BreadcrumbPage>(p => p
            .Add(b => b.Class, "my-page-class")
            .AddChildContent("Current Page"));

        var cls = cut.Find("span").GetAttribute("class");
        Assert.Contains("my-page-class", cls);
        Assert.Contains("font-normal", cls);
    }

    [Fact]
    public void BreadcrumbPage_Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.BreadcrumbPage>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "breadcrumb-page"
            })
            .AddChildContent("Current Page"));

        Assert.Equal("breadcrumb-page", cut.Find("span").GetAttribute("data-testid"));
    }

    // BreadcrumbSeparator tests

    [Fact]
    public void BreadcrumbSeparator_Renders_Li_Element()
    {
        var cut = _ctx.Render<L.BreadcrumbSeparator>();

        Assert.NotNull(cut.Find("li"));
    }

    [Fact]
    public void BreadcrumbSeparator_Has_Presentation_Role()
    {
        var cut = _ctx.Render<L.BreadcrumbSeparator>();

        var li = cut.Find("li");
        Assert.Equal("presentation", li.GetAttribute("role"));
        Assert.Equal("true", li.GetAttribute("aria-hidden"));
    }

    [Fact]
    public void BreadcrumbSeparator_Renders_Default_Icon_When_No_Child_Content()
    {
        var cut = _ctx.Render<L.BreadcrumbSeparator>();

        // Should render an svg icon (Blazicon renders an svg)
        Assert.NotNull(cut.Find("svg"));
    }

    [Fact]
    public void BreadcrumbSeparator_Renders_Custom_Child_Content()
    {
        var cut = _ctx.Render<L.BreadcrumbSeparator>(p => p
            .AddChildContent("<span>/</span>"));

        Assert.Contains("/", cut.Find("li").TextContent);
    }

    [Fact]
    public void BreadcrumbSeparator_Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.BreadcrumbSeparator>(p => p
            .Add(b => b.Class, "my-separator-class"));

        var cls = cut.Find("li").GetAttribute("class");
        Assert.Contains("my-separator-class", cls);
    }

    [Fact]
    public void BreadcrumbSeparator_Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.BreadcrumbSeparator>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "separator"
            }));

        Assert.Equal("separator", cut.Find("li").GetAttribute("data-testid"));
    }

    // Full breadcrumb structure integration test

    [Fact]
    public void Full_Breadcrumb_Structure_Renders_Correctly()
    {
        var cut = _ctx.Render<L.Breadcrumb>(p => p
            .AddChildContent<L.BreadcrumbList>(listParams => listParams
                .AddChildContent<L.BreadcrumbItem>(item1Params => item1Params
                    .AddChildContent<L.BreadcrumbLink>(linkParams => linkParams
                        .Add(l => l.Href, "/")
                        .AddChildContent("Home")))
                .AddChildContent<L.BreadcrumbSeparator>()
                .AddChildContent<L.BreadcrumbItem>(item2Params => item2Params
                    .AddChildContent<L.BreadcrumbPage>(pageParams => pageParams
                        .AddChildContent("Current")))));

        Assert.NotNull(cut.Find("nav[aria-label='breadcrumb']"));
        Assert.NotNull(cut.Find("ol"));
        Assert.Equal(2, cut.FindAll("li:not([role='presentation'])").Count);
        Assert.NotNull(cut.Find("a[href='/']"));
        Assert.NotNull(cut.Find("span[aria-current='page']"));
    }
}
