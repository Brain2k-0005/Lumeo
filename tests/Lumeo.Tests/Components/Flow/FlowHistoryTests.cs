using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

public class FlowHistoryTests
{
    private static List<L.FlowNode> Nodes(params double[] xs) => xs.Select((x, i) => new L.FlowNode("n" + i, x, 0)).ToList();
    private static readonly List<L.FlowEdge> NoEdges = new();

    [Fact]
    public void Default_Capacity_Is_Fifty()
    {
        var h = new L.FlowHistory();
        Assert.Equal(50, h.Capacity);
    }

    [Fact]
    public void Throws_For_A_Capacity_Below_One()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new L.FlowHistory(0));
    }

    [Fact]
    public void Fresh_History_Cannot_Undo_Or_Redo()
    {
        var h = new L.FlowHistory();
        Assert.False(h.CanUndo);
        Assert.False(h.CanRedo);
        Assert.Null(h.Current);
    }

    [Fact]
    public void After_One_Push_There_Is_Nothing_To_Undo_To()
    {
        var h = new L.FlowHistory();
        h.Push(Nodes(0), NoEdges);
        Assert.False(h.CanUndo); // it IS the baseline
        Assert.False(h.CanRedo);
        Assert.Equal(0, h.Current!.Nodes[0].X);
    }

    [Fact]
    public void Undo_Returns_To_The_Previous_Push_In_Order()
    {
        var h = new L.FlowHistory();
        h.Push(Nodes(0), NoEdges);   // baseline
        h.Push(Nodes(10), NoEdges);  // edit 1
        h.Push(Nodes(20), NoEdges);  // edit 2

        var back1 = h.Undo();
        Assert.Equal(10, back1!.Nodes[0].X);
        var back2 = h.Undo();
        Assert.Equal(0, back2!.Nodes[0].X);
        Assert.False(h.CanUndo);
    }

    [Fact]
    public void Redo_Replays_Forward_In_The_Same_Order_It_Was_Undone()
    {
        var h = new L.FlowHistory();
        h.Push(Nodes(0), NoEdges);
        h.Push(Nodes(10), NoEdges);
        h.Push(Nodes(20), NoEdges);
        h.Undo();
        h.Undo();

        var fwd1 = h.Redo();
        Assert.Equal(10, fwd1!.Nodes[0].X);
        var fwd2 = h.Redo();
        Assert.Equal(20, fwd2!.Nodes[0].X);
        Assert.False(h.CanRedo);
    }

    [Fact]
    public void A_Push_After_An_Undo_Discards_The_Redo_Future()
    {
        var h = new L.FlowHistory();
        h.Push(Nodes(0), NoEdges);
        h.Push(Nodes(10), NoEdges);
        h.Push(Nodes(20), NoEdges);
        h.Undo(); // now at "10", "20" is redoable
        Assert.True(h.CanRedo);

        h.Push(Nodes(99), NoEdges); // a fresh edit instead of redoing

        Assert.False(h.CanRedo);
        Assert.Equal(99, h.Current!.Nodes[0].X);
        var back = h.Undo();
        Assert.Equal(10, back!.Nodes[0].X); // "20" is gone
    }

    [Fact]
    public void Undo_Returns_Null_When_There_Is_Nothing_To_Undo()
    {
        var h = new L.FlowHistory();
        Assert.Null(h.Undo());
        h.Push(Nodes(0), NoEdges);
        Assert.Null(h.Undo());
    }

    [Fact]
    public void Redo_Returns_Null_When_There_Is_Nothing_To_Redo()
    {
        var h = new L.FlowHistory();
        Assert.Null(h.Redo());
        h.Push(Nodes(0), NoEdges);
        Assert.Null(h.Redo());
    }

    [Fact]
    public void The_Stack_Is_Capped_And_Drops_The_Oldest_Entry()
    {
        var h = new L.FlowHistory(capacity: 3);
        h.Push(Nodes(0), NoEdges);
        h.Push(Nodes(1), NoEdges);
        h.Push(Nodes(2), NoEdges);
        h.Push(Nodes(3), NoEdges); // drops the "0" baseline
        Assert.Equal(3, h.Count);

        h.Undo();
        var oldest = h.Undo();
        Assert.Equal(1, oldest!.Nodes[0].X); // "0" is gone, not "1"
        Assert.False(h.CanUndo);
    }

    [Fact]
    public void Changed_Fires_On_Push_Undo_Redo_And_Clear()
    {
        var h = new L.FlowHistory();
        var count = 0;
        h.Changed += () => count++;

        h.Push(Nodes(0), NoEdges);
        Assert.Equal(1, count);
        h.Push(Nodes(1), NoEdges);
        Assert.Equal(2, count);
        h.Undo();
        Assert.Equal(3, count);
        h.Redo();
        Assert.Equal(4, count);
        h.Clear();
        Assert.Equal(5, count);
    }

    [Fact]
    public void Clear_Resets_Everything_And_A_New_Push_Re_Seeds_The_Baseline()
    {
        var h = new L.FlowHistory();
        h.Push(Nodes(0), NoEdges);
        h.Push(Nodes(1), NoEdges);
        h.Clear();

        Assert.Null(h.Current);
        Assert.False(h.CanUndo);
        Assert.False(h.CanRedo);

        h.Push(Nodes(5), NoEdges);
        Assert.False(h.CanUndo); // re-seeded baseline
        Assert.Equal(5, h.Current!.Nodes[0].X);
    }
}
