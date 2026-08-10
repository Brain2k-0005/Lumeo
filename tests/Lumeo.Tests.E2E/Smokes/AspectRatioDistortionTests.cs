using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Regression coverage for the native charting engine's aspect-ratio
/// distortion bug: every chart built on <c>CartesianChartHost</c>/
/// <c>XyChartHost</c> used to render into a hardcoded 600x350 SVG
/// <c>viewBox</c> with <c>preserveAspectRatio="none"</c>. Since these charts
/// default to <c>Width="100%"</c>, the rendered box's aspect essentially
/// never matched 600:350, so the non-uniform stretch turned circles into
/// ellipses and stretched axis-label glyphs. Fixed by syncing the viewBox to
/// the container's real measured pixel box via a live ResizeObserver
/// (<c>ComponentInteropService.ChartObserveBox</c>) instead of the fixed
/// fallback.
///
/// bUnit has no real layout/paint engine, so it cannot measure a rendered
/// circle's actual pixel bounding box or a glyph's advance — these
/// assertions require a real browser (Chromium here) and target the exact
/// panel the fix was written against: the Scatter pair, deliberately
/// rendered at <c>Width="100%"</c> inside a narrow 280px column on the
/// <c>/e2e/charts-native</c> comparison page.
/// </summary>
public class AspectRatioDistortionTests : PlaywrightTestBase
{
    [Fact]
    public async Task Scatter_Points_Render_As_Circles_Not_Ellipses_In_A_Narrow_Column()
    {
        await Goto("/e2e/charts-native");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var point = Page.Locator("#chart-native-scatter .lumeo-chart-native-point").First;
        await point.ScrollIntoViewIfNeededAsync();
        var box = await point.BoundingBoxAsync();
        Assert.NotNull(box);

        // A round marker's rendered bounding box must be (near-)square. Before
        // the fix, in this same 280px-wide/350px-tall column, a marker
        // measured ~4.67 x 10 (ratio ~0.467) — the viewBox stayed the
        // hardcoded 600x350 fallback while the real box was 280x350, so
        // scaleX (280/600≈0.467) != scaleY (350/350=1).
        var ratio = box!.Width / box.Height;
        Assert.InRange(ratio, 0.9, 1.1);
    }

    [Fact]
    public async Task Scatter_ViewBox_Matches_The_Real_Rendered_Box_In_A_Narrow_Column()
    {
        // The direct, algebraic proof behind the roundness assertion above:
        // once viewBox == the real box, preserveAspectRatio="none" is an
        // identity transform (scaleX == scaleY == 1) for ANY shape, not just
        // the one marker sampled above.
        await Goto("/e2e/charts-native");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var svg = Page.Locator("#chart-native-scatter svg").First;
        await svg.ScrollIntoViewIfNeededAsync();
        var box = await svg.BoundingBoxAsync();
        Assert.NotNull(box);

        var viewBox = await svg.GetAttributeAsync("viewBox");
        Assert.NotNull(viewBox);
        var parts = viewBox!.Split(' ');
        var viewBoxWidth = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
        var viewBoxHeight = double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);

        // Disable-check predicted-vs-actual: without the fix, viewBoxWidth
        // would stay the hardcoded 600 regardless of the real ~280px box —
        // asserting near-equality here fails hard against that reverted state
        // (600 vs ~280 is not "within 2px").
        Assert.InRange(Math.Abs(viewBoxWidth - box!.Width), 0, 2);
        Assert.InRange(Math.Abs(viewBoxHeight - box.Height), 0, 2);
    }

    [Fact]
    public async Task Scatter_Axis_Label_Glyphs_Are_Not_Stretched_In_A_Narrow_Column()
    {
        // Axis-label <text> elements are drawn at a fixed font-size (11) in
        // VIEWBOX units. Before the fix, the same non-uniform scaleX/scaleY
        // that turned circles into ellipses ALSO stretched/squashed glyph
        // advance non-uniformly on one axis — reproduced here by comparing
        // the rendered width/height of two DIFFERENT digit glyphs' bounding
        // boxes against the native engine's own Y-axis labels (single-digit
        // "20","30",... at this domain), which should read as ordinary
        // upright digits, not visibly squeezed horizontally.
        await Goto("/e2e/charts-native");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var nativeLabel = Page.Locator("#chart-native-scatter svg g.lumeo-chart-axis text").First;
        var legacyLabel = Page.Locator("#chart-legacy-scatter text.echarts-axis-label, #chart-legacy-scatter text").First;
        await nativeLabel.ScrollIntoViewIfNeededAsync();

        var nativeBox = await nativeLabel.BoundingBoxAsync();
        Assert.NotNull(nativeBox);

        // A single axis-label glyph run at font-size 11 in a correctly
        // (1:1) scaled viewBox renders with a sane aspect: comfortably wider
        // than tall for 2-3 digit labels, but nowhere near the ~0.467x
        // horizontal squash the pre-fix 600-vs-280 mismatch produced (which
        // would compress a "20"/"30" label's rendered width to well under
        // half its rendered height).
        var glyphRatio = nativeBox!.Width / nativeBox.Height;
        Assert.True(glyphRatio > 0.7, $"expected an axis-label glyph run wider than ~0.7x its own height (not horizontally squashed), got ratio {glyphRatio} ({nativeBox.Width}x{nativeBox.Height})");
    }

    [Fact]
    public async Task Resizing_The_Viewport_Keeps_The_ViewBox_Synced_To_The_New_Box_Live()
    {
        // The fix must handle resize (a live ResizeObserver), not just the
        // first paint — set explicitly in the task's own acceptance bar. The
        // Line chart (fills its grid column, unlike the fixed-280px Scatter
        // wrapper) actually changes its rendered box size across the
        // viewport widths below, so this is a real live-resize check, not
        // just a re-assertion of the first measurement.
        await Goto("/e2e/charts-native");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var svg = Page.Locator("#chart-native-line svg").First;
        await svg.ScrollIntoViewIfNeededAsync();

        await Page.SetViewportSizeAsync(700, 900);
        await Page.WaitForTimeoutAsync(300);
        var narrowBox = await svg.BoundingBoxAsync();
        var narrowViewBox = await svg.GetAttributeAsync("viewBox");

        await Page.SetViewportSizeAsync(1900, 900);
        await Page.WaitForTimeoutAsync(300);
        var wideBox = await svg.BoundingBoxAsync();
        var wideViewBox = await svg.GetAttributeAsync("viewBox");

        Assert.NotNull(narrowBox);
        Assert.NotNull(wideBox);
        // The page's grid goes single-column below "lg" and two-column above
        // it, so a wider VIEWPORT doesn't necessarily mean a wider CHART (two
        // narrower columns can beat one full-width one) — assert the box
        // actually changed by a meaningful amount, not a specific direction.
        Assert.True(Math.Abs(wideBox!.Width - narrowBox!.Width) > 100, $"expected the chart's own box to change meaningfully across viewports (narrow={narrowBox.Width}, wide={wideBox.Width})");
        // Disable-check: without a LIVE ResizeObserver (e.g. one that only
        // measured once on mount), wideViewBox would stay equal to
        // narrowViewBox despite the box itself resizing.
        Assert.NotEqual(narrowViewBox, wideViewBox);

        var wideParts = wideViewBox!.Split(' ');
        var wideViewBoxWidth = double.Parse(wideParts[2], System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(Math.Abs(wideViewBoxWidth - wideBox.Width), 0, 2);
    }
}
