using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// Phase 2, T4 — the drag/resize/progress/create parity suite (design spec
/// Phase 2's "Interactions" phase; plan <c>docs/superpowers/plans/2026-07-21-gantt-v3-phase2.md</c>
/// task T4). Drives real Playwright <see cref="IMouse"/> pointer gestures (not
/// JS-dispatched synthetic events) against BOTH <c>/e2e/gantt-v2</c> and
/// <c>/e2e/gantt-v3</c> — Chromium translates a real mouse device's
/// down/move/up into genuine <c>PointerEvent</c>s as well as the legacy mouse
/// events, so this exercises v2's <c>mousedown</c>/<c>mousemove</c>/<c>mouseup</c>
/// listeners and v3's <c>pointerdown</c>/<c>pointermove</c>/<c>pointerup</c>
/// listeners identically to a real user.
///
/// Event sink: both host pages (Phase 2, T4 addition — see
/// <c>GanttV2Page.razor</c>/<c>GanttV3Page.razor</c>'s own remarks) render the
/// LAST OnTaskClick/OnDateChange/OnProgressChange (both routes) and
/// OnTaskUpdate/OnTaskCreate/TasksChanged (v3-only) payload as JSON into hidden
/// <c>data-testid="event-sink-*"</c> elements, so a committed edit's exact
/// dates/progress/ParentId/GroupLabel can be asserted (and compared between
/// routes for the identical gesture) without reaching into server-side state.
///
/// Fixture task <c>fe3</c> ("Integration", 2026-03-08 .. 2026-03-15, progress 0,
/// no dependencies, group "Frontend") is this suite's primary drag target — a
/// clean candidate with nothing else (arrows, milestone rendering) to disturb
/// the assertions. <c>fe1</c>/<c>fe2</c> (the one real dependency edge with an
/// adjacent row) are reserved for the arrow-reroute spec (pin item H); other
/// tasks are used where a fresh, untouched task is needed (pin item K).
///
/// Pin-item map (design spec Phase 2 T4 dispatch, letters A-K):
///   A: <see cref="Move_drag_shifts_dates_identically_between_v2_and_v3"/>
///   B: <see cref="Resize_right_edge_shifts_the_end_date_identically_between_v2_and_v3"/>,
///      <see cref="Resize_left_edge_is_a_v3_only_addition_v2_has_no_left_resize_at_all"/>
///   C: <see cref="Progress_drag_commits_the_same_rounded_percent_on_both_routes"/>
///   D: <see cref="Sub_threshold_interaction_on_the_move_zone_leaves_no_residue_no_commit_but_still_fires_a_click"/>,
///      <see cref="Sub_threshold_interaction_on_resize_and_progress_zones_leaves_no_residue_no_commit_and_no_click"/>
///   E: <see cref="Milestone_pointerdown_drag_moves_the_whole_bar_in_v3_but_v2_has_no_milestone_drag_at_all"/>
///   F: <see cref="Readonly_blocks_every_real_drag_gesture_on_both_routes"/>,
///      <see cref="Readonly_milestone_click_fires_on_v2_but_not_on_v3"/>
///   G: <see cref="No_move_ghost_survives_a_completed_drag"/>,
///      <see cref="Pointercancel_mid_drag_removes_the_ghost_and_commits_nothing"/>
///   H: <see cref="Arrow_endpoints_reroute_correctly_after_a_move_drag"/>
///   I: <see cref="CanDrop_ghost_shows_invalid_over_the_blackout_window_and_clears_when_dragged_back_to_valid"/>,
///      <see cref="CanDrop_dropping_on_an_invalid_position_reverts_silently"/>,
///      <see cref="CanDrop_never_validates_when_unset"/>,
///      <see cref="CanDrop_validates_exactly_once_per_distinct_snapped_position_under_a_jittery_drag"/>,
///      <see cref="CanDrop_never_validates_during_a_progress_or_a_create_drag"/>
///   J: <see cref="Drag_create_beside_an_existing_bar_on_an_occupied_row_creates_a_snapped_task"/>,
///      <see cref="Drag_create_after_panning_computes_dates_from_the_new_origin"/>,
///      <see cref="Row_track_divs_align_with_their_rows_for_leaf_summary_and_group_header_contexts"/>,
///      <see cref="Sub_threshold_interaction_on_an_empty_track_creates_nothing"/>
///   K: <see cref="Task_click_fires_with_the_current_payload_on_both_routes"/>,
///      <see cref="A_completed_drag_never_also_fires_a_click_and_the_next_genuine_click_still_works"/>
///
/// Interpretation call (pin item D): the dispatch's "NO ghost/commit/click"
/// wording is read as applying its "no click" clause to the resize/progress
/// zones specifically — a sub-threshold interaction on the MOVE zone is v2/v3's
/// own documented click-vs-drag boundary (T1/T2 reports: "a below-threshold
/// 'move'-mode mousedown falls back to a click"), which pin item K's own specs
/// exercise directly. Treating "no click" as universal would contradict K and
/// the shipped, reviewed T1/T2 design — flagged in the report.
/// </summary>
public class GanttDragParityTests : GanttParityTestBase
{
    private const string V2Root = "[data-testid='gantt-v2-root']";
    private const string V3Root = "[data-testid='gantt-v3-root']";
    private const string TreeRoot = "[data-testid='gantt-v3-tree-root']";

    // GanttScale.PixelsPerDay for Day/Week/Month (colW/step, colW/30 — see that
    // method's own remarks) — hardcoded here (not referenced: GanttScale is
    // internal to Lumeo.Gantt) purely to compute DETERMINISTIC, exact-integer
    // day shifts for the pixel deltas this suite drives; not a re-derivation of
    // the production formula used for correctness (the assertions compare
    // against the SINK's actual committed dates, not against a hand re-run of
    // GanttScale itself).
    private const int DayPxDay = 38;
    private const int DayPxWeek = 20;
    private const int DayPxMonth = 4;

