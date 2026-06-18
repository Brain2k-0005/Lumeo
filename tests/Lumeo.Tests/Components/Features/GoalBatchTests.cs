using System;
using System.Linq;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Features;

/// <summary>
/// Goal batch:
///   #210 Pagination — data-driven pager mode (Page/TotalPages, ellipsis, prev/next).
///   #269 Button — Href renders a link styled as a button.
///   #295 Heading — As renders the heading styling on a chosen element.
/// </summary>
public class GoalBatchTests
{
    private static BunitContext NewCtx()
    {
        var ctx = new BunitContext();
        ctx.AddLumeoServices();
        return ctx;
    }

    [Fact]
    public void Pagination_data_driven_renders_pages_with_active_and_ellipsis()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Pagination>(p => p
            .Add(x => x.Page, 5)
            .Add(x => x.TotalPages, 10));

        string[] pageTexts = cut.FindAll("button")
            .Select(b => b.TextContent.Trim())
            .Where(t => t.Length > 0)
            .ToArray();

        // boundary=1, sibling=1 around page 5 -> 1, 4, 5, 6, 10
        Assert.Contains("1", pageTexts);
        Assert.Contains("4", pageTexts);
        Assert.Contains("5", pageTexts);
        Assert.Contains("6", pageTexts);
        Assert.Contains("10", pageTexts);
        Assert.DoesNotContain("2", pageTexts); // collapsed into an ellipsis

        // current page is marked
        Assert.Equal("5", cut.Find("[aria-current='page']").TextContent.Trim());
    }

    [Fact]
    public void Pagination_computes_total_from_items_and_fires_page_changed()
    {
        using var ctx = NewCtx();
        int? changed = null;
        var cut = ctx.Render<Lumeo.Pagination>(p => p
            .Add(x => x.Page, 1)
            .Add(x => x.TotalItems, 95)   // /10 => 10 pages
            .Add(x => x.PageSize, 10)
            .Add(x => x.PageChanged, EventCallback.Factory.Create<int>(this, v => changed = v)));

        var pageTen = cut.FindAll("button").First(b => b.TextContent.Trim() == "10");
        pageTen.Click();
        Assert.Equal(10, changed);
    }

    [Fact]
    public void Button_with_href_renders_anchor_styled_as_button()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Button>(p => p
            .Add(x => x.Href, "/go")
            .AddChildContent("Link"));

        var a = cut.Find("a");
        Assert.Equal("/go", a.GetAttribute("href"));
        Assert.Contains("bg-primary", a.GetAttribute("class")); // default variant styling
        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public void Button_without_href_stays_a_button()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Button>(p => p.AddChildContent("Click"));
        Assert.NotNull(cut.Find("button"));
        Assert.Empty(cut.FindAll("a"));
    }

    [Fact]
    public void Button_disabled_href_is_aria_disabled_and_inert()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Button>(p => p
            .Add(x => x.Href, "/go")
            .Add(x => x.Disabled, true)
            .AddChildContent("Link"));

        var a = cut.Find("a");
        Assert.Equal("true", a.GetAttribute("aria-disabled"));
        Assert.Contains("pointer-events-none", a.GetAttribute("class"));
    }

    [Fact]
    public void Heading_as_renders_chosen_element_with_heading_styles()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Heading>(p => p
            .Add(x => x.As, "div")
            .Add(x => x.Level, 2)
            .AddChildContent("Title"));

        var div = cut.Find("div");
        Assert.Contains("text-3xl", div.GetAttribute("class")); // Level 2 size
        Assert.Empty(cut.FindAll("h2"));
    }

    [Fact]
    public void Heading_default_renders_semantic_heading()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Heading>(p => p.Add(x => x.Level, 2).AddChildContent("Title"));
        Assert.NotNull(cut.Find("h2"));
    }
}
