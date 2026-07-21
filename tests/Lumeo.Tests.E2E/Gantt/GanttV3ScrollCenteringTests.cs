using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// v3-ONLY: two scroll-centering regressions from the Codex round 4 review —
/// neither has a v2 counterpart (v2 has no tree pane, and no GanttNav
/// view-mode toolbar of its own — see GanttV3TreeTests'/GanttV3NavTests' own
/// remarks for why those are v3-only surfaces).
/// </summary>
public class GanttV3ScrollCenteringTests : GanttParityTestBase
{
    [Fact]
    public async Task Initial_centering_lands_the_today_marker_mid_viewport_with_the_tree_pane_shown()
    {
        // Bug fix (Codex round 4, P2 #2): EffectiveScrollHost (Gantt3's outer
        // pane) scrolls based on its OWN content's absolute position, which
        // starts with GanttTree's width BEFORE the timeline even begins when
        // a tree pane is shown — but TodayX is computed relative to the
        // TIMELINE's own origin (x=0 at the first date column). Centering on
        // a raw, un-offset TodayX landed the DOM's actual scroll short by
        // exactly the tree's width. See GanttTimeline.ScrollHostLeadingOffset's
        // own remarks for the fix.
        await GotoHost("/e2e/gantt-v3?fixture=today");

        var scrollPane = Page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        await scrollPane.WaitForAsync(new() { Timeout = 15000 });
        await Assertions.Expect(scrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });

        var todayLine = Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-today-line");
        await todayLine.WaitForAsync(new() { Timeout = 15000 });

        var lineBox = await todayLine.BoundingBoxAsync();
        var paneBox = await scrollPane.BoundingBoxAsync();
        Assert.NotNull(lineBox);
        Assert.NotNull(paneBox);

        var paneCenterX = paneBox!.X + paneBox.Width / 2;
        Assert.True(Math.Abs(lineBox!.X - paneCenterX) < 5,
            $"expected the today marker to land within 5px of the pane's horizontal center WITH the tree pane shown, marker.X={lineBox.X}, pane center={paneCenterX}");
    }

    [Fact]
    public async Task Initial_centering_lands_the_today_marker_mid_viewport_without_a_tree_pane()
    {
        // Same assertion, tree pane explicitly disabled (?tree=0 — see
        // GanttV3Page.razor's own remarks) — regression guard: the fix must
        // not OVER-correct and break the no-tree case (ScrollHostLeadingOffset
        // defaults to 0 there).
        await GotoHost("/e2e/gantt-v3?fixture=today&tree=0");

        var scrollPane = Page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        await scrollPane.WaitForAsync(new() { Timeout = 15000 });
        await Assertions.Expect(scrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });

        var todayLine = Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-today-line");
        await todayLine.WaitForAsync(new() { Timeout = 15000 });

        var treeRowCount = await Page.Locator("[data-testid='gantt-v3-root'] [data-row-kind]").CountAsync();
        Assert.Equal(0, treeRowCount); // sanity check ?tree=0 actually suppressed the tree pane

        var lineBox = await todayLine.BoundingBoxAsync();
        var paneBox = await scrollPane.BoundingBoxAsync();
        Assert.NotNull(lineBox);
        Assert.NotNull(paneBox);

        var paneCenterX = paneBox!.X + paneBox.Width / 2;
        Assert.True(Math.Abs(lineBox!.X - paneCenterX) < 5,
            $"expected the today marker to land within 5px of the pane's horizontal center WITHOUT a tree pane, marker.X={lineBox.X}, pane center={paneCenterX}");
    }

    [Fact]
    public async Task Switching_view_mode_keeps_the_previously_centered_date_visible()
    {
        // Bug fix (Codex round 4, P2 #8): a view-mode switch used to reset the
        // viewport to the tasks' own min/max window (ComputeInitialRange) and
        // never even requested a DOM re-scroll at all — whatever the user had
        // navigated to before switching was silently discarded. Gantt3 now
        // captures the CURRENT range's midpoint before switching and recenters
        // the new mode's window around that same date, then requests a scroll
        // (see Gantt3.HandleViewModeChangedAsync's own remarks).
        await GotoHost("/e2e/gantt-v3?viewMode=Day"); // no ?fixture= -> GanttV3Page's default branch -> GanttParityFixtures.SharedTasks()

        var scrollPane = Page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        await scrollPane.WaitForAsync(new() { Timeout = 15000 });
        await Assertions.Expect(scrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });

        // Pick a task bar that's centered in the CURRENT (Day-mode) viewport —
        // its own task ID lets us find "the same task" again after switching
        // to Month mode, to check it's STILL roughly centered rather than
        // wherever Month's own task-derived window happens to place it.
        var centeredBar = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe3']"); // "Integration", mid-range
        await centeredBar.WaitForAsync(new() { Timeout = 15000 });

        var monthToggle = Page.GetByRole(AriaRole.Button, new() { Name = "Month", Exact = true });
        await monthToggle.ClickAsync();

        // The mode switch re-renders the whole canvas at a new column width —
        // wait for the scroll latch to confirm the requested re-center has
        // actually landed before measuring.
        await Assertions.Expect(scrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });

        var barBoxAfter = await centeredBar.BoundingBoxAsync();
        var paneBoxAfter = await scrollPane.BoundingBoxAsync();
        Assert.NotNull(barBoxAfter);
        Assert.NotNull(paneBoxAfter);

        // "Still visible" is the concrete, robust assertion here (a fully
        // reliable EXACT re-center is approximate by design — see
        // Gantt3.VisibleCenterDate's own remarks on why the range's midpoint
        // is a proxy, not a live scroll reading) — the regression this guards
        // against is the bar landing scrolled COMPLETELY out of view, which
        // ToBeInViewportAsync catches directly.
        await Assertions.Expect(centeredBar).ToBeInViewportAsync(new() { Timeout = 5000 });
    }
}
