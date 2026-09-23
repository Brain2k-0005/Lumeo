using Bunit;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>What FlowCanvas renders: the slot structure, nodes at their positions, edges between them, overlays.</summary>
public class FlowCanvasRenderingTests : FlowCanvasTestBase
{
    [Fact]
    public void Root_Is_An_Application_Region_With_The_Height_And_Splatted_Attributes()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .Add(c => c.Height, "420px")
            .Add(c => c.Class, "my-canvas")
            .AddUnmatched("data-testid", "flow-root"));

        var root = cut.Find("[data-slot='flow-canvas']");
        Assert.Equal("application", root.GetAttribute("role"));
        Assert.Equal("Flow canvas", root.GetAttribute("aria-label"));
        Assert.Contains("height:420px", root.GetAttribute("style"));
        Assert.Contains("my-canvas", root.ClassList);
        Assert.Equal("flow-root", root.GetAttribute("data-testid"));
        Assert.NotNull(cut.Find("[data-slot='flow-canvas'] > [data-slot='flow-pane'] > [data-slot='flow-viewport'] > [data-slot='flow-edges']"));
        Assert.NotNull(cut.Find("[data-slot='flow-viewport'] > [data-slot='flow-nodes']"));
    }

    [Fact]
    public void A_Consumer_AriaLabel_Wins_Over_The_Default()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.AddUnmatched("aria-label", "Order pipeline"));
        Assert.Equal("Order pipeline", cut.Find("[data-slot='flow-canvas']").GetAttribute("aria-label"));
    }

    [Fact]
    public void Nodes_Render_At_Their_Flow_Positions_As_Focusable_Groups()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, new List<L.FlowNode> { new("n1", 12.5, -40), new("n2", 300, 80, Width: 180, Height: 64, ZIndex: 3) }));

        var n1 = cut.Find("[data-flow-node='n1']");
        Assert.Equal("12.5", n1.GetAttribute("data-x"));
        Assert.Equal("-40", n1.GetAttribute("data-y"));
        Assert.Contains("transform:translate(12.5px, -40px)", n1.GetAttribute("style"));
        Assert.Equal("0", n1.GetAttribute("tabindex"));
        Assert.Equal("group", n1.GetAttribute("role"));
        Assert.Equal("n1", n1.GetAttribute("aria-label"));
        Assert.Equal("node", n1.GetAttribute("aria-roledescription"));
        Assert.Equal("true", n1.GetAttribute("data-draggable"));
        Assert.Null(n1.GetAttribute("data-selected"));

        var style2 = cut.Find("[data-flow-node='n2']").GetAttribute("style")!;
        Assert.Contains("width:180px", style2);
        Assert.Contains("height:64px", style2);
        Assert.Contains("z-index:3", style2);
    }

    [Fact]
    public void NodeTemplate_Receives_The_Node_Context()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, new List<L.FlowNode> { new("n1", 0, 0, Type: "trigger", Data: "Webhook") })
            .Add(c => c.NodeTemplate, ctx => $"<div class='card' data-kind='{ctx.Node.Type}'>{ctx.Node.Data}|{ctx.Selected}|{ctx.Dragging}|{ctx.Canvas is not null}</div>"));

        var card = cut.Find("[data-flow-node='n1'] > .card");
        Assert.Equal("trigger", card.GetAttribute("data-kind"));
        Assert.Equal("Webhook|False|False|True", card.TextContent);
    }

    [Fact]
    public void Without_A_Template_A_Node_Renders_A_Card_With_Its_Data_Or_Id()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, new List<L.FlowNode> { new("n1", 0, 0, Data: "Hello"), new("n2", 0, 100) }));
        Assert.Equal("Hello", cut.Find("[data-flow-node='n1'] > div").TextContent);
        Assert.Equal("n2", cut.Find("[data-flow-node='n2'] > div").TextContent);
        Assert.Contains("bg-card", cut.Find("[data-flow-node='n2'] > div").ClassList);
    }

    [Fact]
    public void Edges_Reference_Their_Nodes_And_Carry_The_Geometry_Path()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .Add(c => c.Edges, TwoEdges()));

        var paths = cut.FindAll("[data-flow-edge]");
        Assert.Equal(2, paths.Count);

        var ab = cut.Find("[data-edge-id='a-b']");
        Assert.Equal("a", ab.GetAttribute("data-source"));
        Assert.Equal("b", ab.GetAttribute("data-target"));
        Assert.Equal("bezier", ab.GetAttribute("data-edge-type"));
        Assert.Null(ab.GetAttribute("data-source-handle"));
        // Unmeasured nodes use the default 150x40 box: source right-middle (150,20), target left-middle (300,60).
        Assert.Equal(L.FlowGeometry.GetBezierPath(150, 20, L.FlowPosition.Right, 300, 60, L.FlowPosition.Left).D, ab.GetAttribute("d"));
        Assert.Equal("var(--color-border)", ab.GetAttribute("stroke"));

        Assert.Equal("smoothstep", cut.Find("[data-edge-id='b-c']").GetAttribute("data-edge-type"));
    }

    [Fact]
    public void An_Edge_To_A_Missing_Node_Is_Skipped()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .Add(c => c.Edges, new List<L.FlowEdge> { new("x", "a", "ghost"), new("a-b", "a", "b") }));
        Assert.Single(cut.FindAll("[data-flow-edge]"));
    }

    [Fact]
    public void Measured_Handles_Move_The_Edge_Anchors()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .Add(c => c.Edges, new List<L.FlowEdge> { new("a-b", "a", "b", SourceHandle: "out-2", TargetHandle: null) }));

        cut.InvokeAsync(() => cut.Instance.NodesMeasured(new[]
        {
            new L.FlowNodeMeasurement("a", 200, 80, new[]
            {
                new L.FlowHandleMeasurement("out-1", "source", "right", 200, 20),
                new L.FlowHandleMeasurement("out-2", "source", "bottom", 100, 80),
            }),
            new L.FlowNodeMeasurement("b", 160, 50, new[] { new L.FlowHandleMeasurement(null, "target", "top", 80, 0) }),
        }));

        // a at (0,0): handle out-2 at (100,80) facing down; b at (300,40): its target handle (80,0) facing up.
        var expected = L.FlowGeometry.GetBezierPath(100, 80, L.FlowPosition.Bottom, 380, 40, L.FlowPosition.Top).D;
        cut.WaitForAssertion(() => Assert.Equal(expected, cut.Find("[data-edge-id='a-b']").GetAttribute("d")));
    }

    [Fact]
    public void Readonly_Marks_Every_Node_Not_Draggable()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .Add(c => c.Readonly, true));
        Assert.All(cut.FindAll("[data-flow-node]"), n => Assert.Equal("false", n.GetAttribute("data-draggable")));
        Assert.NotNull(cut.Find("[data-slot='flow-canvas'][data-readonly]"));
    }

    [Fact]
    public void A_Node_With_Draggable_False_Is_Not_Draggable_Even_On_An_Editable_Canvas()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()));
        Assert.Equal("true", cut.Find("[data-flow-node='a']").GetAttribute("data-draggable"));
        Assert.Equal("false", cut.Find("[data-flow-node='c']").GetAttribute("data-draggable"));
    }

    [Fact]
    public void Clicking_A_Node_Selects_It_And_Clicking_The_Pane_Clears_It()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()));
        cut.Find("[data-flow-node='b']").Click();
        Assert.NotNull(cut.Find("[data-flow-node='b']").GetAttribute("data-selected"));
        Assert.Equal("true", cut.Find("[data-flow-node='b']").GetAttribute("aria-selected"));

        cut.Find("[data-flow-node='a']").Click();
        Assert.Null(cut.Find("[data-flow-node='b']").GetAttribute("data-selected"));
        Assert.NotNull(cut.Find("[data-flow-node='a']").GetAttribute("data-selected"));

        cut.InvokeAsync(() => cut.Instance.PaneClicked(10, 10));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-flow-node][data-selected]")));
    }

    [Fact]
    public void A_Node_That_Is_Not_Selectable_Does_Not_Become_Selected()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, new List<L.FlowNode> { new("x", 0, 0, Selectable: false) }));
        cut.Find("[data-flow-node='x']").Click();
        Assert.Null(cut.Find("[data-flow-node='x']").GetAttribute("data-selected"));
    }

    [Fact]
    public void The_Engine_Drag_Start_Mirrors_Into_The_Dragging_State()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .Add(c => c.NodeTemplate, ctx => $"<span class='d'>{ctx.Dragging}</span>"));
        cut.InvokeAsync(() => cut.Instance.NodeDragStart(new[] { "a" }));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-flow-node='a']").GetAttribute("data-dragging")));
        Assert.Equal("True", cut.Find("[data-flow-node='a'] .d").TextContent);

        cut.InvokeAsync(() => cut.Instance.NodeDragCancelled());
        cut.WaitForAssertion(() => Assert.Null(cut.Find("[data-flow-node='a']").GetAttribute("data-dragging")));
    }

    [Fact]
    public void Handles_Render_As_Buttons_With_Their_Data_Attributes()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, new List<L.FlowNode> { new("start", 0, 0) })
            .Add(c => c.NodeTemplate, _ => builder =>
            {
                builder.OpenComponent<L.FlowHandle>(0);
                builder.AddAttribute(1, nameof(L.FlowHandle.Type), L.FlowHandleType.Target);
                builder.AddAttribute(2, nameof(L.FlowHandle.Position), L.FlowPosition.Left);
                builder.CloseComponent();
                builder.OpenComponent<L.FlowHandle>(3);
                builder.AddAttribute(4, nameof(L.FlowHandle.Id), "yes");
                builder.AddAttribute(5, nameof(L.FlowHandle.Position), L.FlowPosition.Bottom);
                builder.CloseComponent();
            }));

        var handles = cut.FindAll("[data-flow-node='start'] button[data-flow-handle]");
        Assert.Equal(2, handles.Count);
        Assert.Equal("target", handles[0].GetAttribute("data-handle-type"));
        Assert.Equal("left", handles[0].GetAttribute("data-position"));
        Assert.Equal("Connect to start", handles[0].GetAttribute("aria-label"));
        Assert.Contains("left:0;top:50%", handles[0].GetAttribute("style"));
        Assert.Equal("source", handles[1].GetAttribute("data-handle-type"));
        Assert.Equal("yes", handles[1].GetAttribute("data-handle-id"));
        Assert.Equal("bottom", handles[1].GetAttribute("data-position"));
        Assert.Equal("Connect from start", handles[1].GetAttribute("aria-label"));
        Assert.Equal("button", handles[1].GetAttribute("type"));
    }

    [Fact]
    public void Background_Renders_A_Pattern_Seeded_With_The_Initial_Viewport()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Viewport, new L.FlowViewport(10, 20, 1.5))
            .Add(c => c.FitViewOnInit, false)
            .AddChildContent<L.FlowBackground>(b => b.Add(x => x.Variant, L.FlowBackgroundVariant.Cross).Add(x => x.Gap, 24)));

        var svg = cut.Find("[data-slot='flow-pane'] > svg[data-slot='flow-background']");
        Assert.Equal("cross", svg.GetAttribute("data-variant"));
        Assert.Equal("true", svg.GetAttribute("aria-hidden"));
        var pattern = svg.QuerySelector("pattern[data-flow-background-pattern]")!;
        Assert.Equal("translate(10,20) scale(1.5)", pattern.GetAttribute("patterntransform") ?? pattern.GetAttribute("patternTransform"));
        Assert.Equal("24", pattern.GetAttribute("width"));
        Assert.NotNull(pattern.QuerySelector("[data-flow-background-shape='line']"));
        Assert.Contains($"url(#{pattern.Id})", svg.QuerySelector("rect")!.GetAttribute("fill"));
    }

    [Theory]
    [InlineData(L.FlowBackgroundVariant.Dots, "dot")]
    [InlineData(L.FlowBackgroundVariant.Lines, "line")]
    public void Background_Variants_Draw_Their_Shape(L.FlowBackgroundVariant variant, string shape)
    {
        var cut = Ctx.Render<L.FlowBackground>(p => p.Add(x => x.Variant, variant));
        Assert.NotNull(cut.Find($"[data-flow-background-shape='{shape}']"));
    }

    [Fact]
    public void Controls_Render_Labelled_Buttons_In_The_Chosen_Corner()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .AddChildContent<L.FlowControls>(b => b.Add(x => x.Position, L.FlowControls.ControlsPosition.TopRight)));

        var bar = cut.Find("[data-slot='flow-controls']");
        Assert.Equal("toolbar", bar.GetAttribute("role"));
        Assert.NotNull(bar.GetAttribute("data-flow-overlay"));
        Assert.Contains("right-3", bar.ClassList);
        Assert.Contains("top-3", bar.ClassList);
        var labels = bar.QuerySelectorAll("button").Select(b => b.GetAttribute("aria-label")).ToList();
        Assert.Equal(new[] { "Zoom in", "Zoom out", "Fit view", "Lock canvas" }, labels);
    }

    [Fact]
    public void Controls_Can_Hide_Their_Groups()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .AddChildContent<L.FlowControls>(b => b.Add(x => x.ShowZoom, false).Add(x => x.ShowLock, false)));
        var labels = cut.FindAll("[data-slot='flow-controls'] button").Select(b => b.GetAttribute("aria-label")).ToList();
        Assert.Equal(new[] { "Fit view" }, labels);
    }

    [Fact]
    public void The_Lock_Toggle_Stops_Node_Dragging_And_Reports_Its_State()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .AddChildContent<L.FlowControls>());

        var lockButton = cut.Find("[data-slot='flow-controls'] button[aria-label='Lock canvas']");
        Assert.Equal("false", lockButton.GetAttribute("aria-pressed"));
        lockButton.Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("true", cut.Find("[data-slot='flow-controls'] button[aria-label='Lock canvas']").GetAttribute("aria-pressed"));
            Assert.Equal("false", cut.Find("[data-flow-node='a']").GetAttribute("data-draggable"));
        });
    }

    [Fact]
    public void The_Zoom_Buttons_Disable_At_The_Limits()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Viewport, new L.FlowViewport(0, 0, 2))
            .Add(c => c.MaxZoom, 2)
            .Add(c => c.FitViewOnInit, false)
            .AddChildContent<L.FlowControls>());
        Assert.True(cut.Find("button[aria-label='Zoom in']").HasAttribute("disabled"));
        Assert.False(cut.Find("button[aria-label='Zoom out']").HasAttribute("disabled"));
    }
}
