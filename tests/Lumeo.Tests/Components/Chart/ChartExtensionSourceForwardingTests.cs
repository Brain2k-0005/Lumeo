using System.Reflection;
using Bunit;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Chart;

/// <summary>
/// PR #351 round-6, finding 3: the plugin charts (WordCloud / LiquidFill) load their ECharts
/// EXTENSION on first render, and that call chains into the CORE ECharts load — which happens
/// BEFORE the inner &lt;Chart&gt; mounts. If the per-chart <c>EChartsSource</c> is not forwarded to
/// <c>loadExtension</c>, that first core load falls back to jsDelivr even when the consumer
/// self-hosts, defeating the documented "EChartsSource redirects that chart's core ECharts".
///
/// These tests assert the interop argument now carries the source: <c>loadExtension</c> is invoked
/// with the chart's <c>EChartsSource</c> as its third argument (extension URL + override key
/// unchanged, so the extension script still resolves via the global key as documented).
/// </summary>
public class ChartExtensionSourceForwardingTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        // The plugin charts import echarts-interop.js through ComponentInteropService, which
        // appends ?v=<assembly-version>. Set the module up at that SAME versioned URL in Loose
        // mode so loadExtension/loadECharts are handled and recorded. Mirrors ChartLifecycleTests.
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

    private IReadOnlyList<IReadOnlyList<object?>> LoadExtensionArgs() =>
        _ctx.JSInterop.Invocations
            .Where(i => i.Identifier == "loadExtension")
            .Select(i => i.Arguments)
            .ToList();

    [Fact]
    public void WordCloud_forwards_EChartsSource_to_loadExtension()
    {
        const string source = "/lib/echarts.min.js";

        var cut = _ctx.Render<L.WordCloudChart>(p => p
            .Add(c => c.EChartsSource, source)
            .Add(c => c.Words, new List<L.WordCloudChart.WordCloudItem>
            {
                new() { Text = "alpha", Weight = 10 }
            }));

        cut.WaitForAssertion(() =>
        {
            var calls = LoadExtensionArgs();
            Assert.NotEmpty(calls);
            var args = calls[0];
            // [extensionUrl, overrideKey, echartsSource]
            Assert.True(args.Count >= 3, "loadExtension must receive the EChartsSource as its third argument.");
            Assert.Equal("echartsWordcloud", args[1] as string); // extension still resolves via the global key
            Assert.Equal(source, args[2] as string);             // core ECharts now honours the per-chart source
        });
    }

    [Fact]
    public void LiquidFill_forwards_EChartsSource_to_loadExtension()
    {
        const string source = "/assets/echarts.min.js";

        var cut = _ctx.Render<L.LiquidFillChart>(p => p
            .Add(c => c.EChartsSource, source)
            .Add(c => c.Value, 0.6));

        cut.WaitForAssertion(() =>
        {
            var calls = LoadExtensionArgs();
            Assert.NotEmpty(calls);
            var args = calls[0];
            Assert.True(args.Count >= 3, "loadExtension must receive the EChartsSource as its third argument.");
            Assert.Equal("echartsLiquidfill", args[1] as string);
            Assert.Equal(source, args[2] as string);
        });
    }

    [Fact]
    public void WordCloud_without_EChartsSource_forwards_null_not_a_missing_argument()
    {
        // Regression guard: even with no per-chart source the third arg is present (null), so
        // loadECharts still receives a value and the global override / jsDelivr fallback applies.
        var cut = _ctx.Render<L.WordCloudChart>(p => p
            .Add(c => c.Words, new List<L.WordCloudChart.WordCloudItem>
            {
                new() { Text = "beta", Weight = 5 }
            }));

        cut.WaitForAssertion(() =>
        {
            var calls = LoadExtensionArgs();
            Assert.NotEmpty(calls);
            var args = calls[0];
            Assert.True(args.Count >= 3);
            Assert.Null(args[2]);
        });
    }
}
