using System.Globalization;
using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// v3-ONLY: <c>GanttNav</c>'s Today/Previous/Next period controls (feat/gantt-v3,
/// T4). This is NOT a "delta to pin" like the milestone label offset — v2's
/// <c>Gantt.razor</c> has no navigation controls of any kind. It only exposes the
/// Day/Week/Month/Year zoom toolbar (shared with v3, covered by
/// <c>GanttParityTests.Toolbar_view_mode_switch_recomputes_header_identically</c>)
/// and relies on native horizontal scroll plus a ONE-TIME auto-center-on-today at
/// init (gantt-v2.js's <c>tryScroll</c>, lines 679-693) — there is no explicit
/// "shift the visible window" or "recenter on today" affordance to click. GanttNav
/// (<c>Today</c>/prev/next buttons + a windowed <c>VisibleRange</c>) is new v3
/// functionality per the design spec, not a rendering-equivalence gap — so this
/// spec has no v2 counterpart and asserts v3's own behavior against the
/// <c>PeriodLabel</c> text GanttNav renders.
///
/// Month view is used throughout: <c>PeriodLabel</c> for Month mode is exactly
/// <c>range.Start:"MMMM yyyy"</c> (Gantt3.razor's <c>PeriodLabel</c> getter), a
/// single unambiguous "MMMM yyyy" string, and Month's <c>GanttScale</c> step is
/// exactly 1 calendar month — both make the shift assertions exact string/date
/// arithmetic instead of pixel-tolerance comparisons.
/// </summary>
public class GanttV3NavTests : GanttParityTestBase
{
    private static DateTime ParseLabel(string label) =>
        DateTime.ParseExact(label, "MMMM yyyy", CultureInfo.InvariantCulture);

    private ILocator PeriodLabelLocator => Page.Locator("[data-testid='gantt-v3-root'] span.text-sm.font-medium");

    // Unlike v3's pure client-side rendering everywhere else in this harness,
    // a GanttNav button click round-trips through this Blazor SERVER host's
    // real SignalR circuit before the label updates — reading the label
    // immediately after ClickAsync races that round-trip. Poll (via
    // Playwright's auto-retrying Assertions.Expect) until it differs from the
    // pre-click text, then read the settled value.
    private async Task<string> ClickAndAwaitLabelChange(ILocator button, string previousText)
    {
        await button.ClickAsync();
        await Assertions.Expect(PeriodLabelLocator).Not.ToHaveTextAsync(previousText, new() { Timeout = 10000 });
        return (await PeriodLabelLocator.TextContentAsync())!;
    }

    [Fact]
    public async Task Next_and_previous_shift_the_period_label_by_exactly_one_month()
    {
        await GotoHost("/e2e/gantt-v3?viewMode=Month");
        await PeriodLabelLocator.WaitForAsync(new() { Timeout = 15000 });

        var initialText = (await PeriodLabelLocator.TextContentAsync())!;
        var initial = ParseLabel(initialText);

        var next = Page.GetByRole(AriaRole.Button, new() { Name = "Next period" });
        var previous = Page.GetByRole(AriaRole.Button, new() { Name = "Previous period" });

        var afterNextText = await ClickAndAwaitLabelChange(next, initialText);
        var afterNext = ParseLabel(afterNextText);
        Assert.Equal(initial.AddMonths(1), afterNext);

        var afterNext2Text = await ClickAndAwaitLabelChange(next, afterNextText);
        var afterNext2 = ParseLabel(afterNext2Text);
        Assert.Equal(initial.AddMonths(2), afterNext2);

        var afterPrevText = await ClickAndAwaitLabelChange(previous, afterNext2Text);
        var afterPrev2Text = await ClickAndAwaitLabelChange(previous, afterPrevText);
        var backToStart = ParseLabel(afterPrev2Text);
        Assert.Equal(initial, backToStart);
    }

    [Fact]
    public async Task Today_recenters_the_current_window_on_today_preserving_its_width()
    {
        await GotoHost("/e2e/gantt-v3?viewMode=Month");
        await PeriodLabelLocator.WaitForAsync(new() { Timeout = 15000 });

        // Reproduces Gantt3.ComputeInitialRange's Month branch + GanttParityFixtures.SharedTasks'
        // known min/max dates: minDate = fe1.Start (2026-02-23), maxDate = be6.End (2026-04-03)
        // — the LATEST of every task's (IsMilestone ? Start : End), and be6.End postdates
        // fe-ms's milestone Start (2026-03-08). PadBefore/PadAfter = 12 months for Month mode.
        var rangeStart = new DateTime(2026, 2, 1).AddMonths(-12);
        var rangeEnd = new DateTime(2026, 4, 1).AddMonths(12);
        var width = rangeEnd - rangeStart;

        var initialText = (await PeriodLabelLocator.TextContentAsync())!;
        var initial = ParseLabel(initialText);
        Assert.Equal(rangeStart, initial); // sanity check on the reproduced formula before using it below

        var todayButton = Page.GetByRole(AriaRole.Button, new() { Name = "Today", Exact = true });
        var afterTodayText = await ClickAndAwaitLabelChange(todayButton, initialText);

        var expectedStart = DateTime.Today - new TimeSpan(width.Ticks / 2);
        var afterToday = ParseLabel(afterTodayText);
        Assert.Equal(new DateTime(expectedStart.Year, expectedStart.Month, 1), afterToday);
    }
}
