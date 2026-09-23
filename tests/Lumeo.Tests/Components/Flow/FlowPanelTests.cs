using Bunit;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>FlowPanel: a corner overlay for custom toolbars/legends, exempted from pane gestures.</summary>
public class FlowPanelTests : FlowCanvasTestBase
{
    [Theory]
    [InlineData(L.FlowPanel.PanelPosition.TopLeft, "left-3", "top-3")]
    [InlineData(L.FlowPanel.PanelPosition.TopRight, "right-3", "top-3")]
    [InlineData(L.FlowPanel.PanelPosition.BottomLeft, "left-3", "bottom-3")]
    [InlineData(L.FlowPanel.PanelPosition.BottomRight, "right-3", "bottom-3")]
    public void Sits_In_The_Requested_Corner(L.FlowPanel.PanelPosition position, string x, string y)
    {
        var cut = Ctx.Render<L.FlowPanel>(p => p.Add(c => c.Position, position).AddChildContent("<span>Legend</span>"));
        var root = cut.Find("[data-slot='flow-panel']");
        Assert.Contains(x, root.ClassList);
        Assert.Contains(y, root.ClassList);
    }

    [Fact]
    public void Carries_The_Overlay_Marker_So_The_Engine_Skips_Pane_Gestures_On_It()
    {
        var cut = Ctx.Render<L.FlowPanel>(p => p.AddChildContent("<span>Legend</span>"));
        Assert.True(cut.Find("[data-slot='flow-panel']").HasAttribute("data-flow-overlay"));
    }

    [Fact]
    public void Renders_Its_Content()
    {
        var cut = Ctx.Render<L.FlowPanel>(p => p.AddChildContent("<span data-testid='inner'>Legend</span>"));
        Assert.Equal("Legend", cut.Find("[data-testid='inner']").TextContent);
    }
}
