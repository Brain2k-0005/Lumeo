using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// Gantt v3 Phase 3, T5 — tree pane upgrades: multi-column, splitter,
/// RowTemplate. v3-ONLY (v2 has no tree pane, no splitter, no per-column
/// slots at all — no parity route to compare against, mirroring
/// <c>GanttV3TreeTests</c>'s own "v3-only by definition" framing).
///
/// Drives real Playwright pointer/keyboard input against
/// <c>/e2e/gantt-v3-tree?fixture=columns</c> (<c>GanttV3TreePage.razor</c>'s
/// own <c>columns</c> fixture: a "Duration" <c>GanttTreeColumn</c>, a
/// <c>TreeHeaderMenu</c> trigger button, and a bullet-prefixed
/// <c>RowTemplate</c>), against the known 5-row/3-level
/// <c>GanttParityFixtures.TreeTasks()</c> hierarchy every other
/// <c>GanttV3TreeTests</c> spec already uses.
///
/// The row-alignment assertions here are the REAL (not bUnit-simulated)
/// drift-catching check design spec Phase 3, T5's decision #1 asks for:
/// actual <c>BoundingBoxAsync()</c> reads comparing a tree-pane row's Y
/// center against its corresponding timeline bar's Y center, in a live
/// browser, both before and after a real splitter drag.
/// </summary>
public class GanttV3TreeColumnsSplitterTests : GanttParityTestBase
{
    private const string Root = "[data-testid='gantt-v3-tree-root']";

    [Fact]
    public async Task TreeColumns_Render_A_Duration_Cell_Per_Row_Aligned_With_The_Timeline_Bar()
    {
        await GotoHost("/e2e/gantt-v3-tree?fixture=columns");

        var headerCell = Page.Locator($"{Root} .lumeo-gantt-v3-tree-header-cell");
        await headerCell.First.WaitForAsync(new() { Timeout = 15000 });
        await Expect(headerCell).ToHaveTextAsync("Duration");

        var rows = Page.Locator($"{Root} [data-row-kind='task']");
        await Assertions.Expect(rows).ToHaveCountAsync(5);

        var cells = Page.Locator($"{Root} .gantt-e2e-duration-cell");
        await Assertions.Expect(cells).ToHaveCountAsync(5);
        // root1: 2026-03-01 -> 2026-03-30 = 29 days.
        await Expect(cells.First).ToHaveTextAsync("29d");

        // Real drift-catching alignment: the "Program Kickoff" row's duration
        // cell and its corresponding timeline bar must share the same
        // vertical center — the whole point of the fixed-height/overflow-hidden
        // guard on every row (see GanttTree.razor's own remarks).
        var rootRow = Page.Locator($"{Root} [data-row-kind='task']", new() { HasTextString = "Program Kickoff" });
        var durationCell = rootRow.Locator(".gantt-e2e-duration-cell");
        var bar = Page.Locator($"{Root} [data-task-id='root1']");

        var cellBox = await durationCell.BoundingBoxAsync();
        var barBox = await bar.BoundingBoxAsync();
        Assert.NotNull(cellBox);
        Assert.NotNull(barBox);
        var cellCenterY = cellBox!.Y + cellBox.Height / 2;
        var barCenterY = barBox!.Y + barBox.Height / 2;
        Assert.True(Math.Abs(cellCenterY - barCenterY) < 2.0,
            $"expected the tree column cell and the timeline bar for the same row to share a vertical center, got cell={cellCenterY}, bar={barCenterY}");
    }

    [Fact]
    public async Task TreeHeaderMenu_Slot_Renders_And_Is_Clickable()
    {
        await GotoHost("/e2e/gantt-v3-tree?fixture=columns");

        var menuButton = Page.Locator($"{Root} .gantt-e2e-tree-header-menu");
        await menuButton.WaitForAsync(new() { Timeout = 15000 });
        await Expect(menuButton).ToHaveTextAsync("...");
        await menuButton.ClickAsync(); // must not throw / crash the render
        await Expect(menuButton).ToBeVisibleAsync();
    }

