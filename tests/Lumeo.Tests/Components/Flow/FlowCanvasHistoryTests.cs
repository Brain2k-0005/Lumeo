using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// The canvas' <c>History</c> integration: a baseline is seeded before any edit, every committed
/// change (drag, keyboard move, connect, delete, an externally applied replace such as a layout
/// result) pushes exactly once, and Ctrl+Z/Ctrl+Y/Ctrl+Shift+Z undo/redo while the canvas has focus.
/// </summary>
public class FlowCanvasHistoryTests : FlowCanvasTestBase
{
    [Fact]
    public void Setting_History_Seeds_The_Baseline_On_First_Render()
    {
        var history = new L.FlowHistory();
        RenderBound(ThreeNodes(), p => p.Add(c => c.History, history));

        Assert.NotNull(history.Current);
        Assert.False(history.CanUndo);
        Assert.Equal(3, history.Current!.Nodes.Count);
    }

    [Fact]
    public async Task A_Node_Drag_Commit_Pushes_Exactly_Once()
    {
        var history = new L.FlowHistory();
        var (cut, _) = RenderBound(ThreeNodes(), p => p.Add(c => c.History, history));
        var countBefore = history.Count;

        await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("a", 9, 9) }, cut.Instance._state.Generation));

        Assert.Equal(countBefore + 1, history.Count);
        Assert.True(history.CanUndo);
    }

    [Fact]
    public void A_Keyboard_Move_Commit_Pushes_Exactly_Once()
    {
        var history = new L.FlowHistory();
        var (cut, _) = RenderBound(ThreeNodes(), p => p.Add(c => c.History, history));
        var countBefore = history.Count;

        cut.Find("[data-flow-node='a']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal(countBefore + 1, history.Count);
    }

    [Fact]
    public async Task A_Connect_Commit_Pushes_Exactly_Once()
    {
        var history = new L.FlowHistory();
        var (cut, _, _) = RenderBoundWithEdges(ThreeNodes(), new List<L.FlowEdge>(), p => p.Add(c => c.History, history));
        var countBefore = history.Count;

        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitConnect("a", null, "b", null));

        Assert.True(accepted);
        Assert.Equal(countBefore + 1, history.Count);
    }

    [Fact]
    public async Task A_Delete_That_Removes_Nodes_And_Edges_Together_Pushes_Exactly_Once()
    {
        var history = new L.FlowHistory();
        var (cut, currentNodes, _) = RenderBoundWithEdges(ThreeNodes(), TwoEdges(), p => p.Add(c => c.History, history));
        var countBefore = history.Count;

        cut.Find("[data-flow-node='a']").Click(); // selects a — removing it also removes edge a-b
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "Delete" });

        Assert.Equal(countBefore + 1, history.Count); // one push, not two
        Assert.DoesNotContain(currentNodes(), n => n.Id == "a");
    }

    [Fact]
    public void An_Externally_Applied_Replace_After_Init_Pushes_Once_Like_A_Layout_Result()
    {
        var history = new L.FlowHistory();
        var (cut, _) = RenderBound(ThreeNodes(), p => p.Add(c => c.History, history));
        var countBefore = history.Count;

        var laidOut = new List<L.FlowNode> { new("a", 0, 0), new("b", 200, 0), new("c", 400, 0) };
        cut.Render(p => p.Add(c => c.Nodes, laidOut));

        Assert.Equal(countBefore + 1, history.Count);
    }

    [Fact]
    public void Selection_Alone_Does_Not_Push_History()
    {
        var history = new L.FlowHistory();
        var (cut, _) = RenderBound(ThreeNodes(), p => p.Add(c => c.History, history));
        var countBefore = history.Count;

        cut.Find("[data-flow-node='a']").Click();

        Assert.Equal(countBefore, history.Count);
    }

    [Fact]
    public async Task Ctrl_Z_Undoes_The_Last_Commit()
    {
        var history = new L.FlowHistory();
        var (cut, current) = RenderBound(ThreeNodes(), p => p.Add(c => c.History, history));
        await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("a", 77, 77) }, cut.Instance._state.Generation));
        Assert.Equal(77, Node(current(), "a").X);

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "z", CtrlKey = true });

        Assert.Equal(0, Node(current(), "a").X);
    }

    [Fact]
    public async Task Ctrl_Shift_Z_And_Ctrl_Y_Both_Redo()
    {
        var history = new L.FlowHistory();
        var (cut, current) = RenderBound(ThreeNodes(), p => p.Add(c => c.History, history));
        await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("a", 77, 77) }, cut.Instance._state.Generation));
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "z", CtrlKey = true });
        Assert.Equal(0, Node(current(), "a").X);

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "z", CtrlKey = true, ShiftKey = true });
        Assert.Equal(77, Node(current(), "a").X);

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "z", CtrlKey = true }); // undo again
        Assert.Equal(0, Node(current(), "a").X);
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "y", CtrlKey = true });
        Assert.Equal(77, Node(current(), "a").X);
    }

    [Fact]
    public async Task Undo_Applies_Both_Nodes_And_Edges_And_Does_Not_Push_A_New_Entry()
    {
        var history = new L.FlowHistory();
        var (cut, currentNodes, currentEdges) = RenderBoundWithEdges(ThreeNodes(), new List<L.FlowEdge>(), p => p.Add(c => c.History, history));
        await cut.InvokeAsync(() => cut.Instance.CommitConnect("a", null, "b", null));
        Assert.Single(currentEdges());
        var countAfterConnect = history.Count;

        await cut.InvokeAsync(() => cut.Instance.UndoAsync());

        Assert.Empty(currentEdges());
        Assert.Equal(countAfterConnect, history.Count); // undo does not itself push
    }

    [Fact]
    public async Task Without_A_History_Ctrl_Z_Does_Nothing_And_Delete_Still_Works()
    {
        var (cut, current) = RenderBound(ThreeNodes());
        cut.Find("[data-flow-node='a']").Click();
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "z", CtrlKey = true });
        Assert.Equal(3, current().Count); // no-op, did not crash

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "Delete" });
        Assert.Equal(2, current().Count); // delete still works without History
    }
}
