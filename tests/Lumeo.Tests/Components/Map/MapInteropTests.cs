using System.Reflection;
using Bunit;
using Lumeo.Services;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Map;

/// <summary>
/// The MapLibre/Leaflet surface lives in the map.js satellite module and never runs
/// in bUnit's headless DOM (no real canvas / pan / zoom), so the map itself can't be
/// asserted here. What IS battle-testable — and previously was not covered at all
/// beyond a static landmark assert — is the .NET&lt;-&gt;JS interop contract: mounting
/// lazy-imports map.js (by its cache-versioned path) and runs <c>init</c> against it,
/// and a Center change pushes <c>setCenter</c> to the module. Mirrors the Scheduler
/// interop tests; the ?v= suffix is reconstructed exactly as ComponentInteropService
/// appends it so the SetupModule handle matches what the component imports.
/// </summary>
public class MapInteropTests : IAsyncLifetime
{
    private const string BarePath = "./_content/Lumeo.Maps/js/map.js";

    private static string ModulePath()
    {
        // ComponentInteropService.AppendVersion() tacks ?v=<assembly version> onto
        // _content/Lumeo* module URLs; reconstruct it from the same assembly.
        var asm = typeof(ComponentInteropService).Assembly;
        var v = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? asm.GetName().Version?.ToString()
                ?? "";
        return $"{BarePath}?v={v}";
    }

    private readonly BunitContext _ctx = new();
    private readonly BunitJSModuleInterop _module;

    public MapInteropTests()
    {
        _ctx.AddLumeoServices();
        // Register the Map's own isolated module (versioned path) so its init/setCenter
        // calls resolve and are recorded. Loose so unmocked follow-up sync calls pass.
        _module = _ctx.JSInterop.SetupModule(ModulePath());
        _module.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Mounting_lazy_imports_the_map_module_by_path()
    {
        _ctx.Render<L.Map>();
        // The dynamic import("./_content/Lumeo.Maps/js/map.js?v=…") is the first hop.
        Assert.Contains(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "import"
                 && i.Arguments.Any(a => a is string s && s.StartsWith(BarePath, StringComparison.Ordinal)));
    }

    [Fact]
    public void Mounting_runs_map_init_against_the_module()
    {
        _ctx.Render<L.Map>();
        Assert.Contains(_module.Invocations, i => i.Identifier == "init");
    }

    [Fact]
    public void Changing_center_pushes_setCenter_to_the_module()
    {
        var cut = _ctx.Render<L.Map>(p => p
            .Add(m => m.Center, (51.0, 10.4))
            .Add(m => m.Zoom, 5));

        // Pan/zoom to Paris — the param change must be pushed to the live map.
        cut.Render(p => p
            .Add(m => m.Center, (48.85, 2.35))
            .Add(m => m.Zoom, 11));

        Assert.Contains(_module.Invocations, i => i.Identifier == "setCenter");
    }
}
