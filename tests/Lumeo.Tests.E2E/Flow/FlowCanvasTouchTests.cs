using Lumeo.Tests.E2E.Gantt;
using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Flow;

/// <summary>
/// Two-finger pinch-zoom (shipped in phase 3a, #511's top follow-up: it had no automated
/// coverage). Playwright's own <c>page.Touchscreen</c> can only dispatch a SINGLE point, so a real
/// two-finger gesture goes through the raw CDP <c>Input.dispatchTouchEvent</c> command instead —
/// each call carries the FULL set of currently-down touch points, exactly like a real touchscreen
/// driver reports it. Chromium synthesizes Pointer Events from these, which is what flow.js's
/// pinch gesture (beginPinchGesture/pinchMidpoint/pinchDistance in flow.js) actually listens to —
/// this is genuine coverage of that code path, not a mouse-wheel simulation of the same zoom.
///
/// Same host/base URL/sequential collection as <see cref="FlowCanvasTests"/>.
/// </summary>
public class FlowCanvasTouchTests : GanttParityTestBase
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await Page.SetViewportSizeAsync(1280, 800);
        await Page.AddInitScriptAsync("window.__lumeoFlowDiag = true;");
    }

    private ILocator Root => Page.Locator("[data-testid='flow-root']");

    private async Task OpenAsync()
    {
        await GotoHost("/e2e/flow?fit=0");
        await Assertions.Expect(Root).ToHaveAttributeAsync("data-flow-ready", "done", new() { Timeout = 20000 });
    }

    private async Task<(double X, double Y, double Zoom)> BoundViewportAsync()
    {
        var text = await Page.Locator("[data-testid='flow-viewport-sink']").TextContentAsync() ?? "";
        var parts = text.Split('|');
        return (double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
    }

    private static object TouchPoint(double x, double y) => new { x, y, radiusX = 5, radiusY = 5, force = 1.0 };

    [Fact]
    public async Task Pinching_Two_Fingers_Apart_Zooms_In_Anchored_On_The_Midpoint()
    {
        await OpenAsync();
        var pane = (await Page.Locator("[data-slot='flow-pane']").BoundingBoxAsync())!;
        var cx = pane.X + pane.Width / 2;
        var cy = pane.Y + pane.Height / 2;
        var before = await BoundViewportAsync();

        var cdp = await Page.Context.NewCDPSessionAsync(Page);
        // Two fingers 40px either side of the pane centre, then spread to 100px either side —
        // a real CDP call per frame, exactly like beginPinchGesture/pinchDistance expect.
        await cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
        {
            ["type"] = "touchStart",
            ["touchPoints"] = new object[] { TouchPoint(cx - 40, cy), TouchPoint(cx + 40, cy) },
        });
        for (var i = 1; i <= 6; i++)
        {
            var spread = 40 + i * 10.0;
            await cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
            {
                ["type"] = "touchMove",
                ["touchPoints"] = new object[] { TouchPoint(cx - spread, cy), TouchPoint(cx + spread, cy) },
            });
        }
        await cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
        {
            ["type"] = "touchEnd",
            ["touchPoints"] = Array.Empty<object>(),
        });

        try
        {
            await Page.WaitForFunctionAsync(
                "z => +document.querySelector('[data-testid=flow-viewport-sink]').textContent.split('|')[2] > z * 1.1",
                before.Zoom, new PageWaitForFunctionOptions { Timeout = 10000 });
        }
        catch (TimeoutException)
        {
            var journal = await Page.EvaluateAsync<string>("() => JSON.stringify(window.__lumeoFlowDiag || [])");
            Assert.Fail($"expected the pinch to zoom in from {before.Zoom:F3}; journal: {journal}");
        }
        var after = await BoundViewportAsync();
        Assert.True(after.Zoom > before.Zoom * 1.1, $"expected zoom in, {before.Zoom:F3} -> {after.Zoom:F3}");

        // Anchored on the midpoint: the flow point under the (fixed) pane centre stays fixed on
        // screen — the same math ZoomAt uses for wheel zoom, this time driven by the pinch's
        // midpoint. Recompute the flow point that WAS under the centre before the gesture and
        // check its screen position after didn't move (±2px — the #385 discipline).
        var flowUnderCentreBefore = new
        {
            X = (cx - pane.X - before.X) / before.Zoom,
            Y = (cy - pane.Y - before.Y) / before.Zoom,
        };
        var screenAfterX = flowUnderCentreBefore.X * after.Zoom + after.X;
        var screenAfterY = flowUnderCentreBefore.Y * after.Zoom + after.Y;
        // A few pixels of tolerance, not the wheel-zoom spec's ±2px: each touchMove reports the
        // midpoint's INTEGER pixel coordinates (CDP touch points), so the anchor is recomputed
        // from a slightly rounded midpoint on every one of the six synthesized frames, compounding
        // a small, real quantization drift a continuous mouse-wheel anchor never has.
        Assert.True(Math.Abs(screenAfterX - (cx - pane.X)) <= 5 && Math.Abs(screenAfterY - (cy - pane.Y)) <= 5,
            $"expected the pinch midpoint's flow point to stay under it, drifted to ({screenAfterX:F1}, {screenAfterY:F1}) vs pane-local ({cx - pane.X:F1}, {cy - pane.Y:F1})");
    }

    [Fact]
    public async Task A_Third_Finger_Does_Not_Break_An_Inflight_Pinch()
    {
        await OpenAsync();
        var pane = (await Page.Locator("[data-slot='flow-pane']").BoundingBoxAsync())!;
        var cx = pane.X + pane.Width / 2;
        var cy = pane.Y + pane.Height / 2;
        var before = await BoundViewportAsync();

        var cdp = await Page.Context.NewCDPSessionAsync(Page);
        await cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
        {
            ["type"] = "touchStart",
            ["touchPoints"] = new object[] { TouchPoint(cx - 30, cy), TouchPoint(cx + 30, cy) },
        });
        // A third finger lands mid-gesture — flow.js's onPointerDown ignores anything past two.
        await cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
        {
            ["type"] = "touchStart",
            ["touchPoints"] = new object[] { TouchPoint(cx - 30, cy), TouchPoint(cx + 30, cy), TouchPoint(cx, cy + 100) },
        });
        await cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
        {
            ["type"] = "touchMove",
            ["touchPoints"] = new object[] { TouchPoint(cx - 90, cy), TouchPoint(cx + 90, cy), TouchPoint(cx, cy + 100) },
        });
        await cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
        {
            ["type"] = "touchEnd",
            ["touchPoints"] = new object[] { TouchPoint(cx, cy + 100) },
        });
        await cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object> { ["type"] = "touchEnd", ["touchPoints"] = Array.Empty<object>() });

        try
        {
            await Page.WaitForFunctionAsync(
                "z => +document.querySelector('[data-testid=flow-viewport-sink]').textContent.split('|')[2] > z * 1.1",
                before.Zoom, new PageWaitForFunctionOptions { Timeout = 10000 });
        }
        catch (TimeoutException)
        {
            var journal = await Page.EvaluateAsync<string>("() => JSON.stringify(window.__lumeoFlowDiag || [])");
            Assert.Fail($"expected the pinch to still zoom in with a third finger present; journal: {journal}");
        }
    }
}
