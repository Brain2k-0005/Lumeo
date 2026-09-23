using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// The canvas' global keyboard shortcuts (Ctrl+Z/Ctrl+Y/Ctrl+Shift+Z, Delete/Backspace, the
/// arrow-key nudge) must leave an editable target alone — a node template can render its own
/// input, and a keydown there bubbles to the pane/node-host handlers exactly like a keydown on the
/// node itself (proven: <c>KeyboardEventArgs</c> carries no DOM target, so without this guard a
/// keystroke meant for the field would also undo/delete/move the graph). The guard is driven by
/// <see cref="Lumeo.Services.IComponentInteropService.FlowIsFocusedElementEditableAsync"/> — real
/// flow.js queries <c>document.activeElement</c>; <see cref="TrackingInteropService"/> exposes it
/// as a plain settable bool so a test can simulate "focus is currently in an editable field".
/// </summary>
public class FlowCanvasEditableGuardTests : FlowCanvasTestBase
{
    private static Microsoft.AspNetCore.Components.RenderFragment<L.FlowNodeContext> NodeTemplateWithInput => ctx => builder =>
    {
        builder.OpenElement(0, "div");
        builder.OpenElement(1, "input");
        builder.AddAttribute(2, "data-testid", "node-input");
        builder.CloseElement();
        builder.AddContent(3, ctx.Node.Data?.ToString());
        builder.CloseElement();
    };

    [Fact]
    public async Task Ctrl_Z_From_An_Editable_Target_Does_Not_Pop_History()
    {
        var history = new L.FlowHistory();
        var (cut, current) = RenderBound(ThreeNodes(), p => p
            .Add(c => c.History, history)
            .Add(c => c.NodeTemplate, NodeTemplateWithInput));
        await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("a", 77, 77) }, cut.Instance._state.Generation));
        Assert.Equal(77, Node(current(), "a").X);

        Interop.FlowFocusedElementEditable = true; // simulate: focus is in the <input> right now
        cut.Find("[data-testid='node-input']").KeyDown(new KeyboardEventArgs { Key = "z", CtrlKey = true });

        Assert.Equal(77, Node(current(), "a").X); // unchanged — undo did not run
    }

    [Fact]
    public async Task Ctrl_Z_Still_Undoes_When_Focus_Is_Not_Editable()
    {
        var history = new L.FlowHistory();
        var (cut, current) = RenderBound(ThreeNodes(), p => p
            .Add(c => c.History, history)
            .Add(c => c.NodeTemplate, NodeTemplateWithInput));
        await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("a", 77, 77) }, cut.Instance._state.Generation));

        Interop.FlowFocusedElementEditable = false; // e.g. the node host itself has focus
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "z", CtrlKey = true });

        Assert.Equal(0, Node(current(), "a").X); // undo ran
    }

    [Fact]
    public void Backspace_From_An_Editable_Target_Does_Not_Delete_The_Node()
    {
        var (cut, current) = RenderBound(ThreeNodes(), p => p.Add(c => c.NodeTemplate, NodeTemplateWithInput));
        cut.Find("[data-flow-node='a']").Click(); // select it first

        Interop.FlowFocusedElementEditable = true;
        cut.Find("[data-testid='node-input']").KeyDown(new KeyboardEventArgs { Key = "Backspace" });

        Assert.Equal(3, current().Count); // still there
    }

    [Fact]
    public void Backspace_Still_Deletes_The_Selected_Node_When_Focus_Is_Not_Editable()
    {
        var (cut, current) = RenderBound(ThreeNodes(), p => p.Add(c => c.NodeTemplate, NodeTemplateWithInput));
        cut.Find("[data-flow-node='a']").Click();

        Interop.FlowFocusedElementEditable = false;
        cut.Find("[data-slot='flow-pane']").KeyDown(new KeyboardEventArgs { Key = "Backspace" });

        Assert.Equal(2, current().Count);
        Assert.DoesNotContain(current(), n => n.Id == "a");
    }

    [Fact]
    public void Arrow_Key_From_An_Editable_Target_Does_Not_Nudge_The_Node()
    {
        var (cut, current) = RenderBound(ThreeNodes(), p => p.Add(c => c.NodeTemplate, NodeTemplateWithInput));

        Interop.FlowFocusedElementEditable = true;
        cut.Find("[data-testid='node-input']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal(0, Node(current(), "a").X); // unchanged
    }

    [Fact]
    public void Arrow_Key_Still_Nudges_The_Focused_Node_Host_When_Focus_Is_Not_Editable()
    {
        var (cut, current) = RenderBound(ThreeNodes(), p => p.Add(c => c.NodeTemplate, NodeTemplateWithInput));

        Interop.FlowFocusedElementEditable = false;
        cut.Find("[data-flow-node='a']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal(1, Node(current(), "a").X);
    }

    [Fact]
    public void Escape_Still_Clears_The_Selection_Even_From_An_Editable_Target()
    {
        // Escape is deliberately NOT gated (a user expects it to work everywhere, and it doesn't
        // touch graph data) -- the guard only covers undo/redo/delete/arrow-nudge.
        var (cut, _) = RenderBound(ThreeNodes(), p => p.Add(c => c.NodeTemplate, NodeTemplateWithInput));
        cut.Find("[data-flow-node='a']").Click();

        Interop.FlowFocusedElementEditable = true;
        cut.Find("[data-testid='node-input']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(cut.Instance.IsSelected("a"));
    }
}
