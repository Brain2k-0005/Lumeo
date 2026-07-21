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
}
