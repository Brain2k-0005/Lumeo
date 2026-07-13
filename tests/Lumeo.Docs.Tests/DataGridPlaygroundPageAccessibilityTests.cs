using System.Net;
using System.Text;
using Bunit;
using Lumeo;
using Lumeo.Docs.Pages.Components;
using Lumeo.Docs.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Docs.Tests;

// Quality-wave regression: every Switch/Checkbox in the playground's settings sidebar
// used to expose only an auto-generated "switch-<guid>" / "checkbox-<guid>" id with no
// programmatic label — the visible text next to each control was plain, unassociated
// <Text>, so a screen reader announced nothing meaningful for any of the ~35 toggles.
// Fixed by passing an explicit AriaLabel matching the visible label on every control;
// this guards against the wiring regressing silently.
public class DataGridPlaygroundPageAccessibilityTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    private sealed class EmptyRegistryHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"components\":{}}", Encoding.UTF8, "application/json")
            });
    }

    public DataGridPlaygroundPageAccessibilityTests()
    {
        // Loose mode: the grid registers column-resize/reorder JS interop on
        // OnAfterRenderAsync (real _content/Lumeo/js/components.js module calls) that
        // bUnit has no browser to satisfy — same as AllComponentPagesRenderTests.
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        // Mirrors the real docs WASM app DI (docs/Lumeo.Docs/Program.cs), same set
        // AllComponentPagesRenderTests uses to successfully render this page.
        _ctx.Services.AddLumeo();
        _ctx.Services.AddSingleton<IconService>();
        _ctx.Services.AddSingleton<DynamicIconResolver>();
        _ctx.Services.AddSingleton<PatternFilterService>();
        _ctx.Services.AddSingleton<NavConfigService>();
        _ctx.Services.AddSingleton(new HttpClient(new EmptyRegistryHandler()) { BaseAddress = new Uri("https://test/") });
        _ctx.Services.AddSingleton<RegistryService>();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Every_settings_switch_and_checkbox_has_an_accessible_name()
    {
        var cut = _ctx.Render<DataGridPlaygroundPage>();

        var toggles = cut.FindAll("[role='switch'], [role='checkbox']");

        Assert.True(toggles.Count >= 20, $"Expected at least 20 switch/checkbox controls in the playground sidebar, found {toggles.Count}.");

        var unnamed = toggles
            .Where(el => string.IsNullOrWhiteSpace(el.GetAttribute("aria-label")))
            .Select(el => el.OuterHtml)
            .ToList();

        Assert.True(unnamed.Count == 0,
            $"{unnamed.Count}/{toggles.Count} settings toggles have no aria-label:\n" + string.Join("\n", unnamed));
    }
}
