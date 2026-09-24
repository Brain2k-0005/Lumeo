using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// Phase 5 virtualization (<c>OnlyRenderVisibleNodes</c>): the window/hysteresis rules as pure
/// functions, then the canvas — only nodes in the viewport + one-viewport margin mount, edges
/// touching them draw, a final viewport report re-windows while a small pan does not, measurements
/// survive an unmount, and everything model-level (selection, delete, clipboard, keyboard moves,
/// minimap, fit-view) keeps working for off-screen nodes.
/// </summary>
public class FlowVirtualizationTests : FlowCanvasTestBase
{
    // ── Pure functions ───────────────────────────────────────────────────

    [Fact]
    public void ViewportRect_Is_The_Visible_Pane_In_Flow_Coordinates()
    {
        var r = L.FlowVirtualization.ViewportRect(new L.FlowViewport(-100, -50, 2), 800, 600);
        Assert.Equal(new L.FlowRect(50, 25, 400, 300), r);
        Assert.Null(L.FlowVirtualization.ViewportRect(new L.FlowViewport(0, 0, 1), 0, 600));
    }

    [Fact]
    public void The_Window_Adds_One_Viewport_Of_Margin_In_Every_Direction()
    {
        var w = L.FlowVirtualization.ComputeWindow(new L.FlowRect(0, 0, 800, 600));
        Assert.Equal(new L.FlowRect(-800, -600, 2400, 1800), w);
    }

    [Fact]
    public void Hysteresis_A_Pan_Under_Half_A_Viewport_Keeps_The_Window()
    {
        var built = new L.FlowRect(0, 0, 800, 600);
        Assert.False(L.FlowVirtualization.NeedsRewindow(built, built));
        Assert.False(L.FlowVirtualization.NeedsRewindow(built, built with { X = 399, Y = -299 }));
        Assert.True(L.FlowVirtualization.NeedsRewindow(built, built with { X = 401 }));
        Assert.True(L.FlowVirtualization.NeedsRewindow(built, built with { Y = -301 }));
        Assert.True(L.FlowVirtualization.NeedsRewindow(null, built));
    }

    [Fact]
    public void Hysteresis_Zoom_Rewindows_Only_Past_Half_Or_Double_The_Size()
    {
        var built = new L.FlowRect(0, 0, 800, 600);
        // Zoom in 1.5x around the centre: still inside, not below half the size.
        Assert.False(L.FlowVirtualization.NeedsRewindow(built, new L.FlowRect(133, 100, 533, 400)));
        // Zoom in past 2x: the window is now far bigger than needed.
        Assert.True(L.FlowVirtualization.NeedsRewindow(built, new L.FlowRect(250, 190, 300, 220)));
        // Zoom out exactly 2x around the centre: still (just) inside the half-margin band...
        Assert.False(L.FlowVirtualization.NeedsRewindow(built, new L.FlowRect(-400, -300, 1600, 1200)));
        // ...a little further out leaves it.
        Assert.True(L.FlowVirtualization.NeedsRewindow(built, new L.FlowRect(-480, -360, 1760, 1320)));
    }

    [Fact]
    public void Intersects_Ignores_Touching_Edges()
    {
        var a = new L.FlowRect(0, 0, 10, 10);
        Assert.True(L.FlowVirtualization.Intersects(a, new L.FlowRect(9, 9, 10, 10)));
        Assert.False(L.FlowVirtualization.Intersects(a, new L.FlowRect(10, 0, 10, 10)));
    }

    // ── Canvas ───────────────────────────────────────────────────────────

    // A 40 x 10 grid, 200 flow units apart: x = 0..7800, y = 0..1800.
    private static List<L.FlowNode> Grid()
    {
        var list = new List<L.FlowNode>();
        for (var r = 0; r < 10; r++)
            for (var c = 0; c < 40; c++)
                list.Add(new L.FlowNode($"n{r}-{c}", c * 200, r * 200, Width: 100, Height: 40));
        return list;
    }

    private static List<L.FlowEdge> GridEdges()
    {
        var list = new List<L.FlowEdge>();
        for (var r = 0; r < 10; r++)
            for (var c = 0; c < 39; c++)
                list.Add(new L.FlowEdge($"e{r}-{c}", $"n{r}-{c}", $"n{r}-{c + 1}"));
        return list;
    }

    private (IRenderedComponent<L.FlowCanvas> Cut, Func<IReadOnlyList<L.FlowNode>> Nodes, Func<IReadOnlyList<L.FlowEdge>> Edges) RenderVirtual(
        Action<ComponentParameterCollectionBuilder<L.FlowCanvas>>? extra = null)
    {
        var result = RenderBoundWithEdges(Grid(), GridEdges(), p =>
        {
            p.Add(c => c.OnlyRenderVisibleNodes, true);
            extra?.Invoke(p);
        });
        // The engine reports the pane size at registration.
        result.Cut.InvokeAsync(() => result.Cut.Instance.PaneResized(800, 600)).GetAwaiter().GetResult();
        return result;
    }

