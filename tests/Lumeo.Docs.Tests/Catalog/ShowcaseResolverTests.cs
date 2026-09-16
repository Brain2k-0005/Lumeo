using Lumeo.Docs.Services;
using Lumeo.Docs.Shared.Showcases;
using Xunit;

namespace Lumeo.Docs.Tests.Catalog;

public class ShowcaseResolverTests
{
    [Fact]
    public void Resolves_a_registered_showcase_by_convention()
    {
        var type = ShowcaseResolver.Resolve("Accordion");

        Assert.NotNull(type);
        Assert.Equal(typeof(AccordionShowcase), type);
    }

    [Fact]
    public void Returns_null_for_a_component_with_no_showcase_yet()
    {
        // Fictional name — no "PlainFieldShowcase.razor" exists or ever will, so this
        // stays true regardless of which real components later waves cover.
        Assert.Null(ShowcaseResolver.Resolve("PlainField"));
    }

    [Fact]
    public void Returns_null_for_an_unknown_or_empty_name()
    {
        Assert.Null(ShowcaseResolver.Resolve("NotARealComponent"));
        Assert.Null(ShowcaseResolver.Resolve(""));
        Assert.Null(ShowcaseResolver.Resolve(null!));
    }

    [Fact]
    public void Caches_the_resolution_and_returns_the_identical_type_on_repeat_lookups()
    {
        var first = ShowcaseResolver.Resolve("Tabs");
        var second = ShowcaseResolver.Resolve("Tabs");

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void All_returns_every_discovered_showcase_type_sorted_and_deduplicated()
    {
        var all = ShowcaseResolver.All();

        Assert.True(all.Count >= 19, $"expected at least the 19 wave-0 Navigation showcases, got {all.Count}");
        Assert.Equal(all.Distinct().Count(), all.Count);
        Assert.Equal(all.OrderBy(t => t.Name, StringComparer.Ordinal), all);
        Assert.Contains(typeof(AccordionShowcase), all);
        Assert.All(all, t => Assert.EndsWith("Showcase", t.Name, StringComparison.Ordinal));
    }
}
