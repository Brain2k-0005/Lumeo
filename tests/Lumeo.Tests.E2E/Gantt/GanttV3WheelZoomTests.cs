using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// v3-ONLY: real-browser coverage for Ctrl/Cmd+wheel zoom — GanttV3's own
/// previously entirely-missing feature (zero wheel-related code existed
/// anywhere in gantt-v3.js). The registration/anchor-math/defensive-guard
/// half is already covered by bUnit (<c>GanttV3WheelZoomTests</c> in the
/// main test project); what ONLY a real browser can prove is the actual
/// native 'wheel' event handling this suite exists for: a bare wheel is
/// never intercepted, Ctrl+wheel actually changes the rendered ViewMode, and
/// — the core design requirement — the date under the cursor visibly stays
/// under the cursor across the zoom.
/// </summary>
public class GanttV3WheelZoomTests : GanttParityTestBase
{
    private static async Task WaitForReady(IPage page)
    {
        var scrollPane = page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        await scrollPane.WaitForAsync(new() { Timeout = 15000 });
        await Assertions.Expect(scrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });
    }

    private async Task CtrlWheelAt(float x, float y, float deltaY)
    {
        await Page.Mouse.MoveAsync(x, y);
        await Page.Keyboard.DownAsync("Control");
        await Page.Mouse.WheelAsync(0, deltaY);
        await Page.Keyboard.UpAsync("Control");
    }

    [Fact]
    public async Task Bare_Wheel_Over_The_Timeline_Never_Changes_ViewMode()
    {
        await GotoHost("/e2e/gantt-v3?viewMode=Week&infiniteScroll=0");
        await WaitForReady(Page);

        var canvas = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe1']");
        var box = await canvas.BoundingBoxAsync();
        Assert.NotNull(box);

        // No Ctrl/Cmd held — see WheelZoom's own design remarks: a bare
        // wheel must be indistinguishable from there being no Gantt at all.
        await Page.Mouse.MoveAsync(box!.X + box.Width / 2, box.Y + box.Height / 2);
        await Page.Mouse.WheelAsync(0, -200);
        await Page.WaitForTimeoutAsync(300);

        await Assertions.Expect(Page.Locator("[data-testid='gantt-v3-viewmode']")).ToHaveTextAsync("Week");
    }

    [Fact]
    public async Task CtrlWheel_Up_Zooms_In_And_CtrlWheel_Down_Zooms_Back_Out()
    {
        await GotoHost("/e2e/gantt-v3?viewMode=Week&infiniteScroll=0");
        await WaitForReady(Page);

        var box = (await Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe1']").BoundingBoxAsync())!;
        var viewModeSink = Page.Locator("[data-testid='gantt-v3-viewmode']");

        // deltaY < 0 ("scroll up") zooms IN — Week -> Day (DefaultLevels'
        // coarsest-last order: Day, Week, Month, Year).
        await CtrlWheelAt(box.X + box.Width / 2, box.Y + box.Height / 2, -200);
        await Assertions.Expect(viewModeSink).ToHaveTextAsync("Day", new() { Timeout = 5000 });

        // deltaY > 0 ("scroll down") zooms back OUT — Day -> Week.
        var box2 = (await Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe1']").BoundingBoxAsync())!;
        await CtrlWheelAt(box2.X + box2.Width / 2, box2.Y + box2.Height / 2, 200);
        await Assertions.Expect(viewModeSink).ToHaveTextAsync("Week", new() { Timeout = 5000 });
    }

    [Fact]
    public async Task CtrlWheel_At_The_Finest_Level_Stops_Changing_ViewMode()
    {
        // "At the zoom limits the gesture returns to the browser" — the
        // in-page consequence this suite CAN observe headlessly: once Day
        // (the finest resolved level) is reached, further zoom-in gestures
        // leave the ViewMode sink unchanged rather than throwing/wrapping
        // around. Predicted wrong value under a naive unbounded stepper: the
        // sink would show something other than "Day" (an out-of-range index
        // wrapping to garbage, or an exception breaking the circuit).
        await GotoHost("/e2e/gantt-v3?viewMode=Day&infiniteScroll=0");
        await WaitForReady(Page);

        var box = (await Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe1']").BoundingBoxAsync())!;
        var viewModeSink = Page.Locator("[data-testid='gantt-v3-viewmode']");

        await CtrlWheelAt(box.X + box.Width / 2, box.Y + box.Height / 2, -200);
        await Page.WaitForTimeoutAsync(300);

        await Assertions.Expect(viewModeSink).ToHaveTextAsync("Day");
    }

    [Fact]
    public async Task WheelZoom_False_Disables_The_Gesture_Entirely()
    {
        await GotoHost("/e2e/gantt-v3?viewMode=Week&infiniteScroll=0&wheelZoom=0");
        await WaitForReady(Page);

        var box = (await Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe1']").BoundingBoxAsync())!;
        await CtrlWheelAt(box.X + box.Width / 2, box.Y + box.Height / 2, -200);
        await Page.WaitForTimeoutAsync(300);

        await Assertions.Expect(Page.Locator("[data-testid='gantt-v3-viewmode']")).ToHaveTextAsync("Week");
    }

    [Fact]
    public async Task CtrlWheel_Anchors_The_Zoom_On_The_Pointer_Not_The_Viewport_Center()
    {
        // THE core design requirement: hover exactly at fe1's own left edge
        // (a specific, known date — 2026-02-23), zoom, and confirm that same
        // edge is still under (approximately) the SAME viewport pixel — a
        // viewport-CENTER recenter would instead pull that edge toward
        // whatever the pane's own horizontal midpoint is, which the pane's
        // own width guarantees is measurably different from fe1's edge in
        // this fixture (fe1 renders well left of center at Week zoom).
        await GotoHost("/e2e/gantt-v3?viewMode=Month&infiniteScroll=0");
        await WaitForReady(Page);

        var bar = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe1']");
        await bar.ScrollIntoViewIfNeededAsync();
        var boxBefore = (await bar.BoundingBoxAsync())!;
        var cursorX = boxBefore.X;
        var cursorY = boxBefore.Y + boxBefore.Height / 2;

        await CtrlWheelAt(cursorX, cursorY, -200); // Month -> Week (zoom in one step)
        await Assertions.Expect(Page.Locator("[data-testid='gantt-v3-viewmode']")).ToHaveTextAsync("Week", new() { Timeout = 5000 });

        var boxAfter = (await bar.BoundingBoxAsync())!;

        // A generous but meaningful tolerance: Month mode's own column is
        // wide (v2/v3-parity constant, tens of px), so a CENTER-anchored
        // recenter (the bug this test guards against) would drift fe1's
        // left edge by a large fraction of the pane's own width — far more
        // than this tolerance — while a genuinely pointer-anchored recenter
        // keeps it within a few columns' worth of rounding/snap slack.
        var drift = Math.Abs(boxAfter.X - cursorX);
        Assert.True(drift < 60,
            $"expected fe1's left edge to stay within 60px of the original cursor X ({cursorX:F1}) after a pointer-anchored zoom, drifted to {boxAfter.X:F1} (Δ={drift:F1}px)");
    }
}
