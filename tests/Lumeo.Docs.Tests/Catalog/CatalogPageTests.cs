using System.Net;
using System.Text;
using Bunit;
using Lumeo.Docs.Pages;
using Lumeo.Docs.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Docs.Tests.Catalog;

public class CatalogPageTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public CatalogPageTests()
    {
        _ctx.AddDocsServices();
        _ctx.Services.AddSingleton(new HttpClient(new InMemoryRegistryHandler())
        {
            BaseAddress = new Uri("https://test/")
        });
        _ctx.Services.AddSingleton<Lumeo.Docs.Services.RegistryService>();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_one_section_per_category()
    {
        var cut = _ctx.Render<Lumeo.Docs.Pages.Catalog>();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("id=\"forms\"", cut.Markup);
            Assert.Contains("id=\"data-display\"", cut.Markup);
        });
    }

    [Fact]
    public void Renders_a_card_per_component()
    {
        var cut = _ctx.Render<Lumeo.Docs.Pages.Catalog>();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Input", cut.Markup);
            Assert.Contains("Select", cut.Markup);
            Assert.Contains("Table", cut.Markup);
        });
    }

    private sealed class InMemoryRegistryHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            const string json = """
            {
              "components": {
                "input":  { "name": "Input",  "category": "Forms",        "subcategory": "Inputs",   "description": "Text input.",  "thumbnail": "/preview-cards/input.png",  "nugetPackage": "Lumeo" },
                "select": { "name": "Select", "category": "Forms",        "subcategory": "Selection","description": "Picker.",      "thumbnail": "/preview-cards/select.png", "nugetPackage": "Lumeo" },
                "table":  { "name": "Table",  "category": "Data Display", "subcategory": "Tables",   "description": "Tabular data.","thumbnail": "/preview-cards/table.png",  "nugetPackage": "Lumeo" }
              }
            }
            """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
