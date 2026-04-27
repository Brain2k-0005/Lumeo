using Bunit;
using Lumeo.Docs.Shared;
using Lumeo.Docs.Tests.Helpers;
using Xunit;

namespace Lumeo.Docs.Tests.Catalog;

public class CatalogRailTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public CatalogRailTests() => _ctx.AddDocsServices();
    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_one_anchor_per_category_with_count()
    {
        var categories = new Dictionary<string, int>
        {
            ["Forms"] = 34,
            ["Layout"] = 11,
            ["Data Display"] = 28,
        };
        var cut = _ctx.Render<CatalogRail>(p => p.Add(c => c.Categories, categories));

        Assert.Contains("href=\"#forms\"", cut.Markup);
        Assert.Contains("href=\"#layout\"", cut.Markup);
        Assert.Contains("href=\"#data-display\"", cut.Markup);
        Assert.Contains("Forms", cut.Markup);
        Assert.Contains("34", cut.Markup);
        Assert.Contains("Data Display", cut.Markup);
        Assert.Contains("28", cut.Markup);
    }

    [Fact]
    public void Slugifies_multiword_category_for_anchor()
    {
        var categories = new Dictionary<string, int> { ["Data Display"] = 28 };
        var cut = _ctx.Render<CatalogRail>(p => p.Add(c => c.Categories, categories));
        Assert.Contains("href=\"#data-display\"", cut.Markup);
    }
}
