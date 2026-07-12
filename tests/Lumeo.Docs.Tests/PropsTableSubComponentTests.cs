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
/// PropsTable can render either the top-level component's own parameters (default,
/// SubComponent unset — the ButtonPage path) or a single sub-component's parameters
/// (SubComponent="Name", sourced from api.subComponents.&lt;Name&gt; in registry JSON —
/// e.g. Dialog's DialogTrigger/DialogContent/... tables).
/// </summary>
public class PropsTableSubComponentTests
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

    // Mirrors a composite component's registry shape: a top-level param ("Open") plus one
    // sub-component ("DialogTrigger") with its own distinct param ("AsChild") and event.
    private const string FixtureJson = """
        {
          "name": "Test",
          "category": "Overlays",
          "description": "Test component",
          "nugetPackage": "Lumeo",
          "api": {
            "parameters": [
              { "name": "Open", "type": "bool", "default": "false", "description": "Whether it's open.", "isCascading": false, "captureUnmatched": false, "isEditorRequired": false }
            ],
            "events": [],
            "subComponents": {
              "DialogTrigger": {
                "componentName": "DialogTrigger",
                "parameters": [
                  { "name": "AsChild", "type": "bool", "default": "false", "description": "Render as the child element.", "isCascading": false, "captureUnmatched": false, "isEditorRequired": false },
                  { "name": "Context", "type": "Dialog.DialogContext", "default": "default!", "description": null, "isCascading": true, "captureUnmatched": false, "isEditorRequired": false }
                ],
                "events": [
                  { "name": "OnClick", "type": "EventCallback", "description": "Fired on click." }
                ]
              }
            }
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
    public void Without_SubComponent_Renders_The_Top_Level_Parameters()
    {
        using var ctx = RenderPropsTable(FixtureJson);
        var cut = ctx.Render<PropsTable>(p => p.Add(x => x.Slug, "test"));

        cut.WaitForAssertion(() => Assert.Contains("Open", cut.Markup));
        Assert.DoesNotContain("AsChild", cut.Markup);
    }

    [Fact]
    public void With_SubComponent_Renders_That_SubComponents_Parameters_Instead()
    {
        using var ctx = RenderPropsTable(FixtureJson);
        var cut = ctx.Render<PropsTable>(p => p
            .Add(x => x.Slug, "test")
            .Add(x => x.SubComponent, "DialogTrigger"));

        cut.WaitForAssertion(() => Assert.Contains("AsChild", cut.Markup));
        // Top-level-only param must not leak into the sub-component table.
        Assert.DoesNotContain("Open", cut.Markup);
        // Cascading sub-component param stays excluded, same rule as top-level.
        Assert.DoesNotContain("Context", cut.Markup);
        // Sub-component's own events render too.
        Assert.Contains("OnClick", cut.Markup);
    }
}
