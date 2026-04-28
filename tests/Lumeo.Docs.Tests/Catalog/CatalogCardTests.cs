using Bunit;
using Lumeo.Docs.Services;
using Lumeo.Docs.Shared;
using Lumeo.Docs.Tests.Helpers;
using Xunit;

namespace Lumeo.Docs.Tests.Catalog;

public class CatalogCardTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public CatalogCardTests() => _ctx.AddDocsServices();
    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_name_description_and_thumbnail()
    {
        var component = new RegistryComponent
        {
            Name = "Input",
            Category = "Forms",
            Description = "Captures single-line user text input.",
            Thumbnail = "/preview-cards/input.png",
            NugetPackage = "Lumeo",
            HasDocsPage = true,
            Slug = "input",
        };
        var cut = _ctx.Render<CatalogCard>(p => p.Add(c => c.Component, component));

        Assert.Contains("Input", cut.Markup);
        Assert.Contains("Captures single-line user text input.", cut.Markup);
        Assert.Contains("/preview-cards/input.png", cut.Markup);
        Assert.Contains("href=\"components/input\"", cut.Markup);
    }

    [Fact]
    public void Falls_back_to_icon_when_thumbnail_missing()
    {
        var component = new RegistryComponent
        {
            Name = "MysteryComponent",
            Category = "Forms",
            Description = "No thumbnail yet.",
            Thumbnail = null,
            NugetPackage = "Lumeo",
            HasDocsPage = true,
            Slug = "mystery-component",
        };
        var cut = _ctx.Render<CatalogCard>(p => p.Add(c => c.Component, component));

        Assert.DoesNotContain("<img", cut.Markup);
        Assert.Contains("MysteryComponent", cut.Markup);
        Assert.Contains("catalog-card-icon-fallback", cut.Markup);
    }

    [Fact]
    public void Slugifies_compound_name_for_href()
    {
        var component = new RegistryComponent
        {
            Name = "DatePicker",
            Category = "Forms",
            Description = "Picks a date.",
            Thumbnail = "/preview-cards/date-picker.png",
            NugetPackage = "Lumeo",
            HasDocsPage = true,
            Slug = "date-picker",
        };
        var cut = _ctx.Render<CatalogCard>(p => p.Add(c => c.Component, component));
        Assert.Contains("href=\"components/date-picker\"", cut.Markup);
    }

    [Fact]
    public void Uses_registry_slug_for_compound_acronym_names()
    {
        var component = new RegistryComponent
        {
            Name = "QRCode",
            Category = "Data Display",
            Description = "Renders QR codes.",
            Thumbnail = "/preview-cards/qr-code.png",
            NugetPackage = "Lumeo",
            HasDocsPage = true,
            Slug = "qr-code", // populated by RegistryService in production
        };
        var cut = _ctx.Render<CatalogCard>(p => p.Add(c => c.Component, component));
        Assert.Contains("href=\"components/qr-code\"", cut.Markup);
    }

    [Fact]
    public void Undocumented_component_renders_non_link_with_coming_soon_badge()
    {
        var component = new RegistryComponent
        {
            Name = "BorderBeam",
            Category = "Motion",
            Description = "Animated gradient border beam.",
            Thumbnail = "/preview-cards/border-beam.png",
            NugetPackage = "Lumeo",
            HasDocsPage = false,
            Slug = "border-beam",
        };
        var cut = _ctx.Render<CatalogCard>(p => p.Add(c => c.Component, component));

        // No anchor tag — card is not navigable
        Assert.DoesNotContain("href=\"components/border-beam\"", cut.Markup);
        // Thumbnail still shown so the user sees the visual
        Assert.Contains("/preview-cards/border-beam.png", cut.Markup);
        // "Coming soon" badge is visible
        Assert.Contains("Docs coming soon", cut.Markup);
    }

    [Fact]
    public void Renders_nuget_package_badge_for_satellite_components()
    {
        var component = new RegistryComponent
        {
            Name = "MagicCard",
            Category = "Motion",
            Description = "Card with cursor spotlight and 3D tilt.",
            Thumbnail = "/preview-cards/magic-card.png",
            NugetPackage = "Lumeo.Motion",
            HasDocsPage = true,
            Slug = "magic-card",
        };
        var cut = _ctx.Render<CatalogCard>(p => p.Add(c => c.Component, component));

        // The NuGet badge should be visible for satellite packages
        Assert.Contains("Lumeo.Motion", cut.Markup);
        // The card should still be navigable (HasDocsPage = true)
        Assert.Contains("href=\"components/magic-card\"", cut.Markup);
        // "Docs coming soon" must NOT appear — that badge is only for undocumented cards
        Assert.DoesNotContain("Docs coming soon", cut.Markup);
    }

    [Fact]
    public void Does_not_render_badge_for_core_lumeo_package()
    {
        var component = new RegistryComponent
        {
            Name = "Button",
            Category = "Forms",
            Description = "Core button component.",
            Thumbnail = "/preview-cards/button.png",
            NugetPackage = "Lumeo",
            HasDocsPage = true,
            Slug = "button",
        };
        var cut = _ctx.Render<CatalogCard>(p => p.Add(c => c.Component, component));

        // "Lumeo" badge text must NOT appear in any badge span
        // (The package name "Lumeo" appears in many places, so we check for the badge span specifically)
        Assert.DoesNotContain("Docs coming soon", cut.Markup);
        // The card should be a link since it has a docs page
        Assert.Contains("href=\"components/button\"", cut.Markup);
        // No satellite badge — "Lumeo" package = no badge
        // We check the markup doesn't contain a badge span with exactly "Lumeo" content
        var spans = cut.FindAll("span.absolute");
        Assert.Empty(spans); // no badge spans at all for core components with docs
    }
}