    [Fact]
    public async Task RowTemplate_Renders_Custom_Content_And_The_Toggle_Chevron_Still_Works()
    {
        await GotoHost("/e2e/gantt-v3-tree?fixture=columns");

        var customLabel = Page.Locator($"{Root} .gantt-e2e-row-template", new() { HasTextString = "Program Kickoff" });
        await customLabel.WaitForAsync(new() { Timeout = 15000 });
        await Expect(customLabel).ToHaveTextAsync("• Program Kickoff");

        // The default label span must NOT also render for this row (RowTemplate replaces it, not adds to it).
        var defaultLabel = Page.Locator($"{Root} .lumeo-gantt-v3-tree-label", new() { HasTextString = "Program Kickoff" });
        await Assertions.Expect(defaultLabel).ToHaveCountAsync(0);

        // Chrome (indent + expander) survives RowTemplate — decision #4 — and
        // stays FUNCTIONAL, not just present: collapsing root1 still hides
        // its descendant rows exactly like the non-columns fixture's own spec.
        var rows = Page.Locator($"{Root} [data-row-kind='task']");
        await Assertions.Expect(rows).ToHaveCountAsync(5);
        var root1Row = Page.Locator($"{Root} [data-row-kind='task']", new() { HasTextString = "Program Kickoff" });
        var toggle = root1Row.Locator("button.lumeo-gantt-v3-tree-toggle");
        await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "true");

        await toggle.ClickAsync();

        await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "false");
        await Assertions.Expect(rows).ToHaveCountAsync(2);
    }

    [Fact]
    public async Task Splitter_Pointer_Drag_Resizes_The_Tree_Pane_And_Columns_Move_With_It()
    {
        await GotoHost("/e2e/gantt-v3-tree?fixture=columns");

        var pane = Page.Locator($"{Root} .lumeo-gantt-v3-tree-splitter").Locator("xpath=..");
        var splitter = Page.Locator($"{Root} .lumeo-gantt-v3-tree-splitter");
        await splitter.WaitForAsync(new() { Timeout = 15000 });

        var boxBefore = await pane.BoundingBoxAsync();
        Assert.NotNull(boxBefore);
        var widthBefore = boxBefore!.Width;

        var handleBox = await splitter.BoundingBoxAsync();
        Assert.NotNull(handleBox);
        var startX = handleBox!.X + handleBox.Width / 2;
        var startY = handleBox.Y + handleBox.Height / 2;

        const double dragDistance = 100;
        await Page.Mouse.MoveAsync((float)startX, (float)startY);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync((float)(startX + dragDistance), (float)startY);
        await Page.Mouse.UpAsync();

        var boxAfter = await pane.BoundingBoxAsync();
        Assert.NotNull(boxAfter);
        var widthAfter = boxAfter!.Width;

        Assert.True(Math.Abs((widthAfter - widthBefore) - dragDistance) < 2.0,
            $"expected the tree pane to grow by ~{dragDistance}px, got {widthAfter - widthBefore}px (before={widthBefore}, after={widthAfter})");

        // Columns/rows still align after the resize (the boundary moved, not
        // the rows) — same alignment check as the un-resized spec above,
        // proving the drift guard holds DURING/AFTER a live resize too.
        var rootRow = Page.Locator($"{Root} [data-row-kind='task']", new() { HasTextString = "Program Kickoff" });
        var durationCell = rootRow.Locator(".gantt-e2e-duration-cell");
        var bar = Page.Locator($"{Root} [data-task-id='root1']");
        var cellBox = await durationCell.BoundingBoxAsync();
        var barBox = await bar.BoundingBoxAsync();
        Assert.NotNull(cellBox);
        Assert.NotNull(barBox);
        var cellCenterY = cellBox!.Y + cellBox.Height / 2;
        var barCenterY = barBox!.Y + barBox.Height / 2;
        Assert.True(Math.Abs(cellCenterY - barCenterY) < 2.0,
            $"expected row alignment to survive the resize, got cell={cellCenterY}, bar={barCenterY}");
    }

    [Fact]
    public async Task Splitter_Keyboard_ArrowRight_Grows_The_Pane_And_Updates_AriaValuenow()
    {
        await GotoHost("/e2e/gantt-v3-tree?fixture=columns");

        var splitter = Page.Locator($"{Root} .lumeo-gantt-v3-tree-splitter");
        await splitter.WaitForAsync(new() { Timeout = 15000 });

        var before = await splitter.GetAttributeAsync("aria-valuenow");
        Assert.NotNull(before);
        var beforeValue = int.Parse(before!);

        await splitter.FocusAsync();
        await splitter.PressAsync("ArrowRight");

        await Assertions.Expect(splitter).ToHaveAttributeAsync("aria-valuenow", (beforeValue + 16).ToString(), new() { Timeout = 5000 });
    }

    private static ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);
}
