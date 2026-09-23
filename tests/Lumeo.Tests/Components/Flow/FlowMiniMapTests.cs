using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>FlowMiniMap: one scaled rect per node, a viewport rect, click/drag pans via SetCenterAsync.</summary>
public class FlowMiniMapTests : FlowCanvasTestBase
{
    [Fact]
    public void Renders_One_Rect_Per_Node_Plus_The_Viewport_Rect()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .AddChildContent<L.FlowMiniMap>());

        var svg = cut.Find("[data-slot='flow-minimap'] svg");
        // 3 nodes + 1 viewport rect = 4 <rect> elements.
        Assert.Equal(4, svg.QuerySelectorAll("rect").Length);
    }

    [Fact]
    public void NodeColor_Overrides_The_Default_Fill()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, new List<L.FlowNode> { new("a", 0, 0) })
            .AddChildContent<L.FlowMiniMap>(m => m.Add(x => x.NodeColor, (L.FlowNode n) => "var(--color-destructive)")));

        var rect = cut.Find("[data-slot='flow-minimap'] svg rect");
        Assert.Equal("var(--color-destructive)", rect.GetAttribute("fill"));
    }

    [Fact]
    public void A_Pointer_Down_On_The_Map_Pans_The_Canvas()
    {
        L.FlowViewport? reported = null;
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, new List<L.FlowNode> { new("a", 0, 0, Width: 100, Height: 100) })
            .Add(c => c.ViewportChanged, (L.FlowViewport v) => reported = v)
            .AddChildContent<L.FlowMiniMap>(m => m.Add(x => x.Width, 160).Add(x => x.Height, 100)));

        var svg = cut.Find("[data-slot='flow-minimap'] svg");
        svg.PointerDown(new PointerEventArgs { OffsetX = 80, OffsetY = 50 });

        Assert.NotNull(reported);
    }

    [Fact]
    public void Renders_Nothing_Without_A_Canvas_Context()
    {
        // FlowMiniMap outside FlowCanvas has no cascaded context — it must not throw.
        var cut = Ctx.Render<L.FlowMiniMap>();
        Assert.Empty(cut.FindAll("svg"));
    }
}
