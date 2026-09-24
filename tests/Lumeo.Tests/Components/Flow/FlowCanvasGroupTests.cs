using System.Globalization;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// Phase 5 sub-flows on a live canvas: hosts sit at ABSOLUTE positions while the model keeps
/// children relative; a drag commit (the engine reports absolute positions) converts back; a
/// dragged group's children are not re-reported; Extent=Parent clamps on commit and on arrow keys;
/// z-order; Ctrl+G / Ctrl+Shift+G; subtree delete; clipboard subtrees; resize compensation;
/// document round-trip; group a11y.
/// </summary>
public class FlowCanvasGroupTests : FlowCanvasTestBase
{
    private static List<L.FlowNode> Scene() => new()
    {
        new L.FlowNode("g", 100, 50, Type: "group", Data: "Billing", Width: 400, Height: 300),
        new L.FlowNode("a", 20, 40, Width: 100, Height: 40, ParentId: "g", Extent: L.FlowExtent.Parent),
        new L.FlowNode("b", 200, 40, Width: 100, Height: 40, ParentId: "g"),
        new L.FlowNode("o", 700, 0, Width: 100, Height: 40),
    };

    private static List<L.FlowEdge> SceneEdges() => new()
    {
        new L.FlowEdge("a-b", "a", "b"),
        new L.FlowEdge("b-o", "b", "o"),
    };

    private static string Attr(IRenderedComponent<L.FlowCanvas> cut, string id, string attr)
        => cut.Find($"[data-flow-node='{id}']").GetAttribute(attr) ?? "";

    [Fact]
    public void Hosts_Sit_At_Absolute_Positions_While_The_Model_Stays_Relative()
    {
        var (cut, current) = RenderBound(Scene());

        Assert.Equal("120", Attr(cut, "a", "data-x"));
        Assert.Equal("90", Attr(cut, "a", "data-y"));
        Assert.Contains("translate(120px, 90px)", Attr(cut, "a", "style"));
        Assert.Equal("g", Attr(cut, "a", "data-parent-id"));
        Assert.Equal("parent", Attr(cut, "a", "data-extent"));
        Assert.Equal("", Attr(cut, "b", "data-extent")); // Extent None renders no attribute
        Assert.Null(cut.Find("[data-flow-node='b']").GetAttribute("data-extent"));
        Assert.Equal(20, Node(current(), "a").X); // model untouched
        Assert.Equal(new L.FlowPoint(120, 90), cut.Instance.GetAbsolutePosition("a"));
        Assert.Null(cut.Instance.GetAbsolutePosition("missing"));
    }

    [Fact]
    public void Parents_Render_Before_Children_And_Children_Stack_Above_Their_Group()
    {
        var nodes = Scene();
        nodes.Reverse(); // children listed before their group
        var (cut, _) = RenderBound(nodes);

        var order = cut.FindAll("[data-flow-node]").Select(e => e.GetAttribute("data-flow-node")).ToList();
        Assert.True(order.IndexOf("g") < order.IndexOf("a"));
        Assert.True(order.IndexOf("g") < order.IndexOf("b"));
        Assert.Contains("z-index:1;", Attr(cut, "a", "style"));
        Assert.DoesNotContain("z-index", Attr(cut, "g", "style"));
    }

    [Fact]
    public void An_Edge_Between_Nested_Nodes_Draws_In_A_Raised_Layer()
    {
        var (cut, _, _) = RenderBoundWithEdges(Scene(), SceneEdges());

        var raised = cut.Find("[data-slot='flow-edges-raised']");
        Assert.Equal("1", raised.GetAttribute("data-level"));
        Assert.Contains("z-index:1;", raised.GetAttribute("style"));
        // Both edges touch a nested node (depth 1), so both sit in the raised layer.
        Assert.NotNull(raised.QuerySelector("[data-flow-edge][data-edge-id='a-b']"));
        Assert.NotNull(raised.QuerySelector("[data-flow-edge][data-edge-id='b-o']"));
        Assert.Null(cut.Find("[data-slot='flow-edges']").QuerySelector("[data-flow-edge][data-edge-id='a-b']"));
    }

