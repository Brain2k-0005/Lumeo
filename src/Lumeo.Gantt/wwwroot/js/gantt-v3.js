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
                const logicalTarget = Math.max(0, targetX - w / 2);
                el.scrollLeft = toNativeScrollLeft(el, logicalTarget);
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
    registerVerticalScrollTracking,
    unregisterVerticalScrollTracking,
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
        const logical = fromNativeScrollLeft(canvasEl, canvasEl.scrollLeft);
        headerInnerEl.style.transform = `translateX(${-logical}px)`;
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

// Vertical scroll tracking (Codex round 4, P2 #3): GanttArrowLayer draws one
// SVG path per dependency regardless of scroll position, unlike the bars/tree
// rows it overlays (both already virtualized — see GanttTimeline.Virtualize's
// own remarks and GanttTree's round-2 P2 #10 fix). Reports the scroll
// container's scrollTop/clientHeight back to GanttTimeline (a NEW,
// independent listener rather than piggy-backing on registerHeaderScrollSync
// above: that one is deliberately SKIPPED entirely in Gantt3's shared-pane
// mode — see GanttTimeline.OnAfterRenderAsync's own remarks on why —
// precisely the mode where this virtualization actually matters at scale),
// rAF-throttled so a fast scroll/drag doesn't flood Blazor with an
// invokeMethodAsync round-trip per native 'scroll' event.
const verticalScrollTrackers = new Map(); // el -> { dotNetRef, onScroll, pendingFrame }

function registerVerticalScrollTracking(el, dotNetRef) {
    if (!el || verticalScrollTrackers.has(el)) return;
    const report = () => {
        tracker.pendingFrame = null;
        dotNetRef.invokeMethodAsync('OnGanttV3VerticalScroll', el.scrollTop, el.clientHeight);
    };
    const onScroll = () => {
        if (tracker.pendingFrame) return; // already scheduled for this frame
        tracker.pendingFrame = requestAnimationFrame(report);
    };
    const tracker = { dotNetRef, onScroll, pendingFrame: null };
    el.addEventListener('scroll', onScroll, { passive: true });
    verticalScrollTrackers.set(el, tracker);
    report(); // initial position immediately — covers a pane that's already scrolled before this registers
}

function unregisterVerticalScrollTracking(el) {
    if (!el) return;
    const tracker = verticalScrollTrackers.get(el);
    if (!tracker) return;
    el.removeEventListener('scroll', tracker.onScroll);
    if (tracker.pendingFrame) cancelAnimationFrame(tracker.pendingFrame);
    verticalScrollTrackers.delete(el);
}

// RTL scrollLeft normalization (Codex round 3, P2 #7): every pixel v3 computes
// (GanttScale.DateToPixel, bar/header positions) lives in one LOGICAL axis —
// 0 = earliest date, increasing = later — and is NEVER mirrored for RTL (v2
// doesn't mirror its SVG either; a timeline's date order reading left-to-right
// is a separate concern from the surrounding page's text direction). Native
// `scrollLeft`, however, changes MEANING once an element's *computed*
// `direction` is `rtl` (inherited from a `dir="rtl"` ancestor — Lumeo's own
// RTL surface — even when the scroller itself never sets `dir`), and engines
// have historically disagreed on exactly how:
//   - "default"  (rare/legacy): behaves exactly like LTR — 0 at the physical
//     left edge, increasing rightward — even though direction is rtl.
//   - "negative" (the standardized behavior in evergreen Chrome/Firefox/
//     Safari): 0 sits at the RTL start (physical right edge); scrolling
//     toward the end of the content (physically left) makes scrollLeft
//     NEGATIVE, down to -(scrollWidth - clientWidth).
//   - "reverse"  (old WebKit): 0 also sits at the physical right edge, but
//     scrolling toward the end makes scrollLeft INCREASE instead of going
//     negative.
// Detected once via a throwaway probe (the well-known "detectRTLScrollBehavior"
// pattern used by several JS grid libraries) and cached — the convention is a
// property of the browser engine, not of any one element.
let _rtlScrollConvention = null; // 'default' | 'negative' | 'reverse'

function detectRtlScrollConvention() {
    if (_rtlScrollConvention) return _rtlScrollConvention;
    if (typeof document === 'undefined') return 'negative'; // non-browser test host — assume the modern standard
    const probe = document.createElement('div');
    probe.dir = 'rtl';
    probe.style.cssText = 'position:absolute;visibility:hidden;width:1px;height:1px;overflow:scroll;top:-9999px;left:-9999px;';
    probe.innerHTML = '<div style="width:1000px;height:1px;"></div>';
    document.body.appendChild(probe);

    // Bug fix (Codex round 4, P2 #4): the PREVIOUS probe assigned scrollLeft=1
    // FIRST and inspected the result — but 'default' and 'reverse' both accept
    // non-negative values in the SAME [0, maxScroll] numeric range (only
    // 'negative' rejects/clamps a positive assignment), so a positive
    // assignment reads back as 1 for BOTH 'default' and 'reverse' — the
    // 'reverse' branch below could never be reached; every legacy-WebKit
    // engine this was meant to detect was silently misclassified as
    // 'default' instead. The canonical technique (the well-known
    // "detectRTLScrollType" pattern, e.g. github.com/othree/jquery.rtl-scroll-type)
    // checks the element's UNTOUCHED, natural initial scrollLeft FIRST,
    // before any assignment ever overwrites it — 'reverse' engines are the
    // ONLY ones whose freshly-rendered (never-yet-scrolled) initial position
    // is already non-zero (they render an RTL overflow container pre-scrolled
    // to reflect its natural reading start), which the OLD code destroyed by
    // assigning scrollLeft=1 before ever reading the natural value. Modern
    // evergreen engines (Chrome/Firefox/Safari — 'negative' convention,
    // confirmed empirically: initial=0, +1 assignment clamps back to 0, -1
    // assignment sticks) are unaffected by this fix; only the previously
    // unreachable legacy branch is corrected.
    if (probe.scrollLeft > 0) {
        _rtlScrollConvention = 'reverse';
    } else {
        probe.scrollLeft = 1;
        _rtlScrollConvention = probe.scrollLeft === 0 ? 'negative' : 'default';
    }
    document.body.removeChild(probe);
    return _rtlScrollConvention;
}

// Converts a LOGICAL target scrollLeft (0 = earliest date, as GanttScale
// always computes) into the NATIVE scrollLeft value that achieves the same
// logical position on el, given el's own computed direction and the
// detected engine convention. No-ops under LTR.
function toNativeScrollLeft(el, logicalTarget) {
    if (getComputedStyle(el).direction !== 'rtl') return logicalTarget;
    const maxScroll = Math.max(0, el.scrollWidth - el.clientWidth);
    const convention = detectRtlScrollConvention();
    if (convention === 'negative') return logicalTarget - maxScroll;
    if (convention === 'reverse') return maxScroll - logicalTarget;
    return logicalTarget;
}

// Inverse of toNativeScrollLeft — recovers the LOGICAL position from a
// native scrollLeft reading (used by the header scroll-sync, which needs
// the logical offset to keep the header's own un-mirrored date labels
// aligned with the row canvas's un-mirrored bars).
function fromNativeScrollLeft(el, nativeValue) {
    if (getComputedStyle(el).direction !== 'rtl') return nativeValue;
    const maxScroll = Math.max(0, el.scrollWidth - el.clientWidth);
    const convention = detectRtlScrollConvention();
    if (convention === 'negative') return nativeValue + maxScroll;
    if (convention === 'reverse') return maxScroll - nativeValue;
    return nativeValue;
}

export default ganttV3;
