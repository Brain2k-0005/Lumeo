using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.EmptyState;

public class EmptyStateTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public EmptyStateTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_Root_Div_With_Base_Classes()
    {
        var cut = _ctx.Render<Lumeo.EmptyState>();

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("flex", cls);
        Assert.Contains("flex-col", cls);
        Assert.Contains("items-center", cls);
        Assert.Contains("justify-center", cls);
        Assert.Contains("py-12", cls);
        Assert.Contains("text-center", cls);
    }

    [Fact]
    public void Renders_Title_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.EmptyState>(p => p
            .Add(e => e.Title, "No results found"));

        var h3 = cut.Find("h3");
        Assert.Equal("No results found", h3.TextContent.Trim());
        Assert.Contains("text-lg", h3.GetAttribute("class"));
        Assert.Contains("font-semibold", h3.GetAttribute("class"));
    }

    [Fact]
    public void Does_Not_Render_Title_When_Not_Provided()
    {
        var cut = _ctx.Render<Lumeo.EmptyState>();

        Assert.Empty(cut.FindAll("h3"));
    }

    [Fact]
    public void Renders_Description_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.EmptyState>(p => p
            .Add(e => e.Description, "Try adjusting your search."));

        var p = cut.Find("p");
        Assert.Equal("Try adjusting your search.", p.TextContent.Trim());
        Assert.Contains("text-sm", p.GetAttribute("class"));
        Assert.Contains("text-muted-foreground", p.GetAttribute("class"));
    }

    [Fact]
    public void Does_Not_Render_Description_When_Not_Provided()
    {
        var cut = _ctx.Render<Lumeo.EmptyState>();

        Assert.Empty(cut.FindAll("p"));
    }

    [Fact]
    public void Renders_Icon_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.EmptyState>(p => p
            .Add(e => e.Icon, builder =>
            {
                builder.OpenElement(0, "svg");
                builder.AddAttribute(1, "data-testid", "icon");
                builder.CloseElement();
            }));

        Assert.NotNull(cut.Find("[data-testid='icon']"));
    }

    [Fact]
    public void Icon_Wrapper_Has_Muted_Class()
    {
        var cut = _ctx.Render<Lumeo.EmptyState>(p => p
            .Add(e => e.Icon, builder =>
            {
                builder.OpenElement(0, "span");
                builder.AddContent(1, "icon");
                builder.CloseElement();
            }));

        var iconWrapper = cut.Find("div > div");
        Assert.Contains("text-muted-foreground", iconWrapper.GetAttribute("class"));
        Assert.Contains("mb-4", iconWrapper.GetAttribute("class"));
    }

    [Fact]
    public void Does_Not_Render_Icon_When_Not_Provided()
    {
        var cut = _ctx.Render<Lumeo.EmptyState>(p => p
            .Add(e => e.Title, "Title only"));

        // Only one div (the root) should be present — no icon wrapper
        Assert.Single(cut.FindAll("div"));
    }

    [Fact]
    public void Renders_Action_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.EmptyState>(p => p
            .Add(e => e.Action, builder =>
            {
                builder.OpenElement(0, "button");
                builder.AddContent(1, "Try again");
                builder.CloseElement();
            }));

        Assert.NotNull(cut.Find("button"));
        Assert.Equal("Try again", cut.Find("button").TextContent);
    }

    [Fact]
    public void Action_Wrapper_Has_Margin_Top()
    {
        var cut = _ctx.Render<Lumeo.EmptyState>(p => p
            .Add(e => e.Action, builder =>
            {
                builder.OpenElement(0, "button");
                builder.AddContent(1, "Action");
                builder.CloseElement();
            }));

        var actionWrapper = cut.Find("div > div");
        Assert.Contains("mt-4", actionWrapper.GetAttribute("class"));
    }

    [Fact]
    public void Does_Not_Render_Action_When_Not_Provided()
    {
        var cut = _ctx.Render<Lumeo.EmptyState>(p => p
            .Add(e => e.Title, "No action"));

        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public void Renders_All_Sections_Together()
    {
        var cut = _ctx.Render<Lumeo.EmptyState>(p => p
            .Add(e => e.Title, "Nothing here")
            .Add(e => e.Description, "No items found.")
            .Add(e => e.Icon, builder =>
            {
                builder.OpenElement(0, "span");
                builder.AddContent(1, "icon");
                builder.CloseElement();
            })
            .Add(e => e.Action, builder =>
            {
                builder.OpenElement(0, "button");
                builder.AddContent(1, "Refresh");
                builder.CloseElement();
            }));

        Assert.NotNull(cut.Find("h3"));
        Assert.NotNull(cut.Find("p"));
        Assert.NotNull(cut.Find("button"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.EmptyState>(p => p
            .Add(e => e.Class, "my-empty-class"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-empty-class", cls);
        Assert.Contains("flex", cls);
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<Lumeo.EmptyState>(p => p
            .Add(e => e.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "empty-state"
            }));

        Assert.Equal("empty-state", cut.Find("div").GetAttribute("data-testid"));
    }
}
