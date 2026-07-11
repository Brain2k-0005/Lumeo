using System.Reflection;
using Bunit;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Chart;

/// <summary>
/// Chart's assigned keyboard-gap bullets (HostTabIndex on the host div,
/// AccessibilityLayer on/off, HostAriaLabel/role="img") were flagged by a
/// KeyDown/KeyboardEventArgs literal grep, which Chart has neither — it has
/// no custom key handler (canvas data-point navigation isn't implemented).
/// The REAL surface those bullets describe is already fully covered by
/// ChartAccessibilityTests (19 tests: HostTabIndex focusable/absent,
/// AccessibilityLayer true/false, explicit vs. generated aria-label, role=img).
/// This file adds the one edge that suite doesn't exercise: a chart with
/// AccessibilityLayer=true but NO usable series data must not add a phantom
/// Tab stop for an empty label.
/// </summary>
public class ChartKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        var v = typeof(ComponentInteropService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(ComponentInteropService).Assembly.GetName().Version?.ToString()
            ?? "0";
        var module = _ctx.JSInterop.SetupModule($"./_content/Lumeo.Charts/js/echarts-interop.js?v={v}");
        module.Mode = Bunit.JSRuntimeMode.Loose;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void HostTabIndex_Is_Absent_When_AccessibilityLayer_True_But_No_Usable_Data()
    {
        // No OptionJson at all -> ChartAccessibility.Build returns null -> no
        // summary -> HostAriaLabel is null -> HostTabIndex must stay null too
        // (a focusable element with no accessible name would be a WORSE a11y
        // outcome than not being reachable at all).
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.AccessibilityLayer, true)
            .Add(x => x.ShowLoadingSkeleton, false));

        var host = cut.Find(".lumeo-chart-host");
        Assert.Null(host.GetAttribute("tabindex"));
        Assert.Null(host.GetAttribute("role"));
        Assert.True(string.IsNullOrEmpty(host.GetAttribute("aria-label")));
    }
}
