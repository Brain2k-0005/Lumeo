using System.Net;
using System.Text;
using Bunit;
using Lumeo;
using Lumeo.Docs.Services;
using Lumeo.Docs.Shared;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Docs.Tests;

/// <summary>
/// Codex P2 — PropsTable rendered ALL api.parameters, including [CascadingParameter]s
/// (FormField, InheritedDensity, a parent Context/Shell, ...). Those are supplied by an
/// ancestor component, not set by the consumer, so listing them as a settable prop
/// advertised invalid/internal markup. The table must show only consumer-facing
/// (non-cascading) parameters.
/// </summary>
public class PropsTableCascadingFilterTests
{
    private sealed class FixedJsonHandler : HttpMessageHandler
    {
        private readonly string _json;
        public FixedJsonHandler(string json) => _json = json;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
    }

    // One consumer-facing param ("Class") and one cascading param ("Context") — mirrors the
    // real shape (e.g. DialogContent's Context/Shell cascading params alongside real ones).
    private const string FixtureJson = """
        {
          "name": "Test",
          "category": "Forms",
          "description": "Test component",
          "nugetPackage": "Lumeo",
          "api": {
            "parameters": [
              { "name": "Class", "type": "string?", "default": null, "description": "CSS class.", "isCascading": false, "captureUnmatched": false, "isEditorRequired": false },
              { "name": "Context", "type": "SomeContext", "default": "default!", "description": null, "isCascading": true, "captureUnmatched": false, "isEditorRequired": false }
            ],
            "events": []
          }
        }
        """;

    private static BunitContext RenderPropsTable(string json)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddLumeo();
        ctx.Services.AddSingleton(new HttpClient(new FixedJsonHandler(json)) { BaseAddress = new Uri("https://test/") });
        ctx.Services.AddSingleton<RegistryService>();
        return ctx;
    }

    [Fact]
    public void Cascading_Parameters_Are_Excluded_From_The_Rendered_Table()
    {
        using var ctx = RenderPropsTable(FixtureJson);
        var cut = ctx.Render<PropsTable>(p => p.Add(x => x.Slug, "test"));

        cut.WaitForAssertion(() => Assert.Contains("Class", cut.Markup));
        Assert.DoesNotContain("Context", cut.Markup);
    }

    [Fact]
    public void When_Only_Cascading_Parameters_Exist_The_NoParams_Message_Shows()
    {
        const string cascadingOnly = """
            {
              "name": "Test",
              "category": "Forms",
              "description": "Test component",
              "nugetPackage": "Lumeo",
              "api": {
                "parameters": [
                  { "name": "Context", "type": "SomeContext", "default": "default!", "description": null, "isCascading": true, "captureUnmatched": false, "isEditorRequired": false }
                ],
                "events": []
              }
            }
            """;

        using var ctx = RenderPropsTable(cascadingOnly);
        var cut = ctx.Render<PropsTable>(p => p.Add(x => x.Slug, "test"));

        cut.WaitForAssertion(() => Assert.Contains("takes no public parameters", cut.Markup));
        Assert.DoesNotContain("Context", cut.Markup);
    }
}
