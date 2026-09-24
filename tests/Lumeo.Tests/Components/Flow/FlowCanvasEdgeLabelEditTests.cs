using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// Inline edge-label editing (phase 4, EdgeLabelEditable): double-clicking a label opens an
/// inline Lumeo Input pre-filled with the edge's current label, Enter commits
/// (EdgesChanged + OnEdgeLabelChanged), Escape cancels without changing anything.
/// </summary>
public class FlowCanvasEdgeLabelEditTests : FlowCanvasTestBase
{
    private static List<L.FlowEdge> LabelledEdge() => new() { new("a-b", "a", "b", Label: "Approved") };

    [Fact]
    public void EdgeLabelEditable_True_Renders_No_Label_Div_For_An_Unlabelled_Edge()
    {
        // A regression the E2E suite caught: an always-present (even empty) label div at every
        // edge's midpoint would sit on top of a bare edge's own path and eat its click — breaking
        // "click an edge to select it" for every edge with no Label the moment EdgeLabelEditable
        // is turned on anywhere on the canvas.
        var edges = new List<L.FlowEdge> { new("a-b", "a", "b") }; // no Label
        var (cut, _, _) = RenderBoundWithEdges(ThreeNodes(), edges, p => p.Add(c => c.EdgeLabelEditable, true));

        Assert.Empty(cut.FindAll("[data-flow-edge-label][data-edge-id='a-b']"));
    }

    [Fact]
    public void EdgeLabelEditable_False_Ignores_A_DoubleClick()
    {
        var (cut, _, _) = RenderBoundWithEdges(ThreeNodes(), LabelledEdge());

        cut.Find("[data-flow-edge-label][data-edge-id='a-b']").DoubleClick();

        Assert.Empty(cut.FindAll("[data-flow-edge-label][data-edge-id='a-b'] input"));
    }

    [Fact]
    public void DoubleClick_Opens_An_Inline_Input_Prefilled_With_The_Current_Label()
    {
        var (cut, _, _) = RenderBoundWithEdges(ThreeNodes(), LabelledEdge(), p => p.Add(c => c.EdgeLabelEditable, true));

        cut.Find("[data-flow-edge-label][data-edge-id='a-b']").DoubleClick();

        var input = cut.Find("[data-flow-edge-label][data-edge-id='a-b'] input");
        Assert.Equal("Approved", input.GetAttribute("value"));
    }

    [Fact]
    public void Enter_Commits_The_New_Label_Raising_EdgesChanged_And_OnEdgeLabelChanged()
    {
        // RenderBoundWithEdges already owns EdgesChanged (to track currentEdges()), so this
        // asserts EdgesChanged fired via its effect (the bound list) and OnEdgeLabelChanged fired
        // with the SAME updated edge, rather than wiring a second EdgesChanged handler (bUnit
        // rejects adding the same component parameter twice).
        L.FlowEdge? seen = null;
        var (cut, _, currentEdges) = RenderBoundWithEdges(ThreeNodes(), LabelledEdge(), p => p
            .Add(c => c.EdgeLabelEditable, true)
            .Add(c => c.OnEdgeLabelChanged, (L.FlowEdge e) => seen = e));
        cut.Find("[data-flow-edge-label][data-edge-id='a-b']").DoubleClick();
        var input = cut.Find("[data-flow-edge-label][data-edge-id='a-b'] input");
        input.Input("Rejected");

        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("Rejected", currentEdges()[0].Label);
        Assert.NotNull(seen);
        Assert.Equal("Rejected", seen!.Label);
        Assert.Empty(cut.FindAll("[data-flow-edge-label][data-edge-id='a-b'] input")); // editing closed
    }

    [Fact]
    public void An_Empty_Committed_Label_Clears_It_To_Null()
    {
        var (cut, _, currentEdges) = RenderBoundWithEdges(ThreeNodes(), LabelledEdge(), p => p.Add(c => c.EdgeLabelEditable, true));
        cut.Find("[data-flow-edge-label][data-edge-id='a-b']").DoubleClick();
        var input = cut.Find("[data-flow-edge-label][data-edge-id='a-b'] input");
        input.Input("");

        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Null(currentEdges()[0].Label);
    }

    [Fact]
    public void Escape_Cancels_Without_Changing_The_Label()
    {
        var (cut, _, currentEdges) = RenderBoundWithEdges(ThreeNodes(), LabelledEdge(), p => p.Add(c => c.EdgeLabelEditable, true));
        cut.Find("[data-flow-edge-label][data-edge-id='a-b']").DoubleClick();
        var input = cut.Find("[data-flow-edge-label][data-edge-id='a-b'] input");
        input.Input("Rejected");

        input.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal("Approved", currentEdges()[0].Label);
        Assert.Empty(cut.FindAll("[data-flow-edge-label][data-edge-id='a-b'] input"));
    }

    [Fact]
    public void Readonly_Blocks_Opening_The_Editor_At_All()
    {
        var (cut, _, currentEdges) = RenderBoundWithEdges(ThreeNodes(), LabelledEdge(), p => p
            .Add(c => c.EdgeLabelEditable, true)
            .Add(c => c.Readonly, true));

        cut.Find("[data-flow-edge-label][data-edge-id='a-b']").DoubleClick();

        Assert.Empty(cut.FindAll("[data-flow-edge-label][data-edge-id='a-b'] input"));
        Assert.Equal("Approved", currentEdges()[0].Label);
    }
}
