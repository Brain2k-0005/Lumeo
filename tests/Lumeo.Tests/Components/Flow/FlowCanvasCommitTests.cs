using Bunit;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// The drag commit path: flow.js moves nodes live and calls CommitNodeDrag once on drop, with the
/// node-list generation the drag started under. The canvas applies the positions through
/// NodesChanged (and OnNodeDragStop) unless the drag is stale or editing is off.
/// </summary>
public class FlowCanvasCommitTests : FlowCanvasTestBase
{
    [Fact]
    public async Task A_Commit_Updates_Nodes_Through_NodesChanged_And_Raises_OnNodeDragStop()
    {
        IReadOnlyList<L.FlowNodeChange>? stopped = null;
        var (cut, current) = RenderBound(ThreeNodes(), p => p.Add(c => c.OnNodeDragStop, (IReadOnlyList<L.FlowNodeChange> ch) => stopped = ch));
        var gen = cut.Instance._state.Generation;

        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("a", 48, 96) }, gen));

        Assert.True(accepted);
        Assert.Equal((48d, 96d), (Node(current(), "a").X, Node(current(), "a").Y));
        Assert.Equal((300d, 40d), (Node(current(), "b").X, Node(current(), "b").Y)); // untouched
        Assert.NotNull(stopped);
        Assert.Equal(new L.FlowNodeChange("a", 48, 96), Assert.Single(stopped!));

        var a = cut.Find("[data-flow-node='a']");
        Assert.Equal("48", a.GetAttribute("data-x"));
        Assert.Contains("translate(48px, 96px)", a.GetAttribute("style"));
    }

    [Fact]
    public async Task A_Commit_Redraws_The_Connected_Edges_From_The_New_Positions()
    {
        var (cut, _) = RenderBound(ThreeNodes(), p => p.Add(c => c.Edges, TwoEdges()));
        await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("a", -100, 200) }, cut.Instance._state.Generation));
        var expected = L.FlowGeometry.GetBezierPath(50, 220, L.FlowPosition.Right, 300, 60, L.FlowPosition.Left).D;
        Assert.Equal(expected, cut.Find("[data-edge-id='a-b']").GetAttribute("d"));
    }

    [Fact]
    public async Task Our_Own_List_Coming_Back_Through_The_Binding_Does_Not_Bump_The_Generation()
    {
        var (cut, _) = RenderBound(ThreeNodes());
        var gen = cut.Instance._state.Generation;
        await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("a", 1, 1) }, gen));
        Assert.Equal(gen, cut.Instance._state.Generation);

        // ...so a second drag that started after the first commit still commits.
        Assert.True(await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("b", 2, 2) }, gen)));
    }

    [Fact]
    public async Task A_Commit_From_A_Drag_That_Started_Before_An_External_Replace_Is_Rejected()
    {
        var (cut, current) = RenderBound(ThreeNodes());
        var dragGeneration = cut.Instance._state.Generation;

        // While the drag is in flight the app replaces the node list (server push, "reset layout").
        var replaced = new List<L.FlowNode> { new("a", 500, 500), new("b", 600, 600) };
        cut.Render(p => p.Add(c => c.Nodes, replaced));
        Assert.Equal(dragGeneration + 1, cut.Instance._state.Generation);
        Assert.Equal(dragGeneration + 1, int.Parse(cut.Find("[data-slot='flow-pane']").GetAttribute("data-flow-generation")!));

        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("a", 1, 1) }, dragGeneration));

        Assert.False(accepted);
        Assert.Equal(500, Node(cut.Instance.CurrentNodes, "a").X);
        Assert.Equal("500", cut.Find("[data-flow-node='a']").GetAttribute("data-x"));
        Assert.Equal(0, Node(current(), "a").X); // NodesChanged never fired with the stale drop
    }

    /// <summary>
    /// The late-commit race (memory: blazor-guard-before-await). The app's NodesChanged handler is
    /// slow — it awaits a server round trip (the TCS) — and a second drop arrives while the first
    /// is still being handled. The second commit must build on the first one's list, not on the
    /// list the first replaced, or the first move is silently undone.
    /// </summary>
    [Fact]
    public async Task A_Second_Commit_While_The_First_Is_Still_Being_Handled_Keeps_Both_Moves()
    {
        var gate = new TaskCompletionSource();
        var emitted = new List<IReadOnlyList<L.FlowNode>>();
        IRenderedComponent<L.FlowCanvas>? cut = null;
        cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .Add(c => c.FitViewOnInit, false)
            .Add(c => c.NodesChanged, async (IReadOnlyList<L.FlowNode> n) =>
            {
                emitted.Add(n);
                if (emitted.Count == 1) await gate.Task; // the first save is slow
                cut!.Render(pp => pp.Add(c => c.Nodes, n));
            }));
        var gen = cut.Instance._state.Generation;

        Task<bool> first = null!, second = null!;
        await cut.InvokeAsync(() => { first = cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("a", 11, 11) }, gen); });
        Assert.False(first.IsCompleted);
        await cut.InvokeAsync(() => { second = cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("b", 22, 22) }, gen); });

        gate.SetResult();
        Assert.True(await first);
        Assert.True(await second);

        var last = emitted[^1];
        Assert.Equal((11d, 11d), (Node(last, "a").X, Node(last, "a").Y));
        Assert.Equal((22d, 22d), (Node(last, "b").X, Node(last, "b").Y));
        // The first (older) echo landing after the second must not roll the canvas back either.
        Assert.Equal(11, Node(cut.Instance.CurrentNodes, "a").X);
        Assert.Equal(22, Node(cut.Instance.CurrentNodes, "b").X);
    }

    [Fact]
    public async Task An_Older_Echo_Arriving_After_A_Newer_One_Does_Not_Roll_Back()
    {
        var (cut, _) = RenderBound(ThreeNodes());
        var gen = cut.Instance._state.Generation;
        IReadOnlyList<L.FlowNode>? firstList = null;
        await cut.InvokeAsync(async () =>
        {
            await cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("a", 5, 5) }, gen);
            firstList = cut.Instance.CurrentNodes;
            await cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("a", 9, 9) }, gen);
        });
        cut.Render(p => p.Add(c => c.Nodes, firstList)); // a parent replaying the older list
        Assert.Equal(9, Node(cut.Instance.CurrentNodes, "a").X);
        Assert.Equal(gen, cut.Instance._state.Generation);
    }

    [Fact]
    public async Task Readonly_Rejects_A_Commit()
    {
        var (cut, current) = RenderBound(ThreeNodes(), p => p.Add(c => c.Readonly, true));
        Assert.False(await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("a", 1, 1) }, cut.Instance._state.Generation)));
        Assert.Equal(0, Node(current(), "a").X);
    }

    [Fact]
    public async Task NodesDraggable_False_Rejects_A_Commit()
    {
        var (cut, _) = RenderBound(ThreeNodes(), p => p.Add(c => c.NodesDraggable, false));
        Assert.False(await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("a", 1, 1) }, cut.Instance._state.Generation)));
    }

    [Fact]
    public async Task A_Non_Draggable_Node_In_The_Payload_Is_Ignored()
    {
        IReadOnlyList<L.FlowNodeChange>? stopped = null;
        var (cut, current) = RenderBound(ThreeNodes(), p => p.Add(c => c.OnNodeDragStop, (IReadOnlyList<L.FlowNodeChange> ch) => stopped = ch));
        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[]
        {
            new L.FlowNodeChange("c", 999, 999),   // Draggable: false
            new L.FlowNodeChange("b", 10, 20),
            new L.FlowNodeChange("ghost", 1, 1),   // not on the canvas
        }, cut.Instance._state.Generation));
        Assert.True(accepted);
        Assert.Equal(120, Node(current(), "c").X);
        Assert.Equal(10, Node(current(), "b").X);
        Assert.Equal("b", Assert.Single(stopped!).Id);
    }

    [Fact]
    public async Task A_Commit_With_Only_Unknown_Or_Invalid_Entries_Is_Rejected()
    {
        var (cut, _) = RenderBound(ThreeNodes());
        var gen = cut.Instance._state.Generation;
        Assert.False(await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("ghost", 1, 1) }, gen)));
        Assert.False(await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("a", double.NaN, 1) }, gen)));
        Assert.False(await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(Array.Empty<L.FlowNodeChange>(), gen)));
    }

    [Fact]
    public async Task Without_A_Binding_The_Canvas_Keeps_Its_Own_Copy()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.FitViewOnInit, false));
        Assert.True(await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("a", 70, 80) }, cut.Instance._state.Generation)));
        Assert.Equal("70", cut.Find("[data-flow-node='a']").GetAttribute("data-x"));
    }

    [Fact]
    public async Task A_Commit_Clears_The_Dragging_State()
    {
        var (cut, _) = RenderBound(ThreeNodes());
        await cut.InvokeAsync(() => cut.Instance.NodeDragStart(new[] { "a" }));
        Assert.NotNull(cut.Find("[data-flow-node='a']").GetAttribute("data-dragging"));
        await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("a", 3, 3) }, cut.Instance._state.Generation));
        Assert.Null(cut.Find("[data-flow-node='a']").GetAttribute("data-dragging"));
    }
}
