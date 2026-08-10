using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Scheduler;

/// <summary>
/// Real-pointer coverage for the first-party Scheduler view engine's interaction layer
/// (wave 1b) — drives genuine Playwright <see cref="IMouse"/> gestures against
/// <c>/e2e/scheduler-views-preview</c> (<c>docs/Lumeo.Docs/Pages/E2E/SchedulerViewsPreview.razor</c>).
/// This is the half of spec §7.2's bUnit/Playwright split bUnit's headless DOM cannot exercise
/// at all: drag pixel maths, ghost element lifecycle (created on drag start, removed on
/// drop/cancel, the REAL chip never touched), and the browser-local now-indicator's actual
/// position. See <c>tests/Lumeo.Tests/Components/Scheduler/*ViewTests.cs</c> for the C#-side
/// (fail-closed ValidateDrop/CommitDrag/CommitCreate) half of the split.
/// </summary>
public class SchedulerViewsDragTests : PlaywrightTestBase
{
    private const string GhostSelector = "[data-scheduler-ghost]";

    private async Task<(float X, float Y)> CenterOf(ILocator locator)
    {
        var box = await locator.BoundingBoxAsync();
        Assert.NotNull(box);
        return (box!.X + box.Width / 2, box.Y + box.Height / 2);
    }

    private async Task DragAsync((float X, float Y) from, (float X, float Y) to, int steps = 6)
    {
        await Page.Mouse.MoveAsync(from.X, from.Y);
        await Page.Mouse.DownAsync();
        // Cross DRAG_THRESHOLD_PX (4px) immediately so a ghost is created, then move in
        // a few steps toward the target (mirrors the Gantt E2E suite's own DragAsync).
        await Page.Mouse.MoveAsync(from.X + 6, from.Y + 6);
        for (var i = 1; i <= steps; i++)
        {
            var t = (float)i / steps;
            await Page.Mouse.MoveAsync(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t);
        }
        await Page.Mouse.UpAsync();
    }

