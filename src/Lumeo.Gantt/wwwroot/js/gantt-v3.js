// gantt-v3.js — minimal scroll interop for GanttV3's Blazor-rendered timeline
// (Lumeo.Gantt). Everything else about the v3 render tree is plain Razor +
// CSS; this is the ONE JS slice T4's parity harness pulled forward (Codex
// review wave, P1: initial viewport showed an empty grid because
// Gantt3.ComputeInitialRange pads ~60 day columns before the first task and
// nothing ever moved scrollLeft off 0).
//
// Unlike v2 (gantt-v2.js), v3 never wipes/rebuilds its own DOM on a data
// change — Blazor diffs it — so there is no "preserve scroll across
// re-renders" concern here, only the retry-until-measurable pattern v2's own
// tryScroll uses (gantt-v2.js lines 673-693): a freshly (or not yet) laid-out
// element can report clientWidth === 0 for the first few frames, so centering
// immediately would silently no-op.

export const ganttV3 = {
    // Centers targetX (a pixel offset within the timeline's own scrollable
    // content) in el's viewport — mirrors gantt-v2.js's tryScroll exactly:
    // `scrollLeft = max(0, targetX - clientWidth / 2)`, retried via
    // requestAnimationFrame up to 30 attempts until the element reports a
    // real width.
    //
    // Deflake fix (CI-only race, review wave round 3): GanttV3ScrollToXAsync's
    // Task resolves as soon as this call is DISPATCHED, not when the
    // requestAnimationFrame-scheduled scroll actually lands — a Playwright
    // spec that scrolls the host away and then asserts the today-marker is
    // OUT of view had no way to know whether the component's own initial
    // scroll-to-today had already fired-and-settled BEFORE it acted, so on a
    // slow CI runner the initial scroll could land AFTER the test's
    // scroll-away, dragging the marker back into view and failing the
    // precondition. The stamp below is set in the SAME call that performs the
    // scroll (atomic — not a second interop round-trip that could reorder
    // relative to it), so a test can await it deterministically instead of
    // guessing with a timeout.
    centerOn(el, targetX) {
        if (!el) return;
        const tryScroll = (attempt) => {
            const w = el.clientWidth;
            if (w > 50) {
                el.scrollLeft = Math.max(0, targetX - w / 2);
                el.setAttribute('data-gantt-v3-initial-scroll', 'done');
            } else if (attempt < 30) {
                requestAnimationFrame(() => tryScroll(attempt + 1));
            }
        };
        requestAnimationFrame(() => tryScroll(0));
    },
};

export default ganttV3;
