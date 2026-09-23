using Bunit;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// FlowControls: zoom/fit/lock plus the phase-3 additions — a snap-grid toggle bound to the canvas'
/// SnapToGrid, and undo/redo buttons driven by the canvas' History.
/// </summary>
public class FlowControlsTests : FlowCanvasTestBase
{
    [Fact]
    public void ShowSnapToggle_False_By_Default_Renders_No_Snap_Button()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .AddChildContent<L.FlowControls>());

        Assert.Empty(cut.FindAll("[aria-label='Snap to grid']"));
    }

    [Fact]
    public void Clicking_The_Snap_Toggle_Flips_The_Canvas_Effective_SnapToGrid()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .AddChildContent<L.FlowControls>(ctl => ctl.Add(x => x.ShowSnapToggle, true)));

        var button = cut.Find("[aria-label='Snap to grid']");
        Assert.Equal("false", button.GetAttribute("aria-pressed"));
        Assert.False(cut.Instance.EffectiveSnapToGrid);

        button.Click();

        Assert.True(cut.Instance.EffectiveSnapToGrid);
        Assert.Equal("true", cut.Find("[aria-label='Snap to grid']").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Toggling_Snap_Raises_SnapToGridChanged()
    {
        bool? changed = null;
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .Add(c => c.SnapToGridChanged, (bool v) => changed = v)
            .AddChildContent<L.FlowControls>(ctl => ctl.Add(x => x.ShowSnapToggle, true)));

        cut.Find("[aria-label='Snap to grid']").Click();

        Assert.True(changed);
    }

    [Fact]
    public void ShowHistory_Renders_Disabled_Undo_Redo_Without_A_History()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .AddChildContent<L.FlowControls>(ctl => ctl.Add(x => x.ShowHistory, true)));

        Assert.True(cut.Find("[aria-label='Undo']").HasAttribute("disabled"));
        Assert.True(cut.Find("[aria-label='Redo']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Clicking_Undo_Calls_Through_To_The_Canvas_History()
    {
        var history = new L.FlowHistory();
        L.FlowNode? movedTo = null;
        IRenderedComponent<L.FlowCanvas>? cut = null;
        cut = Ctx.Render<L.FlowCanvas>(p => p
            .Add(c => c.Nodes, ThreeNodes())
            .Add(c => c.History, history)
            .Add(c => c.NodesChanged, (IReadOnlyList<L.FlowNode> n) =>
            {
                movedTo = n.First(x => x.Id == "a");
                cut!.Render(pp => pp.Add(x => x.Nodes, n));
            })
            .AddChildContent<L.FlowControls>(ctl => ctl.Add(x => x.ShowHistory, true)));

        await cut.InvokeAsync(() => cut.Instance.CommitNodeDrag(new[] { new L.FlowNodeChange("a", 50, 50) }, cut.Instance._state.Generation));
        Assert.False(cut.Find("[aria-label='Undo']").HasAttribute("disabled"));

        cut.Find("[aria-label='Undo']").Click();

        Assert.Equal(0, movedTo!.X);
    }
}
