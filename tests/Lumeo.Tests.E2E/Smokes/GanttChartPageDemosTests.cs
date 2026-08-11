using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// PR #396 (Gantt v3 promotion) follow-up: the docs page at
/// <c>/components/gantt-chart</c> (also served at <c>/components/gantt</c>)
/// went from a thin, mostly-single-feature demo set to 14 live GanttChart
/// instances covering dependency arrows, milestones, the OnTaskUpdate commit
/// gate (reject + accept-with-adjustment), progress drag, all 7 zoom levels +
/// the floating zoom control, grouping, ParentId hierarchy with a resizable
/// multi-column tree pane, summary rollups, the GanttSettingsMenu companion,
/// read-only mode, per-task/per-group bar colour, toolbar trailing content,
/// the sub-day NowIndicator + MarkOffDays shading, leaf-row checkbox
/// selection, tree-row drag reorder, and a custom RowTemplate.
///
/// Same rationale as <see cref="GanttV3PreviewPageTests"/>: a Blazor render
/// exception kills everything below it in the component tree, so a page that
/// half-renders can still look fine at a glance — this asserts EVERY section
/// actually reaches the DOM, not just that the page responded.
///
/// Timeout budget: this page hosts 14 live GanttChart instances (vs. 5 on the
/// heaviest page in the rest of this suite, <c>/e2e/gantt-v3-preview</c>,
/// which needed its own 30000ms bump over the usual 15000ms). Measured
/// locally (warm dev server, 3 runs): the LAST demo's first task bar reaching
/// the DOM took 16.3s-16.9s end to end. Scaling by the same
/// local-to-CI headroom that page's own comment documents (~2.5-3x), this
/// uses 45000ms.
/// </summary>
public class GanttChartPageDemosTests : PlaywrightTestBase
{
    private const int HeavyPageTimeoutMs = 45000;

    private static readonly string[] DemoSectionIds =
    [
        "a-realistic-project-plan-hierarchy-dependencies-milestones",
        "drag-to-reschedule-with-a-real-commit-gate",
        "progress-drag",
        "view-modes-zoom-control",
        "grouping-swim-lanes",
        "hierarchy-tree-pane-multi-column-resizable-splitter",
        "settings-menu-every-reui-parity-toggle-two-way-bound",
        "read-only",
        "per-task-bar-colour",
        "toolbar-trailing-content",
        "today-marker-off-day-shading",
        "row-selection-leaf-checkboxes",
        "row-reorder-drag-tree-rows",
        "custom-row-content-rowtemplate",
    ];

    [Fact]
    public async Task Page_loads_and_renders_every_demo_section_with_a_live_chart()
    {
        var response = await Goto("/components/gantt-chart");
        Assert.NotNull(response);
        Assert.True(response.Ok, $"GET /components/gantt-chart returned HTTP {response.Status}");

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        foreach (var id in DemoSectionIds)
        {
            var section = Page.Locator($"#{id}");
            await section.WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });

            // Per-section presence, not just that the page responded: assert THIS
            // section's own GanttChart actually rendered at least one task bar
            // (a render exception on an earlier section would otherwise still
            // leave this heading in the DOM while everything below it is empty).
            var bar = section.Locator("[data-task-id]").First;
            await bar.WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });
        }

        // Blazor's standard unhandled-exception banner — hidden by default.
        var errorUi = Page.Locator("#blazor-error-ui");
        if (await errorUi.CountAsync() > 0)
        {
            await Assertions.Expect(errorUi).ToBeHiddenAsync(new() { Timeout = 2000 });
        }
    }

    [Fact]
    public async Task Row_reorder_demo_shows_a_tree_pane_with_drag_handles()
    {
        // Regression guard: this demo's dataset has no ParentId/GroupBy, which
        // otherwise resolves ShowTreePane to OFF by default — and AllowRowReorder
        // is a tree-pane-row gesture, so without ShowTreePane="true" explicitly
        // set, the demo would render bars with no way to actually reorder them.
        await Goto("/components/gantt-chart");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var section = Page.Locator("#row-reorder-drag-tree-rows");
        await section.WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });

        var nameCells = section.Locator(".lumeo-gantt-v3-tree-name-cell");
        await nameCells.First.WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });
        Assert.Equal(5, await nameCells.CountAsync());
    }

    [Fact]
    public async Task Row_selection_demo_updates_the_selected_count_on_checkbox_click()
    {
        await Goto("/components/gantt-chart");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var section = Page.Locator("#row-selection-leaf-checkboxes");
        await section.WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });

        var nothingSelected = section.GetByText("Nothing selected.");
        await nothingSelected.WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });

        // "API" is a leaf task (no children) in this demo's hierarchy fixture —
        // its checkbox is a real SelectedIds member, unlike a parent's. Lumeo's
        // Checkbox is a shadcn-style role="checkbox" <button>, not a native
        // <input type="checkbox">.
        var apiCheckbox = section.Locator(".lumeo-gantt-v3-tree-name-cell", new() { HasTextString = "API" })
            .Locator("button[role='checkbox']");
        await apiCheckbox.ClickAsync(new() { Timeout = HeavyPageTimeoutMs });

        await Assertions.Expect(section.GetByText("1 task(s) selected")).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Fact]
    public async Task Settings_menu_demo_opens_the_settings_popover()
    {
        await Goto("/components/gantt-chart");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var section = Page.Locator("#settings-menu-every-reui-parity-toggle-two-way-bound");
        await section.WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });

        var settingsButton = section.Locator("button[aria-label='Settings']");
        await settingsButton.WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });
        await settingsButton.ClickAsync();
        await Page.Locator("label:has-text('Zoom control')").WaitForAsync(new() { Timeout = 5000 });
    }

    [Fact]
    public async Task Today_marker_demo_shows_the_now_line_and_off_day_shading()
    {
        await Goto("/components/gantt-chart");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var section = Page.Locator("#today-marker-off-day-shading");
        await section.WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });

        // NowIndicator's precise time-line only renders at sub-day granularity —
        // this demo deliberately starts in HalfDay zoom so it's visible without
        // any interaction (see the demo's own caption/code comment).
        await section.Locator(".lumeo-gantt-v3-now-line").WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });
        await section.Locator(".lumeo-gantt-v3-off-day").First.WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });
    }
}
