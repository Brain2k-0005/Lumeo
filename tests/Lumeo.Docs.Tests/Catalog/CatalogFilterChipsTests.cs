using Bunit;
using Lumeo.Docs.Shared;
using Lumeo.Docs.Tests.Helpers;
using Xunit;

namespace Lumeo.Docs.Tests.Catalog;

public class CatalogFilterChipsTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public CatalogFilterChipsTests() => _ctx.AddDocsServices();
    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_All_chip_plus_one_per_category()
    {
        var cats = new[] { "Forms", "Layout", "Data Display" };
        string? selected = null;
        var cut = _ctx.Render<CatalogFilterChips>(p => p
            .Add(c => c.Categories, cats)
            .Add(c => c.Selected, selected)
            .Add(c => c.SelectedChanged, (string? s) => selected = s));

        Assert.Contains("All", cut.Markup);
        Assert.Contains("Forms", cut.Markup);
        Assert.Contains("Layout", cut.Markup);
        Assert.Contains("Data Display", cut.Markup);
    }

    [Fact]
    public void Clicking_chip_invokes_callback_with_category()
    {
        var cats = new[] { "Forms", "Layout" };
        string? selected = null;
        var cut = _ctx.Render<CatalogFilterChips>(p => p
            .Add(c => c.Categories, cats)
            .Add(c => c.Selected, selected)
            .Add(c => c.SelectedChanged, (string? s) => selected = s));

        cut.Find("[data-chip='Forms']").Click();
        Assert.Equal("Forms", selected);
    }
}
