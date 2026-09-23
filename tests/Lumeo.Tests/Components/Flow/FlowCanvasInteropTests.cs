using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// The engine registration lifecycle through IComponentInteropService: registered once on mount
/// with the options bag, re-pushed only when an option changes, torn down on dispose — including a
/// dispose that lands while the register call is still in flight.
/// </summary>
public class FlowCanvasInteropTests : FlowCanvasTestBase
{
    private L.FlowCanvas.FlowEngineOptions LastOptions => Assert.IsType<L.FlowCanvas.FlowEngineOptions>(Interop.LastFlowOptions);

    [Fact]
    public void Mount_Registers_The_Engine_Once_With_The_Options_And_A_DotNet_Reference()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .Add(c => c.MinZoom, 0.5)
            .Add(c => c.MaxZoom, 3)
            .Add(c => c.SnapToGrid, true)
            .Add(c => c.SnapGrid, (10d, 20d))
            .Add(c => c.PanOnDrag, false)
            .Add(c => c.FitViewPadding, 0.2));

        Assert.Equal(1, Interop.FlowRegisterCanvasCallCount);
        Assert.NotNull(Interop.LastFlowDotNetRef);
        var o = LastOptions;
        Assert.Equal(0.5, o.MinZoom);
        Assert.Equal(3, o.MaxZoom);
        Assert.Equal(new[] { 10d, 20d }, o.Snap);
        Assert.True(o.NodesDraggable);
        Assert.False(o.PanOnDrag);
        Assert.True(o.ZoomOnScroll);
        Assert.False(o.Readonly);
        Assert.True(o.FitViewOnInit);
        Assert.Equal(0.2, o.FitViewPadding);

        cut.Render(p => p.Add(c => c.Height, "300px")); // an unrelated change
        Assert.Equal(1, Interop.FlowRegisterCanvasCallCount);
        Assert.Equal(0, Interop.FlowUpdateOptionsCallCount);
    }

    [Fact]
    public void Snap_Is_Null_Unless_SnapToGrid_Is_On()
    {
        Ctx.Render<L.FlowCanvas>();
        Assert.Null(LastOptions.Snap);
    }

    [Fact]
    public void An_Option_Change_Pushes_New_Options_Without_Re_Registering()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()));
        cut.Render(p => p.Add(c => c.Readonly, true));

        Assert.Equal(1, Interop.FlowRegisterCanvasCallCount);
        Assert.Equal(1, Interop.FlowUpdateOptionsCallCount);
        Assert.True(LastOptions.Readonly);
        Assert.False(LastOptions.NodesDraggable); // readonly turns drag off in the engine too
    }

    [Fact]
    public void The_Lock_Toggle_Pushes_NodesDraggable_False()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).AddChildContent<L.FlowControls>());
        cut.Find("button[aria-label='Lock canvas']").Click();
        cut.WaitForAssertion(() => Assert.False(LastOptions.NodesDraggable));
        Assert.Equal(1, Interop.FlowUpdateOptionsCallCount);
    }

    [Fact]
    public async Task Dispose_Unregisters_The_Engine()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()));
        await cut.Instance.DisposeAsync();
        Assert.Equal(1, Interop.FlowUnregisterCanvasCallCount);
    }

    /// <summary>
    /// The register call is suspended (TCS gate) when the canvas is disposed. Dispose sees the
    /// registration claimed and unregisters; when the register call then resolves — in JS it would
    /// have created the registration AFTER that unregister — the canvas unregisters again, so no
    /// listener set outlives the component.
    /// </summary>
    [Fact]
    public async Task A_Dispose_During_An_In_Flight_Registration_Unregisters_After_It_Lands()
    {
        var gate = new TaskCompletionSource();
        Interop.FlowRegisterCanvasGate = gate;
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()));
        Assert.Equal(1, Interop.FlowRegisterCanvasCallCount);

        await cut.InvokeAsync(async () => await cut.Instance.DisposeAsync());
        var afterDispose = Interop.FlowUnregisterCanvasCallCount;
        Assert.Equal(1, afterDispose);

        gate.SetResult();
        Interop.FlowRegisterCanvasGate = null;
        cut.WaitForAssertion(() => Assert.Equal(2, Interop.FlowUnregisterCanvasCallCount));
    }

    [Fact]
    public async Task A_Throwing_Registration_Degrades_To_A_Static_Canvas()
    {
        Interop.FlowRegisterCanvasException = new InvalidOperationException("no engine");
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()));
        Assert.Equal(3, cut.FindAll("[data-flow-node]").Count);

        // Nothing registered, so nothing to update or unregister.
        cut.Render(p => p.Add(c => c.Readonly, true));
        Assert.Equal(0, Interop.FlowUpdateOptionsCallCount);
        await cut.Instance.DisposeAsync();
        Assert.Equal(0, Interop.FlowUnregisterCanvasCallCount);
    }

    [Fact]
    public async Task The_Default_Interop_Service_Renders_Without_Throwing()
    {
        // No TrackingInteropService: the real ComponentInteropService over bUnit's loose JS runtime.
        await using var ctx = new BunitContext();
        ctx.AddLumeoServices();
        var cut = ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()).AddChildContent<L.FlowBackground>());
        Assert.Equal(3, cut.FindAll("[data-flow-node]").Count);
    }
}
