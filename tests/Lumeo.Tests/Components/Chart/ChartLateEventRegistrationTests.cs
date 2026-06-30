using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Chart;

/// <summary>
/// Regression coverage for the late event/tooltip wiring path (battle-wave2 #123).
///
/// Event callbacks (OnClick/OnDataZoom/…) and the &lt;ChartTooltip&gt; slot used to be
/// registered ONLY in the firstRender block and the theme-change block of
/// OnAfterRenderAsync. The else-if (_initialized) update branch never re-ran
/// RegisterChartEventsAsync, so a delegate (or tooltip slot) that appeared AFTER the
/// first render — because the parent re-rendered and only THEN bound the handler — was
/// silently never attached to the live ECharts instance.
///
/// The fix re-runs registration on later renders when something is still unregistered,
/// while tracking already-bound event names so a re-run never double-attaches a handler
/// the JS side already has (registerChartEvent does NOT de-dupe).
///
/// These tests assert on the JS interop log: the second argument of
/// <c>registerChartEvent(elementId, eventName, dotnetRef)</c>. Without the fix, the
/// "click" registration is absent because it was wired only on the second render.
/// </summary>
public class ChartLateEventRegistrationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        var module = _ctx.JSInterop.SetupModule("./_content/Lumeo.Charts/js/echarts-interop.js");
        module.Mode = Bunit.JSRuntimeMode.Loose;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const string OneSeries =
        "{\"series\":[{\"type\":\"line\",\"data\":[1,2,3]}]}";

    // Names passed to registerChartEvent(elementId, eventName, dotnetRef).
    private IReadOnlyList<string?> RegisteredEventNames() =>
        _ctx.JSInterop.Invocations
            .Where(i => i.Identifier == "registerChartEvent")
            .Select(i => i.Arguments.Count > 1 ? i.Arguments[1] as string : null)
            .ToList();

    [Fact]
    public void OnClick_Wired_After_First_Render_Is_Registered()
    {
        // First render: no OnClick delegate yet (parent hasn't bound it).
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.OptionJson, OneSeries)
            .Add(x => x.ShowLoadingSkeleton, false));

        Assert.DoesNotContain("click", RegisteredEventNames());

        // Parent re-renders and NOW supplies the OnClick handler.
        cut.Render(p => p
            .Add(x => x.OptionJson, OneSeries)
            .Add(x => x.ShowLoadingSkeleton, false)
            .Add(x => x.OnClick, EventCallback.Factory.Create<L.Chart.ChartEventArgs>(this, _ => { })));

        // The late-bound handler must now be attached to the live instance.
        Assert.Contains("click", RegisteredEventNames());
    }

    [Fact]
    public void OnClick_Present_At_First_Render_Is_Not_Re_Registered_On_Later_Update()
    {
        // OnClick is present from the start, so it is registered once on first render.
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.OptionJson, OneSeries)
            .Add(x => x.ShowLoadingSkeleton, false)
            .Add(x => x.OnClick, EventCallback.Factory.Create<L.Chart.ChartEventArgs>(this, _ => { })));

        Assert.Single(RegisteredEventNames(), n => n == "click");

        // An unrelated data update re-renders the chart — it must NOT double-attach the
        // already-bound click handler (registerChartEvent does not de-dupe on the JS side).
        cut.Render(p => p
            .Add(x => x.OptionJson, "{\"series\":[{\"type\":\"line\",\"data\":[9,8,7,6]}]}")
            .Add(x => x.ShowLoadingSkeleton, false)
            .Add(x => x.OnClick, EventCallback.Factory.Create<L.Chart.ChartEventArgs>(this, _ => { })));

        Assert.Single(RegisteredEventNames(), n => n == "click");
    }
}
