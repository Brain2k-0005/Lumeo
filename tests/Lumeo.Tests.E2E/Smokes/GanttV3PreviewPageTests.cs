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
    [Fact]
    public async Task Preview_page_loads_and_renders_every_feature_section()
    {
        var response = await Goto("/e2e/gantt-v3-preview");
        Assert.NotNull(response);
        Assert.True(response.Ok, $"GET /e2e/gantt-v3-preview returned HTTP {response.Status}");

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        foreach (var headingId in new[] { "v3-flat", "v3-grouped", "v3-tree", "v3-tree-columns", "v3-summary-bars", "v3-settings-quarter", "v2-reference" })
        {
            await Page.Locator($"#{headingId}").WaitForAsync(new() { Timeout = 15000 });
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
        await section.WaitForAsync(new() { Timeout = 15000 });

        await Page.Locator(".lumeo-gantt-v3-tree-header-cell", new() { HasTextString = "Progress" }).First
            .WaitForAsync(new() { Timeout = 15000 });
        await Page.Locator(".lumeo-gantt-v3-tree-meta").First.WaitForAsync(new() { Timeout = 15000 });
    }

    [Fact]
    public async Task Preview_page_settings_quarter_section_opens_the_settings_menu()
    {
        await Goto("/e2e/gantt-v3-preview");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.Locator("#v3-settings-quarter").WaitForAsync(new() { Timeout = 15000 });
        var settingsButtons = Page.Locator("button[aria-label='Settings']");
        await settingsButtons.First.WaitForAsync(new() { Timeout = 15000 });
        await settingsButtons.First.ClickAsync();
        await Page.Locator("label:has-text('Zoom control')").WaitForAsync(new() { Timeout = 5000 });
    }
}
