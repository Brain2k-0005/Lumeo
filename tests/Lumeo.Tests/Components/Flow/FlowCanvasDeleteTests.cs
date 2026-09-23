using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// Delete (phase 2): Delete/Backspace on the pane (bubbled from wherever focus is) deletes the
/// current selection. Without OnDelete the canvas removes selected, deletable nodes (and every edge
/// touching one of them) plus directly selected, deletable edges itself, raising
/// NodesChanged/EdgesChanged. With OnDelete the canvas hands off the FlowSelection and does nothing
/// itself.
/// </summary>
public class FlowCanvasDeleteTests : FlowCanvasTestBase
{
    [Fact]
    public async Task Delete_Removes_The_Selected_Node_And_Its_Touching_Edges()
    {
        var (cut, currentNodes, currentEdges) = RenderBoundWithEdges(ThreeNodes(), TwoEdges());
        cut.Find("[data-flow-node='b']").Click(); // touches both a-b and b-c

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "Delete" });

        Assert.DoesNotContain(currentNodes(), n => n.Id == "b");
        Assert.Equal(2, currentNodes().Count);
        Assert.Empty(currentEdges());
        Assert.Empty(cut.FindAll("[data-flow-node='b']"));
    }

    [Fact]
    public void Backspace_Also_Deletes()
    {
        var (cut, currentNodes, _) = RenderBoundWithEdges(ThreeNodes(), new List<L.FlowEdge>());
        cut.Find("[data-flow-node='a']").Click();
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "Backspace" });
        Assert.DoesNotContain(currentNodes(), n => n.Id == "a");
    }

    [Fact]
    public void A_Node_With_Deletable_False_Survives_Delete()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0, Deletable: false), new("b", 100, 0) };
        var (cut, currentNodes, _) = RenderBoundWithEdges(nodes, new List<L.FlowEdge>());
        cut.Find("[data-flow-node='a']").Click(); // shift not needed - single selection
        cut.Find("[data-flow-node='b']").Click(new MouseEventArgs { ShiftKey = true });

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "Delete" });

        Assert.Contains(currentNodes(), n => n.Id == "a");
        Assert.DoesNotContain(currentNodes(), n => n.Id == "b");
    }

    [Fact]
    public void An_Edge_With_Deletable_False_Survives_Direct_Selection_Delete()
    {
        var edges = new List<L.FlowEdge> { new("a-b", "a", "b", Deletable: false) };
        var (cut, _, currentEdges) = RenderBoundWithEdges(ThreeNodes(), edges);
        cut.Find("[data-flow-edge][data-edge-id='a-b']").Click();

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "Delete" });

        Assert.Single(currentEdges());
    }

    [Fact]
    public void Readonly_Blocks_Delete_Entirely()
    {
        var (cut, currentNodes, _) = RenderBoundWithEdges(ThreeNodes(), new List<L.FlowEdge>(),
            p => p.Add(c => c.Readonly, true));
        cut.Find("[data-flow-node='a']").Click();
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "Delete" });
        Assert.Contains(currentNodes(), n => n.Id == "a");
    }

    [Fact]
    public void With_Nothing_Selected_Delete_Is_A_NoOp()
    {
        var (cut, currentNodes, _) = RenderBoundWithEdges(ThreeNodes(), new List<L.FlowEdge>());
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "Delete" });
        Assert.Equal(3, currentNodes().Count);
    }

    [Fact]
    public void With_OnDelete_The_Canvas_Does_Not_Remove_Anything_Itself()
    {
        L.FlowSelection? seen = null;
        var (cut, currentNodes, currentEdges) = RenderBoundWithEdges(ThreeNodes(), TwoEdges(),
            p => p.Add(c => c.OnDelete, (L.FlowSelection s) => seen = s));
        cut.Find("[data-flow-node='a']").Click();

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "Delete" });

        Assert.NotNull(seen);
        Assert.Equal(new[] { "a" }, seen!.NodeIds);
        Assert.Equal(3, currentNodes().Count);
        Assert.Equal(2, currentEdges().Count);
    }

    [Fact]
    public void Custom_DeleteKey_List_Is_Honoured()
    {
        var (cut, currentNodes, _) = RenderBoundWithEdges(ThreeNodes(), new List<L.FlowEdge>(),
            p => p.Add(c => c.DeleteKey, (IReadOnlyList<string>)new[] { "x" }));
        cut.Find("[data-flow-node='a']").Click();

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "Delete" });
        Assert.Contains(currentNodes(), n => n.Id == "a"); // "Delete" is no longer a delete key

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "x" });
        Assert.DoesNotContain(currentNodes(), n => n.Id == "a");
    }
}
