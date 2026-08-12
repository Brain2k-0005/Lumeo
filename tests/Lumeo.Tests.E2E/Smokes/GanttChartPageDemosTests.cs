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
/// Perf fix (Gantt lag/lazy-mount investigation): this page used to mount all
/// 14 GanttChart instances EAGERLY on first render — every one of them paid
/// GanttTimeline.OnAfterRenderAsync's own ~5 sequential awaited interop round
/// trips (drag registration, context-menu registration, header-scroll-sync,
/// vertical-scroll-tracking, scroll-to-today) at once, which is what made the
/// LAST demo's first task bar take 16.3s-16.9s to reach the DOM (see the
/// superseded comment this replaces, and the investigation's own measured
/// numbers). Each demo's <c>&lt;GanttChart&gt;</c> is now wrapped in
/// <c>LazyRender</c> (the same IntersectionObserver-based deferred-mount
/// helper already used on every Map demo on <c>/components/map</c>) — a chart
/// only mounts once its section scrolls within 200px of the viewport, so a
/// visitor who never scrolls past the first demo never pays for the other 13
/// at all. This test now scrolls to each section BEFORE asserting its bar is
/// present (Playwright's plain WaitForAsync does not auto-scroll — only
/// action methods like ClickAsync do), matching that new mount trigger; the
/// timeout budget shrinks accordingly since a single already-visible chart's
/// own mount is fast (measured ~1-3s locally), not 16+ seconds for the whole
/// page.
/// </summary>
public class GanttChartPageDemosTests : PlaywrightTestBase
{
    // 30s, not 10s. This page is the single heaviest WASM interop consumer in the
    // whole docs site (14 GanttChart demos, each mount doing GanttTimeline's own
    // ~5 sequential awaited interop round trips once its LazyRender fires) — and
    // CI's actual failure (PR #396 follow-up: all 6 tests here timed out on the
    // very first section's [data-task-id] wait) reproduced ONLY under CPU
    // contention, never in isolation. Confirmed via a CPU-constrained (2-4 vCPU)
    // Linux headless-Chromium repro against this exact page, driving 6-8
    // concurrent browser instances the way xUnit's default parallel-collection
    // execution runs this class alongside every other Playwright test class in
    // the assembly: individual [data-task-id] waits that take ~1-3s uncontended
    // took 7-21s under that contention — well past the old 10s budget, but
    // reliably under 30s even in the worst tested case (2 vCPU / 8 concurrent
    // browsers). This is the same class of CI-only flake already fixed once on
    // this branch by widening a different test's timeout budget (see this
    // branch's "widen infinite-scroll's extension-commit retry budget for CI"
    // commit) — not a product bug, and not something a real LazyRender
    // regression could hide behind: a genuinely broken mount still never
    // completes and still fails at 30s (verified by temporarily disabling
    // LazyRender's mount trigger — every assertion here still failed, just at
    // the new ceiling instead of the old one).
    private const int HeavyPageTimeoutMs = 30000;

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

            // LazyRender only mounts a demo's GanttChart once its placeholder
            // scrolls within 200px of the viewport (see the class remarks) —
            // a plain WaitForAsync never scrolls anything, so without this the
            // loop would time out on every section past the first screenful.
            await section.ScrollIntoViewIfNeededAsync();

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
        await section.ScrollIntoViewIfNeededAsync(); // LazyRender mount trigger — see class remarks

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
        await section.ScrollIntoViewIfNeededAsync(); // LazyRender mount trigger — see class remarks

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
        await section.ScrollIntoViewIfNeededAsync(); // LazyRender mount trigger — see class remarks

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
        await section.ScrollIntoViewIfNeededAsync(); // LazyRender mount trigger — see class remarks

        // NowIndicator's precise time-line only renders at sub-day granularity —
        // this demo deliberately starts in HalfDay zoom so it's visible without
        // any interaction (see the demo's own caption/code comment).
        await section.Locator(".lumeo-gantt-v3-now-line").WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });
        await section.Locator(".lumeo-gantt-v3-off-day").First.WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });
    }

    // Regression guard for the lazy-mount perf fix itself. Predicted values:
    // against the PRE-FIX code (every GanttChart mounted eagerly on first
    // render, which is what produced the 16.3s-16.9s page-ready time this
    // fix addresses), the last demo's [data-task-id] count would already be
    // > 0 immediately after load, before any scroll — Virtualize renders an
    // initial batch for every mounted instance regardless of scroll position,
    // and ALL 14 instances mounted immediately. Against the FIXED code, that
    // same count must be EXACTLY 0 before scrolling (LazyRender is still
    // showing its skeleton placeholder, nothing has mounted below the fold
    // yet) and only becomes > 0 once the section is scrolled into view. A
    // fixture bug that left the demo eagerly mounted (LazyRender never
    // applied, or its IntersectionObserver never wired up) would make the
    // "before" assertion fail with a concrete wrong value (count > 0, not 0).
    [Fact]
    public async Task Below_The_Fold_Demo_Charts_Do_Not_Mount_Until_Scrolled_Near()
    {
        await Goto("/components/gantt-chart");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The LAST demo is far below the fold on any normal viewport height —
        // check it FIRST, before anything else in this test scrolls the page
        // (a scroll anywhere is exactly the trigger being asserted against).
        var lastSection = Page.Locator("#custom-row-content-rowtemplate");
        await lastSection.WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });
        var barsBeforeScroll = await lastSection.Locator("[data-task-id]").CountAsync();
        Assert.Equal(0, barsBeforeScroll);

        // Control: prove this test isn't just catching "nothing ever mounts" —
        // the FIRST demo mounts once actually scrolled to, same as every other
        // section (PageHeader/InstallUsage/Alert/"When to Use" above it are
        // tall enough that even the first demo sits outside a typical test
        // viewport's initial 200px LazyRender margin without an explicit
        // scroll — see the passing Page_loads_and_renders_every_demo_section
        // test's own identical per-section ScrollIntoViewIfNeededAsync call).
        var firstSection = Page.Locator("#a-realistic-project-plan-hierarchy-dependencies-milestones");
        await firstSection.ScrollIntoViewIfNeededAsync();
        await firstSection.Locator("[data-task-id]").First.WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });

        await lastSection.ScrollIntoViewIfNeededAsync();
        await lastSection.Locator("[data-task-id]").First.WaitForAsync(new() { Timeout = HeavyPageTimeoutMs });
        var barsAfterScroll = await lastSection.Locator("[data-task-id]").CountAsync();
        Assert.True(barsAfterScroll > 0, "expected the last demo's GanttChart to mount once scrolled into view");
    }
}
