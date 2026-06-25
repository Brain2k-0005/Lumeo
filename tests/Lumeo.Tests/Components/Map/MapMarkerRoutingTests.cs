using System.Reflection;
using Bunit;
using Lumeo.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Map;

/// <summary>
/// Regression cover for the in-flight marker click/drag mis-route (battle-wave2 #137,
/// lifecycle). The old code stamped each marker in the <c>setMarkers</c> payload with its
/// POSITIONAL index (<c>id = i</c>) and resolved <c>OnMarkerClick</c>/<c>OnMarkerDragEnd</c>
/// via <c>_markers[markerId]</c>. When a marker is removed asynchronously, those indices
/// shift, so a click event already queued in JS arrives carrying a now-reused index and
/// fires the WRONG marker (or indexes past the end). The fix gives every marker a stable
/// string id (MapMarker._markerId) sent in the payload, and routes callbacks through a
/// Dictionary&lt;string, MapMarker&gt; rebuilt to match the set JS was last handed — so a
/// stale id simply finds no entry and is ignored. The MapLibre canvas never runs in bUnit,
/// so we drive the .NET&lt;-&gt;JS contract directly, mirroring MapInteropTests' SetupModule +
/// payload-reflection idiom and MapViewStateTests' JSInvokable-via-InvokeAsync idiom.
/// </summary>
public class MapMarkerRoutingTests : IAsyncLifetime
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

    public MapMarkerRoutingTests()
    {
        _ctx.AddLumeoServices();
        _module = _ctx.JSInterop.SetupModule(ModulePath());
        _module.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Read the id of each entry in the latest setMarkers payload, in order.
    private IReadOnlyList<object?> LatestMarkerIds()
    {
        var setMarkers = _module.Invocations.Last(i => i.Identifier == "setMarkers");
        var payload = setMarkers.Arguments[1]; // (mapId, payload[])
        var markers = ((System.Collections.IEnumerable)payload!).Cast<object>().ToList();
        return markers
            .Select(m => m.GetType().GetProperty("id")?.GetValue(m))
            .ToList();
    }

    private static RenderFragment Marker(double lat, double lon, EventCallback onClick) => b =>
    {
        b.OpenComponent<L.MapMarker>(0);
        b.AddAttribute(1, "Lat", lat);
        b.AddAttribute(2, "Lon", lon);
        b.AddAttribute(3, "OnClick", onClick);
        b.CloseComponent();
    };

    [Fact]
    public void Marker_payload_id_is_a_stable_string_not_a_positional_index()
    {
        var clicked = false;
        var cb = EventCallback.Factory.Create(this, () => clicked = true);

        _ctx.Render<L.Map>(p => p
            .Add(m => m.ChildContent, Marker(48.85, 2.35, cb)));

        var ids = LatestMarkerIds();
        Assert.Single(ids);
        // The bug stamped id = i (boxed int 0). The fix stamps the marker's stable
        // GUID id, which is a string. Asserting the runtime type pins the contract.
        Assert.IsType<string>(ids[0]);
        Assert.False(clicked); // sanity: rendering alone must not fire OnClick
    }

    [Fact]
    public async Task OnMarkerClick_routes_by_stable_id_to_the_correct_marker()
    {
        var firstClicked = false;
        var secondClicked = false;
        var first = EventCallback.Factory.Create(this, () => firstClicked = true);
        var second = EventCallback.Factory.Create(this, () => secondClicked = true);

        var cut = _ctx.Render<L.Map>(p => p
            .Add(m => m.ChildContent, b =>
            {
                b.OpenComponent<L.MapMarker>(0);
                b.AddAttribute(1, "Lat", 48.85);
                b.AddAttribute(2, "Lon", 2.35);
                b.AddAttribute(3, "OnClick", first);
                b.CloseComponent();
                b.OpenComponent<L.MapMarker>(4);
                b.AddAttribute(5, "Lat", 51.5);
                b.AddAttribute(6, "Lon", -0.12);
                b.AddAttribute(7, "OnClick", second);
                b.CloseComponent();
            }));

        var ids = LatestMarkerIds();
        Assert.Equal(2, ids.Count);
        var secondId = (string)ids[1]!;

        // JS reports a click on the SECOND marker by its stable id. Only that
        // marker's callback must fire — never the first (the old positional path
        // could fire the wrong one after a reorder/removal).
        await cut.InvokeAsync(() => cut.Instance.OnMarkerClick(secondId));

        Assert.True(secondClicked);
        Assert.False(firstClicked);
    }

    [Fact]
    public async Task OnMarkerClick_with_a_stale_id_after_removal_is_ignored_without_throwing()
    {
        var liveClicked = false;
        var live = EventCallback.Factory.Create(this, () => liveClicked = true);

        var cut = _ctx.Render<L.Map>(p => p
            .Add(m => m.ChildContent, Marker(48.85, 2.35, live)));

        // Simulate an in-flight click that arrives carrying an id JS knew about but
        // that no longer maps to any live marker (the marker was removed before the
        // event was dispatched). The router must drop it: no throw, no wrong-marker fire.
        var ex = await Record.ExceptionAsync(() =>
            cut.InvokeAsync(() => cut.Instance.OnMarkerClick("lumeo-map-marker-stale-removed")));

        Assert.Null(ex);
        Assert.False(liveClicked);
    }

    [Fact]
    public async Task OnMarkerDragEnd_routes_by_stable_id_to_the_correct_marker()
    {
        (double Lat, double Lon)? dropped = null;
        var onDragEnd = EventCallback.Factory.Create<(double Lat, double Lon)>(
            this, c => dropped = c);

        var cut = _ctx.Render<L.Map>(p => p
            .Add(m => m.ChildContent, b =>
            {
                b.OpenComponent<L.MapMarker>(0);
                b.AddAttribute(1, "Lat", 48.85);
                b.AddAttribute(2, "Lon", 2.35);
                b.AddAttribute(3, "Draggable", true);
                b.AddAttribute(4, "OnDragEnd", onDragEnd);
                b.CloseComponent();
            }));

        var id = (string)LatestMarkerIds()[0]!;

        await cut.InvokeAsync(() => cut.Instance.OnMarkerDragEnd(id, 52.52, 13.40));

        Assert.NotNull(dropped);
        Assert.Equal((52.52, 13.40), dropped!.Value);
    }
}