    private static HashSet<string> MountedIds(IRenderedComponent<L.FlowCanvas> cut)
        => cut.FindAll("[data-flow-node]").Select(e => e.GetAttribute("data-flow-node")!).ToHashSet();

    [Fact]
    public void Nothing_Mounts_Until_The_Pane_Has_A_Size()
    {
        var (cut, _, _) = RenderBoundWithEdges(Grid(), GridEdges(), p => p.Add(c => c.OnlyRenderVisibleNodes, true));
        Assert.Empty(cut.FindAll("[data-flow-node]"));
        Assert.Equal("", cut.Find("[data-slot='flow-canvas']").GetAttribute("data-flow-virtualized"));
    }

    [Fact]
    public void Only_Nodes_In_The_Viewport_Plus_One_Viewport_Margin_Mount()
    {
        var (cut, _, _) = RenderVirtual();
        var mounted = MountedIds(cut);

        // Viewport (0,0)-(800,600) at zoom 1 → window (-800,-600)-(1600,1200): columns 0..7, rows 0..5.
        Assert.Equal(8 * 6, mounted.Count);
        Assert.Contains("n0-0", mounted);
        Assert.Contains("n5-7", mounted);
        Assert.DoesNotContain("n0-8", mounted);
        Assert.DoesNotContain("n6-0", mounted);
        Assert.Equal(400, cut.Instance.CurrentNodes.Count); // the model keeps everything
    }

    [Fact]
    public void Only_Edges_Touching_A_Mounted_Node_Are_Drawn()
    {
        var (cut, _, _) = RenderVirtual();
        var edges = cut.FindAll("[data-flow-edge]").Select(e => e.GetAttribute("data-edge-id")).ToHashSet();
        Assert.True(edges.Contains("e0-7"), string.Join(",", edges.OrderBy(x => x)) + " | mounted " + MountedIds(cut).Count); // n0-7 (mounted) -> n0-8 (not): still drawn
        Assert.DoesNotContain("e0-8", edges); // both ends off-window
        Assert.DoesNotContain("e6-0", edges);
        Assert.Equal(6 * 8, edges.Count); // rows 0..5, edges 0..7
    }

    [Fact]
    public async Task A_Final_Viewport_Report_Far_Away_Rewindows()
    {
        var (cut, _, _) = RenderVirtual();
        var before = cut.Instance.RewindowCount;

        // Pan 4000 flow units to the right.
        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(-4000, 0, 1, final: true));

