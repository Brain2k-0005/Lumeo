using Bunit;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>FlowNodeToolbar: renders only over a single selected node, positioned from its rect and the current viewport.</summary>
public class FlowNodeToolbarTests : FlowCanvasTestBase
{
    [Fact]
    public void Renders_Nothing_With_No_Selection()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .AddChildContent<L.FlowNodeToolbar>(t => t.AddChildContent("<button>Do</button>")));
        Assert.Empty(cut.FindAll("[data-slot='flow-toolbar']"));
    }

    [Fact]
    public void Renders_Nothing_With_Multiple_Nodes_Selected()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .AddChildContent<L.FlowNodeToolbar>(t => t.AddChildContent("<button>Do</button>")));
        cut.Find("[data-flow-node='a']").Click();
        cut.Find("[data-flow-node='b']").Click(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { ShiftKey = true });
        Assert.Empty(cut.FindAll("[data-slot='flow-toolbar']"));
    }

    [Fact]
    public void Renders_Above_The_Single_Selected_Node_By_Default()
    {
        var nodes = new List<L.FlowNode> { new("a", 100, 100, Width: 120, Height: 40) };
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, nodes)
            .AddChildContent<L.FlowNodeToolbar>(t => t.AddChildContent("<button>Do</button>")));
        cut.Find("[data-flow-node='a']").Click();

        var toolbar = cut.Find("[data-slot='flow-toolbar']");
        Assert.Equal("a", toolbar.GetAttribute("data-flow-toolbar-for"));
        Assert.Equal("top", toolbar.GetAttribute("data-flow-toolbar-position"));
        // Centre X = 100 + 120/2 = 160 (viewport starts at 0,0 / zoom 1 since FitViewOnInit is off by default here... )
        Assert.Contains("left:160px", toolbar.GetAttribute("style"));
    }

    [Fact]
    public void Bottom_Position_Anchors_Under_The_Node()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0, Width: 100, Height: 50) };
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, nodes)
            .AddChildContent<L.FlowNodeToolbar>(t => t.Add(x => x.Position, L.FlowNodeToolbar.ToolbarPosition.Bottom).AddChildContent("<button>Do</button>")));
        cut.Find("[data-flow-node='a']").Click();
        Assert.Equal("bottom", cut.Find("[data-slot='flow-toolbar']").GetAttribute("data-flow-toolbar-position"));
    }

    [Fact]
    public void Renders_Nothing_Without_A_Canvas_Context()
    {
        var cut = Ctx.Render<L.FlowNodeToolbar>(p => p.AddChildContent("<button>Do</button>"));
        Assert.Empty(cut.FindAll("[data-slot='flow-toolbar']"));
    }
}
