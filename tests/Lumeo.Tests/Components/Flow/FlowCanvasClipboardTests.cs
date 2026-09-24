using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// Clipboard (phase 4): Ctrl+C copies the selection (nodes + the edges between two selected
/// nodes), Ctrl+V pastes it back with new ids and a (20, 20) offset, Ctrl+D duplicates the CURRENT
/// selection directly without touching what Ctrl+C last copied. Honours the same editable-target
/// guard every other pane shortcut does (checked first in HandlePaneKeyDownAsync).
/// </summary>
public class FlowCanvasClipboardTests : FlowCanvasTestBase
{
    [Fact]
    public void CtrlV_Without_A_Prior_CtrlC_Is_A_NoOp()
    {
        var (cut, currentNodes) = RenderBound(ThreeNodes());
        cut.Find("[data-flow-node='a']").Click();

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "v", CtrlKey = true });

        Assert.Equal(3, currentNodes().Count);
    }

    [Fact]
    public void CtrlC_Then_CtrlV_Pastes_A_Copy_Offset_By_20_20_With_A_New_Id()
    {
        var (cut, currentNodes) = RenderBound(ThreeNodes());
        cut.Find("[data-flow-node='a']").Click();

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "c", CtrlKey = true });
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "v", CtrlKey = true });

        Assert.Equal(4, currentNodes().Count);
        var pasted = currentNodes().Single(n => n.Id != "a" && n.Id != "b" && n.Id != "c");
        Assert.Equal(20, pasted.X); // original "a" is at (0, 0)
        Assert.Equal(20, pasted.Y);
    }

    [Fact]
    public void A_Second_CtrlV_Lands_On_The_Same_20_20_Offset_Not_Cumulative()
    {
        var (cut, currentNodes) = RenderBound(ThreeNodes());
        cut.Find("[data-flow-node='a']").Click();
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "c", CtrlKey = true });

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "v", CtrlKey = true });
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "v", CtrlKey = true });

        Assert.Equal(5, currentNodes().Count);
        var pastedTwice = currentNodes().Where(n => n.Id != "a" && n.Id != "b" && n.Id != "c").ToList();
        Assert.Equal(2, pastedTwice.Count);
        Assert.All(pastedTwice, n => { Assert.Equal(20, n.X); Assert.Equal(20, n.Y); });
        Assert.NotEqual(pastedTwice[0].Id, pastedTwice[1].Id);
    }

    [Fact]
    public void Copy_Includes_Only_Edges_Between_Two_SELECTED_Nodes()
    {
        var (cut, currentNodes, currentEdges) = RenderBoundWithEdges(ThreeNodes(), TwoEdges()); // a-b, b-c
        cut.Find("[data-flow-node='a']").Click();
        cut.Find("[data-flow-node='b']").Click(new MouseEventArgs { ShiftKey = true }); // a+b selected, not c

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "c", CtrlKey = true });
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "v", CtrlKey = true });

        Assert.Equal(5, currentNodes().Count);
        Assert.Equal(3, currentEdges().Count); // a-b, b-c, and exactly one new pasted edge (a'-b')
        var pastedIds = currentNodes().Where(n => n.Id != "a" && n.Id != "b" && n.Id != "c").Select(n => n.Id).ToHashSet();
        var pastedEdge = currentEdges().Single(e => pastedIds.Contains(e.Source));
        Assert.Contains(pastedEdge.Target, pastedIds);
    }

    [Fact]
    public void Pasted_Nodes_Become_The_New_Selection()
    {
        var (cut, currentNodes) = RenderBound(ThreeNodes());
        L.FlowSelection? selection = null;
        cut.Render(p => p.Add(c => c.OnSelectionChanged, (L.FlowSelection s) => selection = s));
        cut.Find("[data-flow-node='a']").Click();
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "c", CtrlKey = true });

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "v", CtrlKey = true });

        var pastedId = currentNodes().Single(n => n.Id != "a" && n.Id != "b" && n.Id != "c").Id;
        Assert.NotNull(selection);
        Assert.Equal(new[] { pastedId }, selection!.NodeIds);
    }

    [Fact]
    public void OnPaste_Receives_The_New_Nodes_And_Edges()
    {
        var (cut, _) = RenderBound(ThreeNodes());
        L.FlowPasteEventArgs? seen = null;
        cut.Render(p => p.Add(c => c.OnPaste, (L.FlowPasteEventArgs a) => seen = a));
        cut.Find("[data-flow-node='a']").Click();
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "c", CtrlKey = true });

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "v", CtrlKey = true });

        Assert.NotNull(seen);
        Assert.Single(seen!.Nodes);
        Assert.Equal(20, seen.Nodes[0].X);
    }

    [Fact]
    public void CtrlD_Duplicates_The_Current_Selection_Without_A_Prior_CtrlC()
    {
        var (cut, currentNodes) = RenderBound(ThreeNodes());
        cut.Find("[data-flow-node='b']").Click();

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "d", CtrlKey = true });

        Assert.Equal(4, currentNodes().Count);
        var pasted = currentNodes().Single(n => n.Id != "a" && n.Id != "b" && n.Id != "c");
        Assert.Equal(320, pasted.X); // b was at (300, 40)
        Assert.Equal(60, pasted.Y);
    }

    [Fact]
    public void NewNodeId_Names_The_Pasted_Nodes()
    {
        var (cut, currentNodes) = RenderBound(ThreeNodes(), p => p.Add(c => c.NewNodeId, (L.FlowNode n) => "copy-of-" + n.Id));
        cut.Find("[data-flow-node='a']").Click();

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "d", CtrlKey = true });

        Assert.Contains(currentNodes(), n => n.Id == "copy-of-a");
    }

    [Fact]
    public void Readonly_Blocks_Paste()
    {
        var (cut, currentNodes) = RenderBound(ThreeNodes(), p => p.Add(c => c.Readonly, true));
        cut.Find("[data-flow-node='a']").Click();
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "d", CtrlKey = true });
        Assert.Equal(3, currentNodes().Count);
    }

    [Fact]
    public void With_Nothing_Selected_CtrlD_Is_A_NoOp()
    {
        var (cut, currentNodes) = RenderBound(ThreeNodes());
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "d", CtrlKey = true });
        Assert.Equal(3, currentNodes().Count);
    }

    [Fact]
    public void An_Editable_Target_Blocks_Every_Clipboard_Shortcut()
    {
        var (cut, currentNodes) = RenderBound(ThreeNodes());
        cut.Find("[data-flow-node='a']").Click();
        Interop.FlowFocusedElementEditable = true;

        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "c", CtrlKey = true });
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "v", CtrlKey = true });
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "d", CtrlKey = true });

        Assert.Equal(3, currentNodes().Count);
    }
}