        var mounted = MountedIds(cut);
        Assert.Equal(before + 1, cut.Instance.RewindowCount);
        Assert.Contains("n0-20", mounted);
        Assert.DoesNotContain("n0-0", mounted);
    }

    [Fact]
    public async Task A_Small_Pan_Does_Not_Rewindow()
    {
        var (cut, _, _) = RenderVirtual();
        var before = cut.Instance.RewindowCount;
        var mountedBefore = MountedIds(cut);

        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(-300, -200, 1, final: true));

        Assert.Equal(before, cut.Instance.RewindowCount);
        Assert.Equal(mountedBefore, MountedIds(cut));
    }

    [Fact]
    public async Task An_Imperative_Viewport_Change_Rewindows()
    {
        var (cut, _, _) = RenderVirtual();
        await cut.InvokeAsync(() => cut.Instance.SetViewportAsync(new L.FlowViewport(-6000, -1200, 1)));
        Assert.Contains("n6-30", MountedIds(cut));
        Assert.DoesNotContain("n0-0", MountedIds(cut));
    }

    [Fact]
    public async Task Measurements_Survive_A_Node_Being_Unmounted()
    {
        var nodes = Grid().Select(n => n with { Width = null, Height = null }).ToList();
        var (cut, _, _) = RenderBoundWithEdges(nodes, GridEdges(), p => p.Add(c => c.OnlyRenderVisibleNodes, true));
        await cut.InvokeAsync(() => cut.Instance.PaneResized(800, 600));
        await cut.InvokeAsync(() => cut.Instance.NodesMeasured(new[] { new L.FlowNodeMeasurement("n0-0", 222, 77, null) }));

        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(-6000, 0, 1, final: true));
        Assert.DoesNotContain("n0-0", MountedIds(cut));

        var rect = cut.Instance.GetNodeRect(nodes[0]);
        Assert.Equal((222d, 77d), (rect.Width, rect.Height));
        Assert.Equal(222, cut.Instance.MeasuredSizes["n0-0"].Width);
    }

    [Fact]
    public async Task Selected_Off_Screen_Nodes_Stay_Selected_And_Delete_And_Copy_With_The_Selection()
    {
        var (cut, nodes, _) = RenderVirtual();
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(new[] { "n0-0", "n9-39" })); // n9-39 is not mounted
        Assert.DoesNotContain("n9-39", MountedIds(cut));

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "d", CtrlKey = true });
        Assert.Equal(402, nodes().Count);
        Assert.Contains(nodes(), n => n.X == 39 * 200 + 20 && n.Y == 9 * 200 + 20);

        await cut.InvokeAsync(() => cut.Instance.SelectAsync(new[] { "n0-0", "n9-39" }));
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "Delete" });
        Assert.DoesNotContain(nodes(), n => n.Id == "n9-39");
        Assert.DoesNotContain(nodes(), n => n.Id == "n0-0");
    }

    [Fact]
    public async Task Arrow_Keys_Move_An_Off_Screen_Selected_Node_Too()
    {
        var (cut, nodes, _) = RenderVirtual();
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(new[] { "n0-0", "n9-39" }));

        cut.Find("[data-flow-node='n0-0']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal(39 * 200 + 1, Node(nodes(), "n9-39").X);
    }

    [Fact]
    public async Task A_Drag_Of_The_Selection_Moves_Its_Unmounted_Members_By_The_Same_Delta()
    {
        var (cut, nodes, _) = RenderVirtual();
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(new[] { "n0-0", "n9-39" }));

        // Only the mounted member takes part in the live drag; the engine reports just that one.
        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("n0-0", 30, 40) }, cut.Instance._state.Generation));

        Assert.True(accepted);
        Assert.Equal((30d, 40d), (Node(nodes(), "n0-0").X, Node(nodes(), "n0-0").Y));
        Assert.Equal((39d * 200 + 30, 9d * 200 + 40), (Node(nodes(), "n9-39").X, Node(nodes(), "n9-39").Y));
    }

    [Fact]
    public void A_Mounted_Child_Brings_Its_Parent_Group()
    {
        var nodes = new List<L.FlowNode>
        {
            new("g", -5000, 0, Width: 100, Height: 100),    // far off-window, left
            new("c", 5050, 20, Width: 100, Height: 40, ParentId: "g"), // Extent None, absolute (50, 20): visible
        };
        var (cut, _) = RenderBound(nodes, p => p.Add(c => c.OnlyRenderVisibleNodes, true));
        cut.InvokeAsync(() => cut.Instance.PaneResized(800, 600)).GetAwaiter().GetResult();
        Assert.Equal(new[] { "g", "c" }, cut.FindAll("[data-flow-node]").Select(e => e.GetAttribute("data-flow-node")));
    }

    [Fact]
    public void The_MiniMap_Still_Draws_Every_Node()
    {
        var (cut, _, _) = RenderVirtual(p => p.Add(c => c.ChildContent, b =>
        {
            b.OpenComponent<L.FlowMiniMap>(0);
            b.CloseComponent();
        }));
        Assert.Equal(400, cut.FindAll("[data-slot='flow-minimap'] svg > rect[fill-opacity]").Count);
    }

    [Fact]
    public async Task FitView_On_A_Virtualized_Canvas_Fits_The_Model_Not_The_Mounted_Window()
    {
        var (cut, _, _) = RenderVirtual(p => p.Add(c => c.MinZoom, 0.01));
        await cut.InvokeAsync(() => cut.Instance.FitViewAsync());

        Assert.Empty(Interop.FlowFitViewCalls); // never the engine's DOM fit
        var v = cut.Instance.CurrentViewport;
        // Bounds 7900 x 1840 into 800 x 600 with 10% padding: width-bound, zoom = 800 / (7900 * 1.1).
        Assert.Equal(800 / (7900 * 1.1), v.Zoom, 6);
        Assert.Equal(400, cut.FindAll("[data-flow-node]").Count); // everything is on screen now
    }

    [Fact]
    public void FitViewOnInit_On_A_Virtualized_Canvas_Fits_From_The_Model_Once_The_Pane_Has_A_Size()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, Grid().Take(4).ToList())
            .Add(c => c.OnlyRenderVisibleNodes, true)
            .Add(c => c.FitViewOnInit, true));
        Assert.Empty(cut.FindAll("[data-flow-node]"));
        cut.InvokeAsync(() => cut.Instance.PaneResized(800, 600)).GetAwaiter().GetResult();

        Assert.NotEqual(new L.FlowViewport(0, 0, 1), cut.Instance.CurrentViewport);
        Assert.Equal(4, cut.FindAll("[data-flow-node]").Count);
        Assert.Contains("|1", cut.Find("[data-slot='flow-pane']").GetAttribute("data-flow-viewport")); // stamp id 1
    }

    [Fact]
    public void The_Engine_Is_Told_The_Canvas_Is_Virtualized()
    {
        var (cut, _, _) = RenderVirtual();
        var options = Assert.IsType<L.FlowCanvas.FlowEngineOptions>(Interop.LastFlowOptions);
        Assert.True(options.Virtualized);
    }

    [Fact]
    public void Without_Virtualization_Every_Node_Mounts_Before_Any_Pane_Size()
    {
        var (cut, _, _) = RenderBoundWithEdges(Grid(), GridEdges());
        Assert.Equal(400, cut.FindAll("[data-flow-node]").Count);
    }
}
