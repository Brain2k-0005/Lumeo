using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// v3-ONLY: RTL scroll-normalization regression coverage (Codex round 3, P2 #7).
/// v2 has no RTL-aware scroll handling to compare against (gantt-v2.js's
/// tryScroll assumes an LTR scrollLeft convention outright), so there is no v2
/// parity baseline here — this asserts v3's own behavior against Lumeo's
/// standard RTL idiom, <c>DirectionProvider</c> (see <c>GanttV3Page.razor</c>'s
/// <c>?rtl=1</c> handling).
///
/// GanttTimeline/GanttScale never mirror the actual date-column layout for RTL
/// (a timeline's date order reading left-to-right is independent of the page's
/// text direction — see gantt-v3.js's own remarks on this), so the ONLY thing
/// that needs to work correctly under <c>dir="rtl"</c> is the scroll-to-today
/// centering: without the JS-side scrollLeft convention normalization, the
/// computed target either silently clamps to 0 or lands in the wrong place,
/// leaving the today marker outside the initial viewport.
/// </summary>
public class GanttV3RtlTests : GanttParityTestBase
{
    [Fact]
    public async Task Initial_centering_lands_the_today_marker_in_viewport_under_rtl()
    {
        await GotoHost("/e2e/gantt-v3?fixture=today&rtl=1");

        var scrollPane = Page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        await scrollPane.WaitForAsync(new() { Timeout = 15000 });

        // Sanity check the page actually rendered under RTL — a page that
        // silently fell back to LTR would trivially pass everything below
        // without proving the normalization does anything.
        var direction = await scrollPane.EvaluateAsync<string>("el => getComputedStyle(el).direction");
        Assert.Equal("rtl", direction);

        var todayLine = Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-today-line");
        await todayLine.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 15000 });

        // Same completion latch every other scroll-to-today spec uses (gantt-v3.js's
        // centerOn stamps this atomically with the scroll it performs) — waits out
        // the fire-and-forget interop race rather than a blind delay.
        await Assertions.Expect(scrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });

        await Assertions.Expect(todayLine).ToBeInViewportAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task Lower_header_date_labels_keep_physical_earliest_to_latest_order_under_rtl()
    {
        // Bug fix (Codex round 4, P2 #9): the lower-header row is the ONLY
        // part of the header using `display:flex` for layout (upper-run
        // labels, grid lines, and bars all use `position:absolute; left:Xpx`,
        // which is direction-agnostic) — a flex container's default
        // `flex-direction:row` reverses child ORDER under an inherited
        // `dir="rtl"`, so this row alone would visually flip to latest-first
        // while everything else stayed physically earliest-first, misaligning
        // every column. Fixed via `dir="ltr"` forcing this one container's
        // layout back to physical (DOM) order regardless of page direction.
        //
        // Asserted via DOM-order-vs-physical-order comparison rather than
        // against a specific scroll position: under RTL, the native
        // scrollLeft=0 position is the RTL START (physically the far right of
        // the whole un-mirrored, much-wider-than-viewport canvas — NOT "the
        // earliest rendered date"), so there is no simple "reset to the
        // logical start" scroll value to assert against directly here; two
        // ADJACENT labels' relative physical order is scroll-position-
        // independent and is exactly what the bug actually broke.
        await GotoHost("/e2e/gantt-v3?fixture=tall&viewMode=Day&rtl=1");

        var scrollPane = Page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        await scrollPane.WaitForAsync(new() { Timeout = 15000 });

        var labels = Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-header .flex > div");
        var firstBox = await labels.Nth(0).BoundingBoxAsync();
        var secondBox = await labels.Nth(1).BoundingBoxAsync();
        Assert.NotNull(firstBox);
        Assert.NotNull(secondBox);

        Assert.True(firstBox!.X < secondBox!.X,
            $"expected the DOM-earlier (earlier-date) header label to sit physically LEFT of the DOM-later one under RTL, got label[0].X={firstBox.X}, label[1].X={secondBox.X}");
    }
}
