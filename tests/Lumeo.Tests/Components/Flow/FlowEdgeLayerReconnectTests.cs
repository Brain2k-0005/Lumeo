using Bunit;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>The reconnect grab handles (<c>data-flow-edge-end</c>) FlowEdgeLayer renders for a selected, reconnectable edge.</summary>
public class FlowEdgeLayerReconnectTests : FlowCanvasTestBase
{
    private static List<L.FlowEdge> OneEdge() => new() { new L.FlowEdge("a-b", "a", "b") };

    [Fact]
    public void No_Grab_Handles_When_The_Edge_Is_Not_Selected()
    {
        var (cut, _, _) = RenderBoundWithEdges(ThreeNodes(), OneEdge());
        Assert.Empty(cut.FindAll("[data-flow-edge-end]"));
    }

    [Fact]
    public void Selecting_The_Edge_Shows_Both_Grab_Handles()
    {
        var (cut, _, _) = RenderBoundWithEdges(ThreeNodes(), OneEdge());
        cut.Find("[data-flow-edge][data-edge-id='a-b']").Click();

        var handles = cut.FindAll("[data-flow-edge-end]");
        Assert.Equal(2, handles.Count);
        Assert.Contains(handles, h => h.GetAttribute("data-end") == "source");
        Assert.Contains(handles, h => h.GetAttribute("data-end") == "target");
        Assert.All(handles, h => Assert.Equal("a-b", h.GetAttribute("data-edge-id")));
    }

    [Fact]
    public void EdgesReconnectable_False_Suppresses_The_Handles_Even_When_Selected()
    {
        var (cut, _, _) = RenderBoundWithEdges(ThreeNodes(), OneEdge(), p => p.Add(c => c.EdgesReconnectable, false));
        cut.Find("[data-flow-edge][data-edge-id='a-b']").Click();
        Assert.Empty(cut.FindAll("[data-flow-edge-end]"));
    }

    [Fact]
    public void Readonly_Suppresses_The_Handles_Even_When_Selected()
    {
        var (cut, _, _) = RenderBoundWithEdges(ThreeNodes(), OneEdge(), p => p.Add(c => c.Readonly, true));
        cut.Find("[data-flow-edge][data-edge-id='a-b']").Click(); // selection itself still works under Readonly
        Assert.Empty(cut.FindAll("[data-flow-edge-end]"));
    }
}
