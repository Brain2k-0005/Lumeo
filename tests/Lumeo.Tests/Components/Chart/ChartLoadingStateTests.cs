using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Chart;

/// <summary>Covers the Chart loading-skeleton hook — <c>IsLoading</c>, <c>ShowLoadingSkeleton</c>,
/// and <c>SkeletonKind</c>. The skeleton renders as an absolute-positioned child of the chart
/// root so ECharts still has its host div available for mounting; we assert both concerns.</summary>
public class ChartLoadingStateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        var module = _ctx.JSInterop.SetupModule("./_content/Lumeo/js/echarts-interop.js");
        module.Mode = Bunit.JSRuntimeMode.Loose;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Chart_Host_Div_Always_Present_Even_When_Loading()
    {
        // The host div must always exist in the DOM so ECharts has a mount target;
        // the skeleton overlays it rather than replacing it.
        var cut = _ctx.Render<L.Chart>(p => p.Add(x => x.IsLoading, true));

        var host = cut.Find("div.lumeo-chart-host");
        Assert.StartsWith("lumeo-chart-", host.GetAttribute("id") ?? "");
    }

    [Fact]
    public void IsLoading_True_Renders_Skeleton_Overlay()
    {
        var cut = _ctx.Render<L.Chart>(p => p.Add(x => x.IsLoading, true));

        Assert.NotEmpty(cut.FindAll("div.lumeo-chart-skeleton"));
    }

    [Fact]
    public void IsLoading_False_And_Rendered_Hides_Skeleton()
    {
        // After the first render completes the skeleton should retract because
        // IsLoading is false and _hasFirstRendered is flipped true by OnAfterRenderAsync.
        var cut = _ctx.Render<L.Chart>();

        Assert.Empty(cut.FindAll("div.lumeo-chart-skeleton"));
    }

    [Fact]
    public void ShowLoadingSkeleton_False_Skips_Skeleton_Even_When_Loading()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.IsLoading, true)
            .Add(x => x.ShowLoadingSkeleton, false));

        Assert.Empty(cut.FindAll("div.lumeo-chart-skeleton"));
    }

    [Fact]
    public void SkeletonKind_Bars_Forwards_To_Skeleton()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.IsLoading, true)
            .Add(x => x.SkeletonKind, L.ChartSkeletonKind.Bars));

        var root = cut.Find("div.lumeo-chart-skeleton");
        Assert.Equal("bars", root.GetAttribute("data-lumeo-chart-skeleton"));
    }

    [Fact]
    public void SkeletonKind_Pie_Forwards_To_Skeleton()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.IsLoading, true)
            .Add(x => x.SkeletonKind, L.ChartSkeletonKind.Pie));

        var root = cut.Find("div.lumeo-chart-skeleton");
        Assert.Equal("pie", root.GetAttribute("data-lumeo-chart-skeleton"));
    }

    [Fact]
    public void IsLoading_Toggled_Back_On_After_First_Render_Shows_Skeleton_Again()
    {
        // Simulates a consumer refetch cycle: chart mounts, data loads, user clicks refresh.
        var cut = _ctx.Render<L.Chart>();
        Assert.Empty(cut.FindAll("div.lumeo-chart-skeleton"));

        cut.Render(p => p.Add(x => x.IsLoading, true));

        Assert.NotEmpty(cut.FindAll("div.lumeo-chart-skeleton"));
    }

    [Fact]
    public void BarChart_Wrapper_Defaults_SkeletonKind_To_Bars()
    {
        var cut = _ctx.Render<L.BarChart>(p => p.Add(x => x.IsLoading, true));

        var root = cut.Find("div.lumeo-chart-skeleton");
        Assert.Equal("bars", root.GetAttribute("data-lumeo-chart-skeleton"));
    }

    [Fact]
    public void LineChart_Wrapper_Defaults_SkeletonKind_To_Line()
    {
        var cut = _ctx.Render<L.LineChart>(p => p.Add(x => x.IsLoading, true));

        var root = cut.Find("div.lumeo-chart-skeleton");
        Assert.Equal("line", root.GetAttribute("data-lumeo-chart-skeleton"));
    }

    [Fact]
    public void PieChart_Wrapper_Defaults_SkeletonKind_To_Pie()
    {
        var cut = _ctx.Render<L.PieChart>(p => p.Add(x => x.IsLoading, true));

        var root = cut.Find("div.lumeo-chart-skeleton");
        Assert.Equal("pie", root.GetAttribute("data-lumeo-chart-skeleton"));
    }

    [Fact]
    public void ScatterChart_Wrapper_Defaults_SkeletonKind_To_Scatter()
    {
        var cut = _ctx.Render<L.ScatterChart>(p => p.Add(x => x.IsLoading, true));

        var root = cut.Find("div.lumeo-chart-skeleton");
        Assert.Equal("scatter", root.GetAttribute("data-lumeo-chart-skeleton"));
    }

    [Fact]
    public void HeatmapChart_Wrapper_Defaults_SkeletonKind_To_Grid()
    {
        var cut = _ctx.Render<L.HeatmapChart>(p => p.Add(x => x.IsLoading, true));

        var root = cut.Find("div.lumeo-chart-skeleton");
        Assert.Equal("grid", root.GetAttribute("data-lumeo-chart-skeleton"));
    }

    [Fact]
    public void Consumer_Can_Override_Wrapper_Default_SkeletonKind()
    {
        var cut = _ctx.Render<L.BarChart>(p => p
            .Add(x => x.IsLoading, true)
            .Add(x => x.SkeletonKind, L.ChartSkeletonKind.Generic));

        var root = cut.Find("div.lumeo-chart-skeleton");
        Assert.Equal("generic", root.GetAttribute("data-lumeo-chart-skeleton"));
    }
}
