using Bunit;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// Connect (phase 2): CommitConnect is the JSInvokable both the pointer drag-and-drop gesture and
/// the keyboard "Enter, Enter" state machine in flow.js call on a proposed connection. It validates
/// NodesConnectable / per-node Connectable / Readonly / IsValidConnection, then either raises
/// OnConnect or appends a FlowEdge itself and raises EdgesChanged.
/// </summary>
public class FlowCanvasConnectTests : FlowCanvasTestBase
{
    [Fact]
    public async Task Without_OnConnect_A_Commit_Appends_An_Edge_And_Raises_EdgesChanged()
    {
        var (cut, _, currentEdges) = RenderBoundWithEdges(ThreeNodes(), new List<L.FlowEdge>());

        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitConnect("a", "out", "b", "in"));

        Assert.True(accepted);
        var edge = Assert.Single(currentEdges());
        Assert.Equal("a", edge.Source);
        Assert.Equal("b", edge.Target);
        Assert.Equal("out", edge.SourceHandle);
        Assert.Equal("in", edge.TargetHandle);
        Assert.NotNull(cut.Find($"[data-flow-edge][data-edge-id='{edge.Id}']"));
    }

    [Fact]
    public async Task With_OnConnect_The_Canvas_Does_Not_Append_An_Edge_Itself()
    {
        L.FlowConnection? seen = null;
        var (cut, _, currentEdges) = RenderBoundWithEdges(ThreeNodes(), new List<L.FlowEdge>(),
            p => p.Add(c => c.OnConnect, (L.FlowConnection c) => seen = c));

        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitConnect("a", null, "b", null));

        Assert.True(accepted);
        Assert.Equal(new L.FlowConnection("a", null, "b", null), seen);
        Assert.Empty(currentEdges());
    }

    [Fact]
    public async Task IsValidConnection_Returning_False_Rejects_The_Connection()
    {
        var seenConnect = false;
        var (cut, _, currentEdges) = RenderBoundWithEdges(ThreeNodes(), new List<L.FlowEdge>(),
            p => p.Add(c => c.IsValidConnection, (L.FlowConnection _) => false)
                  .Add(c => c.OnConnect, (L.FlowConnection _) => seenConnect = true));

        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitConnect("a", null, "b", null));

        Assert.False(accepted);
        Assert.False(seenConnect);
        Assert.Empty(currentEdges());
    }

    [Fact]
    public async Task IsValidConnection_Sees_The_Proposed_Connection()
    {
        L.FlowConnection? seen = null;
        var (cut, _, _) = RenderBoundWithEdges(ThreeNodes(), new List<L.FlowEdge>(),
            p => p.Add(c => c.IsValidConnection, (L.FlowConnection conn) => { seen = conn; return true; }));

        await cut.InvokeAsync(() => cut.Instance.CommitConnect("a", "src", "b", "tgt"));

        Assert.Equal(new L.FlowConnection("a", "src", "b", "tgt"), seen);
    }

    [Fact]
    public async Task NodesConnectable_False_Rejects_Every_Connection()
    {
        var (cut, _, currentEdges) = RenderBoundWithEdges(ThreeNodes(), new List<L.FlowEdge>(),
            p => p.Add(c => c.NodesConnectable, false));

        Assert.False(await cut.InvokeAsync(() => cut.Instance.CommitConnect("a", null, "b", null)));
        Assert.Empty(currentEdges());
    }

    [Fact]
    public async Task Readonly_Rejects_Every_Connection()
    {
        var (cut, _, currentEdges) = RenderBoundWithEdges(ThreeNodes(), new List<L.FlowEdge>(),
            p => p.Add(c => c.Readonly, true));

        Assert.False(await cut.InvokeAsync(() => cut.Instance.CommitConnect("a", null, "b", null)));
        Assert.Empty(currentEdges());
    }

    [Fact]
    public async Task A_Non_Connectable_Node_Rejects_The_Connection_On_Either_End()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0, Connectable: false), new("b", 300, 0) };
        var (cut, _, currentEdgesA) = RenderBoundWithEdges(nodes, new List<L.FlowEdge>());
        Assert.False(await cut.InvokeAsync(() => cut.Instance.CommitConnect("a", null, "b", null)));
        Assert.Empty(currentEdgesA());

        var nodes2 = new List<L.FlowNode> { new("a", 0, 0), new("b", 300, 0, Connectable: false) };
        var (cut2, _, currentEdgesB) = RenderBoundWithEdges(nodes2, new List<L.FlowEdge>());
        Assert.False(await cut2.InvokeAsync(() => cut2.Instance.CommitConnect("a", null, "b", null)));
        Assert.Empty(currentEdgesB());
    }

    [Fact]
    public async Task A_Connection_To_A_Missing_Node_Is_Rejected()
    {
        var (cut, _, currentEdges) = RenderBoundWithEdges(ThreeNodes(), new List<L.FlowEdge>());
        Assert.False(await cut.InvokeAsync(() => cut.Instance.CommitConnect("a", null, "ghost", null)));
        Assert.Empty(currentEdges());
    }

    [Fact]
    public void Handles_Are_Tab_Stops_With_Their_Role_And_Type_Data()
    {
        var cut = Ctx.Render<L.FlowHandle>(p => p.Add(x => x.Type, L.FlowHandleType.Source).Add(x => x.Id, "out"));
        var button = cut.Find("button");
        Assert.Equal("0", button.GetAttribute("tabindex"));
        Assert.Equal("source", button.GetAttribute("data-handle-type"));
        Assert.Equal("out", button.GetAttribute("data-handle-id"));
    }

    [Fact]
    public void A_Node_Host_Carries_Connectable_And_Selectable_Data_Attributes()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0, Connectable: false, Selectable: false) };
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, nodes));
        var host = cut.Find("[data-flow-node='a']");
        Assert.Equal("false", host.GetAttribute("data-connectable"));
        Assert.Equal("false", host.GetAttribute("data-selectable"));
    }
}
