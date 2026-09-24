using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// Findings LU-02, LU-03, LU-05, LU-06, LU-07, LU-08, LU-09 from the SQL Analyst (Blazor Server)
/// field report against 5.11.0. LU-01/LU-10 (FlowLayout) live in <see cref="FlowLayoutTests"/>;
/// LU-04 in <see cref="FlowCanvasEdgeRenderingTests"/>; LU-11 in <see cref="FlowCanvasRenderingTests"/>.
/// </summary>
public class FlowCanvasSqlAnalystFindingsTests : FlowCanvasTestBase
{
    private static Microsoft.AspNetCore.Components.RenderFragment<L.FlowNodeContext> NodeTemplateWithHandles => ctx => builder =>
    {
        builder.OpenElement(0, "div");
        builder.OpenComponent<L.FlowHandle>(1);
        builder.AddAttribute(2, nameof(L.FlowHandle.Type), L.FlowHandleType.Source);
        builder.CloseComponent();
        builder.OpenComponent<L.FlowHandle>(3);
        builder.AddAttribute(4, nameof(L.FlowHandle.Type), L.FlowHandleType.Target);
        builder.CloseComponent();
        builder.CloseElement();
    };

    // ── LU-02: FlowEdge.Class / Style ──────────────────────────────────────

    [Fact]
    public void Edge_Class_Is_Merged_Onto_The_Path_And_Style_Wins_Last()
    {
        var edges = new List<L.FlowEdge> { new("a-b", "a", "b", Class: "critical-edge", Style: "stroke:red;") };
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.Edges, edges));

        var path = cut.Find("[data-flow-edge][data-edge-id='a-b']");
        Assert.Contains("critical-edge", path.ClassList);
        // Style is appended after the library's own declarations, so it wins in the cascade.
        Assert.EndsWith("stroke:red;", path.GetAttribute("style"));
    }

    [Fact]
    public void Edge_Without_Class_Or_Style_Renders_Unaffected()
    {
        var edges = new List<L.FlowEdge> { new("a-b", "a", "b") };
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.Edges, edges));
        var path = cut.Find("[data-flow-edge][data-edge-id='a-b']");
        Assert.Empty(path.ClassList);
    }

    // ── LU-03: marker follows the edge colour ──────────────────────────────

    [Fact]
    public void Selected_And_Unselected_Edges_Use_Different_Markers()
    {
        var edges = new List<L.FlowEdge> { new("a-b", "a", "b") };
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.Edges, edges).Add(c => c.ElementsSelectable, true));

        var path = cut.Find("[data-flow-edge][data-edge-id='a-b']");
        var defaultMarkerUrl = path.GetAttribute("marker-end");
        Assert.StartsWith("url(#", defaultMarkerUrl);
        var defaultMarkerId = defaultMarkerUrl!.Substring(5).TrimEnd(')');
        var defaultMarker = cut.Find("marker#" + defaultMarkerId);
        // The default marker's arrowhead follows --lumeo-flow-edge-stroke (same token the edge's
        // own stroke reads), not a hardcoded colour — so a consumer override reaches it too.
        Assert.Contains("var(--lumeo-flow-edge-stroke", defaultMarker.QuerySelector("path")!.GetAttribute("fill"));

        cut.Find("[data-flow-edge][data-edge-id='a-b']").Click();

        var selectedMarkerUrl = cut.Find("[data-flow-edge][data-edge-id='a-b']").GetAttribute("marker-end");
        Assert.NotEqual(defaultMarkerUrl, selectedMarkerUrl);
        var selectedMarkerId = selectedMarkerUrl!.Substring(5).TrimEnd(')');
        var selectedMarker = cut.Find("marker#" + selectedMarkerId);
        Assert.Contains("var(--color-primary)", selectedMarker.QuerySelector("path")!.GetAttribute("fill"));
    }

    // ── LU-05: handles are not tab stops (and are aria-hidden) when connecting is impossible ──

    [Fact]
    public void Handles_Are_Not_Tab_Stops_When_The_Canvas_Is_Readonly()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, new List<L.FlowNode> { new("a", 0, 0) })
            .Add(c => c.Readonly, true)
            .Add(c => c.NodeTemplate, NodeTemplateWithHandles));

        var handles = cut.FindAll("[data-flow-node='a'] [data-flow-handle]");
        Assert.Equal(2, handles.Count);
        Assert.All(handles, h => Assert.Equal("-1", h.GetAttribute("tabindex")));
        Assert.All(handles, h => Assert.Equal("true", h.GetAttribute("aria-hidden")));
        // Still visible as ports: no display:none / hidden attribute.
        Assert.All(handles, h => Assert.Null(h.GetAttribute("hidden")));
    }

    [Fact]
    public void Handles_Are_Not_Tab_Stops_When_NodesConnectable_Is_Off()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, new List<L.FlowNode> { new("a", 0, 0) })
            .Add(c => c.NodesConnectable, false)
            .Add(c => c.NodeTemplate, NodeTemplateWithHandles));

        Assert.All(cut.FindAll("[data-flow-node='a'] [data-flow-handle]"), h => Assert.Equal("-1", h.GetAttribute("tabindex")));
    }

    [Fact]
    public void Handles_Are_Not_Tab_Stops_When_The_Nodes_Own_Connectable_Is_Off()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, new List<L.FlowNode> { new("a", 0, 0, Connectable: false) })
            .Add(c => c.NodeTemplate, NodeTemplateWithHandles));

        Assert.All(cut.FindAll("[data-flow-node='a'] [data-flow-handle]"), h => Assert.Equal("-1", h.GetAttribute("tabindex")));
    }

    [Fact]
    public void Handles_Stay_Tab_Stops_When_Connecting_Is_Actually_Possible()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, new List<L.FlowNode> { new("a", 0, 0) })
            .Add(c => c.NodeTemplate, NodeTemplateWithHandles));

        var handles = cut.FindAll("[data-flow-node='a'] [data-flow-handle]");
        Assert.All(handles, h => Assert.Equal("0", h.GetAttribute("tabindex")));
        Assert.All(handles, h => Assert.Null(h.GetAttribute("aria-hidden")));
    }

    // ── LU-06: Enter/Space on a focused node raises OnNodeClick ────────────

    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")]
    public void Enter_Or_Space_On_A_Focused_Node_Raises_OnNodeClick(string key)
    {
        L.FlowNode? clicked = null;
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .Add(c => c.OnNodeClick, (L.FlowNode n) => clicked = n));

        cut.Find("[data-flow-node='b']").KeyDown(new KeyboardEventArgs { Key = key });

        Assert.NotNull(clicked);
        Assert.Equal("b", clicked!.Id);
    }

    [Fact]
    public void Enter_On_A_Focused_Node_Also_Selects_It_Like_A_Click_Would()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()));
        cut.Find("[data-flow-node='b']").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.NotNull(cut.Find("[data-flow-node='b']").GetAttribute("data-selected"));
    }

    [Fact]
    public void Space_From_An_Editable_Target_Does_Not_Raise_OnNodeClick()
    {
        // Same editable-target guard as the arrow-nudge and the global shortcuts: typing a literal
        // space into a node template's own input must not also "click" the node.
        L.FlowNode? clicked = null;
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .Add(c => c.OnNodeClick, (L.FlowNode n) => clicked = n));
        Interop.FlowFocusedElementEditable = true;

        cut.Find("[data-flow-node='b']").KeyDown(new KeyboardEventArgs { Key = " " });

        Assert.Null(clicked);
    }

    // ── LU-07: FitViewAsync re-measures the pane fresh before computing a fit ──

    [Fact]
    public async Task FitViewAsync_Uses_A_Freshly_Measured_Pane_Size_Not_The_Stale_Report()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0, Width: 100, Height: 100), new("b", 400, 300, Width: 100, Height: 100) };
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, nodes).Add(c => c.FitViewOnInit, false).Add(c => c.FitViewPadding, 0));
        // The last PaneResized report says 400x300 (a container size BEFORE it grew) ...
        await cut.InvokeAsync(() => cut.Instance.PaneResized(400, 300));
        // ... but the engine's live, freshly-measured size (what a real getBoundingClientRect
        // would return right now) is already 800x600 — the ResizeObserver's own report just
        // has not arrived yet. FitViewAsync must use THIS size, not the stale 400x300 one.
        Interop.FlowFreshPaneSize = new double[] { 800, 600 };

        await cut.InvokeAsync(() => cut.Instance.FitViewAsync());

        Assert.True(Interop.FlowGetPaneSizeCallCount > 0);
        var expected = L.FlowGeometry.FitView(new[] { new L.FlowRect(0, 0, 100, 100), new L.FlowRect(400, 300, 100, 100) }, 800, 600, 0, 0.25, 2)!.Value;
        Assert.Equal(expected, cut.Instance.CurrentViewport);
        // The refreshed size is also remembered for next time.
        Assert.Equal((800d, 600d), cut.Instance.PaneSize);
    }

    [Fact]
    public async Task GetPaneSizeAsync_Returns_The_Live_Size_And_Falls_Back_To_The_Last_Known_One()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.FitViewOnInit, false));
        await cut.InvokeAsync(() => cut.Instance.PaneResized(300, 200));

        // No fresher size from the engine (simulates "no registered engine yet" / a no-op interop).
        Interop.FlowFreshPaneSize = null;
        Assert.Equal((300d, 200d), await cut.InvokeAsync(() => cut.Instance.GetPaneSizeAsync()));

        Interop.FlowFreshPaneSize = new double[] { 900, 700 };
        Assert.Equal((900d, 700d), await cut.InvokeAsync(() => cut.Instance.GetPaneSizeAsync()));
        Assert.Equal((900d, 700d), cut.Instance.PaneSize);
    }

    // ── LU-08: FitViewAsync(options) — MinZoom/MaxZoom override, anchor, OnFitView ──

    [Fact]
    public async Task FitViewAsync_Options_Overrides_MinZoom_And_Anchors_On_A_Node_When_Clamped()
    {
        // A graph much larger than the pane: the plain fit would need a tiny zoom, well under the
        // MinZoom override below. With an anchor, the canvas centres on that node AT MinZoom
        // instead of zooming out further to show everything.
        var nodes = new List<L.FlowNode>
        {
            new("root", 0, 0, Width: 100, Height: 100),
            new("far", 5000, 5000, Width: 100, Height: 100),
        };
        L.FlowViewport? fitted = null;
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, nodes)
            .Add(c => c.FitViewOnInit, false)
            .Add(c => c.OnFitView, (L.FlowViewport v) => fitted = v));
        await cut.InvokeAsync(() => cut.Instance.PaneResized(800, 600));

        await cut.InvokeAsync(() => cut.Instance.FitViewAsync(new L.FlowFitViewOptions(MinZoom: 0.92, AnchorNodeId: "root")));

        var vp = cut.Instance.CurrentViewport;
        Assert.Equal(0.92, vp.Zoom, 6);
        // Centred on "root" (50,50) at zoom 0.92, not on the whole bounds' centre.
        var expectedX = 400 - 50 * 0.92;
        var expectedY = 300 - 50 * 0.92;
        Assert.Equal(expectedX, vp.X, 3);
        Assert.Equal(expectedY, vp.Y, 3);
        Assert.Equal(vp, fitted);
    }

    [Fact]
    public async Task FitViewAsync_Options_Without_Clamping_Behaves_Like_The_Plain_Fit()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0, Width: 100, Height: 100), new("b", 200, 0, Width: 100, Height: 100) };
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, nodes).Add(c => c.FitViewOnInit, false).Add(c => c.FitViewPadding, 0));
        await cut.InvokeAsync(() => cut.Instance.PaneResized(800, 600));

        await cut.InvokeAsync(() => cut.Instance.FitViewAsync(new L.FlowFitViewOptions(AnchorNodeId: "a")));

        var expected = L.FlowGeometry.FitView(new[] { new L.FlowRect(0, 0, 100, 100), new L.FlowRect(200, 0, 100, 100) }, 800, 600, 0, 0.25, 2)!.Value;
        Assert.Equal(expected, cut.Instance.CurrentViewport);
    }

    [Fact]
    public async Task OnFitView_Fires_For_The_Plain_FitViewAsync_Too()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0, Width: 100, Height: 100) };
        L.FlowViewport? fitted = null;
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, nodes)
            .Add(c => c.FitViewOnInit, false)
            .Add(c => c.OnFitView, (L.FlowViewport v) => fitted = v));
        await cut.InvokeAsync(() => cut.Instance.PaneResized(800, 600));

        await cut.InvokeAsync(() => cut.Instance.FitViewAsync());

        Assert.NotNull(fitted);
        Assert.Equal(cut.Instance.CurrentViewport, fitted!.Value);
    }

    [Fact]
    public async Task FitCompleted_Engine_Callback_Raises_OnFitView()
    {
        // Simulates the engine's own DOM-measured fit (e.g. FitViewOnInit) reporting completion —
        // no application call to FitViewAsync was awaited for this one.
        L.FlowViewport? fitted = null;
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .Add(c => c.FitViewOnInit, false)
            .Add(c => c.OnFitView, (L.FlowViewport v) => fitted = v));

        await cut.InvokeAsync(() => cut.Instance.FitCompleted(10, 20, 1.5));

        Assert.Equal(new L.FlowViewport(10, 20, 1.5), fitted);
    }

    // ── LU-09: default edge label pill does not wrap ───────────────────────

    [Fact]
    public void Default_Edge_Label_Pill_Does_Not_Wrap_And_Truncates()
    {
        var edges = new List<L.FlowEdge> { new("a-b", "a", "b", Label: "owns · a very long label that should not wrap") };
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.Edges, edges));

        var label = cut.Find("[data-flow-edge-label] span");
        Assert.Contains("whitespace-nowrap", label.ClassList);
        Assert.Contains("overflow-hidden", label.ClassList);
        Assert.Contains("text-ellipsis", label.ClassList);
    }
}
