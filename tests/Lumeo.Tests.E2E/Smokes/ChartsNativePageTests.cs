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
}
