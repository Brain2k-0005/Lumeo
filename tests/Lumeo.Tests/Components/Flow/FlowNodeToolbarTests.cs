using Bunit;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// FlowNodeToolbar: with one selected node it renders over that node's own rect (and carries
/// data-flow-toolbar-for so flow.js can track it live during a pointer drag); with two or more
/// (phase 4) it renders ONCE, at the union of every selected node's rect, with no
/// data-flow-toolbar-for (no live per-frame drag tracking for a multi-selection — see
/// FlowNodeToolbar.razor's own remark).
/// </summary>
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
    public void With_Two_Selected_Nodes_One_Toolbar_Renders_At_Their_Bounding_Box()
    {
        var nodes = new List<L.FlowNode>
        {
            new("a", 0, 0, Width: 100, Height: 40),
            new("b", 300, 100, Width: 100, Height: 40),
        };
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, nodes)
            .AddChildContent<L.FlowNodeToolbar>(t => t.AddChildContent("<button>Do</button>")));
        cut.Find("[data-flow-node='a']").Click();
        cut.Find("[data-flow-node='b']").Click(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { ShiftKey = true });

        var toolbars = cut.FindAll("[data-slot='flow-toolbar']");
        Assert.Single(toolbars);
        var toolbar = toolbars[0];
        Assert.True(string.IsNullOrEmpty(toolbar.GetAttribute("data-flow-toolbar-for"))); // no live drag tracking for a multi-selection
        // Bounds: x 0..400, y 0..140 -> top edge y=0, centre x = (0+400)/2 = 200; the default
        // Offset (8px) then floats it 8px further up (Position.Top -> negative offset).
        Assert.Contains("left:200px", toolbar.GetAttribute("style"));
        Assert.Contains("top:-8px", toolbar.GetAttribute("style"));
    }

    [Fact]
    public void A_Third_Node_Joining_The_Selection_Grows_The_Bounding_Box()
    {
        var nodes = new List<L.FlowNode>
        {
            new("a", 0, 0, Width: 100, Height: 40),
            new("b", 300, 100, Width: 100, Height: 40),
            new("c", -200, 0, Width: 100, Height: 40),
        };
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, nodes)
            .AddChildContent<L.FlowNodeToolbar>(t => t.AddChildContent("<button>Do</button>")));
        cut.Find("[data-flow-node='a']").Click();
        cut.Find("[data-flow-node='b']").Click(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { ShiftKey = true });
        cut.Find("[data-flow-node='c']").Click(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { ShiftKey = true });

        // Bounds now: x -200..400, centre x = (−200+400)/2 = 100.
        Assert.Contains("left:100px", cut.Find("[data-slot='flow-toolbar']").GetAttribute("style"));
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