    [Fact]
    public void Edge_Paths_Use_Absolute_Anchors_And_Stamp_Them()
    {
        var (cut, _, _) = RenderBoundWithEdges(Scene(), SceneEdges());
        var path = cut.Find("[data-flow-edge][data-edge-id='a-b']");
        // a's right edge midpoint: (120 + 100, 90 + 20); b's left edge midpoint: (300, 110).
        Assert.Equal("220|110|right", path.GetAttribute("data-sa"));
        Assert.Equal("300|110|left", path.GetAttribute("data-ta"));
        Assert.StartsWith("M220,110", path.GetAttribute("d"));
    }

    [Fact]
    public async Task Dragging_A_Group_Commits_Only_The_Group_Its_Children_Stay_Relative()
    {
        var (cut, current) = RenderBound(Scene());
        IReadOnlyList<L.FlowNodeChange>? stop = null;
        cut.Render(p => p.Add(c => c.OnNodeDragStop, (IReadOnlyList<L.FlowNodeChange> c) => stop = c));

        // The engine moves the group AND its children live and reports everyone's ABSOLUTE position.
        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[]
        {
            new L.FlowNodeChange("g", 150, 80),
            new L.FlowNodeChange("a", 170, 120),
            new L.FlowNodeChange("b", 350, 120),
        }, cut.Instance._state.Generation));

        Assert.True(accepted);
        Assert.Equal((150d, 80d), (Node(current(), "g").X, Node(current(), "g").Y));
        Assert.Equal((20d, 40d), (Node(current(), "a").X, Node(current(), "a").Y));
        Assert.Equal((200d, 40d), (Node(current(), "b").X, Node(current(), "b").Y));
        Assert.Equal(new[] { "g" }, stop!.Select(c => c.Id));
        Assert.Equal("170", Attr(cut, "a", "data-x")); // re-rendered at the new absolute position
    }

    [Fact]
    public async Task Dragging_A_Child_Commits_A_Position_Relative_To_Its_Parent()
    {
        var (cut, current) = RenderBound(Scene());
        await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("b", 300, 150) }, cut.Instance._state.Generation));
        Assert.Equal((200d, 100d), (Node(current(), "b").X, Node(current(), "b").Y));
    }

    [Fact]
    public async Task An_Extent_Parent_Child_Is_Clamped_Inside_Its_Parent_On_Commit()
    {
        var (cut, current) = RenderBound(Scene());
        // Dropped far outside the group: clamped to its bottom-right corner (400-100, 300-40).
        await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("a", 2000, 2000) }, cut.Instance._state.Generation));
        Assert.Equal((300d, 260d), (Node(current(), "a").X, Node(current(), "a").Y));

        // Extent None: b may leave the group (it still moves with it).
        await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("b", 2000, 2000) }, cut.Instance._state.Generation));
        Assert.Equal((1900d, 1950d), (Node(current(), "b").X, Node(current(), "b").Y));
    }

    [Fact]
    public void Arrow_Keys_Clamp_An_Extent_Parent_Child_At_Its_Parents_Edge()
    {
        var nodes = Scene();
        nodes[1] = nodes[1] with { X = 0, Y = 0 };
        var (cut, current) = RenderBound(nodes);
        var commits = 0;
        cut.Render(p => p.Add(c => c.OnNodeDragStop, (IReadOnlyList<L.FlowNodeChange> _) => commits++));

        cut.Find("[data-flow-node='a']").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });

        Assert.Equal(0, Node(current(), "a").X);
        Assert.Equal(0, commits); // pinned: nothing changed, nothing committed

        cut.Find("[data-flow-node='a']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal(1, Node(current(), "a").X);
    }

    [Fact]
    public void Arrow_Keys_On_A_Selected_Group_And_Child_Move_The_Child_Only_Once()
    {
        var (cut, current) = RenderBound(Scene());
        cut.Find("[data-flow-node='g']").Click();
        cut.Find("[data-flow-node='b']").Click(new MouseEventArgs { ShiftKey = true });

        cut.Find("[data-flow-node='g']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal(101, Node(current(), "g").X);
        Assert.Equal(200, Node(current(), "b").X); // relative — travels with g, not moved twice
    }

    [Fact]
    public void CtrlG_Groups_The_Selection_Into_A_New_Group_At_Unchanged_Absolute_Positions()
    {
        var nodes = new List<L.FlowNode>
        {
            new("x", 100, 100, Width: 100, Height: 40),
            new("y", 300, 200, Width: 100, Height: 40),
            new("z", 900, 900, Width: 100, Height: 40),
        };
        var (cut, current) = RenderBound(nodes, p => p.Add(c => c.NewNodeId, n => n.Type == L.FlowGroupNode.GroupType ? "grp" : "copy"));
        cut.Find("[data-flow-node='x']").Click();
        cut.Find("[data-flow-node='y']").Click(new MouseEventArgs { ShiftKey = true });

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "g", CtrlKey = true });

        var list = current();
        var grp = Node(list, "grp");
        Assert.Equal(L.FlowGroupNode.GroupType, grp.Type);
        Assert.Null(grp.ParentId);
        // bounds (100,100)-(400,240), padding 20, label row 28.
        Assert.Equal((80d, 52d), (grp.X, grp.Y));
        Assert.Equal((340d, 208d), (grp.Width!.Value, grp.Height!.Value));
        Assert.Equal("grp", Node(list, "x").ParentId);
        Assert.Equal((20d, 48d), (Node(list, "x").X, Node(list, "x").Y));
        Assert.Equal((220d, 148d), (Node(list, "y").X, Node(list, "y").Y));
        Assert.Null(Node(list, "z").ParentId);
        Assert.True(list.ToList().FindIndex(n => n.Id == "grp") < list.ToList().FindIndex(n => n.Id == "x"));
        Assert.Equal("100", Attr(cut, "x", "data-x")); // absolute position unchanged on screen
        Assert.True(cut.Instance.IsSelected("grp"));
        Assert.Contains("2", cut.Find("[aria-live='polite']").TextContent);
    }

    [Fact]
    public void CtrlShiftG_Dissolves_A_Selected_Group()
    {
        var (cut, current, currentEdges) = RenderBoundWithEdges(Scene(),
            new List<L.FlowEdge> { new("g-o", "g", "o"), new("a-b", "a", "b") });
        cut.Find("[data-flow-node='g']").Click();

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "G", CtrlKey = true, ShiftKey = true });

        var list = current();
        Assert.DoesNotContain(list, n => n.Id == "g");
        Assert.Null(Node(list, "a").ParentId);
        Assert.Equal((120d, 90d), (Node(list, "a").X, Node(list, "a").Y)); // absolute kept
        Assert.Equal((300d, 90d), (Node(list, "b").X, Node(list, "b").Y));
        Assert.Equal(new[] { "a-b" }, currentEdges().Select(e => e.Id)); // the group's own edge is gone
        Assert.True(cut.Instance.IsSelected("a") && cut.Instance.IsSelected("b"));
    }

    [Fact]
    public void CtrlShiftG_On_A_Child_Takes_It_Out_Of_Its_Group()
    {
        var (cut, current) = RenderBound(Scene());
        cut.Find("[data-flow-node='b']").Click();

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "G", CtrlKey = true, ShiftKey = true });

        Assert.Null(Node(current(), "b").ParentId);
        Assert.Equal((300d, 90d), (Node(current(), "b").X, Node(current(), "b").Y));
        Assert.Contains(current(), n => n.Id == "g");
    }

    [Fact]
    public void CtrlG_From_An_Editable_Target_Does_Nothing()
    {
        var (cut, current) = RenderBound(Scene());
        cut.Find("[data-flow-node='o']").Click();
        Interop.FlowFocusedElementEditable = true;

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "g", CtrlKey = true });

        Assert.Equal(4, current().Count);
    }

    [Fact]
    public void Deleting_A_Group_Deletes_Its_Subtree_And_Every_Edge_Touching_It()
    {
        var nodes = Scene();
        nodes.Add(new L.FlowNode("deep", 5, 5, ParentId: "a"));
        var (cut, current, currentEdges) = RenderBoundWithEdges(nodes, SceneEdges());
        cut.Find("[data-flow-node='g']").Click();

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "Delete" });

        Assert.Equal(new[] { "o" }, current().Select(n => n.Id));
        Assert.Empty(currentEdges());
    }

    [Fact]
    public void A_Non_Deletable_Child_Survives_Its_Groups_Delete_At_The_Same_Absolute_Position()
    {
        var nodes = Scene();
        nodes[2] = nodes[2] with { Deletable = false };
        var (cut, current) = RenderBound(nodes);
        cut.Find("[data-flow-node='g']").Click();

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "Delete" });

        var b = Node(current(), "b");
        Assert.Null(b.ParentId);
        Assert.Equal((300d, 90d), (b.X, b.Y));
        Assert.DoesNotContain(current(), n => n.Id == "a");
    }

    [Fact]
    public void OnDelete_Receives_The_Whole_Subtree()
    {
        L.FlowSelection? seen = null;
        var (cut, _) = RenderBound(Scene(), p => p.Add(c => c.OnDelete, (L.FlowSelection s) => seen = s));
        cut.Find("[data-flow-node='g']").Click();
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "Delete" });
        Assert.Equal(new[] { "a", "b", "g" }, seen!.NodeIds.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void Duplicating_A_Group_Duplicates_Its_Children_Inside_The_Copy()
    {
        var n = 0;
        var (cut, current, currentEdges) = RenderBoundWithEdges(Scene(), SceneEdges(), p => p.Add(c => c.NewNodeId, _ => "n" + (++n)));
        cut.Find("[data-flow-node='g']").Click();

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "d", CtrlKey = true });

        var list = current();
        Assert.Equal(7, list.Count);
        var copyGroup = Node(list, "n1");
        Assert.Equal((120d, 70d), (copyGroup.X, copyGroup.Y)); // the group moves by (20, 20)
        var copyA = Node(list, "n2");
        Assert.Equal("n1", copyA.ParentId);
        Assert.Equal((20d, 40d), (copyA.X, copyA.Y)); // the child keeps its relative position
        Assert.Contains(currentEdges(), e => e.Source == "n2" && e.Target == "n3"); // a-b remapped
        Assert.DoesNotContain(currentEdges(), e => e.Source == "n3" && e.Target == "o"); // b-o leaves the copy
    }

    [Fact]
    public async Task Resizing_A_Group_From_Its_West_Edge_Keeps_Its_Children_In_Place_On_Screen()
    {
        var (cut, current) = RenderBound(Scene());
        // The engine reports the ABSOLUTE rect: the west grip moved the group's left edge 30 left.
        await cut.InvokeAsync(() => cut.Instance.CommitNodeResize("g", 70, 50, 430, 300, cut.Instance._state.Generation));

        var list = current();
        Assert.Equal((70d, 430d), (Node(list, "g").X, Node(list, "g").Width!.Value));
        Assert.Equal(50, Node(list, "a").X); // was 20; +30 keeps the absolute 120
        Assert.Equal("120", Attr(cut, "a", "data-x"));
    }

    [Fact]
    public async Task Resizing_A_Child_Commits_A_Relative_Position()
    {
        var (cut, current) = RenderBound(Scene());
        await cut.InvokeAsync(() => cut.Instance.CommitNodeResize("b", 290, 90, 150, 60, cut.Instance._state.Generation));
        Assert.Equal((190d, 40d, 150d), (Node(current(), "b").X, Node(current(), "b").Y, Node(current(), "b").Width!.Value));
    }

    [Fact]
    public void A_Group_Announces_Its_Child_Count()
    {
        var (cut, _) = RenderBound(Scene());
        var g = cut.Find("[data-flow-node='g']");
        Assert.Equal("g, 2 nodes", g.GetAttribute("aria-label"));
        Assert.Equal("group", g.GetAttribute("aria-roledescription"));
        Assert.NotNull(g.GetAttribute("data-flow-group"));
        Assert.Equal("node", cut.Find("[data-flow-node='a']").GetAttribute("aria-roledescription"));
    }

    [Fact]
    public void Without_A_NodeTemplate_A_Group_Node_Renders_FlowGroupNode_With_Its_Label()
    {
        var (cut, _) = RenderBound(Scene());
        var box = cut.Find("[data-flow-node='g'] [data-slot='flow-group']");
        Assert.Contains("Billing", box.QuerySelector("[data-slot='flow-group-label']")!.TextContent);
        Assert.Contains("bg-primary/5", box.GetAttribute("class"));

        cut.Find("[data-flow-node='g']").Click(); // selected: resize grips appear
        Assert.NotEmpty(cut.FindAll("[data-flow-node='g'] [data-flow-resize-handle]"));
    }

    [Fact]
    public void ParentId_And_Extent_Round_Trip_Through_The_Document_And_Json()
    {
        var (cut, _) = RenderBound(Scene());
        var json = JsonSerializer.Serialize(cut.Instance.ToDocument());
        var doc = JsonSerializer.Deserialize<L.FlowDocument>(json)!;

        var a = doc.Nodes.Single(n => n.Id == "a");
        Assert.Equal("g", a.ParentId);
        Assert.Equal(L.FlowExtent.Parent, a.Extent);
        Assert.Null(doc.Nodes.Single(n => n.Id == "o").ParentId);
        Assert.Equal(L.FlowExtent.None, doc.Nodes.Single(n => n.Id == "b").Extent);
    }

    [Fact]
    public void FitView_And_Selection_Bounds_Use_Absolute_Rects()
    {
        var (cut, _) = RenderBound(Scene());
        cut.Find("[data-flow-node='a']").Click();
        var bounds = cut.Instance.SelectedNodesBounds();
        Assert.Equal(new L.FlowRect(120, 90, 100, 40), bounds);
    }

    [Fact]
    public async Task ExportSvg_Draws_Groups_Under_The_Edges_At_Absolute_Positions()
    {
        var (cut, _, _) = RenderBoundWithEdges(Scene(), SceneEdges());
        var svg = await cut.InvokeAsync(() => cut.Instance.ExportSvgAsync());
        var groupAt = svg.IndexOf("<rect x=\"100\" y=\"50\" width=\"400\"", StringComparison.Ordinal);
        var firstEdge = svg.IndexOf("<path", StringComparison.Ordinal);
        Assert.True(groupAt > 0 && groupAt < firstEdge, svg);
        Assert.Contains("<rect x=\"120\" y=\"90\"", svg);
    }

    [Fact]
    public void MiniMap_Draws_Children_At_Absolute_Positions()
    {
        // A child at relative (0,0) of a group at (1000, 1000) must not land at the map's origin.
        var nodes = new List<L.FlowNode>
        {
            new("g", 1000, 1000, Width: 200, Height: 200),
            new("c", 0, 0, Width: 200, Height: 200, ParentId: "g"),
        };
        var (cut, _) = RenderBound(nodes, p => p.Add(c => c.ChildContent, b =>
        {
            b.OpenComponent<L.FlowMiniMap>(0);
            b.CloseComponent();
        }));
        var rects = cut.FindAll("[data-slot='flow-minimap'] svg > rect[fill-opacity]");
        Assert.Equal(2, rects.Count);
        Assert.Equal(rects[0].GetAttribute("x"), rects[1].GetAttribute("x"));
        Assert.Equal(rects[0].GetAttribute("y"), rects[1].GetAttribute("y"));
        _ = CultureInfo.InvariantCulture;
    }
}
