using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// Gantt v3 Phase 3, T9 — horizontal infinite scroll (<c>Gantt3.InfiniteScroll</c>).
/// Real-browser proof to complement <c>GanttV3Phase3T9Tests</c> (bUnit): a genuine
/// native 'scroll' event driving the rAF-throttled report all the way through
/// gantt-v3.js's <c>hasActiveDrag</c>/scroll-correction pipeline, the ±1px visual-
/// stability assertion the design spec calls for explicitly, a REAL pointer drag
/// proving mid-gesture suppression, and the InfiniteScroll=false v2-parity spec.
/// </summary>
public class GanttV3InfiniteScrollTests : GanttParityTestBase
{
    private ILocator ScrollPane => Page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;

    // The "done" latch (below) only proves gantt-v3.js's OWN centerOn call has
    // PHYSICALLY set scrollLeft — it says nothing about whether the resulting
    // native 'scroll' event has already round-tripped through the rAF-
    // throttled report()/OnGanttV3VerticalScroll/GanttTimeline pipeline on the
    // SERVER (design spec Phase 3, T9 — ScrollToCenterAsync's own
    // _suppressNextExtensionReport flag is armed by EVERY mount, since every
    // chart performs an initial scroll-to-today/-center attempt — see its
    // remarks). Acting immediately after "done" risks a genuine race: this
    // test's OWN forced scrollLeft + dispatchEvent('scroll') can coalesce
    // with the STILL-PENDING mount report into the SAME rAF-throttled
    // report() call, landing as ONE report that inherits the mount's own
    // suppression instead of being treated as the test's OWN deliberate
    // action. Polling the SAME report-count data attribute
    // GanttV3CanvasChromeTests' own remarks describe (incremented ONLY on an
    // actual, post-dedup report) until it reaches at least 1 — proving the
    // mount's OWN settling report has ACTUALLY reached the server, not just
    // that centerOn physically moved scrollLeft — closes that gap.
    private async Task WaitReadyAsync()
    {
        await Assertions.Expect(ScrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });
        var reported = false;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var count = await ScrollPane.EvaluateAsync<double>("el => Number(el.dataset.ganttV3VerticalReportCount) || 0");
            if (count >= 1) { reported = true; break; }
            await Page.WaitForTimeoutAsync(50);
        }
        if (!reported) Assert.Fail("the mount's own initial scroll report never reached the server within the retry budget.");
        // The counter above proves JS DISPATCHED the report; it says nothing
        // about the SignalR round-trip actually having been PROCESSED
        // server-side yet (report() never awaits invokeMethodAsync's own
        // promise) — a short bounded settle closes that remaining gap, same
        // idiom this file's own InfiniteScroll_False_Byte_Matches... spec
        // already uses for an analogous "no positive latch exists for this
        // negative-proof wait" situation.
        await Page.WaitForTimeoutAsync(250);
    }

    // Deterministic "extension has committed" signal: el.scrollWidth only ever
    // grows via a T9 extension (nothing else in this suite's fixtures resizes
    // the rendered date range after mount) — polled rather than blindly
    // delayed, matching every other stabilization helper in this project's
    // Gantt E2E suite (e.g. GanttV3CanvasChromeTests' own
    // ForceScrollLeftZeroAndWaitStableAsync).
    private async Task<double> WaitForScrollWidthToGrowAsync(double before)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var current = await ScrollPane.EvaluateAsync<double>("el => el.scrollWidth");
            if (current > before) return current;
            await Page.WaitForTimeoutAsync(100);
        }
        Assert.Fail($"scrollWidth never grew past {before} — the extension never committed within the retry budget.");
        return -1; // unreachable
    }

    [Fact]
    public async Task Scrolling_Near_The_Leading_Edge_Grows_The_Range_And_The_Header_Shows_More_Columns()
    {
        await GotoHost("/e2e/gantt-v3?tree=0");
        await WaitReadyAsync();

        var scrollWidthBefore = await ScrollPane.EvaluateAsync<double>("el => el.scrollWidth");
        var columnsBefore = await Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-header .flex > div").CountAsync();

        // scrollLeft=0 is ALWAYS within one viewport-width of the leading edge
        // (window.Start=0 < any positive clientWidth) — see
        // GanttTimeline.OnGanttV3VerticalScroll's own remarks.
        await ScrollPane.EvaluateAsync("el => { el.scrollLeft = 0; el.dispatchEvent(new Event('scroll')); }");

        var scrollWidthAfter = await WaitForScrollWidthToGrowAsync(scrollWidthBefore);
        Assert.True(scrollWidthAfter > scrollWidthBefore,
            $"expected scrollWidth to grow past {scrollWidthBefore}, got {scrollWidthAfter}");

        // The header renders one column per date unit, unvirtualized (see
        // GanttTimeline.Units/TotalWidth's own remarks) — a leading extension
        // (Day mode, PadBefore=60) adds exactly 60 MORE columns, all showing
        // EARLIER dates than anything rendered before.
        var columnsAfter = await Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-header .flex > div").CountAsync();
        Assert.Equal(60, columnsAfter - columnsBefore);
    }

    [Fact]
    public async Task Scrolling_Near_The_Trailing_Edge_Grows_The_Range_And_The_Header_Shows_More_Columns()
    {
        await GotoHost("/e2e/gantt-v3?tree=0");
        await WaitReadyAsync();

        // Bug fix (this task's own local E2E gate): SharedTasks' own dates
        // (Feb-Apr 2026) sit far in the PAST relative to this suite's real
        // wall-clock "today" — the mount's own default scroll-to-today
        // attempt already clamps to (at or very near) the trailing edge, so
        // assigning el.scrollLeft = el.scrollWidth below would otherwise be a
        // genuine JS no-op (same value as already-current) — no new native
        // 'scroll' event, no new report at all (report()'s own dedup guard).
        // Moving to the middle first guarantees the LATER assignment back to
        // the trailing edge is a real, reportable change.
        await ScrollPane.EvaluateAsync("el => { el.scrollLeft = el.scrollWidth / 2; el.dispatchEvent(new Event('scroll')); }");
        await Page.WaitForTimeoutAsync(200);

        var scrollWidthBefore = await ScrollPane.EvaluateAsync<double>("el => el.scrollWidth");
        var columnsBefore = await Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-header .flex > div").CountAsync();

        // el.scrollWidth (the browser clamps an assignment to the real max
        // automatically — same idiom GanttV3CanvasChromeTests' own
        // ForceScrollLeftMaxAndWaitStableAsync uses) always lands within one
        // viewport-width of the trailing edge by construction.
        await ScrollPane.EvaluateAsync("el => { el.scrollLeft = el.scrollWidth; el.dispatchEvent(new Event('scroll')); }");

        var scrollWidthAfter = await WaitForScrollWidthToGrowAsync(scrollWidthBefore);
        Assert.True(scrollWidthAfter > scrollWidthBefore,
            $"expected scrollWidth to grow past {scrollWidthBefore}, got {scrollWidthAfter}");

        var columnsAfter = await Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-header .flex > div").CountAsync();
        Assert.Equal(60, columnsAfter - columnsBefore); // Day mode PadAfter=60
    }

    // The design spec's own explicit proof requirement: "bar x stays visually
    // stable during extension (±1px)". Picks a bar still (partially) in view
    // right at the leading-edge threshold — NOT scrollLeft=0 itself, which
    // would show only empty padding with no bar to measure at all.
    [Fact]
    public async Task Bar_X_Stays_Visually_Stable_During_A_Leading_Extension_Within_One_Pixel()
    {
        await GotoHost("/e2e/gantt-v3?tree=0");
        await WaitReadyAsync();

        var bar = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe1']");
        await bar.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 15000 });

        var clientWidth = await ScrollPane.EvaluateAsync<double>("el => el.clientWidth");
        // Comfortably inside the "near leading edge" threshold (window.Start
        // < clientWidth) — fe1 (60 columns' worth of pixels past the range's
        // own Origin — Day mode PadBefore=60 * 38px = 2280) need NOT be
        // visually in the viewport for this measurement: Playwright's own
        // BoundingBoxAsync reports an element's page-coordinate box
        // regardless of whether it is currently scrolled into view (a
        // negative/out-of-viewport X is still a real, meaningful, comparable
        // value here) — so this test only needs a position that genuinely
        // satisfies the leading threshold, not one that also frames fe1.
        var scrollLeft = Math.Max(0, clientWidth - 300);
        // Bug fix (this task's own local E2E gate): the "done" latch below
        // can ALREADY read "done" from mount's own initial-scroll before this
        // test's own recenter ever runs (WaitReadyAsync already waited out
        // THAT one) — a stale-latch false pass, the exact class of bug this
        // project's OTHER Gantt E2E specs already document (see
        // GanttDragParityTests.ResetScrollLeftAsync's own remarks and this
        // file's own WaitReadyAsync). Cleared here so the LATER wait for
        // "done" can only be satisfied by THIS action's own recenter.
        await ScrollPane.EvaluateAsync("el => el.removeAttribute('data-gantt-v3-initial-scroll')");

        // Bug fix (this task's own local E2E gate — flaked 1/3 loop runs):
        // the scrollLeft assignment/dispatch and the "before" measurement
        // below used to be TWO separate round trips (each its own Playwright
        // CDP call) — the extension this assignment triggers is processed
        // server-side, entirely independently of Playwright's own IPC, so it
        // could occasionally COMPLETE (and re-render fe1 at its NEW,
        // post-extension position) in the gap between them, corrupting
        // "before" into an ALREADY-shifted value with nothing left to detect
        // as a change. Combined into ONE atomic JS evaluation — the
        // assignment and the read happen within the SAME synchronous script
        // execution, with no possible interleaving point for the server's
        // own (separate, async) response to land in between.
        var before = await ScrollPane.EvaluateAsync<BeforeSnapshot>($$"""
            el => {
                el.scrollLeft = {{scrollLeft.ToString(System.Globalization.CultureInfo.InvariantCulture)}};
                el.dispatchEvent(new Event('scroll'));
                const bar = document.querySelector("[data-testid='gantt-v3-root'] [data-task-id='fe1']");
                const rect = bar.getBoundingClientRect();
                return { x: rect.x, y: rect.y, scrollWidth: el.scrollWidth };
            }
            """);
        var boxBefore = new PlaywrightBoundingBox(before.X, before.Y);
        var scrollWidthBefore = before.ScrollWidth;

        var scrollWidthAfter = await WaitForScrollWidthToGrowAsync(scrollWidthBefore);
        Assert.True(scrollWidthAfter > scrollWidthBefore);

        // Same-frame scroll correction settled: gantt-v3.js's centerOn stamps
        // this attribute atomically with the scrollLeft assignment it
        // performs (see its own remarks) — every other scroll-completion
        // assertion in this project's Gantt E2E suite polls the SAME latch.
        // Guaranteed fresh (not stale) by the removeAttribute above.
        await Assertions.Expect(ScrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });

        var boxAfter = await bar.BoundingBoxAsync();
        Assert.NotNull(boxAfter);

        Assert.True(Math.Abs(boxAfter!.X - boxBefore.X) <= 1.0,
            $"expected fe1's screen X to stay within 1px across the leading extension, before={boxBefore.X}, after={boxAfter.X}");
        Assert.True(Math.Abs(boxAfter.Y - boxBefore.Y) <= 1.0,
            $"expected fe1's screen Y to be unaffected by a horizontal extension, before={boxBefore.Y}, after={boxAfter.Y}");
    }

    // Plain mutable shapes for the atomic before-measurement evaluate call
    // above — Playwright's own EvaluateAsync<T> deserializer instantiates T
    // via a PARAMETERLESS constructor + property setters (NOT JSON
    // constructor binding), so a record with a primary constructor throws
    // MissingMethodException at runtime despite compiling cleanly; property
    // NAMES must match the JS object's own keys (case-insensitively).
    private sealed class BeforeSnapshot
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double ScrollWidth { get; set; }
    }

    private sealed record PlaywrightBoundingBox(double X, double Y);

    // Decision 3 — a REAL pointer drag (not JS dispatchEvent) held across a
    // scroll event that would otherwise trigger an extension. Proves
    // gantt-v3.js's own activeDragGestureCount-backed GanttV3HasActiveDragAsync
    // gate closes the loop in an actual browser, not just the bUnit mock.
    [Fact]
    public async Task Dragging_A_Bar_Does_Not_Extend_The_Range_While_The_Drag_Is_Held()
    {
        await GotoHost("/e2e/gantt-v3?tree=0");
        await WaitReadyAsync();

        var bar = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe1']");
        await bar.ScrollIntoViewIfNeededAsync();
        var box = await bar.BoundingBoxAsync();
        Assert.NotNull(box);
        var centerX = (float)(box!.X + box.Width / 2);
        var centerY = (float)(box.Y + box.Height / 2);

        var scrollWidthBefore = await ScrollPane.EvaluateAsync<double>("el => el.scrollWidth");

        // Real pointer gesture: press, move past the 3px drag threshold, HOLD.
        await Page.Mouse.MoveAsync(centerX, centerY);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(centerX + 10, centerY);

        // While the drag is still held, force the pane to the leading edge —
        // the exact condition that triggers an extension outside a drag (see
        // Scrolling_Near_The_Leading_Edge_Grows_The_Range... above).
        await ScrollPane.EvaluateAsync("el => { el.scrollLeft = 0; el.dispatchEvent(new Event('scroll')); }");
        // Give the (would-be) rAF-throttled report + async gesture-suppression
        // check a few real frames to land, same idiom
        // ShowOffscreenIndicators_False_Renders_No_Chip_Even_When_Scrolled_Away
        // uses to prove a NEGATIVE (nothing happened).
        await Page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(() => requestAnimationFrame(r))))");
        await Page.WaitForTimeoutAsync(300);

        var scrollWidthDuringDrag = await ScrollPane.EvaluateAsync<double>("el => el.scrollWidth");
        Assert.Equal(scrollWidthBefore, scrollWidthDuringDrag);

        // Release — the drag itself may or may not commit a move (not this
        // test's concern); what matters is that suppression is temporary, not
        // permanent.
        await Page.Mouse.UpAsync();

        // The pane's native scrollLeft is ALREADY 0 from the assignment above
        // (suppressing the EXTENSION never un-does the physical scroll
        // itself) — re-assigning the SAME value is a genuine JS no-op (no new
        // native 'scroll' event, so no new report at all — report()'s own
        // dedup guard, see its remarks). Moves away first so the FOLLOWING
        // assignment back to 0 is a real, reportable change.
        await ScrollPane.EvaluateAsync("el => { el.scrollLeft = el.scrollWidth / 2; el.dispatchEvent(new Event('scroll')); }");
        await Page.WaitForTimeoutAsync(200);

        await ScrollPane.EvaluateAsync("el => { el.scrollLeft = 0; el.dispatchEvent(new Event('scroll')); }");
        var scrollWidthAfterRelease = await WaitForScrollWidthToGrowAsync(scrollWidthBefore);
        Assert.True(scrollWidthAfterRelease > scrollWidthBefore,
            "expected a normal (post-drag) near-leading-edge scroll to extend the range again");
    }

    // Design spec Phase 3, T9, decision 1's own parity half: InfiniteScroll=false
    // must byte-match v2's fixed padding, both AT MOUNT and once scrolled to
    // either edge — v2 never had infinite scroll to begin with, so "byte-match"
    // here means v3's OWN rendered total width equals v2's, and neither one
    // ever grows past it.
    [Fact]
    public async Task InfiniteScroll_False_Byte_Matches_V2s_Fixed_Padding_Both_At_Mount_And_At_Either_Edge()
    {
        await GotoHost("/e2e/gantt-v2");
        var v2Pane = Page.Locator("[data-testid='gantt-v2-root'] .lumeo-gantt-host");
        await v2Pane.WaitForAsync(new() { Timeout = 15000 });
        var v2ScrollWidth = await v2Pane.EvaluateAsync<double>("el => el.scrollWidth");

        await GotoHost("/e2e/gantt-v3?tree=0&infiniteScroll=0");
        await WaitReadyAsync();
        var v3ScrollWidthAtMount = await ScrollPane.EvaluateAsync<double>("el => el.scrollWidth");

        Assert.True(Math.Abs(v2ScrollWidth - v3ScrollWidthAtMount) < 2.0,
            $"expected v3(InfiniteScroll=false)'s rendered total width to byte-match v2's fixed padding, v2={v2ScrollWidth}, v3={v3ScrollWidthAtMount}");

        // Scroll to BOTH edges — neither grows the range under InfiniteScroll=false.
        await ScrollPane.EvaluateAsync("el => { el.scrollLeft = 0; el.dispatchEvent(new Event('scroll')); }");
        await Page.WaitForTimeoutAsync(400); // no positive "it grew" latch to poll for a negative — bounded settle, mirrors ShowOffscreenIndicators_False's own idiom
        var v3ScrollWidthAfterLeading = await ScrollPane.EvaluateAsync<double>("el => el.scrollWidth");
        Assert.Equal(v3ScrollWidthAtMount, v3ScrollWidthAfterLeading);

        await ScrollPane.EvaluateAsync("el => { el.scrollLeft = el.scrollWidth; el.dispatchEvent(new Event('scroll')); }");
        await Page.WaitForTimeoutAsync(400);
        var v3ScrollWidthAfterTrailing = await ScrollPane.EvaluateAsync<double>("el => el.scrollWidth");
        Assert.Equal(v3ScrollWidthAtMount, v3ScrollWidthAfterTrailing);
    }

    // RTL: v3's date axis is NEVER mirrored for RTL (physical left is always
    // the earliest date — see gantt-v3.js's own remarks), but NATIVE
    // scrollLeft's sign/zero-point convention genuinely differs across engines
    // (gantt-v3.js's own three-convention table: "negative"/"default"/
    // "reverse") — deliberately does NOT assume which one this suite's engine
    // uses or which physical extreme is "leading" in native terms (that would
    // require re-deriving fromNativeScrollLeft's own conversion IN the test,
    // duplicating logic rather than proving it). Instead drives BOTH native
    // extremes (an assignment the browser clamps automatically regardless of
    // convention) and asserts each one correctly extends — proving
    // GanttTimeline.OnGanttV3VerticalScroll's threshold check (built on the
    // ALREADY fromNativeScrollLeft-normalized reported scrollLeft — see its
    // own remarks) needs no RTL-specific branch, by construction, for EITHER
    // physical direction.
    [Fact]
    public async Task Scrolling_To_Either_Physical_Extreme_Under_Rtl_Grows_The_Range()
    {
        await GotoHost("/e2e/gantt-v3?tree=0&rtl=1");
        await WaitReadyAsync();

        var direction = await ScrollPane.EvaluateAsync<string>("el => getComputedStyle(el).direction");
        Assert.Equal("rtl", direction);

        // Bug fix (this task's own local E2E gate — see Scrolling_Near_The_Trailing_Edge...'s
        // own remarks): SharedTasks' dates sit in the wall-clock past, so the
        // mount's own default scroll-to-today attempt may ALREADY rest at
        // (or very near) one of the two physical extremes below — guarantees
        // the FIRST assignment is a real, reportable change regardless.
        //
        // MUST be a NEGATIVE assignment here, not a positive one (e.g.
        // el.scrollWidth / 2): under the "negative" RTL scroll convention
        // (gantt-v3.js's own three-convention table — the standard in every
        // evergreen engine this suite runs against) the valid native range is
        // [-maxScroll, 0], so ANY positive assignment clamps to the SAME
        // native 0 the mount's own scroll-to-today attempt already clamped
        // to (today sits far beyond the trailing edge, and 0 is that
        // convention's own "logical=maxScroll" value) — a positive "move to
        // the middle" would silently be JUST as much of a no-op as physical
        // extreme #1 itself, exactly the bug this comment block replaces.
        await ScrollPane.EvaluateAsync("el => { el.scrollLeft = -el.scrollWidth / 4; el.dispatchEvent(new Event('scroll')); }");
        await Page.WaitForTimeoutAsync(200);

        // Physical extreme #1: native scrollLeft driven to its maximum
        // (browser-clamped regardless of convention). Deliberately does NOT
        // wait for the "done" recenter latch here (unlike
        // Bar_X_Stays_Visually_Stable...'s own LTR-only use of it) — WHICH
        // physical extreme corresponds to the LOGICAL leading (recentering)
        // edge vs. the trailing (non-recentering — see
        // HandleRangeExtensionRequestAsync's own class remarks: trailing
        // never emits a scroll intent at all) edge depends on the engine's
        // OWN RTL scroll convention (gantt-v3.js's own three-convention
        // table) — this test intentionally never assumes which, so it cannot
        // assume a recenter happens here either; scrollWidth growth alone is
        // the convention-agnostic proof this test actually needs.
        var scrollWidthBefore1 = await ScrollPane.EvaluateAsync<double>("el => el.scrollWidth");
        await ScrollPane.EvaluateAsync("el => { el.scrollLeft = el.scrollWidth; el.dispatchEvent(new Event('scroll')); }");
        var scrollWidthAfter1 = await WaitForScrollWidthToGrowAsync(scrollWidthBefore1);
        Assert.True(scrollWidthAfter1 > scrollWidthBefore1,
            $"expected scrollWidth to grow past {scrollWidthBefore1} at the first physical extreme under RTL, got {scrollWidthAfter1}");

        // Physical extreme #2: native scrollLeft driven to its minimum (a
        // large negative assignment — the browser clamps this too, whichever
        // convention is in effect; "reverse"/"default" never go negative at
        // all, so this is a no-op landing back at their own native 0 there,
        // which is STILL the opposite extreme from #1 above under those
        // conventions).
        var scrollWidthBefore2 = await ScrollPane.EvaluateAsync<double>("el => el.scrollWidth");
        await ScrollPane.EvaluateAsync("el => { el.scrollLeft = -el.scrollWidth; el.dispatchEvent(new Event('scroll')); }");
        var scrollWidthAfter2 = await WaitForScrollWidthToGrowAsync(scrollWidthBefore2);
        Assert.True(scrollWidthAfter2 > scrollWidthBefore2,
            $"expected scrollWidth to grow past {scrollWidthBefore2} at the second physical extreme under RTL, got {scrollWidthAfter2}");
    }
}
