using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Gantt v3 Phase 3, T10 (docs preview page, Part D): the WIP branch-preview
/// page at <c>/e2e/gantt-v3-preview</c> now showcases the T5/T3/T2/T8
/// features the phase added — multi-column tree + splitter (+ the T10a
/// <c>ShowTaskMeta</c> line), summary rollup bars, and the
/// <c>GanttSettingsMenu</c> companion starting in Quarter mode. Sanity-checks
/// that the page actually renders all four sections and no Blazor unhandled
/// exception surfaces, without needing to visit each Gantt v3 docs concept
/// individually — same framing as <c>CatalogPageRendersTests</c>.
/// </summary>
public class GanttV3PreviewPageTests : PlaywrightTestBase
{
    // CI investigation (post-T10, PR #387 e2e failure): the two locator waits
    // below (loop over every heading, and #v3-settings-quarter specifically)
    // timed out at 15000ms in CI but passed reliably (~8-9s) on a fast local
    // dev box, with zero Blazor/console errors either way — ruled out a
    // culture/timezone bug (retested forcing TimezoneId=UTC + default
    // invariant culture, matching Ubuntu CI, still clean) and a logic bug in
    // the new T2/T3/T5 grouping/rollup/Quarter-scale math (GanttRowModel,
    // GanttRollupModel and GanttScale reviewed directly; rollup math floors
    // its weight divisor so it can never hit 0/NaN, Quarter's column math is
    // pure constant arithmetic, no index/range op that could throw). This
    // page is the heaviest in the whole Smoke suite by a wide margin — FIVE
    // live Gantt3/Gantt instances rendered at once (flat, grouped, tree,
    // tree-columns+splitter, quarter+settings-menu over a ~15-month roadmap,
    // plus the v2 reference) — yet it inherited the SAME 15000ms constant
    // every single-instance Smoke page uses, and unlike the other
    // multi-circuit-heavy Gantt spec (GanttParityTestBase/
    // GanttSequentialCollection, added in T4 for the identical class of
    // "correct but resource-contention-timed-out under CI's shared runner"
    // failure) it was never given headroom for its own weight. Reproducing
    // the exact CI timing wasn't possible on this dev machine (even pinning
    // the whole `dotnet test` process to 2-4 CPU cores under full-suite
    // parallel contention only pushed #v3-settings-quarter from ~9s to
    // ~12-13s, short of the 15s cliff — this machine's per-core throughput
    // still outpaces a GitHub Actions runner), but the trend is consistent
    // and unambiguous, so the timeout below is widened rather than the
    // fixture logic changed.
    private const int HeavyPageTimeoutMs = 30000;

    [Fact]
    public async Task Preview_page_loads_and_renders_every_feature_section()
    {
        var response = await Goto("/e2e/gantt-v3-preview");
        Assert.NotNull(response);
        Assert.True(response.Ok, $"GET /e2e/gantt-v3-preview returned HTTP {response.Status}");

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        foreach (var headingId in new[] { "v3-flat", "v3-grouped", "v3-tree", "v3-tree-columns", "v3-summary-bars", "v3-settings-quarter", "v2-reference" })
        {
            await Page.Locator($"#{headingId}").WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });
        }

        // Blazor's standard unhandled-exception banner — hidden by default
        // (display:none), shown on an uncaught render exception. A broken
        // parameter binding on any of the new sections above would surface
        // here even if the specific heading it's under still happened to render.
        var errorUi = Page.Locator("#blazor-error-ui");
        if (await errorUi.CountAsync() > 0)
        {
            await Assertions.Expect(errorUi).ToBeHiddenAsync(new() { Timeout = 2000 });
        }
    }

    [Fact]
    public async Task Preview_page_tree_columns_section_renders_the_progress_column_and_meta_line()
    {
        await Goto("/e2e/gantt-v3-preview");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var section = Page.Locator("#v3-tree-columns").Locator("xpath=following-sibling::*[1]");
        await section.WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });

        await Page.Locator(".lumeo-gantt-v3-tree-header-cell", new() { HasTextString = "Progress" }).First
            .WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });
        await Page.Locator(".lumeo-gantt-v3-tree-meta").First.WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });
    }

    [Fact]
    public async Task Preview_page_settings_quarter_section_opens_the_settings_menu()
    {
        await Goto("/e2e/gantt-v3-preview");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.Locator("#v3-settings-quarter").WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });
        var settingsButtons = Page.Locator("button[aria-label='Settings']");
        await settingsButtons.First.WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });
        await settingsButtons.First.ClickAsync();
        await Page.Locator("label:has-text('Zoom control')").WaitForAsync(new() { Timeout = 5000 });
    }
}
