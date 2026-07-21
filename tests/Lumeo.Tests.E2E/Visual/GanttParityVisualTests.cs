using Lumeo.Tests.E2E.Gantt;
using Microsoft.Playwright;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Lumeo.Tests.E2E.Visual;

/// <summary>
/// Visual snapshots for the Gantt v2/v3 parity harness (feat/gantt-v3, T4) — one
/// baseline per (route, view mode) pair, following <c>HomePageVisualTests</c>'
/// exact conventions (baseline dir, perceptual ImageSharp diff, update flag,
/// CI-skip-without-a-Linux-baseline guard — see that class' remarks for why the
/// guard exists: the committed baselines are generated on Windows and Linux
/// Chromium's font AA drifts well past the pixel-tolerance threshold).
///
/// Drives <c>tests/Lumeo.Tests.ServerHost</c> (via <see cref="GanttParityTestBase"/>),
/// NOT the docs site <see cref="PlaywrightTestBase"/> targets — see that class'
/// remarks for the separate <c>LUMEO_GANTT_E2E_BASE_URL</c> env var.
/// </summary>
public class GanttParityVisualTests : GanttParityTestBase
{
    private const int ViewportWidth = 1400;
    private const int ViewportHeight = 700;
    private const double ToleranceRatio = 0.005; // 0.5 %, same as HomePageVisualTests
    private const int PixelDeltaThreshold = 30;  // same Manhattan-distance threshold

