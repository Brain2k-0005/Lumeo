using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Lumeo.Docs.Services;
using Lumeo.Docs.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Docs.Tests.Services;

public class NavConfigServiceTests
{
    [Fact]
    public async Task LoadsAndCachesGroups()
    {
        var json = """
        {
          "groups": [
            { "id": "form", "label": "Form", "section": "components", "subgroups": [
              { "label": "Inputs", "items": [ { "label": "Input", "href": "components/input" } ] }
            ]}
          ]
        }
        """;
        var http = new HttpClient(new StubHandler(json)) { BaseAddress = new Uri("https://test/") };
        var svc = new NavConfigService(http);

        var config1 = await svc.GetAsync();
        var config2 = await svc.GetAsync();

        Assert.Same(config1, config2);
        Assert.Single(config1.Groups);
        Assert.Equal("Form", config1.Groups[0].Label);
        Assert.Single(config1.Groups[0].Subgroups!);
        Assert.Equal("Input", config1.Groups[0].Subgroups![0].Items[0].Label);
    }

    [Fact]
    public async Task GetForSectionAsync_filters_to_matching_section()
    {
        var json = """
        {
          "groups": [
            { "id": "getting-started", "label": "Getting Started", "section": "docs", "items": [ { "label": "Introduction", "href": "docs/introduction" } ] },
            { "id": "form", "label": "Form", "section": "components", "items": [ { "label": "Button", "href": "components/button" } ] }
          ]
        }
        """;
        var http = new HttpClient(new StubHandler(json)) { BaseAddress = new Uri("https://test/") };
        var svc = new NavConfigService(http);

        var docsConfig = await svc.GetForSectionAsync("docs");
        var compConfig = await svc.GetForSectionAsync("components");

        Assert.Single(docsConfig.Groups);
        Assert.Equal("Getting Started", docsConfig.Groups[0].Label);

        Assert.Single(compConfig.Groups);
        Assert.Equal("Form", compConfig.Groups[0].Label);
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }
}
