using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// FlowNodeResizer (phase 4): <c>CommitNodeResize</c> is the JSInvokable a pointer resize gesture
/// calls on drop, mirroring CommitNodeDrag's generation guard and acceptance rules. Shift+arrow on
/// a focused node that carries a FlowNodeResizer resizes it instead of the plain "move x10" every
/// other node's Shift+arrow still does (FlowCanvas only knows a node HAS a resizer because
/// FlowNodeResizer registers itself — RegisterResizer/UnregisterResizer).
/// </summary>
public class FlowCanvasResizeTests : FlowCanvasTestBase
{
    private static Microsoft.AspNetCore.Components.RenderFragment<L.FlowNodeContext> ResizableTemplate(
        double minW = 40, double minH = 20, double? maxW = null, double? maxH = null) => ctx => builder =>
    {
        builder.OpenComponent<L.FlowNodeResizer>(0);
        builder.AddAttribute(1, "MinWidth", minW);
        builder.AddAttribute(2, "MinHeight", minH);
        if (maxW is { } w) builder.AddAttribute(3, "MaxWidth", (double?)w);
        if (maxH is { } h) builder.AddAttribute(4, "MaxHeight", (double?)h);
        builder.AddAttribute(5, "AlwaysVisible", true);
        builder.CloseComponent();
        builder.OpenElement(6, "div");
        builder.AddContent(7, ctx.Node.Id);
        builder.CloseElement();
    };

    [Fact]
    public async Task CommitNodeResize_Updates_Width_Height_And_Position()
    {
        var (cut, currentNodes) = RenderBound(ThreeNodes());

        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitNodeResize("a", 10, 20, 200, 120, cut.Instance._state.Generation));

        Assert.True(accepted);
        var a = Node(currentNodes(), "a");
        Assert.Equal(10, a.X);
        Assert.Equal(20, a.Y);
        Assert.Equal(200, a.Width);
        Assert.Equal(120, a.Height);
    }

    [Fact]
    public async Task CommitNodeResize_Raises_OnNodeResizeStop_After_NodesChanged()
    {
        IReadOnlyList<L.FlowNodeResizeChange>? seen = null;
        var (cut, _) = RenderBound(ThreeNodes(), p => p.Add(c => c.OnNodeResizeStop, (IReadOnlyList<L.FlowNodeResizeChange> c) => seen = c));

        await cut.InvokeAsync(() => cut.Instance.CommitNodeResize("a", 0, 0, 180, 90, cut.Instance._state.Generation));

        var change = Assert.Single(seen!);
        Assert.Equal("a", change.Id);
        Assert.Equal(180, change.Width);
        Assert.Equal(90, change.Height);
    }

    [Fact]
    public async Task A_Stale_Generation_Is_Rejected()
    {
        var (cut, _) = RenderBound(ThreeNodes());
        var stale = cut.Instance._state.Generation;
        // An external replace (not an echo of the canvas' own commit) bumps the generation — a
        // resize commit that started under the OLD generation must not land on top of it.
        cut.Render(p => p.Add(c => c.Nodes, new List<L.FlowNode> { new("a", 5, 5), new("b", 305, 45), new("c", 125, 265) }));

        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitNodeResize("a", 0, 0, 200, 200, stale));

        Assert.False(accepted);
        Assert.Equal(5, cut.Instance.CurrentNodes.Single(n => n.Id == "a").X); // untouched — the external replace won
    }

    [Fact]
    public async Task Readonly_Rejects_A_Resize_Commit()
    {
        var (cut, currentNodes) = RenderBound(ThreeNodes(), p => p.Add(c => c.Readonly, true));

        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitNodeResize("a", 0, 0, 200, 200, cut.Instance._state.Generation));

        Assert.False(accepted);
        Assert.Null(Node(currentNodes(), "a").Width);
    }

    [Fact]
    public async Task A_Nonpositive_Size_Is_Rejected()
    {
        var (cut, _) = RenderBound(ThreeNodes());
        Assert.False(await cut.InvokeAsync(() => cut.Instance.CommitNodeResize("a", 0, 0, 0, 50, cut.Instance._state.Generation)));
        Assert.False(await cut.InvokeAsync(() => cut.Instance.CommitNodeResize("a", 0, 0, 50, -1, cut.Instance._state.Generation)));
    }

    [Fact]
    public void ShiftArrow_On_A_Node_With_A_Resizer_Resizes_Instead_Of_Fast_Moving()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0, Width: 100, Height: 50) };
        var (cut, currentNodes) = RenderBound(nodes, p => p.Add(c => c.NodeTemplate, ResizableTemplate()));

        cut.Find("[data-flow-node='a']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight", ShiftKey = true });

        var a = Node(currentNodes(), "a");
        Assert.Equal(108, a.Width); // grows by the 8px keyboard-resize step
        Assert.Equal(50, a.Height);
        Assert.Equal(0, a.X); // ArrowRight never moves X/Y
    }

    [Fact]
    public void ShiftArrow_Resize_Clamps_To_MinWidth()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0, Width: 42, Height: 50) };
        var (cut, currentNodes) = RenderBound(nodes, p => p.Add(c => c.NodeTemplate, ResizableTemplate(minW: 40)));

        cut.Find("[data-flow-node='a']").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft", ShiftKey = true });

        Assert.Equal(40, Node(currentNodes(), "a").Width); // 42 - 8 = 34, clamped up to the 40 MinWidth
    }

    [Fact]
    public void ShiftArrow_Resize_Clamps_To_MaxWidth()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0, Width: 196, Height: 50) };
        var (cut, currentNodes) = RenderBound(nodes, p => p.Add(c => c.NodeTemplate, ResizableTemplate(maxW: 200)));

        cut.Find("[data-flow-node='a']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight", ShiftKey = true });

        Assert.Equal(200, Node(currentNodes(), "a").Width); // 196 + 8 = 204, clamped down to the 200 MaxWidth
    }

    [Fact]
    public void ShiftArrow_On_A_Node_Without_A_Resizer_Still_Fast_Moves()
    {
        var (cut, currentNodes) = RenderBound(ThreeNodes());

        cut.Find("[data-flow-node='a']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight", ShiftKey = true });

        var a = Node(currentNodes(), "a");
        Assert.Equal(10, a.X); // the existing "shift = ×10" plain move, unchanged by phase 4
        Assert.Null(a.Width);
    }

    [Fact]
    public void FlowNodeResizer_Renders_Only_When_The_Node_Is_Selected_Without_AlwaysVisible()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0, Width: 100, Height: 50) };
        var (cut, _) = RenderBound(nodes, p => p.Add(c => c.NodeTemplate, (Microsoft.AspNetCore.Components.RenderFragment<L.FlowNodeContext>)(ctx => builder =>
        {
            builder.OpenComponent<L.FlowNodeResizer>(0);
            builder.CloseComponent();
        })));

        Assert.Empty(cut.FindAll("[data-flow-resizer]"));
        cut.Find("[data-flow-node='a']").Click();
        Assert.Single(cut.FindAll("[data-flow-resizer]"));
    }

    [Fact]
    public void FlowNodeResizer_Renders_Eight_Grips()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0, Width: 100, Height: 50) };
        var (cut, _) = RenderBound(nodes, p => p.Add(c => c.NodeTemplate, ResizableTemplate()));

        Assert.Equal(8, cut.FindAll("[data-flow-resize-handle]").Count);
    }
}