    [Theory]
    [InlineData("v2", "Day")]
    [InlineData("v2", "Week")]
    [InlineData("v2", "Month")]
    [InlineData("v2", "Year")]
    [InlineData("v3", "Day")]
    [InlineData("v3", "Week")]
    [InlineData("v3", "Month")]
    [InlineData("v3", "Year")]
    public async Task Gantt_page_matches_baseline(string route, string viewMode)
    {
        // QuarterDay/HalfDay are deliberately excluded from the visual set: they
        // have no toolbar button in either version (settable only via the
        // ViewMode parameter/query string) and render the same bar/header
        // machinery already covered pixel-for-pixel by the QuarterDay/HalfDay
        // cases in GanttParityTests' header-label-run + bar-geometry theories —
        // a visual snapshot of them would add baseline weight without covering
        // anything the functional assertions don't already pin.
        if (Environment.GetEnvironmentVariable("CI") == "true"
            && Environment.GetEnvironmentVariable("LUMEO_E2E_UPDATE_SNAPSHOTS") != "1")
        {
            return;
        }

        await Page.SetViewportSizeAsync(ViewportWidth, ViewportHeight);
        await GotoHost($"/e2e/gantt-{route}?viewMode={viewMode}");

        var rootTestId = route == "v2" ? "gantt-v2-root" : "gantt-v3-root";
        await Page.Locator($"[data-testid='{rootTestId}'] [data-task-id]").First
            .WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 15000 });

        await Page.AddStyleTagAsync(new() { Content = "*, *::before, *::after { animation-duration: 0s !important; animation-delay: 0s !important; transition-duration: 0s !important; transition-delay: 0s !important; }" });
        await Page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
        // GanttV3ScrollToXAsync's Task resolves as soon as the JS call is
        // dispatched, NOT when its internal requestAnimationFrame-scheduled
        // scroll actually lands (gantt-v3.js's centerOn is fire-and-forget by
        // design — see its own remarks) — a screenshot taken right after the
        // two rAF ticks above could still race that scroll (Codex review wave
        // P1 fix; caught here because the Month-mode baseline briefly showed
        // the pre-scroll position before this delay was added).
        await Page.WaitForTimeoutAsync(250);

        if (route == "v3")
        {
            // Codex round 2, P2 #8 ("visual snapshot drift"): pin the scroll
            // deterministically via the SAME latch GanttV3NavTests already uses
            // (gantt-v3.js's centerOn stamps this attribute atomically with the
            // scroll it performs) instead of relying solely on the blind delay
            // above, which only ever bounded the RACE, not WHERE the scroll
            // actually lands — that depends on ShouldAttemptTodayScroll's own
            // v2-parity gate (GanttTimeline.razor's remarks) reacting correctly
            // to wherever DateTime.Today/the browser's resolved date happens to
            // fall relative to SharedTasks' fixed 2026 window on the day this
            // runs, which the scroll-gate fix (not this wait) is what actually
            // stabilizes; this wait only removes the SEPARATE async-landing race.
            // Codex round 3, P2 #1: the scroll-to-today latch attribute now
            // lands on Gantt3's shared OUTER pane (the "overflow:auto" wrapper
            // around the tree+timeline flex row), not on GanttTimeline's own
            // row-canvas div — that div no longer scrolls (or carries
            // overflow-x-auto) at all once Gantt3 supplies ScrollHost, since
            // ITS scroll interop calls now all target the outer pane directly
            // (see GanttTimeline.EffectiveScrollHost's remarks).
            var scrollHost = Page.Locator($"[data-testid='{rootTestId}'] div[style*='overflow']").First;
            await Assertions.Expect(scrollHost).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 5000 });
        }

        var screenshotBytes = await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Clip = new Clip { X = 0, Y = 0, Width = ViewportWidth, Height = ViewportHeight },
        });

        var baselinePath = Path.Combine(GetSnapshotsDir(), $"gantt-{route}-{viewMode}.png");

        if (Environment.GetEnvironmentVariable("LUMEO_E2E_UPDATE_SNAPSHOTS") == "1")
        {
            Directory.CreateDirectory(GetSnapshotsDir());
            await File.WriteAllBytesAsync(baselinePath, screenshotBytes);
            return;
        }

        Assert.True(File.Exists(baselinePath),
            $"Visual baseline missing at: {baselinePath}. Run with LUMEO_E2E_UPDATE_SNAPSHOTS=1 to generate it.");

        var baselineBytes = await File.ReadAllBytesAsync(baselinePath);

        using var baselineImage = Image.Load<Rgba32>(baselineBytes);
        using var currentImage = Image.Load<Rgba32>(screenshotBytes);

        if (baselineImage.Width != currentImage.Width || baselineImage.Height != currentImage.Height)
        {
            Assert.Fail($"Screenshot dimensions changed: baseline {baselineImage.Width}x{baselineImage.Height} vs current {currentImage.Width}x{currentImage.Height}.");
        }

        int width = baselineImage.Width, height = baselineImage.Height;
        int totalPixels = width * height, differentPixels = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var b = baselineImage[x, y];
                var c = currentImage[x, y];
                var manhattan = Math.Abs(b.R - c.R) + Math.Abs(b.G - c.G) + Math.Abs(b.B - c.B) + Math.Abs(b.A - c.A);
                if (manhattan > PixelDeltaThreshold) differentPixels++;
            }
        }

        var ratio = (double)differentPixels / totalPixels;
        if (ratio >= ToleranceRatio)
        {
            var diffPath = Path.Combine(Path.GetTempPath(), $"lumeo-gantt-visual-diff-{route}-{viewMode}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png");
            await File.WriteAllBytesAsync(diffPath, screenshotBytes);
            Assert.Fail($"gantt-{route}-{viewMode} screenshot differs from baseline: {differentPixels:N0}/{totalPixels:N0} pixels ({ratio:P2}) exceed the threshold. Current screenshot saved: {diffPath}. If intentional, regenerate with LUMEO_E2E_UPDATE_SNAPSHOTS=1.");
        }
    }

    private static string GetSnapshotsDir()
    {
        var here = AppContext.BaseDirectory;
        var repo = Path.GetFullPath(Path.Combine(here, "..", "..", "..", "..", ".."));
        return Path.Combine(repo, "tests", "Lumeo.Tests.E2E", "Snapshots");
    }
}
