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
            Slug = "qr-code", // populated by RegistryService in production
        };
        var cut = _ctx.Render<CatalogCard>(p => p.Add(c => c.Component, component));
        Assert.Contains("href=\"components/qr-code\"", cut.Markup);
    }
}
