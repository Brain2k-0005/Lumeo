using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// Phase 5: FlowLayout lays out per group — each sibling set on its own, children inside their
/// group (relative, inset by padding + label row), groups grown to fit, the top level laid out with
/// cross-group edges lifted to the groups that contain their ends.
/// </summary>
public class FlowLayoutGroupTests
{
    private static List<L.FlowNode> Nodes() => new()
    {
        new L.FlowNode("start", 0, 0, Width: 100, Height: 40),
        new L.FlowNode("g", 0, 0, Type: "group", Width: 50, Height: 50),
        new L.FlowNode("c1", 999, 999, Width: 100, Height: 40, ParentId: "g"),
        new L.FlowNode("c2", 999, 999, Width: 100, Height: 40, ParentId: "g"),
        new L.FlowNode("end", 0, 0, Width: 100, Height: 40),
    };

    private static List<L.FlowEdge> Edges() => new()
    {
        new L.FlowEdge("s-c1", "start", "c1"), // crosses into the group: lifted to start -> g
        new L.FlowEdge("c1-c2", "c1", "c2"),
        new L.FlowEdge("c2-end", "c2", "end"), // lifted to g -> end
    };

    private static L.FlowNode N(IEnumerable<L.FlowNode> nodes, string id) => nodes.Single(n => n.Id == id);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Children_Are_Laid_Out_Inside_Their_Group_Relative_To_It(bool tree)
    {
        var opts = new L.FlowLayoutOptions(NodeSpacing: 10, RankSpacing: 50);
        var result = tree ? L.FlowLayout.Tree(Nodes(), Edges(), opts) : L.FlowLayout.Layered(Nodes(), Edges(), opts);

        // Inside the group: c1 then c2 left-to-right, starting at (padding, padding + label row).
        Assert.Equal((20d, 48d), (N(result, "c1").X, N(result, "c1").Y));
        Assert.Equal((170d, 48d), (N(result, "c2").X, N(result, "c2").Y)); // 20 + 100 + 50
        Assert.Equal("g", N(result, "c1").ParentId);

        // The group grew (never shrank) to hold them: 170 + 100 + 20 wide, 48 + 40 + 20 tall.
        Assert.Equal((290d, 108d), (N(result, "g").Width!.Value, N(result, "g").Height!.Value));

        // Top level: start -> g -> end, ranked left to right.
        Assert.True(N(result, "start").X < N(result, "g").X);
        Assert.True(N(result, "g").X + 290 <= N(result, "end").X);
        Assert.Equal(new[] { "start", "g", "c1", "c2", "end" }, result.Select(n => n.Id)); // input order kept
    }

    [Fact]
    public void A_Group_Larger_Than_Its_Content_Keeps_Its_Size()
    {
        var nodes = Nodes();
        nodes[1] = nodes[1] with { Width = 1000, Height = 600 };
        var result = L.FlowLayout.Tree(nodes, Edges());
        Assert.Equal((1000d, 600d), (N(result, "g").Width!.Value, N(result, "g").Height!.Value));
    }

    [Fact]
    public void Without_Groups_The_Result_Matches_The_Flat_Algorithm()
    {
        var flat = new List<L.FlowNode> { new("a", 0, 0), new("b", 0, 0), new("c", 0, 0) };
        var edges = new List<L.FlowEdge> { new("ab", "a", "b"), new("ac", "a", "c") };
        var result = L.FlowLayout.Tree(flat, edges, new L.FlowLayoutOptions(NodeSpacing: 10, RankSpacing: 20));
        Assert.Equal((0d, 25d), (N(result, "a").X, N(result, "a").Y)); // centred over b (0) and c (50)
        Assert.Equal((170d, 0d), (N(result, "b").X, N(result, "b").Y));
        Assert.Equal((170d, 50d), (N(result, "c").X, N(result, "c").Y));
    }

    [Fact]
    public void Custom_Group_Padding_And_Header_Are_Honoured()
    {
        var result = L.FlowLayout.Layered(Nodes(), Edges(), new L.FlowLayoutOptions(GroupPadding: 5, GroupHeaderHeight: 0));
        Assert.Equal((5d, 5d), (N(result, "c1").X, N(result, "c1").Y));
    }
}
