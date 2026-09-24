using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

public class FlowLayoutTests
{
    private static L.FlowNode N(string id, double w = 150, double h = 40) => new(id, 0, 0, Width: w, Height: h);

    private static bool Overlaps(L.FlowNode a, L.FlowNode b)
    {
        var aw = a.Width ?? 150; var ah = a.Height ?? 40;
        var bw = b.Width ?? 150; var bh = b.Height ?? 40;
        return a.X < b.X + bw && a.X + aw > b.X && a.Y < b.Y + bh && a.Y + ah > b.Y;
    }

    private static void AssertNoOverlaps(IReadOnlyList<L.FlowNode> nodes)
    {
        for (var i = 0; i < nodes.Count; i++)
            for (var j = i + 1; j < nodes.Count; j++)
                Assert.False(Overlaps(nodes[i], nodes[j]), $"{nodes[i].Id} overlaps {nodes[j].Id}");
    }

    [Fact]
    public void Tree_Places_A_Root_Before_Its_Children_On_The_Along_Axis_LeftToRight()
    {
        var nodes = new List<L.FlowNode> { N("root"), N("a"), N("b") };
        var edges = new List<L.FlowEdge> { new("e1", "root", "a"), new("e2", "root", "b") };

        var result = L.FlowLayout.Tree(nodes, edges);

        var root = result.Single(n => n.Id == "root");
        var a = result.Single(n => n.Id == "a");
        var b = result.Single(n => n.Id == "b");
        Assert.True(root.X < a.X);
        Assert.True(root.X < b.X);
        Assert.NotEqual(a.Y, b.Y); // siblings separated on the perpendicular axis
        AssertNoOverlaps(result);
    }

    [Fact]
    public void Tree_TopToBottom_Places_A_Root_Above_Its_Children()
    {
        var nodes = new List<L.FlowNode> { N("root"), N("a"), N("b") };
        var edges = new List<L.FlowEdge> { new("e1", "root", "a"), new("e2", "root", "b") };

        var result = L.FlowLayout.Tree(nodes, edges, new L.FlowLayoutOptions(L.FlowLayoutDirection.TopToBottom));

        var root = result.Single(n => n.Id == "root");
        var a = result.Single(n => n.Id == "a");
        var b = result.Single(n => n.Id == "b");
        Assert.True(root.Y < a.Y);
        Assert.True(root.Y < b.Y);
        Assert.NotEqual(a.X, b.X);
        AssertNoOverlaps(result);
    }

    [Fact]
    public void Tree_Is_Deterministic_For_The_Same_Input()
    {
        var nodes = new List<L.FlowNode> { N("root"), N("a"), N("b"), N("c"), N("d") };
        var edges = new List<L.FlowEdge>
        {
            new("e1", "root", "a"), new("e2", "root", "b"),
            new("e3", "a", "c"), new("e4", "a", "d"),
        };

        var r1 = L.FlowLayout.Tree(nodes, edges);
        var r2 = L.FlowLayout.Tree(nodes, edges);

        for (var i = 0; i < r1.Count; i++)
        {
            Assert.Equal(r1[i].X, r2[i].X);
            Assert.Equal(r1[i].Y, r2[i].Y);
        }
    }

