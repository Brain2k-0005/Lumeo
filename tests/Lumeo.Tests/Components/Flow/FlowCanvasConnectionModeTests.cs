using Bunit;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// ConnectionMode (phase 4): Strict (default, source-to-target only) vs Loose (any handle to any
/// other handle, direction inferred from the drag) is enforced entirely in flow.js — the pointer
/// gesture and keyboard-connect state machine, not CommitConnect, decide what may even be
/// PROPOSED. What FlowCanvas.razor owns, and what these tests can verify without a real DOM, is
/// that the mode reaches the engine's options bag correctly, and that CommitConnect itself (the
/// commit step, reachable however the gesture decided to call it) is unaffected by the mode.
/// </summary>
public class FlowCanvasConnectionModeTests : FlowCanvasTestBase
{
    [Fact]
    public void Strict_By_Default_Serialises_As_Loose_False()
    {
        RenderBound(ThreeNodes()); // the first render registers the engine with its options bag

        var options = Assert.IsType<L.FlowCanvas.FlowEngineOptions>(Interop.LastFlowOptions);
        Assert.False(options.Loose);
        Assert.Equal("strict", options.ConnectionMode);
    }

    [Fact]
    public void Loose_Serialises_As_Loose_True()
    {
        var (cut, _) = RenderBound(ThreeNodes(), p => p.Add(c => c.ConnectionMode, L.FlowConnectionMode.Loose));

        var options = Assert.IsType<L.FlowCanvas.FlowEngineOptions>(Interop.LastFlowOptions);
        Assert.True(options.Loose);
        Assert.Equal("loose", options.ConnectionMode);
    }

    [Theory]
    [InlineData(L.FlowConnectionMode.Strict)]
    [InlineData(L.FlowConnectionMode.Loose)]
    public async Task CommitConnect_Itself_Behaves_The_Same_In_Either_Mode(L.FlowConnectionMode mode)
    {
        var (cut, _, currentEdges) = RenderBoundWithEdges(ThreeNodes(), new List<L.FlowEdge>(), p => p.Add(c => c.ConnectionMode, mode));

        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitConnect("a", null, "b", null));

        Assert.True(accepted);
        Assert.Single(currentEdges());
    }
}
