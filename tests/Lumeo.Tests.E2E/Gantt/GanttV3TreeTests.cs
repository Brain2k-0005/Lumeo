using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// v3-ONLY: <c>GanttTask.ParentId</c> hierarchy + tree-pane collapse (feat/gantt-v3,
/// T4). v2 has no <c>ParentId</c> concept at all — there is no v2 route to compare
/// against here by definition (see <c>GanttV3TreePage.razor</c>'s remarks), so
/// this spec asserts v3's OWN behavior only, against
/// <c>GanttParityFixtures.TreeTasks()</c>'s known 5-task, 3-level hierarchy:
/// root1 (children: child1, child2), child1 (children: grandchild1), root2 (leaf).
/// </summary>
public class GanttV3TreeTests : GanttParityTestBase
{
    [Fact]
    public async Task Tree_renders_five_rows_at_expected_depths()
    {
        await GotoHost("/e2e/gantt-v3-tree");
        var rows = Page.Locator("[data-testid='gantt-v3-tree-root'] [data-row-kind='task']");
        await rows.First.WaitForAsync(new() { Timeout = 15000 });
        await Assertions.Expect(rows).ToHaveCountAsync(5);

        await AssertDepth("Program Kickoff", 0);   // root1
        await AssertDepth("Design Phase", 16);      // child1 (depth 1)
        await AssertDepth("Wireframes", 32);        // grandchild1 (depth 2)
        await AssertDepth("Build Phase", 16);        // child2 (depth 1)
        await AssertDepth("Independent Task", 0);    // root2
    }

    [Fact]
    public async Task Collapsing_a_parent_hides_its_descendant_rows_bars_and_arrows()
    {
        await GotoHost("/e2e/gantt-v3-tree");
        var rows = Page.Locator("[data-testid='gantt-v3-tree-root'] [data-row-kind='task']");
        await rows.First.WaitForAsync(new() { Timeout = 15000 });

        var bars = Page.Locator("[data-testid='gantt-v3-tree-root'] [data-task-id]");
        var arrows = Page.Locator("[data-testid='gantt-v3-tree-root'] path.lumeo-gantt-v3-arrow");
        // Auto-retrying (Blazor's <Virtualize> can render a placeholder pass
        // before its real item count settles — a one-shot CountAsync() right
        // after the first row attaches occasionally raced that and read 0).
        await Assertions.Expect(bars).ToHaveCountAsync(5);
        await Assertions.Expect(arrows).ToHaveCountAsync(1); // child2 -> child1

        var root1Row = Page.Locator("[data-testid='gantt-v3-tree-root'] [data-row-kind='task']", new() { HasTextString = "Program Kickoff" });
        var toggle = root1Row.Locator("button.lumeo-gantt-v3-tree-toggle");
        await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "true");

        await toggle.ClickAsync();

        await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "false");
        // root1's 3 descendants (child1, grandchild1, child2) vanish from BOTH panes.
        await Assertions.Expect(rows).ToHaveCountAsync(2);
        await Assertions.Expect(bars).ToHaveCountAsync(2);
        await Assertions.Expect(arrows).ToHaveCountAsync(0); // child2->child1 arrow: both endpoints hidden
    }

    [Fact]
    public async Task Collapsing_And_Reexpanding_A_Tall_Group_Refreshes_Arrow_Culling_Without_Any_Scroll()
    {
        // Bug fix (Codex round 6, P1 #2): the arrow-culling window used to
        // ONLY recompute in response to a native 'scroll' event -
        // collapsing/expanding a hierarchy node changes the row count
        // WITHOUT ever firing one, so the window stayed at whatever it was
        // last computed against. GanttParityFixtures.TallHierarchyFixture's
        // root + 50 chained children (51 rows * 36px = 1836px) is tall
        // enough to need real virtualization/culling in the 900px pane,
        // unlike TreeTasks' own 5-row fixture (too short to ever
        // meaningfully cull anything either way - see
        // Collapsing_a_parent_hides_its_descendant_rows_bars_and_arrows above).
        await GotoHost("/e2e/gantt-v3-tree?fixture=tall");

        var rows = Page.Locator("[data-testid='gantt-v3-tree-root'] [data-row-kind='task']");
        await rows.First.WaitForAsync(new() { Timeout = 15000 });

        var arrows = Page.Locator("[data-testid='gantt-v3-tree-root'] path.lumeo-gantt-v3-arrow");
        // Fewer than the full 49-edge chain render initially - some rows
        // are scrolled out of the 900px pane, proving culling is genuinely
        // active for this fixture (unlike TreeTasks').
        await Assertions.Expect(arrows).Not.ToHaveCountAsync(49, new() { Timeout = 10000 });
        var beforeCount = await arrows.CountAsync();
        Assert.True(beforeCount > 0 && beforeCount < 49,
            $"expected the tall hierarchy fixture's 49-edge chain to be partially culled, got {beforeCount}");

        var rootRow = Page.Locator("[data-testid='gantt-v3-tree-root'] [data-row-kind='task']", new() { HasTextString = "Root" });
        var toggle = rootRow.Locator("button.lumeo-gantt-v3-tree-toggle");
        await toggle.ClickAsync(); // collapse - all 50 children hidden, row count drops to 1
        await Assertions.Expect(arrows).ToHaveCountAsync(0);

        await toggle.ClickAsync(); // re-expand - row count jumps back to 51, with NO scroll at all
        // If the culling window were still stuck at whatever it settled on
        // for the collapsed (1-row) state, every arrow would stay culled here.
        await Assertions.Expect(arrows).Not.ToHaveCountAsync(0, new() { Timeout = 10000 });
    }

    private async Task AssertDepth(string label, int expectedIndentPx)
    {
        var row = Page.Locator("[data-testid='gantt-v3-tree-root'] [data-row-kind='task']", new() { HasTextString = label });
        var style = await row.Locator(".lumeo-gantt-v3-tree-indent").GetAttributeAsync("style");
        Assert.Contains($"width:{expectedIndentPx}px", style);
    }

    private static ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);
}
