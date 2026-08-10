using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// The unlisted branch-preview page at <c>/e2e/charts-native</c> (PR #392) puts
/// all 14 native chart types next to their ECharts legacy counterpart, fed from
/// identical data, so the owner can actually look at the first-party charting
/// engine instead of taking "it compiles" on faith. Same framing as
/// <c>GanttV3PreviewPageTests</c>: a Blazor render exception kills everything
/// below it in the component tree, so a page that half-renders can still return
/// HTTP 200 — assert per-pair presence via each instance's own <c>id</c>
/// attribute (forwarded through <c>AdditionalAttributes</c> to the chart's root
/// element per the project's component convention), not just that the page
/// responded.
/// </summary>
public class ChartsNativePageTests : PlaywrightTestBase
{
    // One (native id, legacy id) pair per native chart type shipped in PR #392.
    private static readonly (string Native, string Legacy)[] _pairs =
    {
        ("chart-native-line", "chart-legacy-line"),
        ("chart-native-area", "chart-legacy-area"),
        ("chart-native-bar", "chart-legacy-bar"),
        ("chart-native-mixed", "chart-legacy-mixed"),
        ("chart-native-scatter", "chart-legacy-scatter"),
        ("chart-native-effect-scatter", "chart-legacy-effect-scatter"),
        ("chart-native-waterfall", "chart-legacy-waterfall"),
        ("chart-native-pie", "chart-legacy-pie"),
        ("chart-native-donut", "chart-legacy-donut"),
        ("chart-native-nightingale", "chart-legacy-nightingale"),
        ("chart-native-radar", "chart-legacy-radar"),
        ("chart-native-heatmap", "chart-legacy-heatmap"),
        ("chart-native-boxplot", "chart-legacy-boxplot"),
        ("chart-native-candlestick", "chart-legacy-candlestick"),
    };

    [Fact]
    public async Task Preview_page_loads_and_renders_all_14_native_vs_echarts_pairs_with_no_console_errors()
    {
        var consoleErrors = new List<string>();
        var pageErrors = new List<string>();
        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error") consoleErrors.Add(msg.Text);
        };
        Page.PageError += (_, error) => pageErrors.Add(error);

        var response = await Goto("/e2e/charts-native");
        Assert.NotNull(response);
        Assert.True(response.Ok, $"GET /e2e/charts-native returned HTTP {response.Status}");

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Every native/legacy pair must render its own root element — proves
        // the component tree didn't stop partway through (see class remarks).
        foreach (var (nativeId, legacyId) in _pairs)
        {
            await Page.Locator($"#{nativeId}").WaitForAsync(new() { Timeout = 30000 });
            await Page.Locator($"#{legacyId}").WaitForAsync(new() { Timeout = 30000 });
        }

        // Bonus sections: large-data heatmap control and the loading-state pair.
        await Page.Locator("#chart-native-heatmap-large").WaitForAsync(new() { Timeout = 30000 });
        await Page.Locator("#chart-legacy-heatmap-large").WaitForAsync(new() { Timeout = 30000 });
        await Page.Locator("#heatmap-cell-count").WaitForAsync(new() { Timeout = 5000 });
        await Page.Locator("#chart-native-loading").WaitForAsync(new() { Timeout = 30000 });
        await Page.Locator("#chart-legacy-loading").WaitForAsync(new() { Timeout = 30000 });

        // Blazor's standard unhandled-exception banner — hidden by default
        // (display:none), shown on an uncaught render exception.
        var errorUi = Page.Locator("#blazor-error-ui");
        if (await errorUi.CountAsync() > 0)
        {
            await Assertions.Expect(errorUi).ToBeHiddenAsync(new() { Timeout = 2000 });
        }

