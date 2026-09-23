using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// Selection (phase 2): click replaces, shift/ctrl-click toggles, a pane click or Escape clears,
/// CommitMarquee (the shift-drag hit-test flow.js performs) replaces the node selection, edge click
/// selects an edge the same way nodes do. OnSelectionChanged is raised on every change.
/// </summary>
public class FlowCanvasSelectionTests : FlowCanvasTestBase
{
    private static List<L.FlowSelection> Track(out Action<L.FlowSelection> handler)
    {
        var seen = new List<L.FlowSelection>();
        handler = s => seen.Add(s);
        return seen;
    }

    [Fact]
    public void A_Plain_Click_Selects_Only_That_Node()
    {
        var seen = Track(out var handler);
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.OnSelectionChanged, handler));

        cut.Find("[data-flow-node='a']").Click();
        Assert.Equal("", cut.Find("[data-flow-node='a']").GetAttribute("data-selected"));
        cut.Find("[data-flow-node='b']").Click();

        Assert.Null(cut.Find("[data-flow-node='a']").GetAttribute("data-selected"));
        Assert.Equal("", cut.Find("[data-flow-node='b']").GetAttribute("data-selected"));
        Assert.Equal(2, seen.Count);
        Assert.Equal(new[] { "b" }, seen[^1].NodeIds);
    }

    [Fact]
    public void Shift_Click_Toggles_Membership_Without_Clearing_The_Rest()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()));
        cut.Find("[data-flow-node='a']").Click();
        cut.Find("[data-flow-node='b']").Click(new MouseEventArgs { ShiftKey = true });

        Assert.Equal("", cut.Find("[data-flow-node='a']").GetAttribute("data-selected"));
        Assert.Equal("", cut.Find("[data-flow-node='b']").GetAttribute("data-selected"));

        // Shift-click again removes it from the selection.
        cut.Find("[data-flow-node='b']").Click(new MouseEventArgs { ShiftKey = true });
        Assert.Null(cut.Find("[data-flow-node='b']").GetAttribute("data-selected"));
        Assert.Equal("", cut.Find("[data-flow-node='a']").GetAttribute("data-selected"));
    }

    [Fact]
    public void Ctrl_Click_Toggles_Membership_Like_Shift()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()));
        cut.Find("[data-flow-node='a']").Click();
        cut.Find("[data-flow-node='b']").Click(new MouseEventArgs { CtrlKey = true });
        Assert.Equal("", cut.Find("[data-flow-node='a']").GetAttribute("data-selected"));
        Assert.Equal("", cut.Find("[data-flow-node='b']").GetAttribute("data-selected"));
    }

    [Fact]
    public void An_Unselectable_Node_Never_Gets_Selected_But_Still_Raises_OnNodeClick()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0, Selectable: false) };
        L.FlowNode? clicked = null;
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, nodes).Add(c => c.OnNodeClick, (L.FlowNode n) => clicked = n));
        cut.Find("[data-flow-node='a']").Click();
        Assert.Null(cut.Find("[data-flow-node='a']").GetAttribute("data-selected"));
        Assert.Equal("a", clicked?.Id);
    }

    [Fact]
    public void ElementsSelectable_False_Turns_Selection_Off()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.ElementsSelectable, false));
        cut.Find("[data-flow-node='a']").Click();
        Assert.Null(cut.Find("[data-flow-node='a']").GetAttribute("data-selected"));
    }

    [Fact]
    public async Task A_Pane_Click_Clears_The_Selection_And_Raises_OnSelectionChanged()
    {
        var seen = Track(out var handler);
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.OnSelectionChanged, handler));
        cut.Find("[data-flow-node='a']").Click();
        seen.Clear();

        await cut.InvokeAsync(() => cut.Instance.PaneClicked(0, 0));

        Assert.Null(cut.Find("[data-flow-node='a']").GetAttribute("data-selected"));
        Assert.Single(seen);
        Assert.Empty(seen[0].NodeIds);
    }

    [Fact]
    public void Escape_On_A_Node_Clears_The_Selection()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()));
        cut.Find("[data-flow-node='a']").Click();
        cut.Find("[data-flow-node='a']").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Null(cut.Find("[data-flow-node='a']").GetAttribute("data-selected"));
    }

    [Fact]
    public void Delete_Key_On_The_Pane_Clears_A_Selection_That_Has_Nothing_Deletable()
    {
        // Sanity: the pane itself is a keydown target (tabindex="-1", HandlePaneKeyDownAsync);
        // Escape routed there clears the selection too, independent of which node had focus.
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()));
        cut.Find("[data-flow-node='a']").Click();
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Null(cut.Find("[data-flow-node='a']").GetAttribute("data-selected"));
    }

    [Fact]
    public async Task CommitMarquee_Replaces_The_Selection_With_Selectable_Nodes_Only()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0), new("b", 100, 0), new("c", 200, 0, Selectable: false) };
        var seen = Track(out var handler);
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, nodes).Add(c => c.OnSelectionChanged, handler));

        await cut.InvokeAsync(() => cut.Instance.CommitMarquee(new[] { "a", "b", "c", "ghost" }));

        Assert.Equal("", cut.Find("[data-flow-node='a']").GetAttribute("data-selected"));
        Assert.Equal("", cut.Find("[data-flow-node='b']").GetAttribute("data-selected"));
        Assert.Null(cut.Find("[data-flow-node='c']").GetAttribute("data-selected"));
        var last = Assert.Single(seen);
        Assert.Equal(new HashSet<string> { "a", "b" }, last.NodeIds);
    }

    [Fact]
    public async Task CommitMarquee_Is_A_NoOp_When_ElementsSelectable_Is_False()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0) };
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, nodes).Add(c => c.ElementsSelectable, false));
        await cut.InvokeAsync(() => cut.Instance.CommitMarquee(new[] { "a" }));
        Assert.Null(cut.Find("[data-flow-node='a']").GetAttribute("data-selected"));
    }

    [Fact]
    public void An_Edge_Click_Selects_It_And_Raises_OnEdgeClick()
    {
        L.FlowEdge? clicked = null;
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .Add(c => c.Edges, TwoEdges())
            .Add(c => c.OnEdgeClick, (L.FlowEdge e) => clicked = e));

        cut.Find("[data-flow-edge][data-edge-id='a-b']").Click();

        Assert.Equal("a-b", clicked?.Id);
        Assert.Equal("", cut.Find("[data-flow-edge][data-edge-id='a-b']").GetAttribute("data-selected"));
        Assert.Contains("stroke:var(--color-primary)", cut.Find("[data-flow-edge][data-edge-id='a-b']").GetAttribute("style"));
    }

    [Fact]
    public void Selecting_An_Edge_Clears_Any_Node_Selection()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.Edges, TwoEdges()));
        cut.Find("[data-flow-node='a']").Click();
        cut.Find("[data-flow-edge][data-edge-id='a-b']").Click();
        Assert.Null(cut.Find("[data-flow-node='a']").GetAttribute("data-selected"));
        Assert.Equal("", cut.Find("[data-flow-edge][data-edge-id='a-b']").GetAttribute("data-selected"));
    }

    [Fact]
    public async Task SelectAsync_Replaces_The_Selection_And_Ignores_Unknown_Ids()
    {
        var seen = Track(out var handler);
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.OnSelectionChanged, handler));

        await cut.InvokeAsync(() => cut.Instance.SelectAsync(new[] { "a", "ghost" }));

        Assert.Equal("", cut.Find("[data-flow-node='a']").GetAttribute("data-selected"));
        var last = Assert.Single(seen);
        Assert.Equal(new[] { "a" }, last.NodeIds);
    }

    [Fact]
    public async Task ClearSelectionAsync_Clears_Nodes_And_Edges()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.Edges, TwoEdges()));
        cut.Find("[data-flow-node='a']").Click();
        cut.Find("[data-flow-edge][data-edge-id='b-c']").Click(new MouseEventArgs { ShiftKey = true });

        await cut.InvokeAsync(() => cut.Instance.ClearSelectionAsync());

        Assert.Null(cut.Find("[data-flow-node='a']").GetAttribute("data-selected"));
        Assert.Null(cut.Find("[data-flow-edge][data-edge-id='b-c']").GetAttribute("data-selected"));
    }
}
