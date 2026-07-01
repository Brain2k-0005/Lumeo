using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Popover;

/// <summary>
/// battle-wave2 #86 (state-on-data-change) — PopoverContent positioned itself
/// once inside <c>if (Context.IsOpen &amp;&amp; !_registered)</c>. The <c>_registered</c>
/// latch (which guards the one-shot click-outside registration + focus-on-open)
/// also blocked any re-position, so a <c>Side</c>/<c>Align</c> change while the
/// popover stayed OPEN was silently ignored and the surface stuck to its
/// first-open placement.
///
/// The fix tracks the last-applied anchor <c>(SideStr, AlignStr)</c> and re-runs
/// PositionFixed whenever it differs while open. This asserts the MECHANISM via the
/// recorded <c>positionFixed</c> JSInterop invocations (Arguments[4] == side,
/// Arguments[2] == align).
/// </summary>
public class PopoverRepositionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PopoverRepositionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Renders the popover OPEN with a PopoverContent on the given Side/Align.
    private IRenderedComponent<L.Popover> RenderOpen(L.Side side, L.Align align = L.Align.Center)
        => _ctx.Render<L.Popover>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.ChildContent, Content(side, align)));

    private static RenderFragment Content(L.Side side, L.Align align) => b =>
    {
        b.OpenComponent<L.PopoverContent>(0);
        b.AddAttribute(1, "Side", side);
        b.AddAttribute(2, "Align", align);
        b.AddAttribute(3, "ChildContent",
            (RenderFragment)(inner => inner.AddContent(0, "Popover body")));
        b.CloseComponent();
    };

    private int PositionFixedCountForSide(string side) =>
        _ctx.JSInterop.Invocations.Count(i =>
            i.Identifier == "positionFixed" && Equals(i.Arguments[4], side));

    private int PositionFixedCountForAlign(string align) =>
        _ctx.JSInterop.Invocations.Count(i =>
            i.Identifier == "positionFixed" && Equals(i.Arguments[2], align));

    [Fact]
    public void Side_Change_While_Open_Re_Runs_PositionFixed_With_New_Side()
    {
        // First open positions against the bottom edge.
        var cut = RenderOpen(L.Side.Bottom);
        Assert.Contains("Popover body", cut.Markup);
        Assert.True(PositionFixedCountForSide("bottom") >= 1);

        // No "top" placement has been applied yet.
        Assert.Equal(0, PositionFixedCountForSide("top"));

        // The popover stays OPEN but the consumer flips Side to Top. Without the
        // fix the one-shot _registered latch blocks any re-position, so "top"
        // never reaches the floating layer; with the fix the anchor changed, so
        // PositionFixed re-runs against the top edge.
        cut.Render(p => p
            .Add(c => c.Open, true)
            .Add(c => c.ChildContent, Content(L.Side.Top, L.Align.Center)));

        Assert.True(PositionFixedCountForSide("top") >= 1);
    }

    [Fact]
    public void Align_Change_While_Open_Re_Runs_PositionFixed_With_New_Align()
    {
        var cut = RenderOpen(L.Side.Bottom, L.Align.Center);
        Assert.True(PositionFixedCountForAlign("center") >= 1);
        Assert.Equal(0, PositionFixedCountForAlign("end"));

        // Flip Align while the popover stays open.
        cut.Render(p => p
            .Add(c => c.Open, true)
            .Add(c => c.ChildContent, Content(L.Side.Bottom, L.Align.End)));

        Assert.True(PositionFixedCountForAlign("end") >= 1);
    }

    [Fact]
    public void Same_Anchor_Rerender_While_Open_Does_Not_Re_Position()
    {
        var cut = RenderOpen(L.Side.Bottom, L.Align.Center);
        var afterFirstOpen = PositionFixedCountForSide("bottom");
        Assert.True(afterFirstOpen >= 1);

        // An unrelated parent re-render that keeps Side/Align unchanged must NOT
        // churn the floating layer — the anchor is the same, so no new
        // positionFixed fires.
        cut.Render(p => p
            .Add(c => c.Open, true)
            .Add(c => c.ChildContent, Content(L.Side.Bottom, L.Align.Center)));

        Assert.Equal(afterFirstOpen, PositionFixedCountForSide("bottom"));
    }
}
