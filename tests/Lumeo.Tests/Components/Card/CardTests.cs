using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Card;

public class CardTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public CardTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    // --- Card ---

    [Fact]
    public void Card_Renders_Div_Element()
    {
        var cut = _ctx.Render<L.Card>(p => p.AddChildContent("Content"));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Card_Has_Default_Classes()
    {
        var cut = _ctx.Render<L.Card>(p => p.AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("rounded-lg", cls);
        Assert.Contains("border", cls);
        Assert.Contains("border-border", cls);
        Assert.Contains("bg-card", cls);
        Assert.Contains("text-card-foreground", cls);
    }

    [Fact]
    public void Card_Renders_Child_Content()
    {
        var cut = _ctx.Render<L.Card>(p => p.AddChildContent("Hello Card"));

        Assert.Equal("Hello Card", cut.Find("div").TextContent.Trim());
    }

    [Fact]
    public void Card_Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.Card>(p => p
            .Add(c => c.Class, "my-card-class")
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-card-class", cls);
        Assert.Contains("rounded-lg", cls);
    }

    [Fact]
    public void Card_Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.Card>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "my-card",
                ["aria-label"] = "Card section"
            })
            .AddChildContent("Content"));

        var div = cut.Find("div");
        Assert.Equal("my-card", div.GetAttribute("data-testid"));
        Assert.Equal("Card section", div.GetAttribute("aria-label"));
    }

    // --- CardHeader ---

    [Fact]
    public void CardHeader_Renders_Div_Element()
    {
        var cut = _ctx.Render<L.CardHeader>(p => p.AddChildContent("Header"));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void CardHeader_Has_Default_Classes()
    {
        var cut = _ctx.Render<L.CardHeader>(p => p.AddChildContent("Header"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("flex", cls);
        Assert.Contains("flex-col", cls);
        Assert.Contains("space-y-1.5", cls);
        Assert.Contains("p-6", cls);
    }

    [Fact]
    public void CardHeader_Renders_Child_Content()
    {
        var cut = _ctx.Render<L.CardHeader>(p => p.AddChildContent("My Header"));

        Assert.Equal("My Header", cut.Find("div").TextContent.Trim());
    }

    [Fact]
    public void CardHeader_Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.CardHeader>(p => p
            .Add(c => c.Class, "header-class")
            .AddChildContent("Header"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("header-class", cls);
        Assert.Contains("p-6", cls);
    }

    [Fact]
    public void CardHeader_Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.CardHeader>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "card-header"
            })
            .AddChildContent("Header"));

        Assert.Equal("card-header", cut.Find("div").GetAttribute("data-testid"));
    }

    // --- CardContent ---

    [Fact]
    public void CardContent_Renders_Div_Element()
    {
        var cut = _ctx.Render<L.CardContent>(p => p.AddChildContent("Body"));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void CardContent_Has_Default_Classes()
    {
        var cut = _ctx.Render<L.CardContent>(p => p.AddChildContent("Body"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("p-6", cls);
        Assert.Contains("pt-0", cls);
    }

    [Fact]
    public void CardContent_Renders_Child_Content()
    {
        var cut = _ctx.Render<L.CardContent>(p => p.AddChildContent("My Body"));

        Assert.Equal("My Body", cut.Find("div").TextContent.Trim());
    }

    [Fact]
    public void CardContent_Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.CardContent>(p => p
            .Add(c => c.Class, "content-class")
            .AddChildContent("Body"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("content-class", cls);
        Assert.Contains("p-6", cls);
    }

    [Fact]
    public void CardContent_Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.CardContent>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "card-content"
            })
            .AddChildContent("Body"));

        Assert.Equal("card-content", cut.Find("div").GetAttribute("data-testid"));
    }

    // --- CardFooter ---

    [Fact]
    public void CardFooter_Renders_Div_Element()
    {
        var cut = _ctx.Render<L.CardFooter>(p => p.AddChildContent("Footer"));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void CardFooter_Has_Default_Classes()
    {
        var cut = _ctx.Render<L.CardFooter>(p => p.AddChildContent("Footer"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("flex", cls);
        Assert.Contains("items-center", cls);
        Assert.Contains("p-6", cls);
        Assert.Contains("pt-0", cls);
    }

    [Fact]
    public void CardFooter_Renders_Child_Content()
    {
        var cut = _ctx.Render<L.CardFooter>(p => p.AddChildContent("My Footer"));

        Assert.Equal("My Footer", cut.Find("div").TextContent.Trim());
    }

    [Fact]
    public void CardFooter_Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.CardFooter>(p => p
            .Add(c => c.Class, "footer-class")
            .AddChildContent("Footer"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("footer-class", cls);
        Assert.Contains("flex", cls);
    }

    [Fact]
    public void CardFooter_Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.CardFooter>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "card-footer"
            })
            .AddChildContent("Footer"));

        Assert.Equal("card-footer", cut.Find("div").GetAttribute("data-testid"));
    }

    // --- Full card structure ---

    [Fact]
    public void Full_Card_Structure_Renders_Correctly()
    {
        var cut = _ctx.Render<L.Card>(p => p
            .AddChildContent(builder =>
            {
                builder.OpenComponent<L.CardHeader>(0);
                builder.AddAttribute(1, "ChildContent", (Microsoft.AspNetCore.Components.RenderFragment)(b =>
                {
                    b.AddContent(0, "Card Title");
                }));
                builder.CloseComponent();

                builder.OpenComponent<L.CardContent>(2);
                builder.AddAttribute(3, "ChildContent", (Microsoft.AspNetCore.Components.RenderFragment)(b =>
                {
                    b.AddContent(0, "Card body content");
                }));
                builder.CloseComponent();

                builder.OpenComponent<L.CardFooter>(4);
                builder.AddAttribute(5, "ChildContent", (Microsoft.AspNetCore.Components.RenderFragment)(b =>
                {
                    b.AddContent(0, "Card Footer");
                }));
                builder.CloseComponent();
            }));

        Assert.Contains("Card Title", cut.Markup);
        Assert.Contains("Card body content", cut.Markup);
        Assert.Contains("Card Footer", cut.Markup);
    }
}
