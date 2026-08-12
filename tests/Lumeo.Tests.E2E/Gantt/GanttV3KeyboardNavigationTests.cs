using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// v3-ONLY: real-browser coverage for arrow-key navigation (GanttV3's own
/// previously entirely-missing feature — only Enter/Space activation
/// existed). bUnit (<c>GanttV3KeyboardNavigationTests</c> in the main test
/// project) already proves the roving-tabindex attribute/gate-commit logic
/// directly; what ONLY a real browser can prove is that native DOM focus
/// (<c>document.activeElement</c>) actually MOVES to the target bar's own
/// element — bUnit's headless DOM has no focus manager at all, so
/// <c>ganttV3.focusBar</c>'s <c>element.focus()</c> call is entirely
/// unexercised there.
/// </summary>
public class GanttV3KeyboardNavigationTests : GanttParityTestBase
{
    private static async Task WaitForReady(IPage page)
    {
        var scrollPane = page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        await scrollPane.WaitForAsync(new() { Timeout = 15000 });
        await Assertions.Expect(scrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });
    }

    [Fact]
    public async Task ArrowDown_Moves_Real_Dom_Focus_To_The_Next_Bar()
    {
        await GotoHost("/e2e/gantt-v3?viewMode=Day&infiniteScroll=0");
        await WaitForReady(Page);

        // fe1's own inner content div — the roving-tabindex target (see
        // GanttBar.InnerAttributes' own remarks: tabindex lives on THIS
        // element, not the outer [data-task-id] wrapper).
        var fe1Bar = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe1'] > div[tabindex]").First;
        await fe1Bar.ScrollIntoViewIfNeededAsync();
        await fe1Bar.FocusAsync();
        await Assertions.Expect(fe1Bar).ToBeFocusedAsync();

        await Page.Keyboard.PressAsync("ArrowDown");

        // The PREVIOUSLY-focused bar drops to tabindex="-1" (roving —
        // GanttBar.FocusedTaskId's own contract) and native focus actually
        // moved off it, onto whichever bar row 2 renders.
        await Assertions.Expect(fe1Bar).Not.ToBeFocusedAsync();
        await Assertions.Expect(fe1Bar).ToHaveAttributeAsync("tabindex", "-1");

        var focusedTaskId = await Page.EvaluateAsync<string?>(
            "() => document.activeElement?.closest('[data-task-id]')?.getAttribute('data-task-id')");
        Assert.NotNull(focusedTaskId);
        Assert.NotEqual("fe1", focusedTaskId);

        // The newly-focused bar's OWN inner div is the one carrying
        // tabindex="0" now — confirms the roving tabindex actually moved
        // (not just that SOME element happens to have focus).
        var newFocused = Page.Locator($"[data-testid='gantt-v3-root'] [data-task-id='{focusedTaskId}'] > div[tabindex]").First;
        await Assertions.Expect(newFocused).ToHaveAttributeAsync("tabindex", "0");
        await Assertions.Expect(newFocused).ToBeFocusedAsync();
    }

    [Fact]
    public async Task ShiftArrowRight_Nudges_The_Focused_Task_Through_The_Real_OnTaskUpdate_Gate()
    {
        await GotoHost("/e2e/gantt-v3?viewMode=Day&infiniteScroll=0");
        await WaitForReady(Page);

        var fe1Bar = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe1'] > div[tabindex]").First;
        await fe1Bar.ScrollIntoViewIfNeededAsync();
        await fe1Bar.FocusAsync();

        // fe1: 2026-02-23 -> 2026-03-01 (GanttParityFixtures.SharedTasks).
        var wrapper = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe1']");
        await Assertions.Expect(wrapper).ToHaveAttributeAsync("data-task-start", "2026-02-23");
        await Assertions.Expect(wrapper).ToHaveAttributeAsync("data-task-end", "2026-03-01");

        await Page.Keyboard.DownAsync("Shift");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.UpAsync("Shift");

        // The REAL rendered bar's own data-task-start/-end attributes moved
        // by exactly one day — proves the keyboard nudge landed all the way
        // through GanttTimeline.CommitKeyboardTimingChangeAsync ->
        // OnTaskUpdate -> GanttChart.HandleTaskUpdateAsync -> a real
        // re-render, not just an in-memory event firing.
        await Assertions.Expect(wrapper).ToHaveAttributeAsync("data-task-start", "2026-02-24", new() { Timeout = 5000 });
        await Assertions.Expect(wrapper).ToHaveAttributeAsync("data-task-end", "2026-03-02");

        var sink = await Page.Locator("[data-testid='event-sink-taskupdate']").TextContentAsync();
        Assert.NotNull(sink);
        using var doc = JsonDocument.Parse(sink!);
        Assert.Equal("Keyboard", doc.RootElement.GetProperty("Source").GetString());
        Assert.Equal("fe1", doc.RootElement.GetProperty("Task").GetProperty("Id").GetString());
    }

    [Fact]
    public async Task Readonly_Chart_Ignores_Shift_Arrow_But_Keeps_Focus_Navigation()
    {
        await GotoHost("/e2e/gantt-v3?viewMode=Day&infiniteScroll=0&readonly=1");
        await WaitForReady(Page);

        var fe1Bar = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe1'] > div[tabindex]").First;
        await fe1Bar.ScrollIntoViewIfNeededAsync();
        await fe1Bar.FocusAsync();

        var wrapper = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe1']");
        await Assertions.Expect(wrapper).ToHaveAttributeAsync("data-task-start", "2026-02-23");

        await Page.Keyboard.DownAsync("Shift");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.UpAsync("Shift");
        // Give any (incorrect) commit a moment to land before asserting it didn't.
        await Page.WaitForTimeoutAsync(300);
        await Assertions.Expect(wrapper).ToHaveAttributeAsync("data-task-start", "2026-02-23"); // unchanged

        // Focus navigation (a VIEW action, not an edit) still works while readonly.
        await Page.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(fe1Bar).Not.ToBeFocusedAsync();
    }
}
