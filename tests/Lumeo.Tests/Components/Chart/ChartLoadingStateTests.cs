using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Chart;

/// <summary>Covers the Chart loading-state hook — <c>IsLoading</c>, <c>ShowLoadingSkeleton</c>,
/// <c>SkeletonKind</c>, and <c>SkeletonStyle</c>. The default <c>Phantom</c> style renders the
/// real ECharts with placeholder data (no SVG overlay), so SVG-overlay assertions are gated
/// on <c>SkeletonStyle.Silhouette</c>. Phantom-specific behavior has its own section below.</summary>
public class ChartLoadingStateTests : IAsyncLifetime
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

    [Fact]
    public void Chart_Host_Div_Always_Present_Even_When_Loading()
    {
        var cut = _ctx.Render<L.Chart>(p => p.Add(x => x.IsLoading, true));

        var host = cut.Find("div.lumeo-chart-host");
        Assert.StartsWith("lumeo-chart-", host.GetAttribute("id") ?? "");
    }

    [Fact]
    public void IsLoading_True_Silhouette_Renders_Skeleton_Overlay()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.IsLoading, true)
            .Add(x => x.SkeletonStyle, L.ChartSkeletonStyle.Silhouette));

        Assert.NotEmpty(cut.FindAll("div.lumeo-chart-skeleton"));
    }

    [Fact]
    public void IsLoading_False_And_Rendered_Hides_Skeleton()
    {
        var cut = _ctx.Render<L.Chart>();

        Assert.Empty(cut.FindAll("div.lumeo-chart-skeleton"));
    }

    [Fact]
    public void ShowLoadingSkeleton_False_Skips_Skeleton_Even_When_Loading()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.IsLoading, true)
            .Add(x => x.ShowLoadingSkeleton, false)
            .Add(x => x.SkeletonStyle, L.ChartSkeletonStyle.Silhouette));

        Assert.Empty(cut.FindAll("div.lumeo-chart-skeleton"));
    }

    [Fact]
    public void SkeletonKind_Bars_Forwards_To_Skeleton()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.IsLoading, true)
            .Add(x => x.SkeletonStyle, L.ChartSkeletonStyle.Silhouette)
            .Add(x => x.SkeletonKind, L.ChartSkeletonKind.Bars));

        var root = cut.Find("div.lumeo-chart-skeleton");
        Assert.Equal("bars", root.GetAttribute("data-lumeo-chart-skeleton"));
    }

    [Fact]
    public void SkeletonKind_Pie_Forwards_To_Skeleton()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.IsLoading, true)
            .Add(x => x.SkeletonStyle, L.ChartSkeletonStyle.Silhouette)
            .Add(x => x.SkeletonKind, L.ChartSkeletonKind.Pie));

        var root = cut.Find("div.lumeo-chart-skeleton");
        Assert.Equal("pie", root.GetAttribute("data-lumeo-chart-skeleton"));
    }

    [Fact]
    public void IsLoading_Toggled_Back_On_After_First_Render_Shows_Skeleton_Again()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.SkeletonStyle, L.ChartSkeletonStyle.Silhouette));
        Assert.Empty(cut.FindAll("div.lumeo-chart-skeleton"));

        cut.Render(p => p
            .Add(x => x.SkeletonStyle, L.ChartSkeletonStyle.Silhouette)
            .Add(x => x.IsLoading, true));

        Assert.NotEmpty(cut.FindAll("div.lumeo-chart-skeleton"));
    }

    [Fact]
    public void BarChart_Wrapper_Defaults_SkeletonKind_To_Bars()
    {
        var cut = _ctx.Render<L.BarChart>(p => p
            .Add(x => x.IsLoading, true)
            .Add(x => x.SkeletonStyle, L.ChartSkeletonStyle.Silhouette));

        var root = cut.Find("div.lumeo-chart-skeleton");
        Assert.Equal("bars", root.GetAttribute("data-lumeo-chart-skeleton"));
    }

    [Fact]
    public void LineChart_Wrapper_Defaults_SkeletonKind_To_Line()
    {
        var cut = _ctx.Render<L.LineChart>(p => p
            .Add(x => x.IsLoading, true)
            .Add(x => x.SkeletonStyle, L.ChartSkeletonStyle.Silhouette));

        var root = cut.Find("div.lumeo-chart-skeleton");
        Assert.Equal("line", root.GetAttribute("data-lumeo-chart-skeleton"));
    }

    [Fact]
    public void PieChart_Wrapper_Defaults_SkeletonKind_To_Pie()
    {
        var cut = _ctx.Render<L.PieChart>(p => p
            .Add(x => x.IsLoading, true)
            .Add(x => x.SkeletonStyle, L.ChartSkeletonStyle.Silhouette));

        var root = cut.Find("div.lumeo-chart-skeleton");
        Assert.Equal("pie", root.GetAttribute("data-lumeo-chart-skeleton"));
    }

    [Fact]
    public void ScatterChart_Wrapper_Defaults_SkeletonKind_To_Scatter()
    {
        var cut = _ctx.Render<L.ScatterChart>(p => p
            .Add(x => x.IsLoading, true)
            .Add(x => x.SkeletonStyle, L.ChartSkeletonStyle.Silhouette));

        var root = cut.Find("div.lumeo-chart-skeleton");
        Assert.Equal("scatter", root.GetAttribute("data-lumeo-chart-skeleton"));
    }

    [Fact]
    public void HeatmapChart_Wrapper_Defaults_SkeletonKind_To_Grid()
    {
        var cut = _ctx.Render<L.HeatmapChart>(p => p
            .Add(x => x.IsLoading, true)
            .Add(x => x.SkeletonStyle, L.ChartSkeletonStyle.Silhouette));

        var root = cut.Find("div.lumeo-chart-skeleton");
        Assert.Equal("grid", root.GetAttribute("data-lumeo-chart-skeleton"));
    }

    [Fact]
    public void Consumer_Can_Override_Wrapper_Default_SkeletonKind()
    {
        var cut = _ctx.Render<L.BarChart>(p => p
            .Add(x => x.IsLoading, true)
            .Add(x => x.SkeletonStyle, L.ChartSkeletonStyle.Silhouette)
            .Add(x => x.SkeletonKind, L.ChartSkeletonKind.Generic));

        var root = cut.Find("div.lumeo-chart-skeleton");
        Assert.Equal("generic", root.GetAttribute("data-lumeo-chart-skeleton"));
    }

    // ---- Phantom mode (default) ----

    [Fact]
    public void Phantom_Is_The_Default_SkeletonStyle()
    {
        var cut = _ctx.Render<L.Chart>(p => p.Add(x => x.IsLoading, true));

        // Phantom mode renders no SVG overlay — the chart host carries the phantom data.
        Assert.Empty(cut.FindAll("div.lumeo-chart-skeleton"));
        Assert.NotEmpty(cut.FindAll("div.lumeo-chart-host"));
    }

    [Fact]
    public void Phantom_IsLoading_False_Still_Renders_Chart_Host()
    {
        var cut = _ctx.Render<L.Chart>();

        Assert.NotEmpty(cut.FindAll("div.lumeo-chart-host"));
        Assert.Empty(cut.FindAll("div.lumeo-chart-skeleton"));
    }
}
