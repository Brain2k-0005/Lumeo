using System.Net;
using System.Text;
using Bunit;
using Lumeo;
using Lumeo.Docs.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Lumeo.Docs.Tests;

// Render every routable Components/* docs page with the SAME DI the real docs WASM
// app uses (AddLumeo() + docs services) under bUnit's Loose JSInterop, and assert no
// page throws. Guards against a component change (renamed/removed param, a demo that
// dereferences null, a broken @code block) silently breaking a docs page — the docs
// project compiling proves the demos still bind, this proves they still RENDER.
//
// IconPage is excluded: its only unsatisfiable dependency is the WASM-host-only
// LazyAssemblyLoader (a Blazor framework service bUnit can't supply), not a render
// concern. Every other page renders headless.
public class AllComponentPagesRenderTests
{
    private readonly ITestOutputHelper _out;
    public AllComponentPagesRenderTests(ITestOutputHelper o) => _out = o;

    private static readonly HashSet<string> Excluded = new() { "IconPage" };

    private sealed class EmptyRegistryHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"components\":{}}", Encoding.UTF8, "application/json")
            });
    }

    [Fact]
    public async Task Every_component_page_renders_without_throwing()
    {
        var pageTypes = typeof(Lumeo.Docs.Pages.Catalog).Assembly.GetTypes()
            .Where(t => t.Namespace == "Lumeo.Docs.Pages.Components"
                        && typeof(IComponent).IsAssignableFrom(t)
                        && !t.IsAbstract
                        && t.GetCustomAttributes(typeof(RouteAttribute), false).Length > 0
                        && !Excluded.Contains(t.Name))
            .OrderBy(t => t.Name)
            .ToList();

        var failures = new List<string>();
        foreach (var pageType in pageTypes)
        {
            try
            {
                await using var ctx = new BunitContext();
                ctx.JSInterop.Mode = JSRuntimeMode.Loose;

                // Mirror the real docs WASM app DI (docs/Lumeo.Docs/Program.cs):
                ctx.Services.AddLumeo();
                ctx.Services.AddSingleton<IconService>();
                ctx.Services.AddSingleton<PatternFilterService>();
                ctx.Services.AddSingleton<NavConfigService>();
                ctx.Services.AddSingleton(new HttpClient(new EmptyRegistryHandler()) { BaseAddress = new Uri("https://test/") });
                ctx.Services.AddSingleton<RegistryService>();

                ctx.Render(b => { b.OpenComponent(0, pageType); b.CloseComponent(); });
            }
            catch (Exception ex)
            {
                var root = ex;
                while (root.InnerException is not null) root = root.InnerException;
                failures.Add($"{pageType.Name}: {root.GetType().Name}: {root.Message}");
            }
        }

        _out.WriteLine($"Rendered {pageTypes.Count} component pages; {failures.Count} threw.");
        foreach (var f in failures) _out.WriteLine("  FAIL " + f);

        Assert.True(failures.Count == 0,
            $"{failures.Count}/{pageTypes.Count} component docs pages threw on render:\n" + string.Join("\n", failures));
    }
}
