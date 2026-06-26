using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.HoverCard;

/// <summary>
/// battle-wave2 #80 (state-on-data-change) — HoverCardContent positioned itself
/// once via a one-shot <c>_positioned</c> bool. A <c>Side</c>/<c>Align</c> change
/// while the card stayed OPEN was silently ignored: the latch was already set, so
/// PositionFixed never re-ran and the card stuck to its first-open placement.
///
/// The fix tracks the last-applied anchor <c>(SideStr, AlignStr)</c> and re-runs
/// PositionFixed whenever it differs while open. This asserts the MECHANISM via the
/// recorded <c>positionFixed</c> JSInterop invocations (Arguments[4] == side).
/// </summary>
public class HoverCardRepositionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public HoverCardRepositionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Renders the card OPEN (controlled) with a HoverCardContent on the given Side.
    private IRenderedComponent<L.HoverCard> RenderOpenWithSide(L.Side side)
        => _ctx.Render<L.HoverCard>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.HoverCardContent>(0);
                b.AddAttribute(1, "Side", side);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Card body")));
                b.CloseComponent();
            })));

    private int PositionFixedCount(string side) =>
        _ctx.JSInterop.Invocations.Count(i =>
            i.Identifier == "positionFixed" && Equals(i.Arguments[4], side));

    [Fact]
    public void Side_Change_While_Open_Re_Runs_PositionFixed_With_New_Side()
    {
        // First open positions against the bottom edge.
        var cut = RenderOpenWithSide(L.Side.Bottom);
        Assert.Contains("Card body", cut.Markup);
        Assert.True(PositionFixedCount("bottom") >= 1);

        // No "top" placement has been applied yet.
        Assert.Equal(0, PositionFixedCount("top"));

        // The card stays OPEN but the consumer flips Side to Top. Without the fix the
        // one-shot _positioned latch blocks any re-position, so "top" never reaches
        // the floating layer; with the fix the anchor changed, so PositionFixed
        // re-runs against the top edge.
        cut.Render(p => p
            .Add(c => c.Open, true)
            .Add(c => c.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.HoverCardContent>(0);
                b.AddAttribute(1, "Side", L.Side.Top);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Card body")));
                b.CloseComponent();
            })));

        Assert.True(PositionFixedCount("top") >= 1);
    }

    [Fact]
    public void Same_Side_Rerender_While_Open_Does_Not_Re_Position()
    {
        var cut = RenderOpenWithSide(L.Side.Bottom);
        var afterFirstOpen = PositionFixedCount("bottom");
        Assert.True(afterFirstOpen >= 1);

        // An unrelated parent re-render that keeps Side == Bottom must NOT churn the
        // floating layer — the anchor is unchanged, so no new positionFixed fires.
        cut.Render(p => p
            .Add(c => c.Open, true)
            .Add(c => c.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.HoverCardContent>(0);
                b.AddAttribute(1, "Side", L.Side.Bottom);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Card body")));
                b.CloseComponent();
            })));

        Assert.Equal(afterFirstOpen, PositionFixedCount("bottom"));
    }
}
