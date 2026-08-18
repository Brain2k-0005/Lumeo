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
}
