using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// Gantt v3 Phase 3, T8 — the <c>GanttSettingsMenu</c> companion,
/// <c>DragGhostTemplate</c>, and <c>BarContextMenu</c>. v3-ONLY (no v2
/// equivalent — same framing as <c>GanttV3RowSelectionReorderTests</c>/
/// <c>GanttV3CanvasChromeTests</c>). See <c>GanttV3Phase3T8Tests</c> (bUnit)
/// for the exhaustive component-level coverage (controlled/uncontrolled
/// idioms, Reset semantics, the OffDays empty-vs-null contract); this file
/// complements it with real-browser proof: a genuine settings-menu checkbox
/// click flipping a live chart feature, a real native 'contextmenu' event,
/// and a real pointer drag rendering the custom ghost content.
/// </summary>
public class GanttV3SettingsMenuTests : GanttParityTestBase
{
    private const int DayPxDay = 38;

    private ILocator Bar(string taskId) => Page.Locator($"[data-task-id='{taskId}']");

    // ── Settings menu: a real toggle flips a live chart feature ────────────

    [Fact]
    public async Task Settings_Menu_ShowZoomControl_Toggle_Flips_The_Live_Chart()
    {
        // ShowZoomControl defaults to false on this fixture (no ?showZoomControl=1),
        // and the settings menu's OWN uncontrolled default matches it — so the
        // floating zoom control starts absent.
        await GotoHost("/e2e/gantt-v3?tree=0&settingsMenu=1");
        await Bar("fe1").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        var zoomControl = Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-zoom-control");
        await Assertions.Expect(zoomControl).ToHaveCountAsync(0);

        await Page.Locator("button[aria-label='Settings']").ClickAsync();
        // Checkbox.razor's own markup: <button role="checkbox" id="X"/> + a
        // sibling <label for="X">. Resolving via that for/id relationship
        // (rather than DOM-position axes) is robust regardless of exact nesting.
        var labelEl = Page.Locator("label:has-text('Zoom control')");
        var forId = await labelEl.GetAttributeAsync("for");
        Assert.False(string.IsNullOrEmpty(forId));
        await Page.Locator($"#{forId}").ClickAsync();

        await Assertions.Expect(zoomControl).ToHaveCountAsync(1, new() { Timeout = 10000 });
    }

    // ── BarContextMenu: right-click opens at the pointer ────────────────────

    [Fact]
    public async Task Right_Click_A_Bar_Opens_The_Context_Menu_For_The_Correct_Task()
    {
        await GotoHost("/e2e/gantt-v3?tree=0&barContextMenu=1");
        var bar = Bar("fe4");
        await bar.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        await bar.ClickAsync(new() { Button = MouseButton.Right });

        var item = Page.Locator("[data-testid='gantt-v3-context-menu-item']");
        await Assertions.Expect(item).ToHaveCountAsync(1, new() { Timeout = 10000 });
        await Assertions.Expect(item).ToContainTextAsync("Hardening");

        await item.ClickAsync();
        await Assertions.Expect(Page.Locator("[data-testid='gantt-v3-context-menu-selected']")).ToHaveTextAsync("fe4", new() { Timeout = 10000 });
        // The menu closes itself after a selection (ContextMenuItem's own
        // "the enclosing menu also closes automatically" contract).
        await Assertions.Expect(item).ToHaveCountAsync(0, new() { Timeout = 10000 });
    }

    [Fact]
    public async Task Right_Click_A_Bar_Then_Escape_Closes_The_Menu_Without_Selecting()
    {
        await GotoHost("/e2e/gantt-v3?tree=0&barContextMenu=1");
        var bar = Bar("fe4");
        await bar.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        await bar.ClickAsync(new() { Button = MouseButton.Right });
        var item = Page.Locator("[data-testid='gantt-v3-context-menu-item']");
        await Assertions.Expect(item).ToHaveCountAsync(1, new() { Timeout = 10000 });

        // ContextMenuContent's own OnAfterRenderAsync focuses itself via an
        // ASYNC interop round-trip (JS module load + focus()) — the item
        // being present in the DOM does not itself prove focus has actually
        // landed there yet. Escape only reaches ContextMenuContent's own
        // @onkeydown once IT has focus; pressing it too early (T7's own
        // "don't read/act immediately after a pointer action" lesson,
        // applied here to a KEY action) can target whatever had focus
        // BEFORE the round-trip completes instead. Wait for the menu
        // surface itself to actually be focused first.
        await Assertions.Expect(Page.Locator("[role='menu']")).ToBeFocusedAsync(new() { Timeout = 10000 });

        await Page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(item).ToHaveCountAsync(0, new() { Timeout = 10000 });
        await Assertions.Expect(Page.Locator("[data-testid='gantt-v3-context-menu-selected']")).ToHaveTextAsync("");
    }

