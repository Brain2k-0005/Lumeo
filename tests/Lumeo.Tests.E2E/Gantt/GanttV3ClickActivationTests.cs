using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// v3-ONLY: real-browser coverage for Codex round 5's P1 #1 fix (bar clicks
/// never reached OnTaskClick). The round-5 fix itself was only proven via
/// bUnit, which dispatches events directly to a specific rendered element and
/// does NOT simulate real DOM event bubbling — so it could confirm the inner
/// content div's OWN click handler fires, but not that a click landing
/// anywhere on the bar (as a real user's click would, hitting whichever
/// element is under the cursor) still reaches it via genuine bubbling through
/// Tooltip's own root. <see cref="Lumeo.Tests.ServerHost.Components.Pages.E2E.GanttV3Page"/>
/// wires <c>OnTaskClick</c> to a plain <c>data-testid="gantt-v3-last-clicked"</c>
/// sink specifically for this spec.
/// </summary>
public class GanttV3ClickActivationTests : GanttParityTestBase
{
    [Fact]
    public async Task Clicking_A_Bar_Fires_OnTaskClick_Through_Real_Dom_Bubbling()
    {
        await GotoHost("/e2e/gantt-v3?viewMode=Day"); // no ?fixture= -> GanttV3Page's default branch -> GanttParityFixtures.SharedTasks

        var scrollPane = Page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        await scrollPane.WaitForAsync(new() { Timeout = 15000 });
        await Assertions.Expect(scrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });

        var lastClicked = Page.Locator("[data-testid='gantt-v3-last-clicked']");
        await Assertions.Expect(lastClicked).ToHaveTextAsync("");

        var bar = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe1']");
        await bar.ScrollIntoViewIfNeededAsync();
        // A real Playwright click lands at the element's own visual center —
        // whichever DOM node is actually under that point (GanttBar's inner
        // content div, per round-5's fix) receives the native click first,
        // then it bubbles to Tooltip's own root exactly like a real user's
        // click would. This is precisely the path bUnit's direct event
        // dispatch can't exercise.
        await bar.ClickAsync();

        await Assertions.Expect(lastClicked).ToHaveTextAsync("fe1");
        // CRITICAL fix (Phase-1/Phase-2 reconciliation): GanttBar's own native
        // onclick used to ALWAYS fire alongside GanttTimeline's JS-driven
        // NotifyTaskClick for the identical physical pointer click — the sink
        // above only ever asserted the final VALUE (identical either way),
        // blind to a double invocation. Exactly one count proves the fix.
        await Assertions.Expect(Page.Locator("[data-testid='event-sink-click-count']")).ToHaveTextAsync("1");
    }

    [Fact]
    public async Task Space_Activates_A_Focused_Bar_Without_Scrolling_The_Viewport()
    {
        // Bug fix (Codex round 6, P2 #4): GanttBar's role="button" div has no
        // NATIVE Space-activation semantics, so the browser's own default
        // action for Space (scroll the nearest scrollable ancestor down by
        // roughly a viewport) fired ALONGSIDE HandleKeyDown's own
        // OnTaskClick invocation — a real <button> suppresses that default
        // automatically; a div never does on its own. Fixed via
        // RegisterPreventDefaultKeys (the same targeted, key-specific
        // interop every other Lumeo role=button-on-a-div component already
        // uses for this exact problem — see Card.razor's own precedent).
        await GotoHost("/e2e/gantt-v3?viewMode=Day");

        var scrollPane = Page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        await scrollPane.WaitForAsync(new() { Timeout = 15000 });
        await Assertions.Expect(scrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });

        var lastClicked = Page.Locator("[data-testid='gantt-v3-last-clicked']");
        // The bar's OWN interactive element is its inner content div (round
        // 5's fix moved tabindex/role/keydown there, off the outer Tooltip
        // wrapper) — see GanttBar.InnerAttributes' own remarks.
        //
        // Phase-1/Phase-2 reconciliation fix: "> div" alone used to be
        // unambiguous (GanttBar had exactly one div child) but no longer is —
        // Phase 2's progress-drag handle (GanttBar's own
        // "!Task.IsMilestone && !Readonly" markup) renders a SECOND direct
        // div child for any non-milestone, non-readonly bar like fe1,
        // triggering a Playwright strict-mode violation ("resolved to 2
        // elements"). [role='button'] disambiguates to the one InnerAttributes
        // actually sets it on — the SAME div this spec means to target.
        var bar = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe1'] > div[role='button']");
        await bar.ScrollIntoViewIfNeededAsync();
        await bar.FocusAsync();

        var scrollTopBefore = await scrollPane.EvaluateAsync<double>("el => el.scrollTop");
        await Page.Keyboard.PressAsync(" ");
        var scrollTopAfter = await scrollPane.EvaluateAsync<double>("el => el.scrollTop");

        Assert.Equal(scrollTopBefore, scrollTopAfter);
        await Assertions.Expect(lastClicked).ToHaveTextAsync("fe1");
        // CRITICAL fix (Phase-1/Phase-2 reconciliation): keyboard activation
        // NEVER routes through gantt-v3.js's pointer engine at all (it only
        // listens for pointerdown/pointermove/pointerup), so there was never
        // a double-fire risk here — this pins that exactly-once contract
        // explicitly rather than leaving it merely implied.
        await Assertions.Expect(Page.Locator("[data-testid='event-sink-click-count']")).ToHaveTextAsync("1");
    }
}
