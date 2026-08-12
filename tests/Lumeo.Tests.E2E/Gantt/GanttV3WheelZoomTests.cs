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

        // fe1 (2026-02-23, see GanttParityFixtures) is many months before
        // "today", the initial auto-scroll's own centering anchor. At Week
        // zoom's wide pixels-per-day, that gap places fe1's bar well outside
        // the pane's clipped viewport — its own BoundingBoxAsync (no
        // auto-scroll, unlike Playwright's click/hover helpers) returns a
        // deeply negative X (observed: pane spans roughly x=[17, 1263], fe1's
        // box.X was -1818) — a coordinate the pane's own wheel listener never
        // sees a 'wheel' event dispatched at, since it isn't hit-testable
        // there. ScrollIntoViewIfNeededAsync (already used by
        // CtrlWheel_Anchors_The_Zoom_On_The_Pointer_Not_The_Viewport_Center's
        // own setup, at Month zoom where fe1 already happens to sit in view)
        // is the fix: it scrolls the pane so fe1's bar is actually on-screen
        // BEFORE its box is measured, so the synthetic Ctrl+wheel gesture
        // below lands on the real registered element instead of empty space.
        var bar = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe1']");
        await bar.ScrollIntoViewIfNeededAsync();
        var box = (await bar.BoundingBoxAsync())!;
        var viewModeSink = Page.Locator("[data-testid='gantt-v3-viewmode']");

        // deltaY < 0 ("scroll up") zooms IN — Week -> Day (DefaultLevels'
        // coarsest-last order: Day, Week, Month, Year).
        await CtrlWheelAt(box.X + box.Width / 2, box.Y + box.Height / 2, -200);
        await Assertions.Expect(viewModeSink).ToHaveTextAsync("Day", new() { Timeout = 5000 });

        // deltaY > 0 ("scroll down") zooms back OUT — Day -> Week. Day zoom's
        // even wider pixels-per-day makes the same off-screen risk worse, so
        // this re-scroll is not optional either.
        await bar.ScrollIntoViewIfNeededAsync();
        var box2 = (await bar.BoundingBoxAsync())!;
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
        // THE core design requirement: hover at a specific, known date — fe1's
        // own left edge, 2026-02-23 — zoom, and confirm that same edge is
        // still under (approximately) the SAME viewport pixel — a
        // viewport-CENTER recenter would instead pull that edge toward
        // whatever the pane's own horizontal midpoint is.
        //
        // A prior version of this test hovered at fe1's edge WHEREVER it
        // happened to render at Month zoom's own initial (today-centered)
        // auto-scroll position — which, measured directly, is only ~18px
        // from this pane's own horizontal center (paneCenterX=640,
        // fe1.left=622 in a 1246px-wide pane). A disable-check (temporarily
        // forcing the JS side to always report offsetPx = rect.width / 2,
        // i.e. a genuine viewport-CENTER recenter) still passed that
        // fixture's own 60px tolerance — proof the fixture, not the
        // guarantee, was wrong: it couldn't actually tell a pointer-anchored
        // recenter from a center-anchored one because the two pointer
        // positions were nearly the same pixel to begin with.
        //
        // Fix: explicitly scroll the pane (a plain native 'scroll', the same
        // mechanism a real user's own scrollbar drag would produce) so fe1's
        // edge sits near the pane's own LEFT edge — deliberately far from
        // paneCenterX — BEFORE measuring cursorX, so a center-anchored
        // recenter and a pointer-anchored one are unambiguously
        // distinguishable. Re-run against the SAME disable-check: the
        // sabotaged (center-anchored) build now drifts fe1's edge by
        // ~2177px; the real, pointer-anchored build drifts it by ~40px —
        // over 50x apart, nowhere near this test's own tolerance boundary.
        await GotoHost("/e2e/gantt-v3?viewMode=Month&infiniteScroll=0");
        await WaitForReady(Page);

        var pane = Page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        var bar = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe1']");
        await bar.ScrollIntoViewIfNeededAsync();

        var paneBoxBefore = (await pane.BoundingBoxAsync())!;
        var barBoxBefore = (await bar.BoundingBoxAsync())!;

        // Scroll so fe1's left edge sits ~80px from the pane's own left edge
        // — far from paneBoxBefore's own horizontal center.
        const double desiredOffsetPx = 80.0;
        var scrollDelta = barBoxBefore.X - (paneBoxBefore.X + desiredOffsetPx);
        await pane.EvaluateAsync("(el, d) => { el.scrollLeft += d; }", scrollDelta);

        var boxBefore = (await bar.BoundingBoxAsync())!;
        var cursorX = boxBefore.X;
        var cursorY = boxBefore.Y + boxBefore.Height / 2;
        var paneCenterX = paneBoxBefore.X + paneBoxBefore.Width / 2;
        Assert.True(Math.Abs(cursorX - paneCenterX) > 300,
            $"test setup invariant: cursorX ({cursorX:F1}) must be far from the pane's own center ({paneCenterX:F1}) for this test to have any discriminating power, but only differed by {Math.Abs(cursorX - paneCenterX):F1}px");

        await CtrlWheelAt(cursorX, cursorY, -200); // Month -> Week (zoom in one step)
        await Assertions.Expect(Page.Locator("[data-testid='gantt-v3-viewmode']")).ToHaveTextAsync("Week", new() { Timeout = 5000 });

        var boxAfter = (await bar.BoundingBoxAsync())!;

        // A generous but meaningful tolerance: Month mode's own column is
        // wide (v2/v3-parity constant, tens of px), so a CENTER-anchored
        // recenter (the bug this test guards against) would drift fe1's
        // left edge by hundreds of px (measured ~2177px against a
        // deliberately sabotaged build, far more than this tolerance),
        // while a genuinely pointer-anchored recenter keeps it within a few
        // columns' worth of rounding/snap slack (measured ~40px against the
        // real build).
        var drift = Math.Abs(boxAfter.X - cursorX);
        Assert.True(drift < 120,
            $"expected fe1's left edge to stay within 120px of the original cursor X ({cursorX:F1}) after a pointer-anchored zoom, drifted to {boxAfter.X:F1} (Δ={drift:F1}px)");
    }
}