    // DISABLE-CHECK F target (design spec Phase 3, T8, decision 4 — right-click
    // vs. drag isolation): gantt-v3.js's own registerBarContextMenu checks
    // `activeBarDrags.has(barEl)` (the SAME module-level set registerDrag's own
    // onPointerDown populates at pointerdown, before any threshold is crossed)
    // and swallows the contextmenu entirely while a real gesture is in flight
    // on that bar. Verified live: with the guard temporarily removed, this
    // exact spec failed (the menu opened mid-drag) — restored, green again.
    // See the T8 report for the full predicted-vs-actual run.
    [Fact]
    public async Task A_ContextMenu_Never_Opens_While_A_Real_Drag_Is_In_Flight_On_The_Same_Bar()
    {
        await GotoHost("/e2e/gantt-v3?tree=0&barContextMenu=1");
        var bar = Bar("fe4");
        await bar.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        // See Custom_DragGhostTemplate_Is_Visible_During_A_Live_Drag's own
        // remarks — a raw Page.Mouse.* gesture needs the bar ACTUALLY
        // scrolled into the viewport first, unlike ClickAsync's own built-in
        // auto-scroll.
        await bar.ScrollIntoViewIfNeededAsync();
        var box = await bar.BoundingBoxAsync();
        Assert.NotNull(box);

        var x = box!.X + box.Width / 2;
        var y = box.Y + box.Height / 2;

        // A real pointerdown (left button) on the bar — activeBarDrags now
        // holds this bar's element, even before any movement crosses the
        // drag threshold (registerDrag's own onPointerDown adds it
        // immediately, see gantt-v3.js's own remarks).
        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();
        try
        {
            // A right-click landing on the SAME bar while the left button is
            // still held — a genuine CDP-level right button press/release at
            // the SAME point (Chromium synthesizes the native 'contextmenu'
            // event from this, the same as a real multi-button mouse would;
            // more faithful than a synthetic DispatchEventAsync, which some
            // engines' native context-menu machinery does not treat
            // identically to a trusted device event).
            await Page.Mouse.MoveAsync(x, y);
            await Page.Mouse.DownAsync(new() { Button = MouseButton.Right });
            await Page.Mouse.UpAsync(new() { Button = MouseButton.Right });

            await Page.WaitForTimeoutAsync(300); // give a (wrongly-opened) menu a real chance to appear
            await Assertions.Expect(Page.Locator("[data-testid='gantt-v3-context-menu-item']")).ToHaveCountAsync(0);
        }
        finally
        {
            await Page.Mouse.UpAsync();
        }

        // Sanity: a genuine right-click AFTER releasing works normally —
        // proves the guard is scoped to "drag in flight", not "broken
        // forever" for this bar.
        await bar.ClickAsync(new() { Button = MouseButton.Right });
        await Assertions.Expect(Page.Locator("[data-testid='gantt-v3-context-menu-item']")).ToHaveCountAsync(1, new() { Timeout = 10000 });
    }

    // ── DragGhostTemplate: custom ghost renders during a live drag ──────────

    [Fact]
    public async Task Custom_DragGhostTemplate_Is_Visible_During_A_Live_Drag()
    {
        await GotoHost("/e2e/gantt-v3?tree=0&dragGhostTemplate=1");
        var bar = Bar("fe4");
        await bar.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        // "fe4" (March 2026) sits far outside the initial auto-scroll target
        // (today, months later) — WaitForAsync(Visible) only proves the bar
        // is rendered/attached, not that it's actually scrolled into the
        // viewport (Playwright's "visible" means CSS-visible, not
        // viewport-intersecting). ScrollIntoViewIfNeededAsync (the same
        // helper GanttDragParityTests' own CenterAsync uses for the identical
        // reason) is required before a raw Page.Mouse.* gesture — unlike
        // ClickAsync, Mouse.MoveAsync/DownAsync operate on raw viewport
        // coordinates with no auto-scroll of their own.
        await bar.ScrollIntoViewIfNeededAsync();
        var box = await bar.BoundingBoxAsync();
        Assert.NotNull(box);

        var from = new { X = box!.X + box.Width / 2, Y = box.Y + box.Height / 2 };

        // ":visible" (Playwright's own CSS pseudo-class extension), not a bare
        // testid selector — DragGhostTemplate renders ONE hidden template
        // PER bar (GanttBar.razor's own remarks: a sibling of every bar, not
        // just the one that ends up dragged), so the bare selector matches
        // every task's own hidden template (12 for this fixture) even before
        // any drag starts, and matches BOTH the (still-hidden) templates AND
        // the live clone once a real drag makes gantt-v3.js unhide one of
        // them — only visibility distinguishes "the active ghost" from "an
        // idle template".
        var customGhost = Page.Locator("[data-testid='gantt-v3-custom-ghost']:visible");
        await Assertions.Expect(customGhost).ToHaveCountAsync(0);

        await Page.Mouse.MoveAsync(from.X, from.Y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(from.X + DayPxDay * 3, from.Y); // well past DRAG_THRESHOLD_PX (3)

        await Assertions.Expect(customGhost).ToHaveCountAsync(1, new() { Timeout = 10000 });
        // Same coordinate space as the real bar (design spec Phase 3, T8,
        // decision 3) — the cloned ghost should sit roughly where the real
        // bar + the dragged offset would put it, not at some unrelated
        // origin (the pinned Phase-2 "split coordinate space" bug class).
        var ghostBox = await customGhost.BoundingBoxAsync();
        Assert.NotNull(ghostBox);
        Assert.True(Math.Abs(ghostBox!.Y - box.Y) < 5,
            $"expected the custom ghost to stay on the same row as the dragged bar (Y~{box.Y}), got Y={ghostBox.Y}");
        Assert.True(ghostBox.X > box.X,
            $"expected the custom ghost to have moved right with the drag (bar X={box.X}), got ghost X={ghostBox.X}");

        await Page.Mouse.UpAsync();
        await Assertions.Expect(customGhost).ToHaveCountAsync(0, new() { Timeout = 10000 });
    }
}
