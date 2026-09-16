using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// Real-browser coverage for two overlay-placement defects that only show up
/// when a Tooltip's trigger lives inside a wide, horizontally-scrolled
/// container — exactly what a Gantt v3 bar is (a row canvas that can be
/// thousands of pixels wide, clipped by the timeline pane's own
/// <c>overflow-x-auto</c>). Neither defect is reachable from bUnit: both live
/// entirely in <c>positionFixed()</c>'s <c>update()</c> (src/Lumeo/wwwroot/js/
/// components.js), which bUnit never executes.
///
/// Defect 1 (static-position wrap): <c>update()</c> set
/// <c>content.style.position = 'fixed'</c> and then measured the box's
/// natural size (<c>offsetWidth</c>/<c>offsetHeight</c>) BEFORE assigning
/// <c>top</c>/<c>left</c>. With <c>left: auto</c>, a fixed element lays out
/// at its STATIC position — which, for a bar deep inside a wide row canvas,
/// can be far to the right/left of the viewport's origin — so shrink-to-fit
/// resolves against whatever sliver of viewport width remains from THAT
/// position, not the full viewport. The box wraps, the measured height is
/// wrong, and every top/left computed from it lands the tooltip away from
/// its trigger.
///
/// Defect 2 (raw, unclipped reference rect): <c>update()</c> anchored to
/// <c>reference.getBoundingClientRect()</c> — the trigger's FULL layout box,
/// even when most of it has scrolled out of a clipping ancestor. A bar whose
/// right portion has scrolled past the timeline pane's right edge still
/// reports its full (partly invisible) width, so the tooltip centers on
/// geometry the user can't see and can render entirely outside the pane.
/// </summary>
public class GanttV3BarTooltipTests : GanttParityTestBase
{
    private static async Task WaitForReady(IPage page)
    {
        var scrollPane = page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        await scrollPane.WaitForAsync(new() { Timeout = 15000 });
        await Assertions.Expect(scrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });
    }

    [Fact]
    public async Task Tooltip_Sits_On_A_Bar_Anchored_Far_From_The_Pane_Origin()
    {
        // The e2e host's initial auto-scroll centers on "today" (2026-09-16,
        // see SYSTEM date), which is many months after every SharedTasks
        // fixture task (2026-02-23 .. 2026-04-03) — so on load every bar's
        // row-canvas position is already far from the pane's scrolled-into-
        // view origin, exactly the "wide scrolled container" shape the bug
        // needs. ScrollIntoViewIfNeededAsync (same idiom as
        // GanttV3WheelZoomTests) brings the trigger on-screen for the hover;
        // it does NOT change the trigger's underlying document-flow position,
        // which is what the static-position measurement bug actually keys on.
        await GotoHost("/e2e/gantt-v3?viewMode=Week&infiniteScroll=0");
        await WaitForReady(Page);

        var bar = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='be6']");
        await bar.ScrollIntoViewIfNeededAsync();
        var barBox = (await bar.BoundingBoxAsync())!;

        await Page.Mouse.MoveAsync(barBox.X + barBox.Width / 2, barBox.Y + barBox.Height / 2);

        var tip = Page.Locator("[data-slot='tooltip-content'][data-state='open']").First;
        await tip.WaitForAsync(new() { Timeout = 5000 });
        var tipBox = (await tip.BoundingBoxAsync())!;

        // The tooltip's bottom edge must sit on the bar's top edge minus the
        // 8px gap (Tooltip's own SideOffset default) — the pre-fix bug landed
        // it ~80-100px too high because it measured itself at its wrapped
        // (taller) height instead of its real single-line height.
        var expectedBottom = barBox.Y - 8;
        var verticalDrift = Math.Abs(expectedBottom - (tipBox.Y + tipBox.Height));
        Assert.True(verticalDrift <= 12,
            $"expected tooltip bottom ({tipBox.Y + tipBox.Height:F1}) within 12px of bar top - gap ({expectedBottom:F1}), drifted {verticalDrift:F1}px. " +
            $"barBox={Dump(barBox)} tipBox={Dump(tipBox)}");

        // Horizontal centering must also survive — a wrapped natural-size
        // measurement skews the align="center" math on this axis too.
        var barCenterX = barBox.X + barBox.Width / 2;
        var tipCenterX = tipBox.X + tipBox.Width / 2;
        var horizontalDrift = Math.Abs(barCenterX - tipCenterX);
        Assert.True(horizontalDrift <= 12,
            $"expected tooltip horizontal center ({tipCenterX:F1}) within 12px of bar center ({barCenterX:F1}), drifted {horizontalDrift:F1}px. " +
            $"barBox={Dump(barBox)} tipBox={Dump(tipBox)}");

        // No wrap: a Gantt bar tooltip is always exactly two lines (name +
        // date range) — the bug's wrapped measurement inflated this well
        // past a two-line height (observed ~174px against the field report's
        // real numbers). 80px is a generous ceiling above the real ~62px.
        Assert.True(tipBox.Height < 80,
            $"expected a single (unwrapped) two-line tooltip height (< 80px), measured {tipBox.Height:F1}px. tipBox={Dump(tipBox)}");
    }

    [Fact]
    public async Task Tooltip_Anchors_To_The_Visible_Slice_Of_A_Partially_Scrolled_Out_Bar()
    {
        // ShowOffscreenIndicators renders a sticky chevron button pinned near
        // the pane's clipped edge whenever a bar extends past it — exactly
        // the scenario this test engineers, so left on it would sit on top
        // of the bar and steal the hover. Turning it off isolates the
        // tooltip-placement defect from that unrelated UI element; it plays
        // no part in the positioning bug itself.
        await GotoHost("/e2e/gantt-v3?viewMode=Week&infiniteScroll=0&showOffscreenIndicators=0");
        // A wide viewport keeps the browser's OWN edge nowhere near the
        // timeline pane's right edge, so a wrongly-anchored tooltip has room
        // to land fully outside the pane instead of being coincidentally
        // squeezed back into place by positionFixed's viewport-edge clamp —
        // which is exactly what the field report describes ("floats over the
        // page's TOC", i.e. past the pane but still on-page).
        await Page.SetViewportSizeAsync(1900, 1000);
        await WaitForReady(Page);

        var pane = Page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        var bar = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='be2']");
        await bar.ScrollIntoViewIfNeededAsync();

        var paneBoxBefore = (await pane.BoundingBoxAsync())!;
        var barBoxBefore = (await bar.BoundingBoxAsync())!;

        // Scroll the pane (a plain native 'scroll', same mechanism a real
        // scrollbar drag produces) so only the bar's own left ~40px sits
        // inside the pane and the rest is clipped past its right edge.
        const double visibleSliverPx = 40.0;
        var currentBarLeftFromPaneRight = paneBoxBefore.X + paneBoxBefore.Width - barBoxBefore.X;
        var scrollDelta = visibleSliverPx - currentBarLeftFromPaneRight;
        await pane.EvaluateAsync("(el, d) => { el.scrollLeft += d; }", scrollDelta);

        var paneBox = (await pane.BoundingBoxAsync())!;
        var barBox = (await bar.BoundingBoxAsync())!; // FULL layout box — extends past paneBox's right edge
        var visibleLeft = Math.Max(barBox.X, paneBox.X);
        var visibleRight = Math.Min(barBox.X + barBox.Width, paneBox.X + paneBox.Width);
        var visibleSliceWidth = visibleRight - visibleLeft;
        Assert.True(visibleSliceWidth > 0 && visibleSliceWidth < barBox.Width,
            $"test setup invariant: the bar must be genuinely straddling the pane's right edge (partially clipped), " +
            $"but its visible slice was [{visibleLeft:F1}, {visibleRight:F1}] against a full width of {barBox.Width:F1}. " +
            $"barBox={Dump(barBox)} paneBox={Dump(paneBox)}");

        // Hover near the LEFT of the visible slice, not its center — the
        // slice is only ~40px wide and its rightmost pixels sit right at the
        // pane's clip edge, occasionally landing hit-tests on the pane's own
        // scrollbar/border rather than the bar.
        var hoverX = visibleLeft + 8;
        var hoverY = barBox.Y + barBox.Height / 2;
        await Page.Mouse.MoveAsync(hoverX - 100, hoverY);
        await Page.Mouse.MoveAsync(hoverX, hoverY, new MouseMoveOptions { Steps = 5 });

        var tip = Page.Locator("[data-slot='tooltip-content'][data-state='open']").First;
        await tip.WaitForAsync(new() { Timeout = 5000 });
        var tipBox = (await tip.BoundingBoxAsync())!;

        var visibleCenterX = (visibleLeft + visibleRight) / 2;
        var tipCenterX = tipBox.X + tipBox.Width / 2;

        // The tooltip's horizontal center must land within the pane's own
        // visible rect — the pre-fix bug anchored to the bar's FULL
        // (mostly-invisible) width and rendered the tooltip entirely outside
        // the pane (measured ~169px off in the diagnostic run below).
        Assert.True(tipCenterX >= paneBox.X && tipCenterX <= paneBox.X + paneBox.Width,
            $"expected tooltip horizontal center ({tipCenterX:F1}) inside the pane's visible rect [{paneBox.X:F1}, {paneBox.X + paneBox.Width:F1}]. " +
            $"barBox={Dump(barBox)} paneBox={Dump(paneBox)} tipBox={Dump(tipBox)}");

        var horizontalDrift = Math.Abs(visibleCenterX - tipCenterX);
        Assert.True(horizontalDrift <= 12,
            $"expected tooltip horizontal center ({tipCenterX:F1}) within 12px of the visible slice's center ({visibleCenterX:F1}), drifted {horizontalDrift:F1}px. " +
            $"barBox={Dump(barBox)} paneBox={Dump(paneBox)} tipBox={Dump(tipBox)}");

        // The vertical placement must still be correct too (defect 1 and
        // defect 2 both fire from the same update() pass against the same
        // reference rect).
        var expectedBottom = barBox.Y - 8;
        var verticalDrift = Math.Abs(expectedBottom - (tipBox.Y + tipBox.Height));
        Assert.True(verticalDrift <= 12,
            $"expected tooltip bottom ({tipBox.Y + tipBox.Height:F1}) within 12px of bar top - gap ({expectedBottom:F1}), drifted {verticalDrift:F1}px. " +
            $"barBox={Dump(barBox)} tipBox={Dump(tipBox)}");
    }

    private static string Dump(LocatorBoundingBoxResult box) =>
        $"X={box.X:F1} Y={box.Y:F1} W={box.Width:F1} H={box.Height:F1}";
}
