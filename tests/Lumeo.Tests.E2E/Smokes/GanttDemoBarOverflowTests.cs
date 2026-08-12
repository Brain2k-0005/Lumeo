using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// The legacy docs "Basic project plan" Gantt demo
/// (<c>/components/gantt-legacy</c> — the OLD <c>&lt;Gantt&gt;</c> component,
/// gantt-v2.js) renders 5 dependent tasks spanning 32 days. Gantt's initial
/// scroll centers "today" in the viewport (gantt-v2.js's tryScroll) — fine
/// for a real project with history on both sides, but this demo's data
/// starts EXACTLY at "today" (day 0, no history), so centering wasted half the
/// visible width on empty left-padding and cut the second bar ("Design") off
/// mid-label with no visible scroll affordance (Chromium renders an overlay
/// scrollbar here — invisible until hovered).
///
/// Fixed at the DOCS level (not gantt-v2.js's shared scroll/padding behavior,
/// which real drag-to-reschedule consumers rely on, and which
/// tests/Lumeo.Tests.E2E/Visual/GanttParityVisualTests.cs pins per-pixel
/// baselines against): <c>ColumnWidth="16"</c> on just this demo compresses
/// the timeline enough that the centered viewport comfortably covers both
/// "Research" and "Design".
///
/// RETARGETED (PR #396, "promote GanttChart to public API"): <c>/components/gantt</c>
/// now serves the new <c>GanttChart</c> (gantt-v3.js) — the old <c>&lt;Gantt&gt;</c>
/// component's page moved to <c>/components/gantt-legacy</c>. This test now points at
/// that legacy route; the DOM this test asserts on (<c>.lumeo-gantt-host</c>,
/// <c>g[data-task-id]</c>, <c>rect.lumeo-gantt-bar-bg</c>) is gantt-v2.js's SVG output
/// and doesn't even exist on the new page — GanttChart renders its bars as plain
/// <c>div</c>s (<c>lumeo-gantt-v3-bar-bg</c>), not SVG <c>rect</c>s, inside an unclassed
/// scroll pane. The new page's own flagship demo also deliberately avoids the root
/// cause: its dataset spans day -10 to +30 relative to "today" (see
/// GanttChartPage.razor's <c>BuildBasicTasks</c> comment), not the narrow
/// starts-exactly-at-today range that triggered the clip on gantt-v2. GanttChart is not
/// covered by this test — if it needs the same defect coverage in the future, that's a
/// new test against its own DOM contract, not a repurposing of this one.
///
/// Requires the docs dev-server. See project README.md.
/// </summary>
public class GanttDemoBarOverflowTests : PlaywrightTestBase
{
    private const string OverflowScript =
        "() => { const sec = document.querySelectorAll('section[data-toc-entry]')[0]; " +
        "const host = sec.querySelector('.lumeo-gantt-host'); " +
        "const hostRect = host.getBoundingClientRect(); " +
        "const visibleRight = hostRect.left + host.clientWidth; " +
        "const designG = sec.querySelector('g[data-task-id=\"design\"]'); " +
        "const bar = designG.querySelector('rect.lumeo-gantt-bar-bg') || designG.querySelector('rect'); " +
        "const barRect = bar.getBoundingClientRect(); " +
        "return { visibleRight, barRight: barRect.right, barLeft: barRect.left }; }";

    [Fact]
    public async Task Design_bar_fits_inside_the_visible_gantt_viewport_on_first_paint()
    {
        await Goto("/components/gantt-legacy");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.WaitForSelectorAsync(
            "section g[data-task-id='design']",
            new() { State = WaitForSelectorState.Attached, Timeout = 20000 });
        // Let the JS renderer's requestAnimationFrame-scheduled initial
        // scroll-to-today land (gantt-v2.js's tryScroll).
        await Page.WaitForTimeoutAsync(400);

        var result = await Page.EvaluateAsync<OverflowResult>(OverflowScript);
        var overflowPx = result.BarRight - result.VisibleRight;

        // Predicted-vs-actual (measured live against master, default 1280x720
        // Playwright viewport — the SAME dimensions PlaywrightTestBase's
        // NewPageAsync() uses here):
        //   BEFORE the ColumnWidth fix: Design's bar right edge sat ~260px past
        //   the host's visible right edge (measured: barRight=1262,
        //   visibleRight=1002 -> overflowPx=260).
        //   AFTER the fix (ColumnWidth=16): Design's bar fits with margin to
        //   spare (measured: barRight=976, visibleRight=1002 -> overflowPx=-26).
        // A disable-check (reverting GanttPage.razor's ColumnWidth="16" demo
        // override) reproduces the ~260px overflow and fails this assertion.
        Assert.True(overflowPx <= 0,
            $"Expected the 'Design' bar to fit inside the visible Gantt viewport, but its right edge " +
            $"({result.BarRight:F1}px) overflows the host's visible right edge ({result.VisibleRight:F1}px) " +
            $"by {overflowPx:F1}px.");

        // Sanity: the bar must still actually be ON screen (not scrolled fully
        // out of view in the other direction), i.e. this isn't trivially
        // "passing" because nothing renders.
        Assert.True(result.BarLeft < result.VisibleRight,
            "Expected the 'Design' bar to be at least partially within the visible viewport.");
    }

    private sealed class OverflowResult
    {
        public double VisibleRight { get; set; }
        public double BarRight { get; set; }
        public double BarLeft { get; set; }
    }
}
