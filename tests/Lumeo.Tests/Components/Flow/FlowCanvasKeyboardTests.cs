using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// Keyboard: a focused node moves with the arrow keys (1px, ×10 with Shift, a grid step when
/// snapping) and every press commits like a drop; Escape clears the selection. Node events
/// (click, double-click, context menu, pane click) reach the app.
/// </summary>
public class FlowCanvasKeyboardTests : FlowCanvasTestBase
{
    [Theory]
    [InlineData("ArrowRight", false, 1, 0)]
    [InlineData("ArrowLeft", false, -1, 0)]
    [InlineData("ArrowUp", false, 0, -1)]
    [InlineData("ArrowDown", false, 0, 1)]
    [InlineData("ArrowRight", true, 10, 0)]
    [InlineData("ArrowUp", true, 0, -10)]
    public void Arrow_Keys_Move_The_Focused_Node_And_Commit(string key, bool shift, double dx, double dy)
    {
        IReadOnlyList<L.FlowNodeChange>? stopped = null;
        var (cut, current) = RenderBound(ThreeNodes(), p => p.Add(c => c.OnNodeDragStop, (IReadOnlyList<L.FlowNodeChange> ch) => stopped = ch));

        cut.Find("[data-flow-node='b']").KeyDown(new KeyboardEventArgs { Key = key, ShiftKey = shift });

        Assert.Equal(300 + dx, Node(current(), "b").X);
        Assert.Equal(40 + dy, Node(current(), "b").Y);
        Assert.Equal(new L.FlowNodeChange("b", 300 + dx, 40 + dy), Assert.Single(stopped!));
        Assert.Equal(0, Node(current(), "a").X);
    }

    [Fact]
    public void With_Snapping_A_Press_Moves_One_Grid_Step_And_Lands_On_The_Grid()
    {
        var nodes = new List<L.FlowNode> { new("n", 5, 3) };
        var (cut, current) = RenderBound(nodes, p => p.Add(c => c.SnapToGrid, true).Add(c => c.SnapGrid, (16d, 10d)));
        cut.Find("[data-flow-node='n']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal(16, Node(current(), "n").X);  // 5 + 16 = 21 -> snaps to 16
        Assert.Equal(0, Node(current(), "n").Y);   // y re-snapped too: 3 -> 0
        cut.Find("[data-flow-node='n']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown", ShiftKey = true });
        Assert.Equal(100, Node(current(), "n").Y);
    }

    [Fact]
    public void Arrow_Keys_Move_The_Whole_Selection_When_The_Focused_Node_Is_In_It()
    {
        var (cut, current) = RenderBound(ThreeNodes());
        cut.Find("[data-flow-node='a']").Click(); // selects a
        cut.Find("[data-flow-node='a']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal(1, Node(current(), "a").Y);
        Assert.Equal(40, Node(current(), "b").Y);
    }

    [Theory]
    [InlineData(true, true)]    // readonly
    [InlineData(false, false)]  // NodesDraggable=false
    public void Readonly_Or_Undraggable_Canvases_Ignore_Arrow_Keys(bool isReadonly, bool draggable)
    {
        var changed = 0;
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .Add(c => c.Readonly, isReadonly)
            .Add(c => c.NodesDraggable, draggable)
            .Add(c => c.NodesChanged, (IReadOnlyList<L.FlowNode> _) => changed++));
        cut.Find("[data-flow-node='a']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal(0, changed);
        Assert.Equal("0", cut.Find("[data-flow-node='a']").GetAttribute("data-x"));
    }

    [Fact]
    public void A_Node_With_Draggable_False_Ignores_Arrow_Keys()
    {
        var (cut, current) = RenderBound(ThreeNodes());
        cut.Find("[data-flow-node='c']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal(120, Node(current(), "c").X);
    }

    [Fact]
    public void Escape_Clears_The_Selection()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()));
        cut.Find("[data-flow-node='a']").Click();
        Assert.Single(cut.FindAll("[data-flow-node][data-selected]"));
        cut.Find("[data-flow-node='a']").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Empty(cut.FindAll("[data-flow-node][data-selected]"));
    }

    [Fact]
    public void Node_Click_And_DoubleClick_Reach_The_App()
    {
        var clicks = new List<string>();
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .Add(c => c.OnNodeClick, (L.FlowNode n) => clicks.Add("click:" + n.Id))
            .Add(c => c.OnNodeDoubleClick, (L.FlowNode n) => clicks.Add("dbl:" + n.Id)));
        cut.Find("[data-flow-node='b']").Click();
        cut.Find("[data-flow-node='c']").DoubleClick();
        Assert.Equal(new[] { "click:b", "dbl:c" }, clicks);
    }

    [Fact]
    public void Context_Menu_Carries_The_Node_And_The_Client_Point()
    {
        L.FlowNodeContextMenuEventArgs? args = null;
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .Add(c => c.OnNodeContextMenu, (L.FlowNodeContextMenuEventArgs a) => args = a));
        cut.Find("[data-flow-node='a']").ContextMenu(new MouseEventArgs { ClientX = 321, ClientY = 123 });
        Assert.NotNull(args);
        Assert.Equal("a", args!.Node.Id);
        Assert.Equal(321, args.ClientX);
        Assert.Equal(123, args.ClientY);
    }

    [Fact]
    public async Task A_Pane_Click_Reports_The_Flow_Point()
    {
        L.FlowPoint? point = null;
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.OnPaneClick, (L.FlowPoint pt) => point = pt));
        await cut.InvokeAsync(() => cut.Instance.PaneClicked(42.5, -7));
        Assert.Equal(new L.FlowPoint(42.5, -7), point);
    }
}
