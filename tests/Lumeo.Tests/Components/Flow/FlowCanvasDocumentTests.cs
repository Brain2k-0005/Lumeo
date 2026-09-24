using System.Text.Json;
using Bunit;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// FlowDocument export/import (phase 4): ToDocument() snapshots the canvas' current nodes, edges
/// and viewport; LoadDocumentAsync replaces them (clearing the selection, pushing one History
/// snapshot) and round-trips through System.Text.Json.
/// </summary>
public class FlowCanvasDocumentTests : FlowCanvasTestBase
{
    [Fact]
    public void ToDocument_Snapshots_Nodes_Edges_And_Viewport()
    {
        var (cut, _, _) = RenderBoundWithEdges(ThreeNodes(), TwoEdges(), p => p.Add(c => c.Viewport, new L.FlowViewport(12, -8, 1.5)));

        var doc = cut.Instance.ToDocument();

        Assert.Equal(3, doc.Nodes.Count);
        Assert.Equal(2, doc.Edges.Count);
        Assert.Equal(new L.FlowViewport(12, -8, 1.5), doc.Viewport);
    }

    [Fact]
    public async Task LoadDocumentAsync_Replaces_Nodes_Edges_And_Viewport()
    {
        var (cut, currentNodes, currentEdges) = RenderBoundWithEdges(ThreeNodes(), TwoEdges());
        var doc = new L.FlowDocument(
            new List<L.FlowNode> { new("x", 5, 5), new("y", 50, 50) },
            new List<L.FlowEdge> { new("x-y", "x", "y") },
            new L.FlowViewport(1, 2, 1.25));

        await cut.InvokeAsync(() => cut.Instance.LoadDocumentAsync(doc));

        Assert.Equal(new[] { "x", "y" }, currentNodes().Select(n => n.Id));
        Assert.Single(currentEdges());
        Assert.Equal(new L.FlowViewport(1, 2, 1.25), cut.Instance.CurrentViewport);
    }

    [Fact]
    public async Task LoadDocumentAsync_Clears_The_Selection()
    {
        var (cut, _, _) = RenderBoundWithEdges(ThreeNodes(), TwoEdges());
        cut.Find("[data-flow-node='a']").Click();
        Assert.True(cut.Instance.IsSelected("a"));

        await cut.InvokeAsync(() => cut.Instance.LoadDocumentAsync(new L.FlowDocument(ThreeNodes(), new List<L.FlowEdge>(), new L.FlowViewport(0, 0, 1))));

        Assert.False(cut.Instance.IsSelected("a"));
    }

    [Fact]
    public async Task LoadDocumentAsync_Pushes_Exactly_One_History_Snapshot()
    {
        var history = new L.FlowHistory();
        var (cut, _, _) = RenderBoundWithEdges(ThreeNodes(), new List<L.FlowEdge>(), p => p.Add(c => c.History, history));
        var countBefore = history.Count;

        await cut.InvokeAsync(() => cut.Instance.LoadDocumentAsync(new L.FlowDocument(ThreeNodes(), new List<L.FlowEdge>(), new L.FlowViewport(0, 0, 1))));

        Assert.Equal(countBefore + 1, history.Count);
    }

    [Fact]
    public void FlowDocument_Round_Trips_Through_SystemTextJson()
    {
        var doc = new L.FlowDocument(
            (IReadOnlyList<L.FlowNode>)ThreeNodes(),
            (IReadOnlyList<L.FlowEdge>)TwoEdges(),
            new L.FlowViewport(4, -2, 1.1));

        var json = JsonSerializer.Serialize(doc);
        var back = JsonSerializer.Deserialize<L.FlowDocument>(json);

        Assert.NotNull(back);
        Assert.Equal(doc.Nodes.Count, back!.Nodes.Count);
        Assert.Equal(doc.Nodes.Select(n => n.Id), back.Nodes.Select(n => n.Id));
        Assert.Equal(doc.Edges.Select(e => e.Id), back.Edges.Select(e => e.Id));
        Assert.Equal(doc.Viewport, back.Viewport);
    }
}
