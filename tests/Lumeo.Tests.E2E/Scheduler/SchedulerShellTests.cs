using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Scheduler;

/// <summary>
/// Real-browser coverage for the Scheduler WRAPPER's own chrome against
/// <c>/e2e/scheduler-shell-preview</c> — calendar chips, side-by-side panes, and the opt-in
/// appointment dialog. The bUnit half (<c>tests/Lumeo.Tests/Components/Scheduler/
/// SchedulerCalendarTests.cs</c> and <c>SchedulerEventDialogTests.cs</c>) pins the state
/// transitions; what only a real browser has is a <c>Dialog</c> that actually mounts and
/// traps focus, a native <c>&lt;select&gt;</c> the browser itself populates, and a
/// double-click that goes through the real pointer pipeline rather than a synthesized
/// callback.
/// </summary>
public class SchedulerShellTests : PlaywrightTestBase
{
    [Fact]
    public async Task Double_Clicking_A_Day_Opens_An_Empty_Create_Form_And_Saving_Adds_The_Event()
    {
        await Goto("/e2e/scheduler-shell-preview");

        var section = Page.Locator("[data-testid='dialog-section']");
        await section.ScrollIntoViewIfNeededAsync();
        await Assertions.Expect(Page.Locator("[data-testid='dialog-count-log']")).ToContainTextAsync("Events: 1");

        await section.Locator("[data-cell-date='2026-04-20']").First.DblClickAsync();

        var title = Page.Locator("[data-scheduler-dialog-title]");
        await Assertions.Expect(title).ToBeVisibleAsync(new() { Timeout = 5000 });
        // Empty, not pre-filled with the clicked day's first event: this is the CREATE form.
        await Assertions.Expect(title).ToHaveValueAsync(string.Empty);

        await title.FillAsync("Team offsite");
        await Page.Locator("[data-scheduler-dialog-save]").ClickAsync();

        await Assertions.Expect(Page.Locator("[data-testid='dialog-count-log']"))
            .ToContainTextAsync("Events: 2", new() { Timeout = 5000 });
        await Assertions.Expect(section.Locator("text=Team offsite").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Clicking_An_Event_Edits_It_And_Delete_Removes_It()
    {
        await Goto("/e2e/scheduler-shell-preview");

        var section = Page.Locator("[data-testid='dialog-section']");
        await section.ScrollIntoViewIfNeededAsync();
        await section.Locator("[data-event-id='shell-1']").First.ClickAsync();

        var title = Page.Locator("[data-scheduler-dialog-title]");
        await Assertions.Expect(title).ToBeVisibleAsync(new() { Timeout = 5000 });
        // Populated from the event, and the calendar select shows the one it belongs to.
        await Assertions.Expect(title).ToHaveValueAsync("Standup");
        await Assertions.Expect(Page.Locator("[data-scheduler-dialog-calendar]")).ToHaveValueAsync("team");

        await title.FillAsync("Renamed standup");
        await Page.Locator("[data-scheduler-dialog-save]").ClickAsync();

        await Assertions.Expect(Page.Locator("[data-testid='dialog-change-log']"))
            .ToContainTextAsync("Renamed standup", new() { Timeout = 5000 });
        // The count is unchanged: an edit replaced the event rather than adding a second one.
        await Assertions.Expect(Page.Locator("[data-testid='dialog-count-log']")).ToContainTextAsync("Events: 1");

        await section.Locator("[data-event-id='shell-1']").First.ClickAsync();
        await Assertions.Expect(Page.Locator("[data-scheduler-dialog-delete]")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Page.Locator("[data-scheduler-dialog-delete]").ClickAsync();

        await Assertions.Expect(Page.Locator("[data-testid='dialog-count-log']"))
            .ToContainTextAsync("Events: 0", new() { Timeout = 5000 });
    }

    [Fact]
    public async Task With_The_Dialog_Off_A_Click_Reaches_The_Callers_Handler_And_Nothing_Opens()
    {
        // The whole design constraint: opting out has to leave the consumer's own workflow
        // exactly where it was. A dialog appearing here would displace every caller's form.
        await Goto("/e2e/scheduler-shell-preview");

        var section = Page.Locator("[data-testid='optout-section']");
        await section.ScrollIntoViewIfNeededAsync();
        await section.Locator("[data-event-id='optout-1']").First.ClickAsync();

        await Assertions.Expect(Page.Locator("[data-testid='optout-click-log']"))
            .ToContainTextAsync("clicked optout-1", new() { Timeout = 5000 });
        await Assertions.Expect(Page.Locator("[data-scheduler-dialog-title]")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Side_By_Side_Panes_Split_The_Events_And_A_Chip_Removes_Its_Pane()
    {
        await Goto("/e2e/scheduler-shell-preview");

        var section = Page.Locator("[data-testid='panes-section']");
        await section.ScrollIntoViewIfNeededAsync();

        var team = section.Locator("[data-scheduler-pane='team']");
        var personal = section.Locator("[data-scheduler-pane='personal']");
        await Assertions.Expect(team).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Assertions.Expect(personal).ToBeVisibleAsync();

        // Each pane draws only its own calendar's events.
        await Assertions.Expect(team.Locator("[data-event-id='pane-team']").First).ToBeVisibleAsync();
        await Assertions.Expect(team.Locator("[data-event-id='pane-personal']")).ToHaveCountAsync(0);
        await Assertions.Expect(personal.Locator("[data-event-id='pane-personal']").First).ToBeVisibleAsync();

        // Panes sit beside each other, not stacked.
        var teamBox = await team.BoundingBoxAsync();
        var personalBox = await personal.BoundingBoxAsync();
        Assert.NotNull(teamBox);
        Assert.NotNull(personalBox);
        Assert.True(personalBox!.X > teamBox!.X, $"panes are stacked, not side by side: {teamBox.X} / {personalBox.X}");

        // Switching a calendar off takes its pane with it — and with one left, the split has
        // nothing to compare, so it falls back to the overlay rather than an empty column.
        await section.Locator("[data-scheduler-calendar='personal']").ClickAsync();
        await Assertions.Expect(section.Locator("[data-scheduler-pane]")).ToHaveCountAsync(0, new() { Timeout = 5000 });
        await Assertions.Expect(section.Locator("[data-event-id='pane-team']").First).ToBeVisibleAsync();
        await Assertions.Expect(section.Locator("[data-event-id='pane-personal']")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task The_Overflow_Popover_Stays_Inside_A_Half_Width_Pane()
    {
        // The panel is a fixed 14rem and a day column is the grid's width over its day count, so
        // where the panel may hang cannot be worked out by counting columns — side-by-side panes
        // halve that width without changing the count. It is anchored to the strip's own edges
        // instead, which is geometry rather than markup: only a browser can tell whether it
        // actually fits (Codex review of PR #427).
        // Narrow on purpose. At a wide viewport a half-width pane is still roomy enough for the
        // panel to fit wherever it is anchored, so the test would pass against the bug — the
        // whole finding is about ORDINARY scheduler widths.
        await Page.SetViewportSizeAsync(900, 900);
        await Goto("/e2e/scheduler-shell-preview");

        var section = Page.Locator("[data-testid='panes-section']");
        await section.ScrollIntoViewIfNeededAsync();
        await section.GetByRole(AriaRole.Button, new() { Name = "Week", Exact = true }).ClickAsync();

        var trigger = section.Locator("[data-testid='allday-more']").First;
        await Assertions.Expect(trigger).ToBeVisibleAsync(new() { Timeout = 5000 });
        await trigger.ClickAsync();

        var popover = section.Locator("[data-testid='allday-more-popover']").First;
        await Assertions.Expect(popover).ToBeVisibleAsync(new() { Timeout = 5000 });

        var panel = await popover.BoundingBoxAsync();
        var pane = await section.Locator("[data-scheduler-pane]").First.BoundingBoxAsync();
        Assert.NotNull(panel);
        Assert.NotNull(pane);

        Assert.True(panel!.X >= pane!.X - 1,
            $"the panel starts at {panel.X}, outside its pane at {pane.X}");
        Assert.True(panel.X + panel.Width <= pane.X + pane.Width + 1,
            $"the panel ends at {panel.X + panel.Width}, past its pane's {pane.X + pane.Width}");

        // At its own width, not squashed to a column's. Fitting is trivial to achieve badly:
        // anchored to the CELL, max-w-full resolves against a ~50px day column and the panel
        // collapses to a strip too narrow to read a title in. Against the strip it keeps the
        // 14rem it asks for.
        Assert.True(panel.Width >= 200,
            $"the panel is only {panel.Width}px wide - it is being measured against a column");
    }

    [Fact]
    public async Task The_Month_Overflow_Popover_Keeps_Its_Width_And_Stays_Inside_The_Grid()
    {
        // The month grid is six rows deep, so its popover is anchored to the CELL — top-full
        // against the grid would drop it below the whole month. That rules out max-w-full, which
        // resolves against one day column and collapses the panel to a strip too narrow to read
        // (Codex review of PR #427). Both halves of that are geometry, which markup assertions
        // cannot see: the class-only test passed while the panel was 40px wide.
        await Page.SetViewportSizeAsync(900, 900);
        await Goto("/e2e/scheduler-shell-preview");

        var section = Page.Locator("[data-testid='panes-section']");
        await section.ScrollIntoViewIfNeededAsync();

        var trigger = section.Locator("[data-testid='month-more-events']").First;
        await Assertions.Expect(trigger).ToBeVisibleAsync(new() { Timeout = 5000 });
        await trigger.ClickAsync();

        var popover = section.Locator("[data-testid='month-more-popover']").First;
        await Assertions.Expect(popover).ToBeVisibleAsync(new() { Timeout = 5000 });

        var panel = await popover.BoundingBoxAsync();
        var pane = await section.Locator("[data-scheduler-pane]").First.BoundingBoxAsync();
        Assert.NotNull(panel);
        Assert.NotNull(pane);

        Assert.True(panel!.Width >= 200,
            $"the panel is only {panel.Width}px wide - it is being measured against a column");
        Assert.True(panel.X >= pane!.X - 1 && panel.X + panel.Width <= pane.X + pane.Width + 1,
            $"the panel spans {panel.X}..{panel.X + panel.Width}, outside its pane's {pane.X}..{pane.X + pane.Width}");

        // And it sits under its own trigger rather than below the whole grid.
        var box = await trigger.BoundingBoxAsync();
        Assert.True(panel.Y >= box!.Y && panel.Y - box.Y < 60,
            $"the panel opens {panel.Y - box.Y}px below its trigger");
    }

}