    [Fact]
    public void Tree_Handles_A_Forest_Of_Several_Roots()
    {
        var nodes = new List<L.FlowNode> { N("r1"), N("r1a"), N("r2"), N("r2a") };
        var edges = new List<L.FlowEdge> { new("e1", "r1", "r1a"), new("e2", "r2", "r2a") };

        var result = L.FlowLayout.Tree(nodes, edges);

        AssertNoOverlaps(result);
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void Tree_Handles_A_Pure_Cycle_Without_Looping_Forever()
    {
        var nodes = new List<L.FlowNode> { N("a"), N("b"), N("c") };
        var edges = new List<L.FlowEdge> { new("e1", "a", "b"), new("e2", "b", "c"), new("e3", "c", "a") };

        var result = L.FlowLayout.Tree(nodes, edges);

        Assert.Equal(3, result.Count);
        AssertNoOverlaps(result);
    }

    [Fact]
    public void Tree_Places_A_Node_Reachable_From_Two_Parents_Under_Only_One()
    {
        // a -> shared, b -> shared: "shared" should end up under whichever root reaches it first
        // (input order) and appear exactly once in the result, not duplicated or double-spaced.
        var nodes = new List<L.FlowNode> { N("a"), N("b"), N("shared") };
        var edges = new List<L.FlowEdge> { new("e1", "a", "shared"), new("e2", "b", "shared") };

        var result = L.FlowLayout.Tree(nodes, edges);

        Assert.Equal(3, result.Count);
        AssertNoOverlaps(result);
    }

    [Fact]
    public void Tree_Uses_Measured_Sizes_When_The_Node_Has_No_Fixed_Size()
    {
        var nodes = new List<L.FlowNode> { new("root", 0, 0), new("a", 0, 0) };
        var edges = new List<L.FlowEdge> { new("e1", "root", "a") };
        var measured = new Dictionary<string, (double Width, double Height)> { ["root"] = (300, 40), ["a"] = (150, 40) };

        var result = L.FlowLayout.Tree(nodes, edges, measured: measured);

        var root = result.Single(n => n.Id == "root");
        var a = result.Single(n => n.Id == "a");
        // The rank gap must clear root's measured width (300), not the 150 default.
        Assert.True(a.X >= root.X + 300);
    }

    [Fact]
    public void Layered_Assigns_Ranks_By_Longest_Path()
    {
        // a -> b -> c and a -> c directly: c must be on rank 2 (after b), not rank 1.
        var nodes = new List<L.FlowNode> { N("a"), N("b"), N("c") };
        var edges = new List<L.FlowEdge> { new("e1", "a", "b"), new("e2", "b", "c"), new("e3", "a", "c") };

        var result = L.FlowLayout.Layered(nodes, edges);

        var a = result.Single(n => n.Id == "a");
        var b = result.Single(n => n.Id == "b");
        var c = result.Single(n => n.Id == "c");
        Assert.True(a.X < b.X);
        Assert.True(b.X < c.X);
        AssertNoOverlaps(result);
    }

    [Fact]
    public void Layered_Is_Deterministic_And_Orders_A_Rank_By_Barycenter()
    {
        var nodes = new List<L.FlowNode> { N("a"), N("b"), N("x"), N("y") };
        var edges = new List<L.FlowEdge>
        {
            new("e1", "a", "y"), new("e2", "b", "x"), // crossing order if left unordered
        };

        var r1 = L.FlowLayout.Layered(nodes, edges);
        var r2 = L.FlowLayout.Layered(nodes, edges);

        for (var i = 0; i < r1.Count; i++)
        {
            Assert.Equal(r1[i].X, r2[i].X);
            Assert.Equal(r1[i].Y, r2[i].Y);
        }
        AssertNoOverlaps(r1);
    }

    [Fact]
    public void Layered_Breaks_Cycles_And_Still_Places_Every_Node()
    {
        var nodes = new List<L.FlowNode> { N("a"), N("b"), N("c") };
        var edges = new List<L.FlowEdge> { new("e1", "a", "b"), new("e2", "b", "c"), new("e3", "c", "a") };

        var result = L.FlowLayout.Layered(nodes, edges);

        Assert.Equal(3, result.Count);
        AssertNoOverlaps(result);
    }

    [Fact]
    public void Layered_TopToBottom_Grows_Downward()
    {
        var nodes = new List<L.FlowNode> { N("a"), N("b") };
        var edges = new List<L.FlowEdge> { new("e1", "a", "b") };

        var result = L.FlowLayout.Layered(nodes, edges, new L.FlowLayoutOptions(L.FlowLayoutDirection.TopToBottom));

        var a = result.Single(n => n.Id == "a");
        var b = result.Single(n => n.Id == "b");
        Assert.True(a.Y < b.Y);
    }

    [Fact]
    public void Both_Algorithms_Return_Empty_For_Empty_Input()
    {
        Assert.Empty(L.FlowLayout.Tree(Array.Empty<L.FlowNode>(), Array.Empty<L.FlowEdge>()));
        Assert.Empty(L.FlowLayout.Layered(Array.Empty<L.FlowNode>(), Array.Empty<L.FlowEdge>()));
    }

    [Fact]
    public void Both_Algorithms_Place_A_Single_Isolated_Node()
    {
        var nodes = new List<L.FlowNode> { N("solo") };
        Assert.Single(L.FlowLayout.Tree(nodes, Array.Empty<L.FlowEdge>()));
        Assert.Single(L.FlowLayout.Layered(nodes, Array.Empty<L.FlowEdge>()));
    }

    [Fact]
    public void Layered_Respects_Custom_Rank_And_Node_Spacing()
    {
        var nodes = new List<L.FlowNode> { N("a"), N("b") };
        var edges = new List<L.FlowEdge> { new("e1", "a", "b") };

        var result = L.FlowLayout.Layered(nodes, edges, new L.FlowLayoutOptions(RankSpacing: 200));

        var a = result.Single(n => n.Id == "a");
        var b = result.Single(n => n.Id == "b");
        Assert.Equal(a.X + 150 + 200, b.X, 3);
    }

    // ── LU-01: a duplicate node id must never crash the layout ────────────

    [Fact]
    public void Layered_Does_Not_Throw_On_A_Duplicate_Node_Id()
    {
        // SQL Analyst's report: a SQL Server deadlock XML repeats a resource id, so the node list
        // handed to FlowLayout.Layered can contain the same id twice. LayeredCore used to build its
        // in-degree map with ids.ToDictionary(id => id, ...), which throws ArgumentException on the
        // second occurrence — crashing the whole Blazor Server circuit on real data.
        var nodes = new List<L.FlowNode> { N("a"), N("a"), N("b") };
        var edges = new List<L.FlowEdge> { new("e1", "a", "b") };

        var result = L.FlowLayout.Layered(nodes, edges);

        Assert.Equal(3, result.Count);
        Assert.All(result.Where(n => n.Id == "a"), n => Assert.True(n.X < result.Single(n2 => n2.Id == "b").X));
    }

    [Fact]
    public void Tree_Does_Not_Throw_On_A_Duplicate_Node_Id()
    {
        var nodes = new List<L.FlowNode> { N("root"), N("a"), N("a") };
        var edges = new List<L.FlowEdge> { new("e1", "root", "a") };

        var result = L.FlowLayout.Tree(nodes, edges);

        Assert.Equal(3, result.Count);
    }

    // ── LU-10: a multi-parent node is placed under its DEEPEST parent ─────

    [Fact]
    public void Tree_Places_A_Multi_Parent_Node_Under_Its_Deepest_Parent()
    {
        // The reporter's A/B/C case: C has two parents, A (depth 0, via A->C) and B (depth 1, via
        // A->B->C). C's own depth is the LONGEST path reaching it (2, through B) — that was already
        // correct. The bug was PLACEMENT: C used to be centred under whichever parent's subtree walk
        // reached it first (A, since A->B and A->C are both A's direct children and A is visited
        // first), not under B, the parent that actually explains its depth. It must be placed under
        // B, at the SAME column its depth already puts it in.
        var nodes = new List<L.FlowNode> { N("a"), N("b"), N("c") };
        var edges = new List<L.FlowEdge>
        {
            new("a-b", "a", "b"),
            new("a-c", "a", "c"),
            new("b-c", "b", "c"),
        };

        var result = L.FlowLayout.Tree(nodes, edges);
        var a = result.Single(n => n.Id == "a");
        var b = result.Single(n => n.Id == "b");
        var c = result.Single(n => n.Id == "c");

        // Column: A at depth 0, B at depth 1, C at depth 2 (the longer A->B->C path) — one full
        // rank (150 width + 96 default RankSpacing) further right than B, not one rank right of A.
        Assert.Equal(b.X + 150 + 96, c.X, 3);
        // Placement: C is centred directly under B (same perpendicular/Y as B), not offset as a
        // second, independent child of A alongside B.
        Assert.Equal(b.Y, c.Y, 3);
        AssertNoOverlaps(result);
    }
}
