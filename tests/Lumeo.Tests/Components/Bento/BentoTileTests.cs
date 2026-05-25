using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.Bento;

public class BentoTileTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BentoTileTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Default_Span_And_RowSpan_Render_As_Span_1()
    {
        // Span/RowSpan moved off inline styles onto literal Tailwind classes
        // so the mobile breakpoint can override them. Default tile is a
        // single-cell tile in any grid.
        var cut = _ctx.Render<Lumeo.BentoTile>();

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("col-span-1", cls);
        Assert.Contains("row-span-1", cls);
    }

    [Fact]
    public void Span_Produces_Responsive_Col_Span_Classes()
    {
        // Mobile-first: every tile spans 1 on phones, widens to 2 at sm:, and
        // hits the caller's Span at lg: so wide tiles don't punch holes in
        // the mobile single-column stack.
        var cut = _ctx.Render<Lumeo.BentoTile>(p => p
            .Add(t => t.Span, 3));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("col-span-1", cls);
        Assert.Contains("sm:col-span-2", cls);
        Assert.Contains("lg:col-span-3", cls);
    }

    [Fact]
    public void RowSpan_Produces_Responsive_Row_Span_Classes()
    {
        // RowSpan only kicks in at lg: so mobile rows hug their content
        // height instead of inheriting a desktop multi-row tile.
        var cut = _ctx.Render<Lumeo.BentoTile>(p => p
            .Add(t => t.RowSpan, 2));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("row-span-1", cls);
        Assert.Contains("lg:row-span-2", cls);
    }

    [Fact]
    public void Title_Renders_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.BentoTile>(p => p
            .Add(t => t.Title, "My Title"));

        Assert.Contains("My Title", cut.Markup);
    }

    [Fact]
    public void Description_Renders_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.BentoTile>(p => p
            .Add(t => t.Description, "My description"));

        Assert.Contains("My description", cut.Markup);
    }

    [Fact]
    public void HeaderContent_Overrides_Title_And_Description()
    {
        var cut = _ctx.Render<Lumeo.BentoTile>(p => p
            .Add(t => t.Title, "Title")
            .Add(t => t.HeaderContent, (RenderFragment)(b => b.AddMarkupContent(0, "<h1>Custom</h1>"))));

        Assert.Contains("Custom", cut.Markup);
        // When HeaderContent is present, Title div is NOT rendered
        Assert.DoesNotContain(">Title<", cut.Markup);
    }

    [Fact]
    public void ChildContent_Renders()
    {
        var cut = _ctx.Render<Lumeo.BentoTile>(p => p
            .AddChildContent("<span data-testid='body'>body</span>"));

        Assert.NotNull(cut.Find("[data-testid='body']"));
    }

    [Fact]
    public void FooterContent_Renders_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.BentoTile>(p => p
            .Add(t => t.FooterContent, (RenderFragment)(b => b.AddMarkupContent(0, "<span data-testid='foot'>footer</span>"))));

        Assert.NotNull(cut.Find("[data-testid='foot']"));
    }

    [Fact]
    public void Root_Has_Card_Styling()
    {
        var cut = _ctx.Render<Lumeo.BentoTile>();

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("rounded-xl", cls);
        Assert.Contains("border", cls);
        Assert.Contains("bg-card", cls);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.BentoTile>(p => p
            .Add(t => t.Class, "custom-tile"));

        Assert.Contains("custom-tile", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Forward()
    {
        var cut = _ctx.Render<Lumeo.BentoTile>(p => p
            .Add(t => t.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "tile"
            }));

        Assert.Equal("tile", cut.Find("div").GetAttribute("data-testid"));
    }
}
