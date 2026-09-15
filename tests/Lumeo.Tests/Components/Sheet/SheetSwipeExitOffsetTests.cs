using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sheet;

/// <summary>
/// Field report #464, finding 3 — a slow swipe-to-close release used to snap
/// the panel back to fully open for a moment before the exit animation took
/// over, because the slide-out-to-* keyframes' `from` always started at
/// translate(0) regardless of how far the drag had already pushed the panel.
///
/// The fix: on a swipe dismiss, SheetContent reads the panel's live drag
/// offset back from JS (<c>GetSwipeReleaseOffset</c>) BEFORE dispatching the
/// close, and renders it onto the panel as the <c>--lumeo-sheet-exit-from</c>
/// CSS custom property the keyframes now read for their `from` value. A
/// non-swipe close (Escape / backdrop / X / programmatic) never sets the
/// property, so the keyframes fall back to their original 0 default.
///
/// bUnit has no real touch events, so these tests simulate the JS side by
/// staging <see cref="TrackingInteropService.SwipeReleaseOffsetPx"/> and
/// invoking the captured <see cref="TrackingInteropService.LastDrawerSwipeHandler"/>
/// directly — exactly the shape JS's onTouchEnd invokes on a real dismiss.
/// </summary>
public class SheetSwipeExitOffsetTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public SheetSwipeExitOffsetTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.Sheet> RenderSwipeableSheet(bool open) =>
        _ctx.Render<L.Sheet>(p => p
            .Add(s => s.Open, open)
            .AddChildContent<L.SheetContent>(cp => cp
                .Add(c => c.Side, L.Side.Bottom)
                .Add(c => c.SwipeToClose, true)
                .AddChildContent("Body")));

    [Fact]
    public async Task Swipe_Dismiss_Carries_The_Release_Offset_As_A_Custom_Property()
    {
        var cut = RenderSwipeableSheet(open: true);
        cut.WaitForAssertion(() => Assert.NotNull(_interop.LastDrawerSwipeHandler));

        // Simulate JS reporting the panel was sitting 180px into its dismiss
        // travel when the finger released (a slow swipe that never reached
        // the end of its throw).
        _interop.SwipeReleaseOffsetPx = 180;
        await cut.InvokeAsync(() => _interop.LastDrawerSwipeHandler!());
        // Unlike a real browser (where a [JSInvokable] callback returning
        // triggers a render sweep automatically), bUnit's InvokeAsync doesn't
        // itself schedule a re-render for a handler that never calls
        // StateHasChanged on its own path (Dismiss/TryDismiss don't) — force one
        // so the just-mutated Open/_exiting state actually reaches the markup.
        cut.Render();

        cut.WaitForAssertion(() =>
        {
            var dialog = cut.Find("[role='dialog']");
            Assert.Contains("animate-slide-out-to-bottom", dialog.GetAttribute("class") ?? "");
            Assert.Contains("--lumeo-sheet-exit-from:180px", dialog.GetAttribute("style") ?? "");
        });
    }

    [Fact]
    public async Task NonSwipe_Close_Never_Sets_The_Custom_Property()
    {
        var cut = RenderSwipeableSheet(open: true);
        cut.WaitForAssertion(() => Assert.NotNull(_interop.LastDrawerSwipeHandler));

        // A swipe DID drag the panel this session (staged offset), but the
        // sheet is dismissed programmatically (Open flipped false directly),
        // not via the swipe handler — the exit must still start from 0.
        _interop.SwipeReleaseOffsetPx = 220;
        cut.Render(p => p.Add(s => s.Open, false));

        var dialog = cut.Find("[role='dialog']");
        Assert.Contains("animate-slide-out-to-bottom", dialog.GetAttribute("class") ?? "");
        Assert.DoesNotContain("--lumeo-sheet-exit-from", dialog.GetAttribute("style") ?? "");
    }

    [Fact]
    public async Task Reopening_After_A_Swipe_Dismiss_Clears_The_Offset_For_The_Next_Close()
    {
        var cut = RenderSwipeableSheet(open: true);
        cut.WaitForAssertion(() => Assert.NotNull(_interop.LastDrawerSwipeHandler));

        _interop.SwipeReleaseOffsetPx = 150;
        await cut.InvokeAsync(() => _interop.LastDrawerSwipeHandler!());
        cut.Render();
        cut.WaitForAssertion(() =>
            Assert.Contains("--lumeo-sheet-exit-from:150px", cut.Find("[role='dialog']").GetAttribute("style") ?? ""));

        // Reopen, then close via Escape (not swipe) — the stale offset from the
        // earlier swipe-dismiss must not leak into this unrelated close.
        cut.Render(p => p.Add(s => s.Open, true));
        cut.Render(p => p.Add(s => s.Open, false));

        var dialog = cut.Find("[role='dialog']");
        Assert.DoesNotContain("--lumeo-sheet-exit-from", dialog.GetAttribute("style") ?? "");
    }
}