    private static readonly JsonSerializerOptions SinkJson = new() { PropertyNameCaseInsensitive = true };

    private sealed record SinkTask(
        string Id, string Name, DateTime Start, DateTime End, int Progress,
        string[]? Dependencies, string? CustomClass, bool IsMilestone, string? GroupLabel, string? ParentId);

    private sealed record SinkUpdate(SinkTask Task, string Source);

    // ── Navigation / readiness ──────────────────────────────────────────────

    private async Task WaitV2ReadyAsync() =>
        await Page.Locator($"{V2Root} g.lumeo-gantt-bar-wrapper").First
            .WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 15000 });

    private async Task WaitV3ReadyAsync() =>
        await Page.Locator($"{V3Root} [data-task-id]").First
            .WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 15000 });

    // ── Geometry helpers ─────────────────────────────────────────────────────

    private async Task<(float X, float Y)> CenterAsync(ILocator locator)
    {
        await locator.ScrollIntoViewIfNeededAsync();
        var box = await locator.BoundingBoxAsync();
        Assert.NotNull(box);
        return ((float)(box!.X + box.Width / 2), (float)(box.Y + box.Height / 2));
    }

    private async Task<(float X, float Y)> NearRightEdgeAsync(ILocator locator, double inset = 3)
    {
        await locator.ScrollIntoViewIfNeededAsync();
        var box = await locator.BoundingBoxAsync();
        Assert.NotNull(box);
        return ((float)(box!.X + box.Width - inset), (float)(box.Y + box.Height / 2));
    }

    private async Task<(float X, float Y)> NearLeftEdgeAsync(ILocator locator, double inset = 3)
    {
        await locator.ScrollIntoViewIfNeededAsync();
        var box = await locator.BoundingBoxAsync();
        Assert.NotNull(box);
        return ((float)(box!.X + inset), (float)(box.Y + box.Height / 2));
    }

    // ── Mouse gesture helpers (real IMouse actions, not JS dispatchEvent) ────

    private async Task DragAsync((float X, float Y) from, (float X, float Y) to)
    {
        await Page.Mouse.MoveAsync(from.X, from.Y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(to.X, to.Y);
        await Page.Mouse.UpAsync();
    }

    // ── Sink helpers ─────────────────────────────────────────────────────────

    private ILocator Sink(string testId) => Page.Locator($"[data-testid='{testId}']");

    private async Task<string?> ReadSinkRawAsync(string testId) => await Sink(testId).TextContentAsync();

    private async Task<string> WaitForSinkChangeAsync(string testId, string? previous)
    {
        var locator = Sink(testId);
        if (string.IsNullOrEmpty(previous))
            await Assertions.Expect(locator).Not.ToHaveTextAsync("", new() { Timeout = 10000 });
        else
            await Assertions.Expect(locator).Not.ToHaveTextAsync(previous, new() { Timeout = 10000 });
        return (await locator.TextContentAsync())!;
    }

    private static SinkTask ParseTask(string json) => JsonSerializer.Deserialize<SinkTask>(json, SinkJson)!;
    private static SinkUpdate ParseUpdate(string json) => JsonSerializer.Deserialize<SinkUpdate>(json, SinkJson)!;

    // ── A: move drag, all 3 modes, dates equal v2/v3, duration preserved ────

    // dx (screen pixels) = expectedDayShift * pixelsPerDay(mode) — NOT "N columns":
    // a "column" is 38px in Day mode (1 day/column) but 140px in Week mode (7
    // days/column) and 120px in Month mode (~30 days/column), so driving a
    // fixed number of COLUMNS would require a different multiplier per mode
    // anyway. Computing dx directly from the desired day shift via each mode's
    // PixelsPerDay (Day=38, Week=140/7=20, Month=120/30=4) is both simpler and
    // exactly what v2/v3's own commit math inverts (Math.round(dx/pixelsPerDay)).
    [Theory]
    [InlineData("Day", DayPxDay * 3, 3)]       // 3 days @ 38px/day = 114px
    [InlineData("Week", DayPxWeek * 14, 14)]   // 14 days @ 20px/day = 280px (2 Week columns)
    [InlineData("Month", DayPxMonth * 30, 30)] // 30 days @ 4px/day = 120px (1 Month column)
    public async Task Move_drag_shifts_dates_identically_between_v2_and_v3(string viewMode, int dxPixels, int expectedDayShift)
    {
        var origStart = new DateTime(2026, 3, 8);
        var origEnd = new DateTime(2026, 3, 15);
        var expectedStart = origStart.AddDays(expectedDayShift);
        var expectedEnd = origEnd.AddDays(expectedDayShift);

        await GotoHost($"/e2e/gantt-v2?viewMode={viewMode}");
        await WaitV2ReadyAsync();
        var v2Bar = Page.Locator($"{V2Root} g.lumeo-gantt-bar-wrapper[data-task-id='fe3'] rect.lumeo-gantt-bar-bg");
        var v2Center = await CenterAsync(v2Bar);
        await DragAsync(v2Center, (v2Center.X + dxPixels, v2Center.Y));
        var v2Json = await WaitForSinkChangeAsync("event-sink-datechange", null);
        var v2Task = ParseTask(v2Json);

        await GotoHost($"/e2e/gantt-v3?viewMode={viewMode}");
        await WaitV3ReadyAsync();
        var v3Bar = Page.Locator($"{V3Root} [data-task-id='fe3']");
        var v3Center = await CenterAsync(v3Bar);
        await DragAsync(v3Center, (v3Center.X + dxPixels, v3Center.Y));
        var v3Json = await WaitForSinkChangeAsync("event-sink-datechange", null);
        var v3Task = ParseTask(v3Json);

        Assert.Equal(expectedStart, v2Task.Start);
        Assert.Equal(expectedEnd, v2Task.End);
        Assert.Equal(expectedStart, v3Task.Start);
        Assert.Equal(expectedEnd, v3Task.End);
        Assert.Equal(origEnd - origStart, v2Task.End - v2Task.Start);
        Assert.Equal(origEnd - origStart, v3Task.End - v3Task.Start);
    }

    // ── B: resize right (both routes); resize left (v3-only, no v2 equivalent) ──

    [Fact]
    public async Task Resize_right_edge_shifts_the_end_date_identically_between_v2_and_v3()
    {
        var expectedEnd = new DateTime(2026, 3, 17); // 03-15 + 2 days
        const int dx = DayPxDay * 2;

        await GotoHost("/e2e/gantt-v2");
        await WaitV2ReadyAsync();
        var v2Handle = Page.Locator($"{V2Root} g.lumeo-gantt-bar-wrapper[data-task-id='fe3'] rect.lumeo-gantt-resize");
        var v2From = await CenterAsync(v2Handle);
        await DragAsync(v2From, (v2From.X + dx, v2From.Y));
        var v2Json = await WaitForSinkChangeAsync("event-sink-datechange", null);
        var v2Task = ParseTask(v2Json);

        await GotoHost("/e2e/gantt-v3");
        await WaitV3ReadyAsync();
        var v3Bar = Page.Locator($"{V3Root} [data-task-id='fe3']");
        var v3From = await NearRightEdgeAsync(v3Bar);
        await DragAsync(v3From, (v3From.X + dx, v3From.Y));
        var v3Json = await WaitForSinkChangeAsync("event-sink-taskupdate", null);
        var v3Update = ParseUpdate(v3Json);

        Assert.Equal(new DateTime(2026, 3, 8), v2Task.Start);
        Assert.Equal(expectedEnd, v2Task.End);
        Assert.Equal("ResizeEnd", v3Update.Source);
        Assert.Equal(new DateTime(2026, 3, 8), v3Update.Task.Start);
        Assert.Equal(expectedEnd, v3Update.Task.End);
    }

    [Fact]
    public async Task Resize_left_edge_is_a_v3_only_addition_v2_has_no_left_resize_at_all()
    {
        // v2 has exactly ONE resize handle (right edge only, gantt-v2.js:556-562)
        // — no left-edge hit zone exists to drag at all, so this is a REUI-parity
        // v3 addition (T1 report design decision), asserted here as v3-only
        // behavior, not a v2/v3 comparison.
        var expectedStart = new DateTime(2026, 3, 10); // 03-08 + 2 days
        const int dx = DayPxDay * 2;

        await GotoHost("/e2e/gantt-v3");
        await WaitV3ReadyAsync();
        var bar = Page.Locator($"{V3Root} [data-task-id='fe3']");
        var from = await NearLeftEdgeAsync(bar);
        await DragAsync(from, (from.X + dx, from.Y));
        var json = await WaitForSinkChangeAsync("event-sink-taskupdate", null);
        var update = ParseUpdate(json);

        Assert.Equal("ResizeStart", update.Source);
        Assert.Equal(expectedStart, update.Task.Start);
        Assert.Equal(new DateTime(2026, 3, 15), update.Task.End);
    }

    // ── C: progress drag, payload parity ────────────────────────────────────

    [Fact]
    public async Task Progress_drag_commits_the_same_rounded_percent_on_both_routes()
    {
        // fe3's bar width in Day mode is (end+1day - start) * 38px = 8 days * 38 = 304px.
        // Dragging the progress handle (initially at the LEFT edge — progress=0%)
        // right by exactly half that width lands on precisely 50% for both
        // routes' Math.round-based commit formula (v2 gantt-v2.js:758; v3
        // gantt-v3.js's onPointerUp progress branch).
        const int dx = 152;

        await GotoHost("/e2e/gantt-v2");
        await WaitV2ReadyAsync();
        var v2Handle = Page.Locator($"{V2Root} g.lumeo-gantt-bar-wrapper[data-task-id='fe3'] circle.lumeo-gantt-progress-handle");
        var v2From = await CenterAsync(v2Handle);
        await DragAsync(v2From, (v2From.X + dx, v2From.Y));
        var v2Json = await WaitForSinkChangeAsync("event-sink-progresschange", null);
        var v2Task = ParseTask(v2Json);

        await GotoHost("/e2e/gantt-v3");
        await WaitV3ReadyAsync();
        var v3Handle = Page.Locator($"{V3Root} [data-task-id='fe3'] [data-gantt-progress-handle]");
        var v3From = await CenterAsync(v3Handle);
        await DragAsync(v3From, (v3From.X + dx, v3From.Y));
        var v3Json = await WaitForSinkChangeAsync("event-sink-progresschange", null);
        var v3Task = ParseTask(v3Json);

        Assert.Equal(50, v2Task.Progress);
        Assert.Equal(50, v3Task.Progress);
    }

    // ── D: sub-threshold interactions ───────────────────────────────────────

    [Fact]
    public async Task Sub_threshold_interaction_on_the_move_zone_leaves_no_residue_no_commit_but_still_fires_a_click()
    {
        // See class remarks' "Interpretation call" — the move zone's sub-threshold
        // behavior is v2/v3's documented click-vs-drag boundary, not "no click".
        await GotoHost("/e2e/gantt-v3");
        await WaitV3ReadyAsync();
        var bar = Page.Locator($"{V3Root} [data-task-id='fe4']"); // untouched by other tests
        var center = await CenterAsync(bar);

        await Page.Mouse.MoveAsync(center.X, center.Y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(center.X + 1, center.Y); // 1px — below DRAG_THRESHOLD_PX (3)
        Assert.Equal(0, await Page.Locator(".lumeo-gantt-v3-drag-ghost").CountAsync()); // no ghost mid-gesture
        await Page.Mouse.UpAsync();

        Assert.True(string.IsNullOrEmpty(await ReadSinkRawAsync("event-sink-taskupdate")));
        Assert.Equal(0, await Page.Locator(".lumeo-gantt-v3-drag-ghost").CountAsync());

        var clickJson = await WaitForSinkChangeAsync("event-sink-click", null);
        Assert.Equal("fe4", ParseTask(clickJson).Id);
    }

    [Fact]
    public async Task Sub_threshold_interaction_on_resize_and_progress_zones_leaves_no_residue_no_commit_and_no_click()
    {
        await GotoHost("/e2e/gantt-v3");
        await WaitV3ReadyAsync();
        var bar = Page.Locator($"{V3Root} [data-task-id='fe5']"); // untouched by other tests

        var resizeFrom = await NearRightEdgeAsync(bar);
        await Page.Mouse.MoveAsync(resizeFrom.X, resizeFrom.Y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(resizeFrom.X + 1, resizeFrom.Y);
        await Page.Mouse.UpAsync();
        Assert.Equal(0, await Page.Locator(".lumeo-gantt-v3-drag-ghost").CountAsync());
        Assert.True(string.IsNullOrEmpty(await ReadSinkRawAsync("event-sink-taskupdate")));
        Assert.True(string.IsNullOrEmpty(await ReadSinkRawAsync("event-sink-click")));

        var handle = Page.Locator($"{V3Root} [data-task-id='fe5'] [data-gantt-progress-handle]");
        var progressFrom = await CenterAsync(handle);
        await Page.Mouse.MoveAsync(progressFrom.X, progressFrom.Y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(progressFrom.X + 1, progressFrom.Y);
        await Page.Mouse.UpAsync();
        Assert.Equal(0, await Page.Locator(".lumeo-gantt-v3-drag-ghost").CountAsync());
        Assert.True(string.IsNullOrEmpty(await ReadSinkRawAsync("event-sink-taskupdate")));
        Assert.True(string.IsNullOrEmpty(await ReadSinkRawAsync("event-sink-click")));
    }

    // ── E: milestone edge pointer-down moves (never resizes), v2 has none ──

    [Fact]
    public async Task Milestone_pointerdown_drag_moves_the_whole_bar_in_v3_but_v2_has_no_milestone_drag_at_all()
    {
        const int dx = DayPxDay * 2;

        await GotoHost("/e2e/gantt-v3");
        await WaitV3ReadyAsync();
        var milestone = Page.Locator($"{V3Root} [data-task-id='fe-ms'][data-milestone='true']");
        var from = await NearLeftEdgeAsync(milestone, inset: 2); // "edge" pointer-down, per pin item E
        await DragAsync(from, (from.X + dx, from.Y));
        var json = await WaitForSinkChangeAsync("event-sink-taskupdate", null);
        var update = ParseUpdate(json);

        Assert.Equal("Move", update.Source); // moved, not resized — both Start AND End shift together
        Assert.Equal(new DateTime(2026, 3, 10), update.Task.Start);
        Assert.Equal(new DateTime(2026, 3, 10), update.Task.End);

        // Pinned delta (T1 report design decision #5): v2's milestone <g> only
        // ever registers mouseenter/mouseleave/click — never mousedown — so a
        // real drag gesture on it is structurally a no-op in v2. Asserted here,
        // not just documented, so a future accidental v2 change would be caught.
        await GotoHost("/e2e/gantt-v2");
        await WaitV2ReadyAsync();
        var v2Milestone = Page.Locator($"{V2Root} g.lumeo-gantt-bar-wrapper[data-task-id='fe-ms']");
        var v2From = await CenterAsync(v2Milestone);
        await DragAsync(v2From, (v2From.X + dx, v2From.Y));
        Assert.True(string.IsNullOrEmpty(await ReadSinkRawAsync("event-sink-datechange")));
    }

    // ── F: readonly ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Readonly_blocks_every_real_drag_gesture_on_both_routes()
    {
        const int dx = DayPxDay * 3;

        await GotoHost("/e2e/gantt-v2?readonly=1");
        await WaitV2ReadyAsync();
        var v2Bar = Page.Locator($"{V2Root} g.lumeo-gantt-bar-wrapper[data-task-id='fe3'] rect.lumeo-gantt-bar-bg");
        var v2OrigX = await v2Bar.GetAttributeAsync("x");
        var v2From = await CenterAsync(v2Bar);
        await DragAsync(v2From, (v2From.X + dx, v2From.Y));
        Assert.True(string.IsNullOrEmpty(await ReadSinkRawAsync("event-sink-datechange")));
        Assert.Equal(v2OrigX, await v2Bar.GetAttributeAsync("x")); // no visual mutation either

        await GotoHost("/e2e/gantt-v3?readonly=1");
        await WaitV3ReadyAsync();
        var v3Bar = Page.Locator($"{V3Root} [data-task-id='fe3']");
        var v3From = await CenterAsync(v3Bar);
        await Page.Mouse.MoveAsync(v3From.X, v3From.Y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(v3From.X + dx, v3From.Y);
        Assert.Equal(0, await Page.Locator(".lumeo-gantt-v3-drag-ghost").CountAsync()); // no listener -> no ghost, mid-gesture
        await Page.Mouse.UpAsync();
        Assert.True(string.IsNullOrEmpty(await ReadSinkRawAsync("event-sink-taskupdate")));
    }

    [Fact]
    public async Task Readonly_milestone_click_fires_on_v2_but_not_on_v3()
    {
        // Pinned delta: v2's milestone click listener is UNCONDITIONAL (never
        // guarded by readonly, gantt-v2.js:501-503); v3's Readonly unregisters
        // the ENTIRE delegated pointerdown listener (T1's "no listeners at all"
        // contract), so a real click produces nothing at all in v3. See
        // GanttTimeline.NotifyTaskClick's own remarks for the full rationale.
        await GotoHost("/e2e/gantt-v2?readonly=1");
        await WaitV2ReadyAsync();
        // Click the polygon itself, not the wrapping <g>: the group's own
        // bounding box spans BOTH the diamond AND its label text (rendered to
        // the diamond's right, gantt-v2.js:486-495), so a click at the GROUP's
        // computed center can land in the empty gap between them — the bare
        // <svg> canvas underneath then intercepts the pointer event instead.
        var v2Milestone = Page.Locator($"{V2Root} g.lumeo-gantt-bar-wrapper[data-task-id='fe-ms'] polygon");
        await v2Milestone.ScrollIntoViewIfNeededAsync();
        await v2Milestone.ClickAsync();
        var v2Json = await WaitForSinkChangeAsync("event-sink-click", null);
        Assert.Equal("fe-ms", ParseTask(v2Json).Id);

        await GotoHost("/e2e/gantt-v3?readonly=1");
        await WaitV3ReadyAsync();
        var v3Milestone = Page.Locator($"{V3Root} [data-task-id='fe-ms']");
        await v3Milestone.ScrollIntoViewIfNeededAsync();
        await v3Milestone.ClickAsync();
        Assert.True(string.IsNullOrEmpty(await ReadSinkRawAsync("event-sink-click")));
    }

    // ── G: post-drop DOM cleanliness ────────────────────────────────────────

    [Fact]
    public async Task No_move_ghost_survives_a_completed_drag()
    {
        await GotoHost("/e2e/gantt-v3");
        await WaitV3ReadyAsync();
        var bar = Page.Locator($"{V3Root} [data-task-id='be3']"); // untouched by other tests
        var from = await CenterAsync(bar);

        await Page.Mouse.MoveAsync(from.X, from.Y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(from.X + DayPxDay * 2, from.Y);
        Assert.Equal(1, await Page.Locator(".lumeo-gantt-v3-drag-ghost").CountAsync()); // present mid-drag
        await Page.Mouse.UpAsync();

        await WaitForSinkChangeAsync("event-sink-taskupdate", null);
        Assert.Equal(0, await Page.Locator(".lumeo-gantt-v3-drag-ghost").CountAsync());
    }

    [Fact]
    public async Task Pointercancel_mid_drag_removes_the_ghost_and_commits_nothing()
    {
        await GotoHost("/e2e/gantt-v3");
        await WaitV3ReadyAsync();
        var bar = Page.Locator($"{V3Root} [data-task-id='be4']"); // untouched by other tests
        var from = await CenterAsync(bar);

        await Page.Mouse.MoveAsync(from.X, from.Y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(from.X + DayPxDay * 2, from.Y);
        Assert.Equal(1, await Page.Locator(".lumeo-gantt-v3-drag-ghost").CountAsync());

        var barHandle = await bar.ElementHandleAsync();
        await Page.EvaluateAsync(
            "el => el.dispatchEvent(new PointerEvent('pointercancel', { bubbles: true, cancelable: true, pointerId: 1 }))",
            barHandle);

        await Assertions.Expect(Page.Locator(".lumeo-gantt-v3-drag-ghost")).ToHaveCountAsync(0, new() { Timeout = 5000 });
        await Page.Mouse.UpAsync(); // leave the real mouse-button state clean for subsequent tests

        Assert.True(string.IsNullOrEmpty(await ReadSinkRawAsync("event-sink-taskupdate")));
    }

    // ── H: arrow reroute after move ──────────────────────────────────────────

    [Fact]
    public async Task Arrow_endpoints_reroute_correctly_after_a_move_drag()
    {
        const int dx = DayPxDay * 3;
        var origin = GanttDayModeMath.Origin(new DateTime(2026, 2, 23)); // fe1.Start

        await GotoHost("/e2e/gantt-v3");
        await WaitV3ReadyAsync();
        var fe2Bar = Page.Locator($"{V3Root} [data-task-id='fe2']");
        var from = await CenterAsync(fe2Bar);
        await DragAsync(from, (from.X + dx, from.Y));
        await WaitForSinkChangeAsync("event-sink-taskupdate", null);

        var newFe2Start = new DateTime(2026, 3, 4);
        var newFe2End = new DateTime(2026, 3, 11);
        var (fe1X, fe1W) = GanttDayModeMath.BarGeometry(origin, new DateTime(2026, 2, 23), new DateTime(2026, 3, 1), isMilestone: false);
        var (fe2X, fe2W) = GanttDayModeMath.BarGeometry(origin, newFe2Start, newFe2End, isMilestone: false);
        var expected = GanttDayModeMath.ArrowPath((fe1X, fe1W, 1), (fe2X, fe2W, 2));

        var d = await Page.Locator($"{V3Root} path.lumeo-gantt-v3-arrow[data-arrow-from='fe1'][data-arrow-to='fe2']").GetAttributeAsync("d");
        Assert.NotNull(d);
        var actual = GanttDayModeMath.ParsePathD(d!);

        for (var i = 0; i < expected.Length; i++)
        {
            Assert.True(Math.Abs(expected[i].X - actual[i].X) <= 1.0, $"point[{i}].X");
            Assert.True(Math.Abs(expected[i].Y - actual[i].Y) <= 1.0, $"point[{i}].Y");
        }
    }

    // ── I: CanDrop (v3-only) ─────────────────────────────────────────────────

    [Fact]
    public async Task CanDrop_ghost_shows_invalid_over_the_blackout_window_and_clears_when_dragged_back_to_valid()
    {
        await GotoHost("/e2e/gantt-v3?candrop=1");
        await WaitV3ReadyAsync();
        var bar = Page.Locator($"{V3Root} [data-task-id='fe3']");
        var start = await CenterAsync(bar);
        var ghost = Page.Locator(".lumeo-gantt-v3-drag-ghost");

        await Page.Mouse.MoveAsync(start.X, start.Y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(start.X - DayPxDay * 4, start.Y); // -4 days: Start 03-04, invalid (< blackout 03-05)
        await Assertions.Expect(ghost).ToHaveAttributeAsync("data-invalid", "true", new() { Timeout = 5000 });

        await Page.Mouse.MoveAsync(start.X - DayPxDay * 3, start.Y); // -3 days: Start 03-05, valid (boundary)
        await Assertions.Expect(ghost).Not.ToHaveAttributeAsync("data-invalid", "true", new() { Timeout = 5000 });

        await Page.Mouse.UpAsync();
        var json = await WaitForSinkChangeAsync("event-sink-taskupdate", null);
        Assert.Equal(new DateTime(2026, 3, 5), ParseUpdate(json).Task.Start);
    }

    [Fact]
    public async Task CanDrop_dropping_on_an_invalid_position_reverts_silently()
    {
        await GotoHost("/e2e/gantt-v3?candrop=1");
        await WaitV3ReadyAsync();
        var bar = Page.Locator($"{V3Root} [data-task-id='fe3']");
        var start = await CenterAsync(bar);
        var ghost = Page.Locator(".lumeo-gantt-v3-drag-ghost");

        await Page.Mouse.MoveAsync(start.X, start.Y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(start.X - DayPxDay * 4, start.Y); // invalid position
        await Assertions.Expect(ghost).ToHaveAttributeAsync("data-invalid", "true", new() { Timeout = 5000 });
        await Page.Mouse.UpAsync();

        await Assertions.Expect(ghost).ToHaveCountAsync(0, new() { Timeout = 5000 }); // cleanup still runs on revert
        Assert.True(string.IsNullOrEmpty(await ReadSinkRawAsync("event-sink-taskupdate")));
        Assert.True(string.IsNullOrEmpty(await ReadSinkRawAsync("event-sink-datechange")));
    }

    [Fact]
    public async Task CanDrop_never_validates_when_unset()
    {
        // Default v3 route: CanDrop is never wired (Gantt3.CanDrop == null), so
        // BuildDragOptions' hasCanDrop is false and gantt-v3.js's onPointerMove
        // never even attempts a ValidateDrop call — proven here via the REAL
        // interop call-count sink (only populated when ?candrop=1 wires the
        // predicate at all; on THIS route it's the untouched default field, "0").
        await GotoHost("/e2e/gantt-v3");
        await WaitV3ReadyAsync();
        var bar = Page.Locator($"{V3Root} [data-task-id='fe3']");
        var from = await CenterAsync(bar);
        await DragAsync(from, (from.X + DayPxDay * 3, from.Y));
        await WaitForSinkChangeAsync("event-sink-taskupdate", null);

        // candrop-call-count doesn't exist as a meaningful sink on the non-candrop
        // route (the field is never touched, so it always renders its default "0").
        Assert.Equal("0", await ReadSinkRawAsync("candrop-call-count"));
    }

    [Fact]
    public async Task CanDrop_validates_exactly_once_per_distinct_snapped_position_under_a_jittery_drag()
    {
        await GotoHost("/e2e/gantt-v3?candrop=1");
        await WaitV3ReadyAsync();
        var bar = Page.Locator($"{V3Root} [data-task-id='fe3']");
        var start = await CenterAsync(bar);

        await Page.Mouse.MoveAsync(start.X, start.Y);
        await Page.Mouse.DownAsync();
        // -1 day (03-07, valid), jittered 3x at the SAME snapped position:
        await Page.Mouse.MoveAsync(start.X - DayPxDay * 1, start.Y);
        await Page.Mouse.MoveAsync(start.X - DayPxDay * 1 - 2, start.Y);
        await Page.Mouse.MoveAsync(start.X - DayPxDay * 1 + 2, start.Y);
        // -2 days (03-06, valid) — a genuinely NEW snapped position:
        await Page.Mouse.MoveAsync(start.X - DayPxDay * 2, start.Y);
        await Assertions.Expect(Sink("candrop-call-count")).ToHaveTextAsync("2", new() { Timeout = 5000 });

        await Page.Mouse.UpAsync(); // release at the already-cached -2 day position — no 3rd call
        var json = await WaitForSinkChangeAsync("event-sink-taskupdate", null);
        Assert.Equal(new DateTime(2026, 3, 6), ParseUpdate(json).Task.Start);
        Assert.Equal("2", await ReadSinkRawAsync("candrop-call-count"));
    }

    [Fact]
    public async Task CanDrop_never_validates_during_a_progress_or_a_create_drag()
    {
        await GotoHost("/e2e/gantt-v3?candrop=1");
        await WaitV3ReadyAsync();
        var handle = Page.Locator($"{V3Root} [data-task-id='fe3'] [data-gantt-progress-handle]");
        var from = await CenterAsync(handle);
        await DragAsync(from, (from.X + 152, from.Y));
        await WaitForSinkChangeAsync("event-sink-progresschange", null);
        Assert.Equal("0", await ReadSinkRawAsync("candrop-call-count"));

        await GotoHost("/e2e/gantt-v3?candrop=1&allowCreate=1");
        await WaitV3ReadyAsync();
        var track = Page.Locator($"{V3Root} [data-gantt-row-track][data-row-key='task:fe3']");
        await track.ScrollIntoViewIfNeededAsync();
        var box = await track.BoundingBoxAsync();
        Assert.NotNull(box);
        var createFrom = ((float)(box!.X + 100), (float)(box.Y + box.Height / 2));
        await DragAsync(createFrom, (createFrom.Item1 + DayPxDay * 3, createFrom.Item2));
        await WaitForSinkChangeAsync("event-sink-taskcreate", null);
        Assert.Equal("0", await ReadSinkRawAsync("candrop-call-count"));
    }

    // ── J: drag-create (v3-only) ─────────────────────────────────────────────

    [Fact]
    public async Task Drag_create_beside_an_existing_bar_on_an_occupied_row_creates_a_snapped_task()
    {
        await GotoHost("/e2e/gantt-v3?allowCreate=1");
        await WaitV3ReadyAsync();
        var track = Page.Locator($"{V3Root} [data-gantt-row-track][data-row-key='task:fe3']");
        await track.ScrollIntoViewIfNeededAsync();
        var box = await track.BoundingBoxAsync();
        Assert.NotNull(box);
        // 100px in and +114px further (Day mode, 38px/day): well within the
        // initial (scrollLeft=0) viewport, far to the left of fe3's own bar
        // (which sits ~60 days later) — an EMPTY area on fe3's OWN occupied row.
        var from = ((float)(box!.X + 100), (float)(box.Y + box.Height / 2));
        var to = (from.Item1 + DayPxDay * 3, from.Item2);

        await DragAsync(from, to);
        var json = await WaitForSinkChangeAsync("event-sink-taskcreate", null);
        var update = ParseUpdate(json);

        Assert.Equal("Create", update.Source);
        Assert.Equal(3, (update.Task.End - update.Task.Start).Days);
        Assert.Equal("Frontend", update.Task.GroupLabel); // leaf row -> sibling inheritance (fe3 has no children)
        Assert.Null(update.Task.ParentId);

        var changedJson = await WaitForSinkChangeAsync("event-sink-taskschanged", null);
        Assert.Contains("\"Count\":13", changedJson);
        Assert.Contains(update.Task.Id, changedJson);
    }

    [Fact]
    public async Task Drag_create_after_panning_computes_dates_from_the_new_origin()
    {
        await GotoHost("/e2e/gantt-v3?allowCreate=1");
        await WaitV3ReadyAsync();
        var track = Page.Locator($"{V3Root} [data-gantt-row-track][data-row-key='task:fe3']");
        await track.ScrollIntoViewIfNeededAsync();
        var box = await track.BoundingBoxAsync();
        Assert.NotNull(box);
        var from = ((float)(box!.X + 100), (float)(box.Y + box.Height / 2));
        var to = (from.Item1 + DayPxDay, from.Item2);

        await DragAsync(from, to);
        var json1 = await WaitForSinkChangeAsync("event-sink-taskcreate", null);
        var d1 = ParseUpdate(json1).Task.Start;

        // Fresh navigation (a brand-new page instance, so this is the FIRST create
        // on it — sidesteps any row-shift a prior create's TasksChanged append
        // could introduce into the SAME live page's row list/grouping order,
        // which is orthogonal to what this spec is proving).
        await GotoHost("/e2e/gantt-v3?allowCreate=1");
        await WaitV3ReadyAsync();
        var periodLabel = Page.Locator($"{V3Root} span.text-sm.font-medium");
        var initialLabel = (await periodLabel.TextContentAsync())!;
        await Page.GetByRole(AriaRole.Button, new() { Name = "Next period" }).ClickAsync();
        await Assertions.Expect(periodLabel).Not.ToHaveTextAsync(initialLabel, new() { Timeout = 10000 });

        var track2 = Page.Locator($"{V3Root} [data-gantt-row-track][data-row-key='task:fe3']");
        await track2.ScrollIntoViewIfNeededAsync();
        var box2 = await track2.BoundingBoxAsync();
        Assert.NotNull(box2);
        var from2 = ((float)(box2!.X + 100), (float)(box2.Y + box2.Height / 2));
        var to2 = (from2.Item1 + DayPxDay, from2.Item2);

        await DragAsync(from2, to2);
        var json2 = await WaitForSinkChangeAsync("event-sink-taskcreate", null);
        var d2 = ParseUpdate(json2).Task.Start;

        // Day mode's GanttNav Previous/Next steps VisibleRange (hence Origin) by
        // exactly 1 day (GanttScale's Day config Step=1) without moving scrollLeft
        // — the SAME screen point therefore now maps to origin+1 day.
        Assert.Equal(d1.AddDays(1), d2);
    }

    [Fact]
    public async Task Row_track_divs_align_with_their_rows_for_leaf_summary_and_group_header_contexts()
    {
        await GotoHost("/e2e/gantt-v3-tree?allowCreate=1");
        await Page.Locator($"{TreeRoot} [data-row-kind='task']").First.WaitForAsync(new() { Timeout = 15000 });

        // Leaf: grandchild1 ("Wireframes", depth 2, no children).
        await AssertBarWithinItsTrackBandAsync(TreeRoot, "grandchild1");
        // Summary: root1 ("Program Kickoff", HasChildren=true).
        await AssertBarWithinItsTrackBandAsync(TreeRoot, "root1");

        // Group-header: SharedTasks' "Ops" header immediately precedes "be1" —
        // no bar exists FOR the header row itself, so instead assert the
        // adjacency: be1's row band starts exactly one RowHeight after the
        // header's own track top (a real DOM-to-DOM check, not a re-derivation
        // of GanttScale's RowHeight constant against itself).
        await GotoHost("/e2e/gantt-v3?allowCreate=1");
        await WaitV3ReadyAsync();
        var opsHeaderTrack = Page.Locator($"{V3Root} [data-gantt-row-track][data-row-key='group::Ops']");
        var opsTop = ExtractPx((await opsHeaderTrack.GetAttributeAsync("style"))!, "top");
        var be1Bar = Page.Locator($"{V3Root} [data-task-id='be1']");
        var be1Top = ExtractPx((await be1Bar.GetAttributeAsync("style"))!, "top");
        var rowHeight = ExtractPx((await opsHeaderTrack.GetAttributeAsync("style"))!, "height");
        Assert.True(be1Top >= opsTop + rowHeight - 1 && be1Top <= opsTop + rowHeight * 2 + 1,
            $"be1 bar top {be1Top} not within one row of Ops header track top {opsTop} (rowHeight {rowHeight})");
    }

    private async Task AssertBarWithinItsTrackBandAsync(string rootSelector, string taskId)
    {
        var track = Page.Locator($"{rootSelector} [data-gantt-row-track][data-row-key='task:{taskId}']");
        var trackStyle = (await track.GetAttributeAsync("style"))!;
        var trackTop = ExtractPx(trackStyle, "top");
        var trackHeight = ExtractPx(trackStyle, "height");

        var bar = Page.Locator($"{rootSelector} [data-task-id='{taskId}']");
        var barStyle = (await bar.GetAttributeAsync("style"))!;
        var barTop = ExtractPx(barStyle, "top");

        Assert.True(barTop >= trackTop - 1 && barTop <= trackTop + trackHeight + 1,
            $"{taskId} bar top {barTop} not within its own row-track band [{trackTop}, {trackTop + trackHeight}]");
    }

    private static double ExtractPx(string style, string property)
    {
        var match = System.Text.RegularExpressions.Regex.Match(style, $@"{property}:(-?\d+(?:\.\d+)?)px");
        Assert.True(match.Success, $"style missing {property}px: {style}");
        return double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public async Task Sub_threshold_interaction_on_an_empty_track_creates_nothing()
    {
        await GotoHost("/e2e/gantt-v3?allowCreate=1");
        await WaitV3ReadyAsync();
        var track = Page.Locator($"{V3Root} [data-gantt-row-track][data-row-key='task:fe3']");
        await track.ScrollIntoViewIfNeededAsync();
        var box = await track.BoundingBoxAsync();
        Assert.NotNull(box);
        var from = ((float)(box!.X + 100), (float)(box.Y + box.Height / 2));

        await Page.Mouse.MoveAsync(from.Item1, from.Item2);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(from.Item1 + 1, from.Item2);
        await Page.Mouse.UpAsync();

        Assert.Equal(0, await Page.Locator(".lumeo-gantt-v3-create-ghost").CountAsync());
        Assert.True(string.IsNullOrEmpty(await ReadSinkRawAsync("event-sink-taskcreate")));
        Assert.True(string.IsNullOrEmpty(await ReadSinkRawAsync("event-sink-taskschanged")));
    }

    // ── K: OnTaskClick ────────────────────────────────────────────────────────

    [Fact]
    public async Task Task_click_fires_with_the_current_payload_on_both_routes()
    {
        await GotoHost("/e2e/gantt-v2");
        await WaitV2ReadyAsync();
        // be4 (Progress=0, no dependencies) — a task with a NON-zero progress
        // (e.g. be5) renders its progress-fill rect on top of the background
        // rect (gantt-v2.js z-order), which can cover the bg rect's own
        // center and intercept the click; be4's progress rect is zero-width.
        var v2Bar = Page.Locator($"{V2Root} g.lumeo-gantt-bar-wrapper[data-task-id='be4'] rect.lumeo-gantt-bar-bg");
        await v2Bar.ScrollIntoViewIfNeededAsync();
        await v2Bar.ClickAsync();
        var v2Json = await WaitForSinkChangeAsync("event-sink-click", null);
        var v2Task = ParseTask(v2Json);
        Assert.Equal("be4", v2Task.Id);
        Assert.Equal(new DateTime(2026, 3, 25), v2Task.Start);

        await GotoHost("/e2e/gantt-v3");
        await WaitV3ReadyAsync();
        var v3Bar = Page.Locator($"{V3Root} [data-task-id='be4']");
        await v3Bar.ScrollIntoViewIfNeededAsync();
        await v3Bar.ClickAsync();
        var v3Json = await WaitForSinkChangeAsync("event-sink-click", null);
        var v3Task = ParseTask(v3Json);
        Assert.Equal("be4", v3Task.Id);
        Assert.Equal(new DateTime(2026, 3, 25), v3Task.Start);
    }

    [Fact]
    public async Task A_completed_drag_never_also_fires_a_click_and_the_next_genuine_click_still_works()
    {
        await GotoHost("/e2e/gantt-v3");
        await WaitV3ReadyAsync();
        var bar = Page.Locator($"{V3Root} [data-task-id='be3']"); // untouched by other tests
        var from = await CenterAsync(bar);
        await DragAsync(from, (from.X + DayPxDay * 3, from.Y));
        await WaitForSinkChangeAsync("event-sink-taskupdate", null);

        Assert.True(string.IsNullOrEmpty(await ReadSinkRawAsync("event-sink-click")));

        // Bar re-rendered at its NEW position after the commit — re-locate before clicking.
        var movedBar = Page.Locator($"{V3Root} [data-task-id='be3']");
        await movedBar.ScrollIntoViewIfNeededAsync();
        await movedBar.ClickAsync();
        var clickJson = await WaitForSinkChangeAsync("event-sink-click", null);
        Assert.Equal("be3", ParseTask(clickJson).Id);
    }
}
