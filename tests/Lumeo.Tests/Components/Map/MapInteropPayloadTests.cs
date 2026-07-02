using System.Reflection;
using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Xunit;

namespace Lumeo.Tests.Components.Map;

/// <summary>
/// Verifies that the W4/B5 parameters (ClusterExclude, Properties, ClusterProperties,
/// ClusterColorExpression, ClusterRadius, ClusterMaxZoom, ElementId) flow through the
/// Razor interop boundary correctly and that existing defaults are not changed.
///
/// MapLibre GL JS never runs in bUnit — it requires a real browser DOM. These tests
/// verify the C#-to-JS interop contract: what gets serialised and passed to <c>init</c>
/// and <c>setMarkers</c> on the satellite map module, not what JS does with those values.
/// </summary>
public class MapInteropPayloadTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly BunitJSModuleInterop _mapsModule;

    public MapInteropPayloadTests()
    {
        _ctx.AddLumeoServices();

        // Map.razor imports the satellite map.js via ImportModuleAsync, which appends
        // ?v=<Lumeo-assembly-version>. We register the same URL so bUnit routes the
        // import() to a tracked module stub and records init/setMarkers invocations.
        var version = typeof(ComponentInteropService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(ComponentInteropService).Assembly.GetName().Version?.ToString()
            ?? "0";
        _mapsModule = _ctx.JSInterop.SetupModule(
            $"./_content/Lumeo.Maps/js/map.js?v={version}");
        _mapsModule.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Reads a named property from an anonymous-type object via reflection.
    // Accepts object? so callers don't need null-forgiving operators on Arguments[n].
    private static T? GetProp<T>(object? obj, string prop) =>
        obj is null ? default : (T?)obj.GetType().GetProperty(prop)?.GetValue(obj);

    // Returns the setMarkers invocation — there is exactly one per render.
    private JSRuntimeInvocation SetMarkersInvocation() =>
        Assert.Single(_mapsModule.Invocations, i => i.Identifier == "setMarkers");

    // Extracts the marker array from setMarkers arg[1] (anonymous types boxed as objects).
    private static object[] MarkerPayload(JSRuntimeInvocation inv)
    {
        // The payload is an anonymous-type array; Cast<object>() boxes each element so
        // callers can use reflection without knowing the concrete element type.
        var arg = (System.Collections.IEnumerable)inv.Arguments[1]!;
        return arg.Cast<object>().ToArray();
    }

    // Returns the init options object from the init invocation arg[1].
    private JSRuntimeInvocation InitInvocation() =>
        Assert.Single(_mapsModule.Invocations, i => i.Identifier == "init");

    // -----------------------------------------------------------------------
    // ElementId
    // -----------------------------------------------------------------------

    [Fact]
    public void ElementId_Is_Not_Empty_And_Matches_Root_Div_Id()
    {
        var cut = _ctx.Render<Lumeo.Map>(p => p
            .Add(m => m.Height, "300px"));

        Assert.False(string.IsNullOrWhiteSpace(cut.Instance.ElementId));
        Assert.Equal(cut.Instance.ElementId,
            cut.Find("[role='application']").GetAttribute("id"));
    }

    // -----------------------------------------------------------------------
    // ClusterExclude — default and explicit
    // -----------------------------------------------------------------------

    [Fact]
    public void ClusterExclude_Defaults_To_False_In_Marker_Payload()
    {
        _ctx.Render<Lumeo.Map>(p => p
            .Add(m => m.Cluster, true)
            .Add(m => m.ChildContent, b =>
            {
                b.OpenComponent<Lumeo.MapMarker>(0);
                b.AddAttribute(1, "Lat", 51.5);
                b.AddAttribute(2, "Lon", -0.1);
                b.CloseComponent();
            }));

        var items = MarkerPayload(SetMarkersInvocation());
        Assert.Single(items);
        Assert.False(GetProp<bool>(items[0], "clusterExclude"));
    }

    [Fact]
    public void ClusterExclude_True_Flows_Into_Marker_Payload()
    {
        _ctx.Render<Lumeo.Map>(p => p
            .Add(m => m.Cluster, true)
            .Add(m => m.ChildContent, b =>
            {
                b.OpenComponent<Lumeo.MapMarker>(0);
                b.AddAttribute(1, "Lat", 51.5);
                b.AddAttribute(2, "Lon", -0.1);
                b.AddAttribute(3, "ClusterExclude", true);
                b.CloseComponent();
            }));

        var items = MarkerPayload(SetMarkersInvocation());
        Assert.Single(items);
        Assert.True(GetProp<bool>(items[0], "clusterExclude"));
    }

    // -----------------------------------------------------------------------
    // Properties — null by default; populated when set
    // -----------------------------------------------------------------------

    [Fact]
    public void Properties_Is_Null_By_Default_In_Marker_Payload()
    {
        _ctx.Render<Lumeo.Map>(p => p
            .Add(m => m.ChildContent, b =>
            {
                b.OpenComponent<Lumeo.MapMarker>(0);
                b.AddAttribute(1, "Lat", 51.5);
                b.AddAttribute(2, "Lon", -0.1);
                b.CloseComponent();
            }));

        var items = MarkerPayload(SetMarkersInvocation());
        Assert.Single(items);
        Assert.Null(GetProp<Dictionary<string, object>>(items[0], "properties"));
    }

    [Fact]
    public void Properties_Dict_Flows_Into_Marker_Payload()
    {
        var props = new Dictionary<string, object> { ["score"] = 42, ["region"] = "west" };

        _ctx.Render<Lumeo.Map>(p => p
            .Add(m => m.Cluster, true)
            .Add(m => m.ChildContent, b =>
            {
                b.OpenComponent<Lumeo.MapMarker>(0);
                b.AddAttribute(1, "Lat", 48.1);
                b.AddAttribute(2, "Lon", 11.5);
                b.AddAttribute(3, "Properties", props);
                b.CloseComponent();
            }));

        var items = MarkerPayload(SetMarkersInvocation());
        Assert.Single(items);
        var got = GetProp<Dictionary<string, object>>(items[0], "properties");
        Assert.NotNull(got);
        Assert.Equal(42, got["score"]);
        Assert.Equal("west", got["region"]);
    }

    // -----------------------------------------------------------------------
    // Multiple markers — C# sends ALL markers; JS splits by clusterExclude
    // -----------------------------------------------------------------------

    [Fact]
    public void All_Markers_Are_In_Payload_Regardless_Of_ClusterExclude()
    {
        _ctx.Render<Lumeo.Map>(p => p
            .Add(m => m.Cluster, true)
            .Add(m => m.ChildContent, b =>
            {
                b.OpenComponent<Lumeo.MapMarker>(0);
                b.AddAttribute(1, "Lat", 51.5);
                b.AddAttribute(2, "Lon", -0.1);
                b.AddAttribute(3, "ClusterExclude", false);
                b.CloseComponent();
                b.OpenComponent<Lumeo.MapMarker>(4);
                b.AddAttribute(5, "Lat", 52.5);
                b.AddAttribute(6, "Lon", 13.4);
                b.AddAttribute(7, "ClusterExclude", true);
                b.CloseComponent();
            }));

        var items = MarkerPayload(SetMarkersInvocation());
        // Both markers are always sent to JS; JS performs the DOM/cluster split.
        Assert.Equal(2, items.Length);
        var excluded = items.Count(i => GetProp<bool>(i, "clusterExclude"));
        Assert.Equal(1, excluded);
    }

    // -----------------------------------------------------------------------
    // Cluster init options — ClusterRadius, ClusterMaxZoom
    // -----------------------------------------------------------------------

    [Fact]
    public void ClusterRadius_And_ClusterMaxZoom_Default_To_50_And_14()
    {
        _ctx.Render<Lumeo.Map>(p => p.Add(m => m.Cluster, true));

        var opts = InitInvocation().Arguments[1];
        Assert.Equal(50, GetProp<int>(opts, "clusterRadius"));
        Assert.Equal(14, GetProp<int>(opts, "clusterMaxZoom"));
    }

    [Fact]
    public void ClusterRadius_And_ClusterMaxZoom_Flow_Into_Init_Options()
    {
        _ctx.Render<Lumeo.Map>(p => p
            .Add(m => m.Cluster, true)
            .Add(m => m.ClusterRadius, 75)
            .Add(m => m.ClusterMaxZoom, 10));

        var opts = InitInvocation().Arguments[1];
        Assert.Equal(75, GetProp<int>(opts, "clusterRadius"));
        Assert.Equal(10, GetProp<int>(opts, "clusterMaxZoom"));
    }

    // -----------------------------------------------------------------------
    // ClusterColorExpression
    // -----------------------------------------------------------------------

    [Fact]
    public void ClusterColorExpression_Is_Null_By_Default()
    {
        _ctx.Render<Lumeo.Map>(p => p.Add(m => m.Cluster, true));

        var opts = InitInvocation().Arguments[1];
        Assert.Null(GetProp<object[]>(opts, "clusterColorExpression"));
    }

    [Fact]
    public void ClusterColorExpression_Flows_Into_Init_Options()
    {
        var expr = new object[] { "literal", "#ff0000" };
        _ctx.Render<Lumeo.Map>(p => p
            .Add(m => m.Cluster, true)
            .Add(m => m.ClusterColorExpression, expr));

        var opts = InitInvocation().Arguments[1];
        var got = GetProp<object[]>(opts, "clusterColorExpression");
        Assert.NotNull(got);
        Assert.Equal(expr, got);
    }

    // -----------------------------------------------------------------------
    // ClusterProperties
    // -----------------------------------------------------------------------

    [Fact]
    public void ClusterProperties_Is_Null_By_Default()
    {
        _ctx.Render<Lumeo.Map>(p => p.Add(m => m.Cluster, true));

        var opts = InitInvocation().Arguments[1];
        Assert.Null(GetProp<Dictionary<string, object[]>>(opts, "clusterProperties"));
    }

    [Fact]
    public void ClusterProperties_Flows_Into_Init_Options()
    {
        var customProps = new Dictionary<string, object[]>
        {
            ["totalScore"] = ["+", new object[] { "get", "score" }]
        };
        _ctx.Render<Lumeo.Map>(p => p
            .Add(m => m.Cluster, true)
            .Add(m => m.ClusterProperties, customProps));

        var opts = InitInvocation().Arguments[1];
        var got = GetProp<Dictionary<string, object[]>>(opts, "clusterProperties");
        Assert.NotNull(got);
        Assert.True(got.ContainsKey("totalScore"));
    }

    // -----------------------------------------------------------------------
    // Existing defaults unchanged
    // -----------------------------------------------------------------------

    [Fact]
    public void Existing_Init_Defaults_Unchanged_Without_New_Params()
    {
        // A Map configured without any new params should still produce the same
        // core init payload so existing consumers are not broken.
        _ctx.Render<Lumeo.Map>(p => p
            .Add(m => m.Zoom, 7)
            .Add(m => m.Height, "400px"));

        var opts = InitInvocation().Arguments[1];
        Assert.Equal(7, GetProp<int>(opts, "zoom"));
        // New optional params have safe defaults that don't change behaviour.
        Assert.Null(GetProp<Dictionary<string, object[]>>(opts, "clusterProperties"));
        Assert.Null(GetProp<object[]>(opts, "clusterColorExpression"));
        Assert.Equal(50, GetProp<int>(opts, "clusterRadius"));
        Assert.Equal(14, GetProp<int>(opts, "clusterMaxZoom"));
    }
}
