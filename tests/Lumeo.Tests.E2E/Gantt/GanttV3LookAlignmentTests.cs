using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// Gantt v3 Phase 3, T10a — shadcn look-alignment plan (2026-08-10, §4). Real-
/// browser proof for the items that need it beyond bUnit: G1's ARIA change
/// (GanttNav's zoom switcher is now a <c>Segmented</c> radiogroup, not a
/// <c>ToggleGroup</c>) and G6's new <c>ShowTaskMeta</c> param. The look-only
/// items (G2 card shell, G3 row separators, G4 bar radius/tint/border/label
/// typography, G7 off-day header tint, G8 today-marker precision) are covered
/// by the regenerated visual baselines in
/// <see cref="Lumeo.Tests.E2E.Visual.GanttParityVisualTests"/> instead — a
/// pixel-diff proves them more directly than a DOM-class assertion would.
/// </summary>
public class GanttV3LookAlignmentTests : GanttParityTestBase
{
    // ── G1: Segmented swap — ARIA ────────────────────────────────────────────

    [Fact]
    public async Task Zoom_switcher_uses_a_radiogroup_of_radios_not_a_button_group()
    {
        await GotoHost("/e2e/gantt-v3?tree=0&infiniteScroll=0");
        await Page.Locator("[data-testid='gantt-v3-root'] [data-task-id]").First
            .WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 15000 });

        // Segmented's own root carries role="radiogroup" (Segmented.razor:5);
        // ToggleGroup's did not — this is the exact ARIA upgrade G1 promises
        // (radiogroup/radio is the correct pattern for an exclusive picker).
        var radiogroup = Page.Locator("[data-testid='gantt-v3-root'] [role='radiogroup']");
        await Assertions.Expect(radiogroup).ToHaveCountAsync(1);

        var dayRadio = Page.GetByRole(AriaRole.Radio, new() { Name = "Day", Exact = true });
        await Assertions.Expect(dayRadio).ToHaveCountAsync(1);
        await Assertions.Expect(dayRadio).ToHaveAttributeAsync("aria-checked", "true");

        var weekRadio = Page.GetByRole(AriaRole.Radio, new() { Name = "Week", Exact = true });
        await Assertions.Expect(weekRadio).ToHaveAttributeAsync("aria-checked", "false");

        // Clicking a radio still drives the SAME ViewMode contract Segmented
        // shares with ToggleGroup — the swap changed markup/ARIA, not behavior.
        await weekRadio.ClickAsync();
        await Assertions.Expect(weekRadio).ToHaveAttributeAsync("aria-checked", "true", new() { Timeout = 10000 });
    }

    // ── G6: ShowTaskMeta ─────────────────────────────────────────────────────

    [Fact]
    public async Task ShowTaskMeta_Renders_A_Real_DateRange_And_Progress_Line_In_The_Browser()
    {
        await GotoHost("/e2e/gantt-v3?showTaskMeta=1&infiniteScroll=0");
        await Page.Locator("[data-testid='gantt-v3-root'] [data-task-id]").First
            .WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 15000 });

        var meta = Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-tree-meta").First;
        await meta.WaitForAsync(new() { Timeout = 15000 });

        var text = (await meta.TextContentAsync())!;
        Assert.Contains("%", text);
        Assert.Matches(@"\d", text); // a real date/progress digit rendered, not an empty shell
    }

    [Fact]
    public async Task ShowTaskMeta_Absent_By_Default_Renders_No_Meta_Line()
    {
        // Deliberately no ?showTaskMeta=1 (and the tree pane stays visible,
        // unlike the ARIA test above) — proves the default-false param, not
        // merely "the tree pane is hidden".
        await GotoHost("/e2e/gantt-v3?infiniteScroll=0");
        await Page.Locator("[data-testid='gantt-v3-root'] [data-task-id]").First
            .WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 15000 });

        await Assertions.Expect(Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-tree-meta"))
            .ToHaveCountAsync(0);
    }
}
