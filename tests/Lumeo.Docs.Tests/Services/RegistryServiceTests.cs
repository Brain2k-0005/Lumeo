using System.Net;
using System.Text;
using Bunit;
using Lumeo.Docs.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Docs.Tests.Services;

public class RegistryServiceTests
{
    [Fact]
    public async Task GroupsByCategory_returns_grouped_components()
    {
        var json = """
        {
          "components": {
            "input":   { "name": "Input",   "category": "Forms",        "subcategory": "Inputs",   "description": "An input.",   "thumbnail": "/preview-cards/input.png",   "nugetPackage": "Lumeo", "hasDocsPage": true },
            "select":  { "name": "Select",  "category": "Forms",        "subcategory": "Selection","description": "A select.",   "thumbnail": "/preview-cards/select.png",  "nugetPackage": "Lumeo", "hasDocsPage": true },
            "table":   { "name": "Table",   "category": "Data Display", "subcategory": "Tables",   "description": "A table.",    "thumbnail": "/preview-cards/table.png",   "nugetPackage": "Lumeo", "hasDocsPage": true }
          }
        }
        """;
        var http = new HttpClient(new StubHandler(json)) { BaseAddress = new Uri("https://test/") };
        // Non-in-process JS runtime: RegistryService skips the inline-registry
        // path and exercises the HTTP fetch path under test.
        var svc = new RegistryService(http, new StubJsRuntime());

        var groups = await svc.GroupsByCategoryAsync();

        Assert.Equal(2, groups.Count);
        Assert.Equal(2, groups["Forms"].Count);
        Assert.Single(groups["Data Display"]);
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }

    // Minimal IJSRuntime that is NOT IJSInProcessRuntime, so RegistryService's
    // synchronous inline-registry read is skipped and the fetch path is tested.
    private sealed class StubJsRuntime : Microsoft.JSInterop.IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => default;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => default;
    }
}
