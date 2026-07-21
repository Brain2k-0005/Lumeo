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

    // Browser-local "today" as yyyy-MM-dd (Codex round 2, P2 #9): v2 derives
    // "today" via the BROWSER's `new Date()` (gantt-v2.js:326-327); Gantt3/
    // GanttTimeline previously used C#'s DateTime.Today, which on Blazor Server
    // is the SERVER's local date, not the visiting browser's — a consumer whose
    // browser and server sit in different timezones (or either side of a UTC
    // date-line boundary near midnight) could see "today" land on the wrong
    // calendar day: the marker in the wrong column, GanttNav's Today button
    // recentering on the wrong date. Same local-field construction (never
    // toISOString) as gantt-v2.js's own toLocalDateString, for the same reason:
    // toISOString converts to UTC first, which can roll the calendar day
    // backward in a positive-UTC-offset timezone.
    getLocalDateIso() {
        const d = new Date();
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, '0');
        const day = String(d.getDate()).padStart(2, '0');
        return `${y}-${m}-${day}`;
    },

    registerHeaderScrollSync,
    unregisterHeaderScrollSync,
};

// Sticky-header horizontal scroll sync (Codex round 2, P1 #3 — "sticky header
// still broken"). GanttTimeline.razor's own remarks explain WHY the header
// must live OUTSIDE the row-canvas's horizontal-scroll wrapper: `position:
// sticky` resolves against the NEAREST ancestor that establishes a scroll
// container, and an intervening `overflow-x:auto` element counts as one EVEN
// when its own height auto-fits its content and it therefore never actually
// has anything to scroll vertically — the wave-1 "overflow-y-visible" fix
// never worked because setting overflow-x to anything but visible/clip
// silently promotes a sibling overflow-y:visible to overflow-y:auto too (CSS
// Overflow spec), so the row-canvas wrapper was STILL a scroll container on
// both axes and the header kept sticking to IT instead of Gantt3's real
// outer (vertical) scroller. With the header moved out, it can no longer
// physically BE the same scrolling element as the row canvas, so its
// horizontal position is mirrored here via a `transform: translateX(...)`
// keyed off the canvas's own `scrollLeft` — compositor-only, no layout
// thrash, and (unlike setting scrollLeft) works on an element that never
// establishes its own scroll container.
const headerScrollSyncs = new Map(); // canvasEl -> { headerInnerEl, onScroll }

function registerHeaderScrollSync(canvasEl, headerInnerEl) {
    if (!canvasEl || !headerInnerEl) return;
    if (headerScrollSyncs.has(canvasEl)) return; // idempotent — same canvas, same listener
    const onScroll = () => {
        headerInnerEl.style.transform = `translateX(${-canvasEl.scrollLeft}px)`;
    };
    canvasEl.addEventListener('scroll', onScroll, { passive: true });
    headerScrollSyncs.set(canvasEl, { headerInnerEl, onScroll });
    onScroll(); // sync immediately — covers a canvas that's already scrolled (or about to be, via centerOn) before this registers
}

function unregisterHeaderScrollSync(canvasEl) {
    if (!canvasEl) return;
    const reg = headerScrollSyncs.get(canvasEl);
    if (!reg) return;
    canvasEl.removeEventListener('scroll', reg.onScroll);
    headerScrollSyncs.delete(canvasEl);
}

export default ganttV3;
