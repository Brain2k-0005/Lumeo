using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Scheduler;

/// <summary>
/// Real-browser coverage for the audit's non-drag pointer/DOM findings against
/// <c>/e2e/scheduler-views-preview</c> — the "+N more" popover, the resize-handle visual
/// affordance, and business-hours shading. Per the rigor standard: these are pointer/DOM work
/// (popover positioning, hover-driven visibility, computed CSS), so bUnit's headless DOM can
/// only cover the underlying markup/data — this is the Playwright half of that split. See
/// <c>tests/Lumeo.Tests/Components/Scheduler/*ViewTests.cs</c> for the bUnit half.
/// </summary>
public class SchedulerViewsInteractionTests : PlaywrightTestBase
{
    [Fact]
    public async Task Overflow_Day_More_Trigger_Opens_A_Popover_Listing_The_Hidden_Events()
    {
        // Predicted wrong value against the pre-fix markup: the "+N more" trigger was a plain
        // <span>, not a button — Page.Locator("[data-testid='month-more-events']").ClickAsync()
        // would still technically hit the span (Playwright can click any element), but no
        // popover would ever open (there was no click handler, no popover markup at all) — so
        // the popover-content assertion below is what actually fails against the old state.
        await Goto("/e2e/scheduler-views-preview");

        var monthSection = Page.Locator("[data-testid='month-section']");
        await monthSection.ScrollIntoViewIfNeededAsync();

        var trigger = monthSection.Locator("[data-testid='month-more-events']").First;
        await trigger.ScrollIntoViewIfNeededAsync();
        await trigger.ClickAsync();

        var popover = Page.Locator("[data-testid='month-more-popover']");
        await Assertions.Expect(popover).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Assertions.Expect(popover).ToContainTextAsync("Overflow D");

        // Clicking a hidden event in the popover reaches OnEventClick.
        await popover.Locator("[data-event-id='e2e-overflow-4']").ClickAsync();
        await Assertions.Expect(Page.Locator("[data-testid='last-clicked-log']")).ToContainTextAsync("e2e-overflow-4", new() { Timeout = 5000 });
    }

    [Fact]
    public async Task Hovering_A_Month_Event_Chip_Shows_A_Tooltip_With_Title_And_Time()
    {
        // Spec §3.4's promise ("Hover event chip -> Tooltip"), absent before this task.
        await Goto("/e2e/scheduler-views-preview");

        var monthSection = Page.Locator("[data-testid='month-section']");
        await monthSection.ScrollIntoViewIfNeededAsync();

        var pill = monthSection.Locator("[data-event-id='e2e-1']").First;
        await pill.ScrollIntoViewIfNeededAsync();
        await pill.HoverAsync();

        var tooltip = Page.Locator("[role='tooltip']");
        await Assertions.Expect(tooltip).ToBeVisibleAsync(new() { Timeout = 2000 });
        await Assertions.Expect(tooltip).ToContainTextAsync("Draggable Standup");
    }

    [Fact]
    public async Task Hovering_A_TimeGrid_Event_Chip_Shows_A_Tooltip()
    {
        await Goto("/e2e/scheduler-views-preview");

        var weekSection = Page.Locator("[data-testid='week-section']");
        await weekSection.ScrollIntoViewIfNeededAsync();

        var pill = weekSection.Locator("[data-event-id='e2e-2']").First;
        await pill.ScrollIntoViewIfNeededAsync();
        await pill.HoverAsync();

        var tooltip = Page.Locator("[role='tooltip']");
        await Assertions.Expect(tooltip).ToBeVisibleAsync(new() { Timeout = 2000 });
        await Assertions.Expect(tooltip).ToContainTextAsync("Fixed Review");
    }

    [Fact]
    public async Task TimeGrid_Timed_Pill_Carries_The_Resize_Handle_Data_Attribute()
    {
        // The audit's finding: JS computes 6px resize hit-zones but nothing showed the user an
        // edge is grabbable. Fixed via a visible CSS grip bar keyed off data-resizable — assert
        // the DOM contract the CSS hooks into (the CSS itself isn't asserted here: computed-style
        // assertions would depend on lumeo-scheduler.css actually being linked into this docs
        // page, which this task found is NOT currently wired for ANY Lumeo.Scheduler/Gantt/Maps/
        // FileViewer CSS file on this site — a pre-existing, repo-wide gap outside this task's
        // scope, reported separately).
        await Goto("/e2e/scheduler-views-preview");

        var weekSection = Page.Locator("[data-testid='week-section']");
        var pill = weekSection.Locator("[data-event-id='e2e-1']").First;
        await pill.ScrollIntoViewIfNeededAsync();

        await Assertions.Expect(pill).ToHaveAttributeAsync("data-resizable", "true");
    }

