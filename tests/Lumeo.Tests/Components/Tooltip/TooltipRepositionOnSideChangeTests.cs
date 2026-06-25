using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Tooltip;

/// <summary>
/// Regression coverage for triage #91 (state-on-data-change): changing
/// <see cref="Lumeo.TooltipContent.Side"/> / <see cref="Lumeo.TooltipContent.Offset"/>
/// while the tooltip is OPEN was silently ignored. Placement ran exactly once,
/// gated by a one-shot <c>_registered</c> latch in OnAfterRenderAsync, so the box
/// stayed at its original side/offset after a live param change.
///
/// The fix re-runs PositionFixed when Side/Offset differ from the last applied
/// values while open (storing the applied values so a same-value re-render does
/// not thrash the JS each frame). positionFixed cleans up the prior placement
/// (keyed by contentId) on entry, so re-calling it is safe.
///
/// These tests assert the MECHANISM via the recorded <c>positionFixed</c>
/// JSInterop invocations (bUnit cannot observe the real floating-ui placement).
/// </summary>
public class TooltipRepositionOnSideChangeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TooltipRepositionOnSideChangeTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // A host that drives TooltipContent.Side/Offset from its own parameters so a
    // re-render via probe.Render(...) actually changes the nested content's params
    // while the tooltip stays open. Mirrors MenubarMenuVisibilityProbe.
    private sealed class TooltipSideProbe : ComponentBase
    {
        [Parameter] public L.Side Side { get; set; } = L.Side.Top;
        [Parameter] public int Offset { get; set; } = 8;

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<L.Tooltip>(0);
            builder.AddAttribute(1, "ShowDelay", 0);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TooltipTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Hover me")));
                b.CloseComponent();

                b.OpenComponent<L.TooltipContent>(2);
                b.AddAttribute(3, "Side", Side);
                b.AddAttribute(4, "Offset", Offset);
                b.AddAttribute(5, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Tooltip text")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    private static void OpenTooltip(IRenderedComponent<TooltipSideProbe> cut)
        => cut.Find("div").TriggerEvent("onmouseenter", new MouseEventArgs());

    private int PositionFixedCalls()
        => _ctx.JSInterop.Invocations.Count(i => i.Identifier == "positionFixed");

    [Fact]
    public void Changing_Side_While_Open_ReRuns_PositionFixed()
    {
        var cut = _ctx.Render<TooltipSideProbe>(p => p.Add(x => x.Side, L.Side.Top));
        OpenTooltip(cut);

        // First placement happened on open.
        var callsAfterOpen = PositionFixedCalls();
        Assert.True(callsAfterOpen >= 1);

        // Consumer changes the preferred Side while the tooltip is still open.
        cut.Render(p => p.Add(x => x.Side, L.Side.Bottom));

        // Without the fix the `_registered` one-shot latch blocks the re-position,
        // so the box keeps its original side. With the fix, the applied side
        // changed -> PositionFixed runs again with the new side.
        Assert.True(PositionFixedCalls() > callsAfterOpen);
        Assert.Contains(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "positionFixed" && Equals(i.Arguments[4], "bottom"));
    }

    [Fact]
    public void Changing_Offset_While_Open_ReRuns_PositionFixed()
    {
        var cut = _ctx.Render<TooltipSideProbe>(p => p.Add(x => x.Offset, 8));
        OpenTooltip(cut);
        var callsAfterOpen = PositionFixedCalls();

        cut.Render(p => p.Add(x => x.Offset, 24));

        Assert.True(PositionFixedCalls() > callsAfterOpen);
        Assert.Contains(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "positionFixed" && Equals(i.Arguments[5], 24));
    }

    [Fact]
    public void Same_Side_Re_Render_While_Open_Does_Not_ReRun_PositionFixed()
    {
        // Guards against the fix over-correcting: an unrelated re-render that does
        // not change Side/Offset must not thrash the JS placement every frame.
        var cut = _ctx.Render<TooltipSideProbe>(p => p.Add(x => x.Side, L.Side.Top));
        OpenTooltip(cut);
        var callsAfterOpen = PositionFixedCalls();

        cut.Render(p => p.Add(x => x.Side, L.Side.Top));

        Assert.Equal(callsAfterOpen, PositionFixedCalls());
    }
}
