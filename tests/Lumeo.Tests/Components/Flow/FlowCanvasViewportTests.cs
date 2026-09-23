using Bunit;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// Viewport ownership: .NET renders the transform once, then changes the viewport only by stamping
/// data-flow-viewport="x|y|zoom|id" on the pane (the engine applies it from a MutationObserver in
/// the same frame). Engine reports update .NET's state and ViewportChanged, but never stamp — an
/// echo of a report coming back through @bind-Viewport is not an instruction.
/// </summary>
public class FlowCanvasViewportTests : FlowCanvasTestBase
{
    private static string Stamp(IRenderedComponent<L.FlowCanvas> cut) => cut.Find("[data-slot='flow-pane']").GetAttribute("data-flow-viewport")!;

    [Fact]
    public void The_First_Render_Stamps_And_Draws_The_Initial_Viewport()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Viewport, new L.FlowViewport(12.5, -30, 1.25)));
        Assert.Equal("12.5|-30|1.25|0", Stamp(cut));
        Assert.Contains("transform:translate(12.5px, -30px) scale(1.25)", cut.Find("[data-slot='flow-viewport']").GetAttribute("style"));
        Assert.Contains("transform-origin:0 0", cut.Find("[data-slot='flow-viewport']").GetAttribute("style"));
    }

    [Fact]
    public void A_Default_Or_Invalid_Viewport_Starts_At_The_Origin_At_100_Percent()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Viewport, new L.FlowViewport(5, 5, 0)));
        Assert.Equal("0|0|1|0", Stamp(cut));
    }

    [Fact]
    public void The_Initial_Zoom_Is_Clamped_To_The_Limits()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Viewport, new L.FlowViewport(0, 0, 9)).Add(c => c.MaxZoom, 3));
        Assert.Equal("0|0|3|0", Stamp(cut));
    }

    [Fact]
    public void A_New_Viewport_Parameter_Is_Stamped_With_A_Fresh_Id_And_The_Rendered_Transform_Stays_Put()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Viewport, new L.FlowViewport(0, 0, 1)));
        var layerStyle = cut.Find("[data-slot='flow-viewport']").GetAttribute("style");

        cut.Render(p => p.Add(c => c.Viewport, new L.FlowViewport(-40, 25, 1.5)));

        Assert.Equal("-40|25|1.5|1", Stamp(cut));
        // After mount the engine owns the transform; .NET never rewrites it (it would fight a gesture).
        Assert.Equal(layerStyle, cut.Find("[data-slot='flow-viewport']").GetAttribute("style"));
        Assert.Equal(new L.FlowViewport(-40, 25, 1.5), cut.Instance.CurrentViewport);
    }

    [Fact]
    public async Task An_Engine_Report_Raises_ViewportChanged_And_Its_Echo_Is_Not_Restamped()
    {
        var reported = new List<L.FlowViewport>();
        IRenderedComponent<L.FlowCanvas>? cut = null;
        cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Viewport, new L.FlowViewport(0, 0, 1))
            .Add(c => c.ViewportChanged, (L.FlowViewport v) =>
            {
                reported.Add(v);
                cut!.Render(pp => pp.Add(c => c.Viewport, v)); // @bind-Viewport
            }));

        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(-100, 40, 0.8, false));
        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(-120, 44, 0.8, true));

        Assert.Equal(new[] { new L.FlowViewport(-100, 40, 0.8), new L.FlowViewport(-120, 44, 0.8) }, reported);
        Assert.Equal("0|0|1|0", Stamp(cut)); // never re-stamped: the engine already shows it
        Assert.Equal(new L.FlowViewport(-120, 44, 0.8), cut.Instance.CurrentViewport);
    }

    [Fact]
    public async Task An_Older_Report_Echoing_Back_Late_Is_Not_Stamped_Either()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Viewport, new L.FlowViewport(0, 0, 1)));
        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(-10, 0, 1, false));
        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(-20, 0, 1, false));
        cut.Render(p => p.Add(c => c.Viewport, new L.FlowViewport(-10, 0, 1)));
        Assert.Equal("0|0|1|0", Stamp(cut));
    }

    [Fact]
    public async Task Invalid_Reports_Are_Ignored()
    {
        var calls = 0;
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.ViewportChanged, (L.FlowViewport _) => calls++));
        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(double.NaN, 0, 1, true));
        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(0, 0, 0, true));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task SetViewportAsync_Stamps_Clamps_And_Raises_ViewportChanged()
    {
        L.FlowViewport? raised = null;
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.MinZoom, 0.5).Add(c => c.ViewportChanged, (L.FlowViewport v) => raised = v));
        await cut.InvokeAsync(() => cut.Instance.SetViewportAsync(new L.FlowViewport(7, 8, 0.1)));
        Assert.Equal("7|8|0.5|1", Stamp(cut));
        Assert.Equal(new L.FlowViewport(7, 8, 0.5), raised);
    }

    [Fact]
    public async Task ZoomIn_And_ZoomOut_Step_By_1_2_Around_The_Pane_Centre_From_The_Live_Viewport()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.FitViewOnInit, false));
        await cut.InvokeAsync(() => cut.Instance.PaneResized(800, 600));
        Interop.FlowLiveViewport = new[] { 100.0, 50.0, 1.0 }; // the engine is ahead of the last report

        await cut.InvokeAsync(() => cut.Instance.ZoomInAsync());
        var expected = L.FlowGeometry.ZoomAt(new L.FlowViewport(100, 50, 1), 1.2, 400, 300);
        Assert.Equal(expected, cut.Instance.CurrentViewport);

        Interop.FlowLiveViewport = null; // engine unavailable: falls back to the known state
        await cut.InvokeAsync(() => cut.Instance.ZoomOutAsync());
        Assert.Equal(1.0, cut.Instance.CurrentViewport.Zoom, 9);
        // The pane centre's flow point is back where it was.
        var centre = cut.Instance.ScreenToFlow(400, 300);
        var original = L.FlowGeometry.ScreenToFlow(400, 300, new L.FlowViewport(100, 50, 1));
        Assert.Equal(original.X, centre.X, 9);
        Assert.Equal(original.Y, centre.Y, 9);
    }

    [Fact]
    public async Task ZoomTo_Clamps_To_MaxZoom()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.MaxZoom, 2).Add(c => c.FitViewOnInit, false));
        await cut.InvokeAsync(() => cut.Instance.ZoomToAsync(50));
        Assert.Equal(2, cut.Instance.CurrentViewport.Zoom);
    }

    [Fact]
    public async Task SetCenterAsync_Centres_The_Flow_Point()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.FitViewOnInit, false));
        await cut.InvokeAsync(() => cut.Instance.PaneResized(1000, 500));
        await cut.InvokeAsync(() => cut.Instance.SetCenterAsync(250, 100, 2));
        var screen = cut.Instance.FlowToScreen(250, 100);
        Assert.Equal(500, screen.X, 9);
        Assert.Equal(250, screen.Y, 9);
        Assert.Equal(2, cut.Instance.CurrentViewport.Zoom);
    }

    [Fact]
    public async Task FitViewAsync_Computes_In_DotNet_When_Every_Node_Is_Measured()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0), new("b", 400, 300) };
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, nodes).Add(c => c.FitViewOnInit, false).Add(c => c.FitViewPadding, 0.1));
        await cut.InvokeAsync(() => cut.Instance.PaneResized(800, 500));
        await cut.InvokeAsync(() => cut.Instance.NodesMeasured(new[]
        {
            new L.FlowNodeMeasurement("a", 150, 40, null),
            new L.FlowNodeMeasurement("b", 150, 40, null),
        }));

        await cut.InvokeAsync(() => cut.Instance.FitViewAsync());

        var expected = L.FlowGeometry.FitView(new[] { new L.FlowRect(0, 0, 150, 40), new L.FlowRect(400, 300, 150, 40) }, 800, 500, 0.1, 0.25, 2)!.Value;
        Assert.Equal(expected, cut.Instance.CurrentViewport);
        Assert.EndsWith("|1", Stamp(cut));
        Assert.Empty(Interop.FlowFitViewCalls);
    }

    [Fact]
    public async Task FitViewAsync_Asks_The_Engine_When_A_Node_Was_Never_Measured()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).Add(c => c.FitViewOnInit, false).Add(c => c.MinZoom, 0.1).Add(c => c.MaxZoom, 3));
        await cut.InvokeAsync(() => cut.Instance.PaneResized(800, 500));
        await cut.InvokeAsync(() => cut.Instance.FitViewAsync(0.3));
        Assert.Equal((0.3, 0.1, 3.0), Assert.Single(Interop.FlowFitViewCalls));
        Assert.Equal("0|0|1|0", Stamp(cut));
    }

    [Fact]
    public async Task FitViewAsync_Uses_Fixed_Node_Sizes_Without_Measurements()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0, Width: 100, Height: 100) };
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, nodes).Add(c => c.FitViewOnInit, false).Add(c => c.MaxZoom, 10));
        await cut.InvokeAsync(() => cut.Instance.PaneResized(400, 400));
        await cut.InvokeAsync(() => cut.Instance.FitViewAsync(0));
        Assert.Equal(new L.FlowViewport(0, 0, 4), cut.Instance.CurrentViewport);
    }

    [Fact]
    public void ScreenToFlow_And_FlowToScreen_Use_The_Current_Viewport()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Viewport, new L.FlowViewport(100, 100, 2)).Add(c => c.FitViewOnInit, false));
        Assert.Equal(new L.FlowPoint(50, 0), cut.Instance.ScreenToFlow(200, 100));
        Assert.Equal(new L.FlowPoint(200, 100), cut.Instance.FlowToScreen(50, 0));
    }

    [Fact]
    public async Task A_Final_Report_Refreshes_The_Controls_Zoom_Limits()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.MaxZoom, 2).Add(c => c.FitViewOnInit, false).AddChildContent<L.FlowControls>());
        Assert.False(cut.Find("button[aria-label='Zoom in']").HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(0, 0, 2, true));
        cut.WaitForAssertion(() => Assert.True(cut.Find("button[aria-label='Zoom in']").HasAttribute("disabled")));
    }
}
