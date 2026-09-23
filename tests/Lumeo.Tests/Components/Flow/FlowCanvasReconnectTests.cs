using Bunit;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// CommitReconnect: dragging an existing edge's end onto a new handle. Re-validated the same way a
/// fresh connect is (node Connectable, Readonly, IsValidConnection); without OnReconnect the canvas
/// updates the edge's moved end itself.
/// </summary>
public class FlowCanvasReconnectTests : FlowCanvasTestBase
{
    private static List<L.FlowEdge> OneEdge() => new() { new L.FlowEdge("a-b", "a", "b") };

    [Fact]
    public async Task Reconnecting_The_Target_End_Moves_It_To_The_New_Node()
    {
        var (cut, _, currentEdges) = RenderBoundWithEdges(ThreeNodes(), OneEdge());

        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitReconnect("a-b", "target", "c", null));

        Assert.True(accepted);
        var edge = currentEdges().Single(e => e.Id == "a-b");
        Assert.Equal("a", edge.Source);
        Assert.Equal("c", edge.Target);
    }

    [Fact]
    public async Task Reconnecting_The_Source_End_Moves_It_To_The_New_Node()
    {
        var (cut, _, currentEdges) = RenderBoundWithEdges(ThreeNodes(), OneEdge());

        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitReconnect("a-b", "source", "c", null));

        Assert.True(accepted);
        var edge = currentEdges().Single(e => e.Id == "a-b");
        Assert.Equal("c", edge.Source);
        Assert.Equal("b", edge.Target);
    }

    [Fact]
    public async Task IsValidConnection_Can_Reject_A_Reconnect()
    {
        var (cut, _, currentEdges) = RenderBoundWithEdges(ThreeNodes(), OneEdge(),
            p => p.Add(c => c.IsValidConnection, (L.FlowConnection conn) => conn.Target != "c"));

        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitReconnect("a-b", "target", "c", null));

        Assert.False(accepted);
        Assert.Equal("b", currentEdges().Single(e => e.Id == "a-b").Target); // unchanged
    }

    [Fact]
    public async Task EdgesReconnectable_False_Rejects_Every_Reconnect()
    {
        var (cut, _, currentEdges) = RenderBoundWithEdges(ThreeNodes(), OneEdge(), p => p.Add(c => c.EdgesReconnectable, false));

        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitReconnect("a-b", "target", "c", null));

        Assert.False(accepted);
        Assert.Equal("b", currentEdges().Single(e => e.Id == "a-b").Target);
    }

    [Fact]
    public async Task Readonly_Rejects_A_Reconnect()
    {
        var (cut, _, currentEdges) = RenderBoundWithEdges(ThreeNodes(), OneEdge(), p => p.Add(c => c.Readonly, true));

        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitReconnect("a-b", "target", "c", null));

        Assert.False(accepted);
        Assert.Equal("b", currentEdges().Single(e => e.Id == "a-b").Target);
    }

    [Fact]
    public async Task An_Unknown_Edge_Or_Node_Is_Rejected()
    {
        var (cut, _, _) = RenderBoundWithEdges(ThreeNodes(), OneEdge());

        Assert.False(await cut.InvokeAsync(() => cut.Instance.CommitReconnect("nope", "target", "c", null)));
        Assert.False(await cut.InvokeAsync(() => cut.Instance.CommitReconnect("a-b", "target", "nope", null)));
    }

    [Fact]
    public async Task With_An_OnReconnect_Handler_The_Canvas_Does_Not_Update_Edges_Itself()
    {
        L.FlowReconnectEventArgs? received = null;
        var (cut, _, currentEdges) = RenderBoundWithEdges(ThreeNodes(), OneEdge(),
            p => p.Add(c => c.OnReconnect, (L.FlowReconnectEventArgs a) => received = a));

        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitReconnect("a-b", "target", "c", null));

        Assert.True(accepted);
        Assert.NotNull(received);
        Assert.Equal("a", received!.OldEdge.Source);
        Assert.Equal("b", received.OldEdge.Target);
        Assert.Equal("c", received.NewConnection.Target);
        Assert.Equal("b", currentEdges().Single(e => e.Id == "a-b").Target); // untouched — the app owns it
    }

    [Fact]
    public async Task A_Node_That_Is_Not_Connectable_Rejects_As_The_New_Endpoint()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0), new("b", 100, 0), new("c", 200, 0, Connectable: false) };
        var (cut, _, currentEdges) = RenderBoundWithEdges(nodes, OneEdge());

        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitReconnect("a-b", "target", "c", null));

        Assert.False(accepted);
        Assert.Equal("b", currentEdges().Single(e => e.Id == "a-b").Target);
    }
}
