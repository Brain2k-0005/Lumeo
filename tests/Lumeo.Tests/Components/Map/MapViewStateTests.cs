using System.Reflection;
using Bunit;
using Lumeo.Services;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Map;

/// <summary>
/// Regression cover for the uncontrolled pan/zoom snap-back (battle-wave2 #20).
/// The MapLibre canvas never runs in bUnit, so we drive the .NET&lt;-&gt;JS contract:
/// the renderer reports a user pan via <c>OnViewChanged</c>, then the parent re-renders
/// with the ORIGINAL Center/Zoom literals (the common uncontrolled case). The fix tracks
/// the last *parameter* value separately from the live view, so a same-literal re-render
/// must NOT push <c>setCenter</c> back to the start. Mirrors MapInteropTests' SetupModule
/// + invocation-inspection idiom and Gantt's JSInvokable-via-InvokeAsync idiom.
/// </summary>
public class MapViewStateTests : IAsyncLifetime
{
    private const string BarePath = "./_content/Lumeo.Maps/js/map.js";

    private static string ModulePath()
    {
        var asm = typeof(ComponentInteropService).Assembly;
        var v = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? asm.GetName().Version?.ToString()
                ?? "";
        return $"{BarePath}?v={v}";
    }

    private readonly BunitContext _ctx = new();
    private readonly BunitJSModuleInterop _module;

    public MapViewStateTests()
    {
        _ctx.AddLumeoServices();
        _module = _ctx.JSInterop.SetupModule(ModulePath());
        _module.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private int SetCenterCount() => _module.Invocations.Count(i => i.Identifier == "setCenter");

    [Fact]
    public async Task Uncontrolled_pan_then_same_literal_reRender_does_not_snap_back()
    {
        // Uncontrolled use: Center/Zoom are passed but NOT bound (no *Changed delegate).
        var cut = _ctx.Render<L.Map>(p => p
            .Add(m => m.Center, (51.0, 10.4))
            .Add(m => m.Zoom, 5));

        var before = SetCenterCount();

        // The renderer reports a user pan/zoom to Paris.
        await cut.InvokeAsync(() => cut.Instance.OnViewChanged(48.85, 2.35, 11));

        // A wholly unrelated parent re-render re-passes the ORIGINAL literals.
        cut.Render(p => p
            .Add(m => m.Center, (51.0, 10.4))
            .Add(m => m.Zoom, 5));

        // Without the fix the else-branch saw Center(51,10.4) != _lastCenter(panned)
        // and pushed setCenter back to (51,10.4) — snapping the user's pan away.
        // With the fix, a same-as-last-PARAM re-render pushes nothing.
        Assert.Equal(before, SetCenterCount());
    }

    [Fact]
    public void Controlled_parameter_change_still_pushes_setCenter()
    {
        // Controlled use: the consumer genuinely moves the Center parameter. That MUST
        // still flow through to the live map (guards against an over-broad fix).
        var cut = _ctx.Render<L.Map>(p => p
            .Add(m => m.Center, (51.0, 10.4))
            .Add(m => m.Zoom, 5));

        cut.Render(p => p
            .Add(m => m.Center, (48.85, 2.35))
            .Add(m => m.Zoom, 11));

        Assert.Contains(_module.Invocations, i => i.Identifier == "setCenter");
    }

    [Fact]
    public async Task Controlled_view_change_writes_Center_param_and_fires_CenterChanged()
    {
        // When CenterChanged is bound the component IS allowed to write the parameter
        // and raise the callback so the parent's two-way binding captures the new view.
        (double Lat, double Lon)? pushed = null;
        var cut = _ctx.Render<L.Map>(p => p
            .Add(m => m.Center, (51.0, 10.4))
            .Add(m => m.Zoom, 5)
            .Add(m => m.CenterChanged, ((double Lat, double Lon) c) => { pushed = c; }));

        await cut.InvokeAsync(() => cut.Instance.OnViewChanged(48.85, 2.35, 11));

        Assert.NotNull(pushed);
        Assert.Equal((48.85, 2.35), pushed!.Value);
        Assert.Equal((48.85, 2.35), cut.Instance.Center);
    }
}
