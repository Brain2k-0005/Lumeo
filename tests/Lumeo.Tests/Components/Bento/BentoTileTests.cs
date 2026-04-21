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
        var cut = _ctx.Render<Lumeo.BentoTile>();

        var style = cut.Find("div").GetAttribute("style");
        Assert.Contains("grid-column: span 1 / span 1", style);
        Assert.Contains("grid-row: span 1 / span 1", style);
    }

    [Fact]
    public void Span_Produces_Inline_Grid_Column_Style()
    {
        var cut = _ctx.Render<Lumeo.BentoTile>(p => p
            .Add(t => t.Span, 3));

        Assert.Contains("grid-column: span 3 / span 3", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void RowSpan_Produces_Inline_Grid_Row_Style()
    {
        var cut = _ctx.Render<Lumeo.BentoTile>(p => p
            .Add(t => t.RowSpan, 2));

        Assert.Contains("grid-row: span 2 / span 2", cut.Find("div").GetAttribute("style"));
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
