using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Chart;

/// <summary>
/// Lifecycle regression coverage for Chart (battle-wave2 #124, #125, #207).
///
/// These bugs all concern the chart's JS-instance lifetime across runtime parameter
/// changes — group membership, the tooltip-portal bridge after a theme-driven
/// dispose+reinit, and recovery after a failed first-render init. Each assertion drives
/// the JS interop log (<c>_ctx.JSInterop.Invocations</c>) on the echarts-interop module,
/// which is where the chart issues initChart/updateChart/setChartGroup/registerTooltipSlot.
/// </summary>
public class ChartLifecycleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private Bunit.BunitJSModuleInterop _module = null!;

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        // Chart imports echarts-interop.js through ComponentInteropService.ImportModuleAsync,
        // which appends ?v=<assembly-version> (cache-busting) to every ./_content/Lumeo* URL.
        // The bUnit module setup must target that SAME versioned URL — otherwise a per-test
        // SetException("initChart") registered on the bare URL never fires (in Loose root mode
        // bUnit auto-creates a separate module for the actually-imported ?v= URL), the failed
        // init we want to provoke silently succeeds, and the error banner never renders.
        // Mirrors AffixCallbackTests / InplaceEditor* which load versioned modules the same way.
        var v = typeof(ComponentInteropService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(ComponentInteropService).Assembly.GetName().Version?.ToString()
            ?? "0";
        _module = _ctx.JSInterop.SetupModule($"./_content/Lumeo.Charts/js/echarts-interop.js?v={v}");
        _module.Mode = Bunit.JSRuntimeMode.Loose;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const string OneSeries =
        "{\"series\":[{\"type\":\"line\",\"data\":[1,2,3]}]}";

    // ---- #124: clearing/switching Group detaches the chart from its old group ----

    // The (elementId, groupId) pairs passed to setChartGroup(elementId, groupId). The
    // chart now calls this on every Group switch/clear; before the fix the clear/switch
    // path issued NO disconnect at all (connectCharts only ever assigns a group).
    private IReadOnlyList<(string? Id, string? Group)> SetChartGroupCalls() =>
        _ctx.JSInterop.Invocations
            .Where(i => i.Identifier == "setChartGroup")
            .Select(i => (
                i.Arguments.Count > 0 ? i.Arguments[0] as string : null,
                i.Arguments.Count > 1 ? i.Arguments[1] as string : null))
            .ToList();

    [Fact]
    public void Clearing_Group_Detaches_Chart_From_Old_Group()
    {
        // Chart starts in a sync group.
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.OptionJson, OneSeries)
            .Add(x => x.ShowLoadingSkeleton, false)
            .Add(x => x.Group, "sync-a"));

        // Consumer clears the Group — the chart must stop syncing with the old siblings.
        cut.Render(p => p
            .Add(x => x.OptionJson, OneSeries)
            .Add(x => x.ShowLoadingSkeleton, false)
            .Add(x => x.Group, (string?)null));

        // A setChartGroup call with a null/empty group id detaches the instance. Without
        // the fix no such call exists — the chart stayed wired to "sync-a" forever.
        Assert.Contains(SetChartGroupCalls(), c => string.IsNullOrEmpty(c.Group));
    }

    [Fact]
    public void Switching_Group_Re_Homes_Chart_Onto_New_Group()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.OptionJson, OneSeries)
            .Add(x => x.ShowLoadingSkeleton, false)
            .Add(x => x.Group, "sync-a"));

        // Switch to a different group.
        cut.Render(p => p
            .Add(x => x.OptionJson, OneSeries)
            .Add(x => x.ShowLoadingSkeleton, false)
            .Add(x => x.Group, "sync-b"));

        // The reassignment must target the NEW group; setChartGroup overwrites the old
        // membership so nothing lingers on "sync-a".
        Assert.Contains(SetChartGroupCalls(), c => c.Group == "sync-b");
    }

    // ---- #125: theme change re-registers the <ChartTooltip> portal bridge ----

    private static RenderFragment TooltipChild() => b =>
    {
        b.OpenComponent<L.ChartTooltip>(0);
        b.AddAttribute(1, "ChildContent",
            (RenderFragment<L.ChartTooltipContext>)(ctx => cb => cb.AddContent(0, $"hover:{ctx.SeriesName}")));
        b.CloseComponent();
    };

    private int RegisterTooltipSlotCount() =>
        _ctx.JSInterop.Invocations.Count(i => i.Identifier == "registerTooltipSlot");

    [Fact]
    public void Theme_Change_Re_Registers_The_Tooltip_Bridge()
    {
        // A chart with a custom <ChartTooltip> slot registers the portal bridge once.
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.OptionJson, OneSeries)
            .Add(x => x.ShowLoadingSkeleton, false)
            .Add(x => x.Theme, "light")
            .Add(x => x.ChildContent, TooltipChild()));

        Assert.Equal(1, RegisterTooltipSlotCount());

        // A theme change disposes + re-inits the JS instance. The fresh instance has no
        // tooltip formatter, so the bridge MUST be registered again. Without the fix the
        // _tooltipBridgeRegistered flag stayed true and the re-registration was skipped —
        // the custom tooltip portal silently died after a theme swap.
        cut.Render(p => p
            .Add(x => x.OptionJson, OneSeries)
            .Add(x => x.ShowLoadingSkeleton, false)
            .Add(x => x.Theme, "dark")
            .Add(x => x.ChildContent, TooltipChild()));

        Assert.Equal(2, RegisterTooltipSlotCount());
    }

    // ---- #207: chart recovers (re-inits) after a failed first-render init ----

    private int InitChartCount() =>
        _ctx.JSInterop.Invocations.Count(i => i.Identifier == "initChart");

    [Fact]
    public void Chart_Recovers_And_Re_Inits_After_A_Failed_Init_When_Inputs_Change()
    {
        // Make the FIRST initChart throw (e.g. a bad EChartsSource URL), then let later
        // calls succeed via the loose module default.
        var failInit = true;
        _module
            .SetupVoid("initChart", i => failInit && i.Identifier == "initChart")
            .SetException(new JSException("ECharts CDN failed to load"));

        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.OptionJson, OneSeries)
            .Add(x => x.ShowLoadingSkeleton, false)
            .Add(x => x.EChartsSource, "https://bad.example/echarts.js"));

        // The error branch is showing and the host div is gone.
        Assert.Contains("Chart initialization failed", cut.Markup);
        var initsAfterFailure = InitChartCount();
        Assert.True(initsAfterFailure >= 1);

        // Consumer fixes the source. The init inputs changed, so the chart must clear the
        // error, re-render the host, and re-attempt init. Allow the (now-good) call.
        failInit = false;
        cut.Render(p => p
            .Add(x => x.OptionJson, OneSeries)
            .Add(x => x.ShowLoadingSkeleton, false)
            .Add(x => x.EChartsSource, "https://good.example/echarts.js"));

        // Recovered: the error is cleared (host is back) and init ran again. Before the
        // fix _initialized stayed false and neither OnAfterRenderAsync branch re-ran, so
        // no further initChart was ever issued and the error latched forever.
        Assert.DoesNotContain("Chart initialization failed", cut.Markup);
        Assert.True(InitChartCount() > initsAfterFailure,
            "A changed EChartsSource after an init failure must trigger a fresh initChart.");
    }

    [Fact]
    public void Failed_Init_Without_Input_Change_Does_Not_Throw_On_Re_Render()
    {
        var failInit = true;
        _module
            .SetupVoid("initChart", i => failInit && i.Identifier == "initChart")
            .SetException(new JSException("boom"));

        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.OptionJson, OneSeries)
            .Add(x => x.ShowLoadingSkeleton, false));

        Assert.Contains("Chart initialization failed", cut.Markup);

        // An unrelated re-render (same init inputs) must NOT spuriously retry or throw —
        // recovery is gated on the init-relevant inputs actually changing.
        var ex = Record.Exception(() => cut.Render(p => p
            .Add(x => x.OptionJson, OneSeries)
            .Add(x => x.ShowLoadingSkeleton, false)
            .Add(x => x.Class, "still-broken")));

        Assert.Null(ex);
        Assert.Contains("Chart initialization failed", cut.Markup);
    }
}
