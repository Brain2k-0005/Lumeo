using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Chart;

/// <summary>Covers <see cref="L.ChartSkeleton"/> — the SVG placeholder that renders
/// during chart load. Each <see cref="L.ChartSkeletonKind"/> produces a different
/// silhouette; tests verify the element count / shape per kind and confirm the
/// animatable class is always applied (the `prefers-reduced-motion` gate is a pure
/// CSS media query, so we assert the structure, not the runtime computed style).</summary>
public class ChartSkeletonTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Root_With_Status_Role_And_AriaLabel()
    {
        var cut = _ctx.Render<L.ChartSkeleton>();
        var root = cut.Find("div.lumeo-chart-skeleton");

        Assert.Equal("status", root.GetAttribute("role"));
        Assert.Equal("Loading chart", root.GetAttribute("aria-label"));
        Assert.Equal("polite", root.GetAttribute("aria-live"));
    }

    [Fact]
    public void Default_Height_Matches_Chart_Default()
    {
        var cut = _ctx.Render<L.ChartSkeleton>();
        var root = cut.Find("div.lumeo-chart-skeleton");

        Assert.Contains("height: 350px", root.GetAttribute("style"));
    }

    [Fact]
    public void Custom_Height_And_Width_Applied()
    {
        var cut = _ctx.Render<L.ChartSkeleton>(p => p
            .Add(b => b.Height, "220px")
            .Add(b => b.Width, "480px"));
        var root = cut.Find("div.lumeo-chart-skeleton");

        Assert.Contains("height: 220px", root.GetAttribute("style"));
        Assert.Contains("width: 480px", root.GetAttribute("style"));
    }

    [Fact]
    public void Bars_Kind_Renders_Eight_Rects()
    {
        var cut = _ctx.Render<L.ChartSkeleton>(p => p.Add(b => b.Kind, L.ChartSkeletonKind.Bars));
        var rects = cut.FindAll("rect.lumeo-chart-skel-shape");

        // 8 staggered bars — matches the BarHeights array in ChartSkeleton.
        Assert.Equal(8, rects.Count);
    }

    [Fact]
    public void Line_Kind_Renders_Multi_Line_Polylines()
    {
        var cut = _ctx.Render<L.ChartSkeleton>(p => p.Add(b => b.Kind, L.ChartSkeletonKind.Line));

        // Three overlapping polylines give the skeleton a multi-series look.
        Assert.Equal(3, cut.FindAll("polyline.lumeo-chart-skel-shape").Count);
        // Line variant has no filled area polygon.
        Assert.Empty(cut.FindAll("polygon.lumeo-chart-skel-shape"));
    }

    [Fact]
    public void Area_Kind_Renders_Polylines_And_Polygon()
    {
        var cut = _ctx.Render<L.ChartSkeleton>(p => p.Add(b => b.Kind, L.ChartSkeletonKind.Area));

        Assert.Equal(3, cut.FindAll("polyline.lumeo-chart-skel-shape").Count);
        Assert.Single(cut.FindAll("polygon.lumeo-chart-skel-shape"));
    }

    [Fact]
    public void Pie_Kind_Renders_Four_Slices()
    {
        var cut = _ctx.Render<L.ChartSkeleton>(p => p.Add(b => b.Kind, L.ChartSkeletonKind.Pie));

        // Pie renders 4 <path> wedges — one per 90° slice — each tagged as a pie-slice.
        Assert.Equal(4, cut.FindAll("path[data-skel-shape='pie-slice']").Count);
    }

    [Fact]
    public void Scatter_Kind_Renders_Fifteen_Dots()
    {
        var cut = _ctx.Render<L.ChartSkeleton>(p => p.Add(b => b.Kind, L.ChartSkeletonKind.Scatter));

        // 15 seeded scatter positions — stable across renders.
        Assert.Equal(15, cut.FindAll("circle.lumeo-chart-skel-shape").Count);
    }

    [Fact]
    public void Grid_Kind_Renders_TwentyFive_Cells()
    {
        var cut = _ctx.Render<L.ChartSkeleton>(p => p.Add(b => b.Kind, L.ChartSkeletonKind.Grid));

        // 5x5 heatmap-style layout.
        Assert.Equal(25, cut.FindAll("rect.lumeo-chart-skel-shape").Count);
    }

    [Fact]
    public void Generic_Kind_Renders_Rect_And_Ring()
    {
        var cut = _ctx.Render<L.ChartSkeleton>(p => p.Add(b => b.Kind, L.ChartSkeletonKind.Generic));

        Assert.Single(cut.FindAll("rect.lumeo-chart-skel-shape"));
        Assert.Single(cut.FindAll("circle.lumeo-chart-skel-shape"));
    }

    [Fact]
    public void Shapes_Carry_Animation_Class_For_Pulse()
    {
        // The `.lumeo-chart-skel-shape` class is the sole hook for both the pulse
        // animation and the reduced-motion override. Missing class = no animation
        // AND no reduced-motion safeguard — hence guarding it with a test.
        //
        // Scoped to `rect.lumeo-chart-skel-shape` because the skeleton also renders
        // static axis/legend hint rects (tagged via `data-skel-axis`) that intentionally
        // do NOT animate — they're background structure, not the pulsing shapes.
        var cut = _ctx.Render<L.ChartSkeleton>(p => p.Add(b => b.Kind, L.ChartSkeletonKind.Bars));

        var shapeRects = cut.FindAll("rect.lumeo-chart-skel-shape");
        Assert.NotEmpty(shapeRects);
        foreach (var rect in shapeRects)
        {
            Assert.Contains("lumeo-chart-skel-shape", rect.GetAttribute("class") ?? "");
        }
    }

    [Fact]
    public void Custom_Class_Is_Merged_With_Base()
    {
        var cut = _ctx.Render<L.ChartSkeleton>(p => p.Add(b => b.Class, "my-skel"));
        var root = cut.Find("div.lumeo-chart-skeleton");

        Assert.Contains("my-skel", root.GetAttribute("class"));
        Assert.Contains("lumeo-chart-skeleton", root.GetAttribute("class"));
    }

    [Fact]
    public void Cartesian_Kinds_Render_Axis_And_Legend_Hints()
    {
        // Top 14%: 3 legend pills. Left 10%: 4 y-tick labels. Bottom 12%: 6 x-tick labels.
        // These reserve plot-area space so the real chart can swap in without a layout jump.
        var cut = _ctx.Render<L.ChartSkeleton>(p => p.Add(b => b.Kind, L.ChartSkeletonKind.Bars));

        Assert.Equal(3, cut.FindAll("rect[data-skel-axis='legend']").Count);
        Assert.Equal(4, cut.FindAll("rect[data-skel-axis='y']").Count);
        Assert.Equal(6, cut.FindAll("rect[data-skel-axis='x']").Count);
    }

    [Fact]
    public void Pie_Kind_Renders_Legend_And_External_Label_Anchors()
    {
        // Pie reserves a top-strip legend and 4 external label anchor lines (one per quadrant).
        var cut = _ctx.Render<L.ChartSkeleton>(p => p.Add(b => b.Kind, L.ChartSkeletonKind.Pie));

        Assert.Equal(3, cut.FindAll("rect[data-skel-axis='legend']").Count);
        Assert.Equal(4, cut.FindAll("line[data-skel-axis='pie-label']").Count);
    }

    [Fact]
    public void Generic_Kind_Has_No_Axis_Or_Legend_Hints()
    {
        // Generic shapes fill the box; no reserved axis/legend space — keeps the skeleton
        // minimal for kinds where the real chart has no cartesian grid (Radar, Sankey, Tree, etc.).
        var cut = _ctx.Render<L.ChartSkeleton>(p => p.Add(b => b.Kind, L.ChartSkeletonKind.Generic));

        Assert.Empty(cut.FindAll("rect[data-skel-axis='legend']"));
        Assert.Empty(cut.FindAll("rect[data-skel-axis='x']"));
        Assert.Empty(cut.FindAll("rect[data-skel-axis='y']"));
    }

    [Fact]
    public void Kind_Attribute_Reflects_Selected_Variant()
    {
        var cut = _ctx.Render<L.ChartSkeleton>(p => p.Add(b => b.Kind, L.ChartSkeletonKind.Scatter));
        var root = cut.Find("div.lumeo-chart-skeleton");

        Assert.Equal("scatter", root.GetAttribute("data-lumeo-chart-skeleton"));
    }
}
