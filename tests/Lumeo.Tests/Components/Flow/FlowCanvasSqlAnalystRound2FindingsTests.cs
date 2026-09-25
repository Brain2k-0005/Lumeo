using Bunit;
using Lumeo.Services;
using Microsoft.AspNetCore.Components;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// Findings LU-18/LU-19 from the SQL Analyst (Blazor Server) round-2 field report against
/// 5.11.1. LU-20 (menu separator scrollbar) lives in
/// <see cref="Lumeo.Tests.Components.Overlay.MenuSeparatorScrollbarRegressionTests"/> —
/// it touches DropdownMenu/ContextMenu/Menubar, not Flow.
/// </summary>
public class FlowCanvasSqlAnalystRound2FindingsTests : FlowCanvasTestBase
{
    // ── LU-18: the Animated=true/Dashed=false "flow" overlay path picks up edge.Class/Style ──

    [Fact]
    public void Animated_Edge_Flow_Overlay_Gets_The_Edges_Class()
    {
        var edges = new List<L.FlowEdge> { new("a-b", "a", "b", Animated: true, Class: "critical-edge") };
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.Edges, edges));

        var overlay = cut.Find("[data-flow-edge-flow]");
        Assert.Contains("critical-edge", overlay.ClassList);
    }

    [Fact]
    public void Animated_Edge_Flow_Overlay_Style_Ends_With_The_Edges_Style()
    {
        var edges = new List<L.FlowEdge> { new("a-b", "a", "b", Animated: true, Style: "stroke:red;") };
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.Edges, edges));

        var overlay = cut.Find("[data-flow-edge-flow]");
        // Same rule as the base path (EdgeStyle): edge.Style is appended last so it wins over
        // the library's own default/selected stroke declarations, including on the overlay.
        Assert.EndsWith("stroke:red;", overlay.GetAttribute("style"));
    }

    [Fact]
    public void Animated_Edge_Flow_Overlay_Without_Class_Or_Style_Renders_Unaffected()
    {
        var edges = new List<L.FlowEdge> { new("a-b", "a", "b", Animated: true) };
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.Edges, edges));

        var overlay = cut.Find("[data-flow-edge-flow]");
        Assert.Empty(overlay.ClassList);
        var style = overlay.GetAttribute("style") ?? "";
        Assert.Contains("stroke-dasharray:2 10", style);
    }

    [Fact]
    public void Dashed_Animated_Edge_Renders_No_Flow_Overlay_At_All()
    {
        // Unchanged phase-2 behaviour: the overlay only exists for Animated && !Dashed — a
        // Dashed edge's own stroke-dasharray already reads as "in motion".
        var edges = new List<L.FlowEdge> { new("a-b", "a", "b", Animated: true, Dashed: true, Class: "critical-edge") };
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.Edges, edges));

        Assert.Empty(cut.FindAll("[data-flow-edge-flow]"));
    }

    // ── LU-19: FlowFitViewOptions.AnchorAlign ───────────────────────────────

    [Fact]
    public void FlowFitViewOptions_AnchorAlign_Defaults_To_Center()
    {
        Assert.Equal(L.FlowAnchorAlign.Center, new L.FlowFitViewOptions().AnchorAlign);
    }

    [Fact]
    public void AnchorAlignedViewport_Center_Matches_The_Plain_CenterOn_Placement()
    {
        var anchor = new L.FlowRect(0, 0, 100, 100);
        var centered = L.FlowGeometry.AnchorAlignedViewport(anchor, 0.92, 800, 600, 0.1, L.FlowAnchorAlign.Center);
        var expected = L.FlowGeometry.CenterOn(50, 50, 0.92, 800, 600);
        Assert.Equal(expected, centered);
    }

    [Fact]
    public void AnchorAlignedViewport_Start_Puts_The_Leading_Edge_At_The_Padding_On_Both_Axes()
    {
        var anchor = new L.FlowRect(0, 0, 100, 100);
        var vp = L.FlowGeometry.AnchorAlignedViewport(anchor, 0.92, 800, 600, 0.1, L.FlowAnchorAlign.Start);

        Assert.Equal(0.92, vp.Zoom, 6);
        // Anchor's own left/top edge (both 0 in flow space) lands at 0.1 * pane in from the
        // pane's own left/top edge: X = padX - 0*zoom, Y = padY - 0*zoom.
        Assert.Equal(80, vp.X, 3); // 0.1 * 800
        Assert.Equal(60, vp.Y, 3); // 0.1 * 600
    }

    [Fact]
    public void AnchorAlignedViewport_End_Puts_The_Trailing_Edge_At_The_Padding_On_Both_Axes()
    {
        var anchor = new L.FlowRect(0, 0, 100, 100);
        var vp = L.FlowGeometry.AnchorAlignedViewport(anchor, 0.92, 800, 600, 0.1, L.FlowAnchorAlign.End);

        Assert.Equal(0.92, vp.Zoom, 6);
        // Anchor's own right/bottom edge (100,100) lands at 0.1 * pane in from the pane's own
        // right/bottom edge: X = (paneWidth - padX) - anchor.Right*zoom.
        Assert.Equal(800 - 80 - 100 * 0.92, vp.X, 3);
        Assert.Equal(600 - 60 - 100 * 0.92, vp.Y, 3);
    }

    [Fact]
    public void AnchorAlignedViewport_Start_Is_Rtl_Aware_Horizontally_But_Not_Vertically()
    {
        var anchor = new L.FlowRect(0, 0, 100, 100);
        var vp = L.FlowGeometry.AnchorAlignedViewport(anchor, 0.92, 800, 600, 0.1, L.FlowAnchorAlign.Start, rtl: true);

        // In RTL, "Start" is the anchor's RIGHT edge against the pane's right edge — the mirror
        // of the LTR Start case horizontally (matches the LTR End case's X exactly).
        Assert.Equal(800 - 80 - 100 * 0.92, vp.X, 3);
        // Vertical placement is unaffected by rtl — still the top edge at the top padding.
        Assert.Equal(60, vp.Y, 3);
    }

    [Fact]
    public void FitView_With_AnchorAlign_Delegates_To_AnchorAlignedViewport_When_Clamped()
    {
        var rects = new[] { new L.FlowRect(0, 0, 100, 100), new L.FlowRect(5000, 5000, 100, 100) };
        var vp = L.FlowGeometry.FitView(rects, 800, 600, 0.1, 0.92, 2, new L.FlowRect(0, 0, 100, 100), L.FlowAnchorAlign.Start)!.Value;

        Assert.Equal(0.92, vp.Zoom, 6);
        Assert.Equal(80, vp.X, 3);
        Assert.Equal(60, vp.Y, 3);
    }

    [Fact]
    public void FitView_With_AnchorAlign_Falls_Back_To_The_Plain_Fit_When_Not_Clamped()
    {
        // The align/rtl parameters only matter once the anchor-clamped branch triggers; an
        // ordinary fit (no clamping needed) behaves exactly like before.
        var rects = new[] { new L.FlowRect(0, 0, 100, 100), new L.FlowRect(200, 0, 100, 100) };
        var withAlign = L.FlowGeometry.FitView(rects, 800, 600, 0, 0.25, 2, new L.FlowRect(0, 0, 100, 100), L.FlowAnchorAlign.Start, rtl: true);
        var plain = L.FlowGeometry.FitView(rects, 800, 600, 0, 0.25, 2);
        Assert.Equal(plain, withAlign);
    }

    // ── LU-19: end-to-end through FlowCanvas.FitViewAsync (.NET-computed path — every node sized) ──

    [Fact]
    public async Task FitViewAsync_Options_AnchorAlign_Start_Puts_The_Anchor_At_The_Leading_Edge()
    {
        var nodes = new List<L.FlowNode>
        {
            new("root", 0, 0, Width: 100, Height: 100),
            new("far", 5000, 5000, Width: 100, Height: 100),
        };
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, nodes)
            .Add(c => c.FitViewOnInit, false)
            .Add(c => c.FitViewPadding, 0.1));
        await cut.InvokeAsync(() => cut.Instance.PaneResized(800, 600));

        await cut.InvokeAsync(() => cut.Instance.FitViewAsync(
            new L.FlowFitViewOptions(MinZoom: 0.92, AnchorNodeId: "root", AnchorAlign: L.FlowAnchorAlign.Start)));

        var vp = cut.Instance.CurrentViewport;
        Assert.Equal(0.92, vp.Zoom, 6);
        Assert.Equal(80, vp.X, 3);
        Assert.Equal(60, vp.Y, 3);
    }

    [Fact]
    public async Task FitViewAsync_Options_AnchorAlign_End_Puts_The_Anchor_At_The_Trailing_Edge()
    {
        var nodes = new List<L.FlowNode>
        {
            new("root", 0, 0, Width: 100, Height: 100),
            new("far", 5000, 5000, Width: 100, Height: 100),
        };
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, nodes)
            .Add(c => c.FitViewOnInit, false)
            .Add(c => c.FitViewPadding, 0.1));
        await cut.InvokeAsync(() => cut.Instance.PaneResized(800, 600));

        await cut.InvokeAsync(() => cut.Instance.FitViewAsync(
            new L.FlowFitViewOptions(MinZoom: 0.92, AnchorNodeId: "root", AnchorAlign: L.FlowAnchorAlign.End)));

        var vp = cut.Instance.CurrentViewport;
        Assert.Equal(0.92, vp.Zoom, 6);
        Assert.Equal(800 - 80 - 100 * 0.92, vp.X, 3);
        Assert.Equal(600 - 60 - 100 * 0.92, vp.Y, 3);
    }

    [Fact]
    public async Task FitViewAsync_Options_AnchorAlign_Center_Keeps_The_Pre_LU19_Behaviour()
    {
        var nodes = new List<L.FlowNode>
        {
            new("root", 0, 0, Width: 100, Height: 100),
            new("far", 5000, 5000, Width: 100, Height: 100),
        };
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, nodes)
            .Add(c => c.FitViewOnInit, false)
            .Add(c => c.FitViewPadding, 0.1));
        await cut.InvokeAsync(() => cut.Instance.PaneResized(800, 600));

        await cut.InvokeAsync(() => cut.Instance.FitViewAsync(new L.FlowFitViewOptions(MinZoom: 0.92, AnchorNodeId: "root")));

        var vp = cut.Instance.CurrentViewport;
        Assert.Equal(0.92, vp.Zoom, 6);
        Assert.Equal(400 - 50 * 0.92, vp.X, 3);
        Assert.Equal(300 - 50 * 0.92, vp.Y, 3);
    }

    [Fact]
    public async Task FitViewAsync_Options_AnchorAlign_Start_Is_Rtl_Aware_Under_A_DirectionProvider()
    {
        var nodes = new List<L.FlowNode>
        {
            new("root", 0, 0, Width: 100, Height: 100),
            new("far", 5000, 5000, Width: 100, Height: 100),
        };

        var cut = Ctx.Render(builder =>
        {
            builder.OpenComponent<L.DirectionProvider>(0);
            builder.AddAttribute(1, "Direction", LayoutDirection.Rtl);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<L.FlowCanvas>(0);
                inner.AddAttribute(1, "Nodes", (IReadOnlyList<L.FlowNode>)nodes);
                inner.AddAttribute(2, "FitViewOnInit", false);
                inner.AddAttribute(3, "FitViewPadding", 0.1);
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var flow = cut.FindComponent<L.FlowCanvas>();
        await flow.InvokeAsync(() => flow.Instance.PaneResized(800, 600));
        await flow.InvokeAsync(() => flow.Instance.FitViewAsync(
            new L.FlowFitViewOptions(MinZoom: 0.92, AnchorNodeId: "root", AnchorAlign: L.FlowAnchorAlign.Start)));

        var vp = flow.Instance.CurrentViewport;
        Assert.Equal(0.92, vp.Zoom, 6);
        // Under RTL, "Start" is the anchor's RIGHT edge against the pane's right edge.
        Assert.Equal(800 - 80 - 100 * 0.92, vp.X, 3);
        // Vertical is unaffected by direction.
        Assert.Equal(60, vp.Y, 3);
    }
}
