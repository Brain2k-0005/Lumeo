using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// Phase 5 sub-flows, pure math: a child's X/Y are relative to its parent, absolute positions
/// resolve through the whole chain, missing parents and cycles degrade to top-level, render order
/// puts parents before children, and Extent=Parent clamping.
/// </summary>
public class FlowSubflowGeometryTests
{
    private static List<L.FlowNode> Nested() => new()
    {
        new L.FlowNode("c2", 5, 6, ParentId: "c1"),       // grandchild listed FIRST on purpose
        new L.FlowNode("g", 100, 200, Width: 400, Height: 300),
        new L.FlowNode("c1", 10, 20, ParentId: "g"),
        new L.FlowNode("free", -50, -60),
    };

    [Fact]
    public void Absolute_Positions_Resolve_Through_The_Whole_Parent_Chain()
    {
        var abs = L.FlowGeometry.GetAbsolutePositions(Nested());

        Assert.Equal(new L.FlowPoint(100, 200), abs["g"]);
        Assert.Equal(new L.FlowPoint(110, 220), abs["c1"]);
        Assert.Equal(new L.FlowPoint(115, 226), abs["c2"]);
        Assert.Equal(new L.FlowPoint(-50, -60), abs["free"]);
    }

    [Fact]
    public void A_Missing_Parent_Counts_As_Top_Level()
    {
        var abs = L.FlowGeometry.GetAbsolutePositions(new[] { new L.FlowNode("a", 7, 8, ParentId: "nope") });
        Assert.Equal(new L.FlowPoint(7, 8), abs["a"]);
    }

    [Fact]
    public void A_Parent_Cycle_Makes_Every_Member_Top_Level_And_Terminates()
    {
        var nodes = new[]
        {
            new L.FlowNode("a", 1, 1, ParentId: "b"),
            new L.FlowNode("b", 2, 2, ParentId: "a"),
            new L.FlowNode("self", 3, 3, ParentId: "self"),
            new L.FlowNode("child", 10, 10, ParentId: "a"), // hangs off a cycle member: stays a child of it
        };
        var abs = L.FlowGeometry.GetAbsolutePositions(nodes);

        Assert.Equal(new L.FlowPoint(1, 1), abs["a"]);
        Assert.Equal(new L.FlowPoint(2, 2), abs["b"]);
        Assert.Equal(new L.FlowPoint(3, 3), abs["self"]);
        Assert.Equal(new L.FlowPoint(11, 11), abs["child"]);
    }

    [Fact]
    public void Render_Order_Puts_Parents_Before_Children_And_Keeps_List_Order_Otherwise()
    {
        var h = new L.FlowHierarchy(Nested());
        Assert.Equal(new[] { "g", "free", "c1", "c2" }, h.RenderOrder.Select(n => n.Id));
        Assert.Equal(2, h.MaxDepth);
        Assert.Equal(new[] { "c1", "c2" }, h.DescendantsOf("g"));
        Assert.True(h.IsAncestor("g", "c2"));
        Assert.False(h.IsAncestor("c2", "g"));
    }

    [Fact]
    public void Without_Groups_The_Render_Order_Is_The_Input_List_Itself()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0), new("b", 1, 1) };
        var h = new L.FlowHierarchy(nodes);
        Assert.False(h.HasGroups);
        Assert.Same(nodes, h.RenderOrder);
    }

    [Theory]
    [InlineData(-10, -10, 0, 0)]      // pulled back to the top-left corner
    [InlineData(500, 500, 250, 260)]  // pushed back to the bottom-right (400-150, 300-40)
    [InlineData(40, 50, 40, 50)]      // already inside: untouched
    public void ClampToParent_Keeps_The_Child_Inside(double x, double y, double ex, double ey)
    {
        var p = L.FlowGeometry.ClampToParent(x, y, 150, 40, 400, 300);
        Assert.Equal(new L.FlowPoint(ex, ey), p);
    }

    [Fact]
    public void ClampToParent_Pins_A_Child_Larger_Than_Its_Parent_To_The_Top_Left()
    {
        Assert.Equal(new L.FlowPoint(0, 0), L.FlowGeometry.ClampToParent(30, 30, 500, 500, 100, 100));
    }

    [Fact]
    public void The_Twelve_Variable_Deconstruction_Still_Compiles()
    {
        var node = new L.FlowNode("n", 1, 2, ParentId: "g", Extent: L.FlowExtent.Parent);
        var (id, x, y, _, _, _, _, _, _, _, _, _) = node;
        Assert.Equal(("n", 1d, 2d), (id, x, y));
        var (_, _, _, _, _, _, _, _, _, _, _, _, parentId, extent) = node;
        Assert.Equal("g", parentId);
        Assert.Equal(L.FlowExtent.Parent, extent);
    }
}