        Assert.True(consoleErrors.Count == 0, $"Console errors on /e2e/charts-native:\n{string.Join('\n', consoleErrors)}");
        Assert.True(pageErrors.Count == 0, $"Uncaught page errors on /e2e/charts-native:\n{string.Join('\n', pageErrors)}");
    }

    [Fact]
    public async Task Heatmap_large_data_slider_changes_the_reported_cell_count()
    {
        await Goto("/e2e/charts-native");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var badge = Page.Locator("#heatmap-cell-count");
        await badge.WaitForAsync(new() { Timeout = 30000 });
        var before = await badge.TextContentAsync();

        var slider = Page.Locator("#heatmap-side-slider");
        await slider.WaitForAsync(new() { Timeout = 5000 });
        await slider.FillAsync("40");
        await slider.DispatchEventAsync("input");

        await Assertions.Expect(badge).Not.ToHaveTextAsync(before ?? "", new() { Timeout = 5000 });
    }

    /// <summary>
    /// Regression test for a real hit-testing bug (not just a cosmetic offset):
    /// <c>chart-interop.js</c>'s <c>registerPointerTrack</c> compared a REAL
    /// screen-pixel cursor position against <c>PlotX</c>/<c>PlotWidth</c>, which
    /// are LOGICAL SVG-viewBox units — the two only coincide when a chart
    /// happens to render at exactly its 600-wide viewBox size. Every chart on
    /// this very page renders at "Width=100%" inside a column narrower than
    /// 600px, so this bug resolved the WRONG category for every hover on every
    /// panel here, silently, with no console error — a fully-passing 7000+ test
    /// suite never caught it because bUnit never lays out real pixels. Verified
    /// this concretely predicts the wrong category by reverting the interop
    /// fix locally: hovering dot index 3 ("Apr") then resolved to "Feb".
    /// </summary>
    [Fact]
    public async Task Hovering_a_category_point_resolves_that_same_category_when_rendered_narrower_than_the_viewbox()
    {
        await Goto("/e2e/charts-native");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var chart = Page.Locator("#chart-native-line");
        await chart.ScrollIntoViewIfNeededAsync();

        // Revenue is the first series (dots 0-5 = Jan..Jun); index 3 = "Apr".
        var dot = Page.Locator("#chart-native-line .lumeo-chart-native-dot").Nth(3);
        var box = await dot.BoundingBoxAsync();
        Assert.NotNull(box);
        var (cx, cy) = (box!.X + box.Width / 2, box.Y + box.Height / 2);

        // Move from well outside the plot first so the pointer-track's
        // rAF-throttled resolveIndex reflects the FINAL position, not a stale
        // intermediate one.
        await Page.Mouse.MoveAsync((float)cx - 150, (float)cy - 150);
        await Page.Mouse.MoveAsync((float)cx, (float)cy);

        var announcement = chart.Locator("[aria-live='polite']");
        await Assertions.Expect(announcement).ToContainTextAsync("Apr:", new() { Timeout = 5000 });
    }

    /// <summary>
    /// Regression test for the tooltip position drift the page's own warning
    /// banner used to call out: <c>ChartTooltipHost</c> positions with
    /// <c>position:fixed</c> using coordinates that CartesianChartHost/
    /// XyChartHost previously handed it in LOGICAL viewBox space, not real
    /// screen pixels — drifting from the cursor whenever the rendered SVG size
    /// differs from the viewBox. The Scatter pair is deliberately rendered at
    /// <c>Width="100%"</c> inside a narrow 280px column specifically to
    /// reproduce this. Asserts the tooltip lands within a few pixels of the
    /// documented +14/+14 cursor offset, not merely "somewhere on screen".
    /// </summary>
    [Fact]
    public async Task Tooltip_lands_near_the_cursor_when_the_rendered_svg_differs_from_the_viewbox()
    {
        await Goto("/e2e/charts-native");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var chart = Page.Locator("#chart-native-scatter");
        await chart.ScrollIntoViewIfNeededAsync();

        var point = Page.Locator("#chart-native-scatter .lumeo-chart-native-point").First;
        var box = await point.BoundingBoxAsync();
        Assert.NotNull(box);
        var (cx, cy) = (box!.X + box.Width / 2, box.Y + box.Height / 2);

        await point.HoverAsync();

        var tooltipHost = Page.Locator(".lumeo-chart-tooltip-host").First;
        await tooltipHost.WaitForAsync(new() { Timeout = 5000 });
        var tbox = await tooltipHost.BoundingBoxAsync();
        Assert.NotNull(tbox);

        // Default OffsetX/OffsetY is 14 (ChartTooltipHost) — allow a small
        // tolerance for sub-pixel rounding in the SVG-to-screen conversion,
        // nowhere near the tens-to-thousands of pixels of drift this
        // regressed to pre-fix.
        Assert.InRange(tbox!.X - cx, 8, 20);
        Assert.InRange(tbox.Y - cy, 8, 20);
    }
}