    [Fact]
    public async Task BusinessHours_Section_Marks_Weekend_Day_Columns_Off()
    {
        await Goto("/e2e/scheduler-views-preview");

        var section = Page.Locator("[data-testid='business-hours-section']");
        await section.ScrollIntoViewIfNeededAsync();

        var offSlots = section.Locator("[data-off='true']");
        await Assertions.Expect(offSlots.First).ToBeAttachedAsync(new() { Timeout = 5000 });
        var count = await offSlots.CountAsync();
        Assert.True(count > 0, "expected at least one off-hours/off-day slot when BusinessHours=true");
    }

    [Fact]
    public async Task Month_Section_Marks_Weekend_Cells_Off_When_BusinessHours_Is_Set()
    {
        await Goto("/e2e/scheduler-views-preview");

        var monthSection = Page.Locator("[data-testid='month-section']");
        await monthSection.ScrollIntoViewIfNeededAsync();

        var offCells = monthSection.Locator("[data-off='true']");
        var count = await offCells.CountAsync();
        // Any 6-week month grid always contains at least one full weekend (2 off days),
        // so this is unconditionally > 0 for the month section (BusinessHours="true" there).
        Assert.True(count > 0, "expected weekend cells to carry data-off when BusinessHours=true");
    }

    [Fact]
    public async Task The_Week_Grid_Shows_Its_Day_Header_And_Does_Not_Clip_The_First_Hour()
    {
        // Reported against the live docs as "the week view is cut off at the top": there was no
        // day header at all, and the first hour label was lifted by a negative offset into the
        // scroller's clipping edge, so its digits were sliced in half. Both are geometry, which
        // is why this half lives here rather than in bUnit.
        await Goto("/e2e/scheduler-views-preview");

        var week = Page.Locator("[data-testid='week-section']");
        await week.ScrollIntoViewIfNeededAsync();

        var headers = week.Locator("[data-dayheader]");
        await Assertions.Expect(headers).ToHaveCountAsync(7, new() { Timeout = 5000 });

        // Each header sits above the column it names — by NAME and by POSITION. Matching the
        // date arrays alone passed happily while the two rows were laid out against different
        // widths, which is the whole failure mode: the header labelled the right day and drew
        // it over the wrong column (Codex review, PR #427).
        var headerDates = await headers.EvaluateAllAsync<string[]>(
            "els => els.map(e => e.getAttribute('data-dayheader'))");
        var columnDates = await week.Locator("[data-daycol]").EvaluateAllAsync<string[]>(
            "els => els.map(e => e.getAttribute('data-daycol'))");
        Assert.Equal(columnDates, headerDates);

        for (var i = 0; i < columnDates.Length; i++)
        {
            var head = await headers.Nth(i).BoundingBoxAsync();
            var col = await week.Locator("[data-daycol]").Nth(i).BoundingBoxAsync();
            Assert.NotNull(head);
            Assert.NotNull(col);
            // A pixel of rounding is fine; a scrollbar's width is not.
            Assert.True(Math.Abs(head!.X - col!.X) <= 1,
                $"header {i} ({columnDates[i]}) starts at {head.X} over a column at {col.X}");
            Assert.True(Math.Abs(head.Width - col.Width) <= 1,
                $"header {i} is {head.Width} wide over a column {col.Width} wide");
        }

        // And the first hour label is fully inside the scroller rather than half above it.
        var clipped = await week.Locator("span.absolute").First.EvaluateAsync<bool>(
            @"el => {
                const r = el.getBoundingClientRect();
                let sc = el.parentElement;
                while (sc && getComputedStyle(sc).overflowY !== 'auto') sc = sc.parentElement;
                return sc ? r.top < sc.getBoundingClientRect().top : false;
            }");
        Assert.False(clipped, "the first hour label is clipped by the scroller's top edge");
    }

}