    [Fact]
    public async Task Month_Drag_Move_Creates_A_Ghost_Then_Removes_It_And_Commits()
    {
        await Goto("/e2e/scheduler-views-preview");

        var monthSection = Page.Locator("[data-testid='month-section']");
        await monthSection.ScrollIntoViewIfNeededAsync();

        var pill = monthSection.Locator("[data-event-id='e2e-1']").First;
        await pill.ScrollIntoViewIfNeededAsync();

        // Drag to the 20th of the currently-shown month — a plain day well away
        // from the seeded events and never day 15 (the fail-closed test's own
        // reserved target, see the sibling test below).
        var targetCell = monthSection.Locator("[data-cell-date$='-20']").First;
        await targetCell.ScrollIntoViewIfNeededAsync();

        var from = await CenterOf(pill);
        var to = await CenterOf(targetCell);

        await Page.Mouse.MoveAsync(from.X, from.Y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(from.X + 8, from.Y + 8);

        // Mid-drag: exactly one real chip within the Month section (never
        // duplicated/moved) plus a ghost (appended to document.body, hence unscoped).
        await Assertions.Expect(Page.Locator(GhostSelector)).ToHaveCountAsync(1);
        await Assertions.Expect(monthSection.Locator("[data-event-id='e2e-1']")).ToHaveCountAsync(1);

        await Page.Mouse.MoveAsync(to.X, to.Y);
        await Page.Mouse.UpAsync();

        // Ghost is gone post-drop.
        await Assertions.Expect(Page.Locator(GhostSelector)).ToHaveCountAsync(0);

        // Commit landed (fired OnEventChange -> the harness's "Last change" sink).
        await Assertions.Expect(Page.Locator("[data-testid='month-change-log']")).ToContainTextAsync("e2e-1", new() { Timeout = 5000 });
    }

    [Fact]
    public async Task Month_Drag_Fail_Closed_Rejects_The_15th_And_Never_Commits()
    {
        await Goto("/e2e/scheduler-views-preview");

        var monthSection = Page.Locator("[data-testid='month-section']");
        await monthSection.ScrollIntoViewIfNeededAsync();

        var pill = monthSection.Locator("[data-event-id='e2e-1']").First;
        await pill.ScrollIntoViewIfNeededAsync();

        var targetCell = monthSection.Locator("[data-cell-date$='-15']").First;
        await targetCell.ScrollIntoViewIfNeededAsync();

        var changeLogBefore = await Page.Locator("[data-testid='month-change-log']").TextContentAsync();

        var from = await CenterOf(pill);
        var to = await CenterOf(targetCell);
        await DragAsync(from, to);

        // The fail-closed predicate (SchedulerViewsPreview.RejectDrop15th) was actually
        // invoked — proves the JS side awaited ValidateDrop rather than skipping it.
        await Assertions.Expect(Page.Locator("[data-testid='month-candrop-log']")).ToContainTextAsync("e2e-1", new() { Timeout = 5000 });

        // Ghost cleaned up regardless of the veto outcome.
        await Assertions.Expect(Page.Locator(GhostSelector)).ToHaveCountAsync(0);

        // No commit: the change log is byte-identical to before the drag — the fail-closed
        // path never reached CommitDrag. (An accept-by-default regression would update
        // this text to show a move onto the 15th.)
        var changeLogAfter = await Page.Locator("[data-testid='month-change-log']").TextContentAsync();
        Assert.Equal(changeLogBefore, changeLogAfter);

        // The real chip is exactly where it started (never mutated by JS, per the
        // ghost-only invariant — nothing to "resync" because nothing was ever moved).
        await Assertions.Expect(pill).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Week_Resize_Bottom_Edge_Extends_The_Event_Duration()
    {
        await Goto("/e2e/scheduler-views-preview");

        var weekSection = Page.Locator("[data-testid='week-section']");
        var pill = weekSection.Locator("[data-event-id='e2e-1']").First;
        await pill.ScrollIntoViewIfNeededAsync();

        var box = await pill.BoundingBoxAsync();
        Assert.NotNull(box);

        // Grab the bottom edge (within RESIZE_HANDLE_PX of the pill's bottom).
        var startX = box!.X + box.Width / 2;
        var startY = box.Y + box.Height - 2;

        await Page.Mouse.MoveAsync(startX, startY);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(startX, startY + 48); // ~1 extra hour at 48px/hour
        await Page.Mouse.UpAsync();

        await Assertions.Expect(Page.Locator("[data-testid='week-change-log']")).ToContainTextAsync("e2e-1", new() { Timeout = 5000 });
    }

    [Fact]
    public async Task Week_DragToCreate_On_Empty_Background_Fires_OnDateSelect()
    {
        await Goto("/e2e/scheduler-views-preview");

        var weekSection = Page.Locator("[data-testid='week-section']");
        await weekSection.ScrollIntoViewIfNeededAsync();

        // Any hour cell with no chip on it — data-slot-hour='6' (06:00) is far from
        // the seeded 09:00/14:00 events.
        var cell = weekSection.Locator("[data-slot-hour='6']").First;
        await cell.ScrollIntoViewIfNeededAsync();
        var box = await cell.BoundingBoxAsync();
        Assert.NotNull(box);

        var startX = box!.X + box.Width / 2;
        var startY = box.Y + 4;

        await Page.Mouse.MoveAsync(startX, startY);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(startX, startY + 40);
        await Page.Mouse.UpAsync();

        await Assertions.Expect(Page.Locator("[data-testid='week-change-log']")).ToContainTextAsync("selected", new() { Timeout = 5000 });
    }

    [Fact]
    public async Task Month_DragToCreate_On_Empty_Background_Fires_OnDateSelect()
    {
        // Spec §3.4's Month row promise — previously entirely absent (only click/dblclick on a
        // single cell existed). Drag across two empty cells and expect OnDateSelect, mirroring
        // the existing Week_DragToCreate test's own shape.
        await Goto("/e2e/scheduler-views-preview");

        var monthSection = Page.Locator("[data-testid='month-section']");
        await monthSection.ScrollIntoViewIfNeededAsync();

        // Two adjacent empty cells, never day 15/20/22 (the CanDrop-log tests' own reserved
        // targets) and never one of the seeded event days.
        var fromCell = monthSection.Locator("[data-cell-date$='-05']").First;
        var toCell = monthSection.Locator("[data-cell-date$='-06']").First;
        await fromCell.ScrollIntoViewIfNeededAsync();

        var from = await CenterOf(fromCell);
        var to = await CenterOf(toCell);
        await DragAsync(from, to);

        await Assertions.Expect(Page.Locator("[data-testid='month-change-log']")).ToContainTextAsync("selected", new() { Timeout = 5000 });
    }

    [Fact]
    public async Task Month_Drag_Highlights_The_Range_During_The_Drag()
    {
        await Goto("/e2e/scheduler-views-preview");

        var monthSection = Page.Locator("[data-testid='month-section']");
        await monthSection.ScrollIntoViewIfNeededAsync();

        var fromCell = monthSection.Locator("[data-cell-date$='-05']").First;
        var toCell = monthSection.Locator("[data-cell-date$='-06']").First;
        await fromCell.ScrollIntoViewIfNeededAsync();

        var from = await CenterOf(fromCell);
        var to = await CenterOf(toCell);

        await Page.Mouse.MoveAsync(from.X, from.Y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(from.X + 8, from.Y + 8);
        await Page.Mouse.MoveAsync(to.X, to.Y);

        // Both the start and end cell must carry the selection highlight attribute mid-drag.
        await Assertions.Expect(fromCell).ToHaveAttributeAsync("data-select-target", "true");
        await Assertions.Expect(toCell).ToHaveAttributeAsync("data-select-target", "true");

        await Page.Mouse.UpAsync();

        // Cleaned up post-drop.
        await Assertions.Expect(fromCell).Not.ToHaveAttributeAsync("data-select-target", "true");
    }

    [Fact]
    public async Task Month_Drag_With_CanDrop_Adjustment_Commits_The_Snapped_Date_Not_The_Raw_Drop_Target()
    {
        // SchedulerViewsPreview.RejectDrop15th snaps any drop landing on the 22nd forward to
        // the 23rd. Predicted: dragging e2e-1 onto the 22nd commits to the 23rd, not the 22nd —
        // the concrete, E2E-observable proof that CanDrop's three-way Adjustment reaches the
        // real committed event through the full pointer -> ValidateDrop -> CommitDrag path.
        await Goto("/e2e/scheduler-views-preview");

        var monthSection = Page.Locator("[data-testid='month-section']");
        await monthSection.ScrollIntoViewIfNeededAsync();

        var pill = monthSection.Locator("[data-event-id='e2e-1']").First;
        await pill.ScrollIntoViewIfNeededAsync();

        var targetCell = monthSection.Locator("[data-cell-date$='-22']").First;
        await targetCell.ScrollIntoViewIfNeededAsync();

        var from = await CenterOf(pill);
        var to = await CenterOf(targetCell);
        await DragAsync(from, to);

        // Predicted-vs-actual: the raw drop target was the 22nd; the committed value must show
        // "-23" (the adjusted date), never "-22".
        await Assertions.Expect(Page.Locator("[data-testid='month-change-log']")).ToContainTextAsync("-23", new() { Timeout = 5000 });
        var log = await Page.Locator("[data-testid='month-change-log']").TextContentAsync();
        Assert.DoesNotContain("-22 ", log ?? ""); // trailing space avoids matching "-22" as a substring of "-220..." etc.
    }

    [Fact]
    public async Task NowIndicator_Renders_On_Exactly_One_Column_At_The_Browsers_Local_Time()
    {
        await Goto("/e2e/scheduler-views-preview");

        var weekSection = Page.Locator("[data-testid='week-section']");
        await weekSection.ScrollIntoViewIfNeededAsync();

        var line = weekSection.Locator("[data-scheduler-now-line]:visible");
        await Assertions.Expect(line).ToHaveCountAsync(1, new() { Timeout = 10000 });

        // The line's own inline `top` (set purely by scheduler-views.js from `new Date()` —
        // never a server-computed DateTime, spec §2.2) should land within a generous
        // tolerance of the CURRENT wall-clock minute, proving it's a real, live browser-side
        // computation and not a stale/placeholder value.
        var topPx = await line.EvaluateAsync<double>("el => parseFloat(el.style.top)");
        var now = DateTime.Now;
        var expectedMinutes = now.Hour * 60 + now.Minute;
        var expectedPx = expectedMinutes * (48.0 / 60.0);

        Assert.InRange(topPx, expectedPx - 40, expectedPx + 40); // ~50 minutes of slack for CI clock skew
    }
}
