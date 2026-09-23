using Bunit;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// Edge rendering (phase 2): labels (plain text or a template) positioned at the path midpoint,
/// the end-marker arrow, animated/dashed strokes, and the selected-vs-default stroke colour.
/// </summary>
public class FlowCanvasEdgeRenderingTests : FlowCanvasTestBase
{
    [Fact]
    public void A_Plain_Label_Renders_At_The_Path_Midpoint()
    {
        var edges = new List<L.FlowEdge> { new("a-b", "a", "b", Label: "Approved") };
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.Edges, edges));

        var label = cut.Find("[data-flow-edge-label][data-edge-id='a-b']");
        Assert.Contains("Approved", label.TextContent);
        var expected = L.FlowGeometry.GetBezierPath(150, 20, L.FlowPosition.Right, 300, 60, L.FlowPosition.Left);
        Assert.Contains("left:" + L.FlowGeometry.Fmt(expected.LabelX) + "px", label.GetAttribute("style"));
        Assert.Contains("top:" + L.FlowGeometry.Fmt(expected.LabelY) + "px", label.GetAttribute("style"));
    }

    [Fact]
    public void No_Label_Div_Renders_When_The_Edge_Has_No_Label_And_No_Template()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.Edges, TwoEdges()));
        Assert.Empty(cut.FindAll("[data-flow-edge-label]"));
    }

    [Fact]
    public void EdgeLabelTemplate_Receives_The_Edge_Selection_And_Position()
    {
        var edges = new List<L.FlowEdge> { new("a-b", "a", "b") };
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .Add(c => c.Edges, edges)
            .Add(c => c.EdgeLabelTemplate, (L.FlowEdgeContext ctx) => builder =>
            {
                builder.OpenElement(0, "span");
                builder.AddAttribute(1, "data-testid", "tpl");
                builder.AddContent(2, ctx.Edge.Id + ":" + ctx.Selected);
                builder.CloseElement();
            }));

        Assert.Equal("a-b:False", cut.Find("[data-testid='tpl']").TextContent);
    }

    [Fact]
    public void MarkerEnd_Arrow_Points_At_The_Layers_Marker()
    {
        var edges = new List<L.FlowEdge> { new("a-b", "a", "b", MarkerEnd: "arrow") };
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.Edges, edges));
        var path = cut.Find("[data-flow-edge][data-edge-id='a-b']");
        Assert.StartsWith("url(#", path.GetAttribute("marker-end"));
        var markerId = path.GetAttribute("marker-end")!.Substring(5).TrimEnd(')');
        Assert.NotNull(cut.Find("marker#" + markerId));
    }

    [Fact]
    public void MarkerEnd_Null_Draws_No_Marker()
    {
        var edges = new List<L.FlowEdge> { new("a-b", "a", "b", MarkerEnd: null) };
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.Edges, edges));
        Assert.Null(cut.Find("[data-flow-edge][data-edge-id='a-b']").GetAttribute("marker-end"));
    }

    [Fact]
    public void Dashed_And_Animated_Edges_Get_A_Stroke_Dasharray()
    {
        var edges = new List<L.FlowEdge> { new("dashed", "a", "b", Dashed: true), new("animated", "a", "c", Animated: true) };
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.Edges, edges));
        Assert.Equal("5 4", cut.Find("[data-flow-edge][data-edge-id='dashed']").GetAttribute("stroke-dasharray"));
        Assert.Equal("5 4", cut.Find("[data-flow-edge][data-edge-id='animated']").GetAttribute("stroke-dasharray"));
        Assert.NotNull(cut.Find("[data-flow-edge][data-edge-id='animated']").GetAttribute("data-animated"));
        Assert.Null(cut.Find("[data-flow-edge][data-edge-id='dashed']").GetAttribute("data-animated"));
    }

    [Fact]
    public void Edges_Are_Not_Clickable_When_Selection_Is_Off()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .Add(c => c.Edges, TwoEdges())
            .Add(c => c.ElementsSelectable, false));
        Assert.Contains("pointer-events:none", cut.Find("[data-flow-edge][data-edge-id='a-b']").GetAttribute("style"));
    }
}
