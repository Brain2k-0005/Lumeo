using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// Gantt v3 Phase 3, T7 — canvas chrome: off-screen indicator chips, the
/// floating zoom control, in-bar label contrast on custom colors, and
/// <c>ColorByGroup</c>. v3-ONLY (v2 has no equivalent for any of these — no
/// parity route to compare against, same framing as
/// <c>GanttV3RowSelectionReorderTests</c>). See <c>GanttV3Phase3T7Tests</c>
/// (bUnit) for the exhaustive component-level coverage this file
/// complements with real-browser proof: genuine <c>position:sticky</c>
/// rendering, a real native 'scroll' event driving the rAF-throttled report,
/// and a real click round-tripping through Blazor Server's SignalR circuit.
/// </summary>
public class GanttV3CanvasChromeTests : GanttParityTestBase
{
    private ILocator ScrollPane => Page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;

    // ── Off-screen indicator chips ────────────────────────────────────────────

    // Forces scrollLeft to 0 and waits for it to STAY 0 across two
    // consecutive checks — gantt-v3.js's own initial-centering recenter
    // (centerOn, fired on navigation) is a fire-and-forget rAF-scheduled
    // JS call that can still be in flight when the "done" latch on the
    // pane fires (that attribute is set atomically WITH the scroll
    // assignment, but a STALE queued rAF from an earlier attempt can still
    // land after it — confirmed live: a single blind delay after forcing
    // scrollLeft=0 was NOT sufficient, the forced value was intermittently
    // overwritten a moment later, flipping the SAME spec between "after"
    // and "before" across otherwise-identical runs). Retrying until the
    // value is observed unchanged twice in a row is a deterministic
    // stability check, not a fixed guess at how long the race can take.
    private async Task ForceScrollLeftZeroAndWaitStableAsync()
    {
        double? lastSeen = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await ScrollPane.EvaluateAsync("el => { el.scrollLeft = 0; el.dispatchEvent(new Event('scroll')); }");
            await Page.WaitForTimeoutAsync(150);
            var current = await ScrollPane.EvaluateAsync<double>("el => el.scrollLeft");
            if (current == 0 && lastSeen == 0) return;
            lastSeen = current;
        }
        Assert.Fail("scrollLeft never stabilized at 0 — the initial-centering recenter never settled within the retry budget.");
    }

    [Fact]
    public async Task Scrolling_Away_Reveals_An_Offscreen_Chip_And_Clicking_It_Scrolls_The_Bar_Back_Into_View()
    {
        // SharedTasks' own dates (Feb 23 - Apr 3 2026, Day mode) span far more
        // pixels than the default viewport — forcing scrollLeft to 0 (the
        // EARLIEST rendered date) reliably pushes every task off the trailing
        // edge (all 12 render an "after" chip at scrollLeft=0). Scoped to ONE
        // specific task ("be6"/"Support Handoff", the LAST Ops task — ending
        // Apr 3, the fixture's own latest date) via its own aria-label (the
        // chip has no visible text, only an SVG icon) rather than asserting
        // on ".First": scrolling one bar into view only guarantees THAT bar's
        // OWN chip disappears, not every other task's (each has its own,
        // independent scroll target).
        await GotoHost("/e2e/gantt-v3?tree=0");
        await Assertions.Expect(ScrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });
        await ForceScrollLeftZeroAndWaitStableAsync();

        var chip = Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("^Scroll to Support Handoff") });
        await chip.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await Assertions.Expect(chip).ToHaveAttributeAsync("data-gantt-offscreen-chip", "after");

        // Real bug found via this exact assertion (same root cause as the
        // floating zoom control's own — see GanttTimeline.razor's remarks):
        // an "after" chip's sticky-end positioning only engages when its
        // OWN unstuck flow position already sits near the row's end —
        // without the fix its bounding box lands near the row's START
        // (this pane's own LEFT edge), not the visible viewport's end.
        var chipBox = await chip.BoundingBoxAsync();
        var paneBoxForChip = await ScrollPane.BoundingBoxAsync();
        Assert.NotNull(chipBox);
        Assert.NotNull(paneBoxForChip);
        Assert.True(chipBox!.X > paneBoxForChip!.X + paneBoxForChip.Width / 2,
            $"expected the 'after' chip near the pane's END edge, got X={chipBox.X} (pane spans [{paneBoxForChip.X}, {paneBoxForChip.X + paneBoxForChip.Width}])");

        var scrollLeftBefore = await ScrollPane.EvaluateAsync<double>("el => el.scrollLeft");

        await chip.ClickAsync();

        // The click routes through GanttV3ScrollToXAsync (a real interop call,
        // fire-and-forget on its own internal rAF — see
        // GanttParityVisualTests' identical "wait past the async scroll"
        // remarks) — poll for the scroll position to actually move, then
        // confirm THIS chip is gone (its own bar is now at least partly
        // visible).
        await Assertions.Expect(chip).ToHaveCountAsync(0, new() { Timeout = 10000 });
        var scrollLeftAfter = await ScrollPane.EvaluateAsync<double>("el => el.scrollLeft");
        Assert.True(scrollLeftAfter > scrollLeftBefore,
            $"expected the chip click to scroll the pane forward, before={scrollLeftBefore}, after={scrollLeftAfter}");
    }

    [Fact]
    public async Task ShowOffscreenIndicators_False_Renders_No_Chip_Even_When_Scrolled_Away()
    {
        await GotoHost("/e2e/gantt-v3?tree=0&showOffscreenIndicators=0");
        await Assertions.Expect(ScrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });

        await ScrollPane.EvaluateAsync("el => { el.scrollLeft = 0; el.dispatchEvent(new Event('scroll')); }");
        // Give the (would-be) rAF-throttled report a real frame to land.
        await Page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");

        var chips = Page.Locator("[data-testid='gantt-v3-root'] [data-gantt-offscreen-chip]");
        await Assertions.Expect(chips).ToHaveCountAsync(0);
    }

    // Same stabilization reasoning as ForceScrollLeftZeroAndWaitStableAsync
    // above, generalized to the OTHER extreme: pinning scrollLeft to
    // scrollWidth (the browser clamps this to the real max automatically,
    // same idiom GanttV3ArrowVirtualizationTests already uses for
    // `el.scrollTop = el.scrollHeight`) so every early task's own bar ends up
    // BEFORE the visible window, producing "before" chips rather than
    // "after" ones.
    private async Task ForceScrollLeftMaxAndWaitStableAsync()
    {
        double? lastSeen = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await ScrollPane.EvaluateAsync("el => { el.scrollLeft = el.scrollWidth; el.dispatchEvent(new Event('scroll')); }");
            await Page.WaitForTimeoutAsync(150);
            var current = await ScrollPane.EvaluateAsync<double>("el => el.scrollLeft");
            if (current == lastSeen) return;
            lastSeen = current;
        }
        Assert.Fail("scrollLeft never stabilized at the max — the initial-centering recenter never settled within the retry budget.");
    }

    [Fact]
    public async Task A_Before_Offscreen_Chip_Never_Overlaps_The_Tree_Pane()
    {
        // Real bug found via the FULL Gantt E2E suite, NOT this file's own
        // specs (all of which deliberately use ?tree=0): once T7's chips
        // shipped, two PRE-EXISTING T6 specs
        // (GanttV3RowSelectionReorderTests.Row_Drag_Reorders_Siblings_And_
        // Commits_The_New_Order / Row_Drag_Veto_Path_Never_Commits) started
        // failing consistently. Root cause: GanttTree.RootClass pins the
        // tree pane to the scroll pane's own logical-start edge via a plain
        // "sticky start-0 z-20" (see its own remarks) — a "Before" direction
        // chip pinned to "start-2" (GanttTimeline.OffscreenChipClass) lands
        // at the EXACT SAME logical-start edge, same z-20, whenever a tree
        // pane is actually showing. elementFromPoint at a drag grip's own
        // coordinates resolved to the CHIP, not the grip, silently
        // swallowing every row-reorder pointer gesture. Fixed via
        // GanttTimeline.LeadingPaneWidth (see its own remarks) — a
        // direction-agnostic parameter distinct from ScrollHostLeadingOffset
        // (which is physical/LTR-gated and thus wrongly 0 exactly when RTL,
        // the case this fix must NOT be 0) — applied only to "Before"
        // chips via an inline inset-inline-start override.
        //
        // Deliberately WITHOUT ?tree=0: every fixture on this page sets
        // GroupBy, so GanttRowModel.DefaultShowTreePane's own default
        // renders the tree pane unless a route explicitly opts out (see
        // GanttV3Page.razor's own ?tree=0 remarks) — this spec needs the
        // pane actually competing for the same edge.
        await GotoHost("/e2e/gantt-v3");
        await Assertions.Expect(ScrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });

        var treePane = Page.Locator("[data-testid='gantt-v3-tree-pane']");
        await treePane.WaitForAsync(new() { Timeout = 15000 });
        var treeBox = await treePane.BoundingBoxAsync();
        Assert.NotNull(treeBox);

        await ForceScrollLeftMaxAndWaitStableAsync();

        var beforeChip = Page.Locator("[data-testid='gantt-v3-root'] [data-gantt-offscreen-chip='before']").First;
        await beforeChip.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

        // The chip's own LEFT edge must never land to the left of the tree
        // pane's own RIGHT edge — that overlap is exactly what let the chip
        // intercept pointer events meant for the reorder grip underneath it.
        var chipBox = await beforeChip.BoundingBoxAsync();
        Assert.NotNull(chipBox);
        Assert.True(chipBox!.X >= treeBox!.X + treeBox.Width,
            $"expected the 'before' chip clear of the tree pane's own right edge ({treeBox.X + treeBox.Width}), got X={chipBox.X} (tree pane spans [{treeBox.X}, {treeBox.X + treeBox.Width}])");
    }

    // ── Floating zoom control ─────────────────────────────────────────────────

    private ILocator ZoomControl => Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-zoom-control");
    private ILocator PeriodLabelLocator => Page.Locator("[data-testid='gantt-v3-root'] span.text-sm.font-medium");

    [Fact]
    public async Task Zoom_Control_Renders_Pinned_To_The_Bottom_End_Corner_Of_The_Visible_Canvas()
    {
        // Real bug found via this exact assertion (Chromium, live): a bare
        // "sticky bottom-2 end-2" child of a plain flex-start wrapper never
        // actually engages sticky's "hold in place" behavior at all — it
        // renders at its own unstuck top-start flow position instead (see
        // this file's own remarks in GanttTimeline.razor for the full
        // mechanism). Pins the fix: the control's bounding box must land
        // near the scroll pane's OWN bottom-right corner, not its top-left.
        await Page.SetViewportSizeAsync(1400, 760);
        await GotoHost("/e2e/gantt-v3?viewMode=Month&showZoomControl=1&tree=0");
        await ZoomControl.WaitForAsync(new() { Timeout = 15000 });

        var box = await ZoomControl.BoundingBoxAsync();
        var paneBox = await ScrollPane.BoundingBoxAsync();
        Assert.NotNull(box);
        Assert.NotNull(paneBox);

        var expectedRight = paneBox!.X + paneBox.Width;
        var expectedBottom = paneBox.Y + paneBox.Height;
        Assert.True(Math.Abs((box!.X + box.Width) - expectedRight) < 20,
            $"expected the control's right edge near the pane's own right edge ({expectedRight}), got {box.X + box.Width}");
        Assert.True(Math.Abs((box.Y + box.Height) - expectedBottom) < 20,
            $"expected the control's bottom edge near the pane's own bottom edge ({expectedBottom}), got {box.Y + box.Height}");
    }

    [Fact]
    public async Task Clicking_Zoom_In_On_The_Floating_Control_Switches_The_Whole_Chart_To_A_Finer_Mode()
    {
        await GotoHost("/e2e/gantt-v3?viewMode=Month&showZoomControl=1&tree=0");
        await PeriodLabelLocator.WaitForAsync(new() { Timeout = 15000 });
        var monthLabel = (await PeriodLabelLocator.TextContentAsync())!;
        Assert.DoesNotContain(" – ", monthLabel); // Month's own "MMMM yyyy" format has no en-dash range

        var zoomIn = ZoomControl.Locator("button").Nth(1); // "-" then "+", see GanttZoomControl.razor
        await zoomIn.ClickAsync();

        // Month -> Week: PeriodLabel's default branch ("MMM d, yyyy – MMM d, yyyy")
        // is the only Gantt3.PeriodLabel format containing an en-dash range at
        // day precision — proves the FLOATING control drove a real,
        // chart-wide view-mode switch (the SAME GanttState every other
        // surface reads), not merely its own local state.
        await Assertions.Expect(PeriodLabelLocator).Not.ToHaveTextAsync(monthLabel, new() { Timeout = 10000 });
        var weekLabel = (await PeriodLabelLocator.TextContentAsync())!;
        Assert.Contains(" – ", weekLabel);
    }

    [Fact]
    public async Task Zoom_Out_Button_Is_Disabled_At_The_Coarsest_Level()
    {
        await GotoHost("/e2e/gantt-v3?viewMode=Year&showZoomControl=1&tree=0");
        await ZoomControl.WaitForAsync(new() { Timeout = 15000 });

        var zoomOut = ZoomControl.Locator("button").First;
        await Assertions.Expect(zoomOut).ToBeDisabledAsync();
    }

    [Fact]
    public async Task ShowZoomControl_Defaults_To_Hidden()
    {
        await GotoHost("/e2e/gantt-v3?tree=0");
        await Page.Locator("[data-testid='gantt-v3-root'] [data-task-id]").First.WaitForAsync(new() { Timeout = 15000 });

        await Assertions.Expect(ZoomControl).ToHaveCountAsync(0);
    }

    // ── In-bar label contrast ─────────────────────────────────────────────────

    [Fact]
    public async Task A_Light_Custom_Bar_Color_Renders_The_Label_With_The_Foreground_Token()
    {
        // "fe3" carries GanttParityFixtures.GetBarColor's "#f59e0b" (amber,
        // BT.601 luminance ~0.656 — light) and spans 7 days (266px @ 38px/col
        // in Day mode), comfortably over MinInBarLabelWidth so the label
        // renders INSIDE the bar (not the narrow fallback).
        await GotoHost("/e2e/gantt-v3?tree=0");
        var label = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe3'] .lumeo-gantt-v3-bar-label");
        await label.WaitForAsync(new() { Timeout = 15000 });

        var style = await label.GetAttributeAsync("style") ?? "";
        Assert.Contains("color:var(--color-foreground)", style);
    }

    [Fact]
    public async Task A_Dark_Custom_Bar_Color_Renders_The_Label_With_The_Background_Token()
    {
        // "be1" carries "#22c55e" (green, luminance ~0.535 — dark) and spans
        // 9 days (342px), also comfortably over the narrow-bar threshold.
        await GotoHost("/e2e/gantt-v3?tree=0");
        var label = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='be1'] .lumeo-gantt-v3-bar-label");
        await label.WaitForAsync(new() { Timeout = 15000 });

        var style = await label.GetAttributeAsync("style") ?? "";
        Assert.Contains("color:var(--color-background)", style);
    }

    // ── ColorByGroup ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ColorByGroup_Assigns_The_Same_Colour_To_Every_Task_In_A_Group()
    {
        // Two tasks with no explicit BarColor, both in the "Frontend" group:
        // "fe2" and "fe4" (GetBarColor returns null for both — isolates
        // ColorByGroup's OWN fallback from the fixture's hardcoded fe3/be1
        // custom colors). "fe2" vs "be3" (a DIFFERENT group) is deliberately
        // NOT asserted here as "must differ": with only 5 chart-N slots and
        // just 2 group keys ("Frontend"/"Ops"), a same-slot collision is a
        // real, unremarkable possibility (independently verified: this
        // fixture's own two group keys DO collide onto chart-3) — see
        // GanttColorModelTests' own cross-group-difference coverage, which
        // uses keys ("Design"/"p2") independently confirmed NOT to collide.
        // This spec only proves the guaranteed property: SAME group -> SAME
        // colour, for real.
        await GotoHost("/e2e/gantt-v3?colorByGroup=1&tree=0");
        var fe2Bg = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe2'] .lumeo-gantt-v3-bar-bg");
        var fe4Bg = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe4'] .lumeo-gantt-v3-bar-bg");
        await fe2Bg.WaitForAsync(new() { Timeout = 15000 });

        var fe2Style = await fe2Bg.GetAttributeAsync("style") ?? "";
        var fe4Style = await fe4Bg.GetAttributeAsync("style") ?? "";

        Assert.Contains("--color-chart-", fe2Style);
        Assert.Equal(fe2Style, fe4Style);
    }
}
