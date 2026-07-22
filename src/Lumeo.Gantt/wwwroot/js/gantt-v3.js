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

    // Reads el's CURRENT logical horizontal center (Codex round 5, P2 #5) —
    // the same "logical" coordinate space (0 = the scrollable content's own
    // physical-left origin, RTL-normalized via fromNativeScrollLeft) that
    // centerOn's own targetX already uses, so a caller can round-trip a
    // value read here straight back into GanttV3ScrollToXAsync. Gantt3 uses
    // this to capture what the user ACTUALLY has scrolled to before a
    // view-mode switch recomputes the visible range, instead of assuming the
    // outgoing range's own midpoint (a proxy that silently diverges from
    // reality the moment the user pans manually without touching Today or
    // the range itself). Returns null when the element can't be measured yet
    // (matches centerOn's own clientWidth<=50 "not laid out" guard) so the
    // caller can fall back to its own proxy.
    getScrollCenterX(el) {
        if (!el || el.clientWidth <= 50) return null;
        const logical = fromNativeScrollLeft(el, el.scrollLeft);
        return logical + el.clientWidth / 2;
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
const verticalScrollTrackers = new Map(); // el -> { dotNetRef, onScroll, pendingFrame, lastScrollTop, lastClientHeight, resizeObserver }

function registerVerticalScrollTracking(el, dotNetRef) {
    if (!el || verticalScrollTrackers.has(el)) return;
    const report = () => {
        tracker.pendingFrame = null;
        // Bug fix (Codex round 5, P2 #8): a horizontal-only pan (scrolling the
        // SAME shared pane sideways to browse dates) fires the identical
        // native 'scroll' event this listener reacts to — there is only one
        // 'scroll' event per element, not separate horizontal/vertical ones —
        // so every horizontal drag previously ALSO dispatched a full
        // invokeMethodAsync round-trip reporting an UNCHANGED scrollTop, for
        // no purpose (GanttArrowLayer's culled row-range is a pure function
        // of scrollTop/clientHeight, so recomputing it from the identical
        // inputs can only reproduce the identical result). Caching the last
        // REPORTED scrollTop and skipping the call when it hasn't actually
        // moved fixes this without weakening the rAF gate above, which still
        // caps this check itself to at most once per animation frame.
        //
        // Bug fix (Codex round 6, P1 #2): the dedup above ONLY compared
        // scrollTop, so a pane RESIZE with an unchanged scrollTop (the common
        // case — a window resize rarely also happens to move the scroll
        // position) was silently swallowed by this SAME check, even though
        // clientHeight is the other half of the culling window's own inputs.
        // Now requires BOTH to be unchanged before skipping.
        if (el.scrollTop === tracker.lastScrollTop && el.clientHeight === tracker.lastClientHeight) return;
        tracker.lastScrollTop = el.scrollTop;
        tracker.lastClientHeight = el.clientHeight;
        // Debug/test-observability counter (Codex round 5, P2 #8): the
        // invokeMethodAsync call below crosses a Blazor Server SignalR
        // round-trip with no console/network signal an E2E test could
        // observe directly — this data attribute, incremented ONLY on an
        // actual (post-dedup) report, gives Playwright a deterministic count
        // to assert "no report fired" against, matching the existing
        // data-gantt-v3-initial-scroll latch's own reasoning (centerOn's remarks).
        el.dataset.ganttV3VerticalReportCount = String((Number(el.dataset.ganttV3VerticalReportCount) || 0) + 1);
        dotNetRef.invokeMethodAsync('OnGanttV3VerticalScroll', el.scrollTop, el.clientHeight);
    };
    const onScroll = () => {
        if (tracker.pendingFrame) return; // already scheduled for this frame
        tracker.pendingFrame = requestAnimationFrame(report);
    };
    const tracker = { dotNetRef, onScroll, pendingFrame: null, lastScrollTop: null, lastClientHeight: null, resizeObserver: null };
    el.addEventListener('scroll', onScroll, { passive: true });
    // Bug fix (Codex round 6, P1 #2): a rows-count change never fires this
    // native 'scroll' event at all (nothing about the SCROLL POSITION
    // changes), but neither does a PANE-SIZE change on its own (e.g. the host
    // page's layout reflowing, or a consumer resizing the Height parameter's
    // container) — the culling window's OTHER input, clientHeight, can go
    // stale independently of any scroll. A ResizeObserver on the SAME element
    // reuses the identical rAF-gated `onScroll` scheduler (report() already
    // reads clientHeight fresh each time), so this is the cheapest correct
    // addition: no new dedupe/throttle logic, no per-frame .NET calls beyond
    // what already existed. The rows-count case itself is handled entirely in
    // C# (GanttTimeline.OnAfterRenderAsync re-derives the culling window
    // locally from the last-reported values — see its own remarks) since a
    // ResizeObserver on this element can't see a rows-count change at all
    // (the pane's own box is height-capped and doesn't resize when its
    // SCROLLABLE CONTENT grows/shrinks).
    if (typeof ResizeObserver !== 'undefined') {
        tracker.resizeObserver = new ResizeObserver(onScroll);
        tracker.resizeObserver.observe(el);
    }
    verticalScrollTrackers.set(el, tracker);
    report(); // initial position immediately — covers a pane that's already scrolled before this registers
}

function unregisterVerticalScrollTracking(el) {
    if (!el) return;
    const tracker = verticalScrollTrackers.get(el);
    if (!tracker) return;
    el.removeEventListener('scroll', tracker.onScroll);
    if (tracker.resizeObserver) tracker.resizeObserver.disconnect();
    if (tracker.pendingFrame) cancelAnimationFrame(tracker.pendingFrame);
    verticalScrollTrackers.delete(el);
}

// RTL scrollLeft normalization (Codex round 3, P2 #7): every pixel v3 computes
// (GanttScale.DateToPixel, bar/header positions) lives in one LOGICAL axis —
// 0 = earliest date (always the content's own physical-LEFT edge — see
// GanttTimeline.ScrollHostLeadingOffset's own remarks on why that holds even
// under RTL once the round-5 layout fix landed), increasing = later — and is
// NEVER mirrored for RTL (v2 doesn't mirror its SVG either; a timeline's date
// order reading left-to-right is a separate concern from the surrounding
// page's text direction). Native `scrollLeft`, however, changes MEANING once
// an element's *computed* `direction` is `rtl` (inherited from a `dir="rtl"`
// ancestor — Lumeo's own RTL surface — even when the scroller itself never
// sets `dir`), and engines have historically disagreed on exactly how.
//
// Bug fix (Codex round 6, P2 #3 — THIRD iteration on this probe; round 4,
// P2 #4 fixed which branch was reachable, this fixes which LABEL each
// reachable branch actually gets, since a wrong label pairs the right
// detection with the WRONG conversion formula down in toNativeScrollLeft/
// fromNativeScrollLeft). Independently re-derived from first principles
// below with concrete numbers (scrollWidth=1000, clientWidth=200,
// maxScroll=800) rather than trusted-by-inspection — every row is checkable
// against toNativeScrollLeft's own formula:
//
// | Label      | Natural (untouched) scrollLeft | Assigning +1 (from 0)      | native @ logical=0 (phys-left) | native @ logical=maxScroll (phys-right) | Formula (native from logical) |
// |------------|---------------------------------|----------------------------|----------------------------------|--------------------------------------------|--------------------------------|
// | "negative" | 0                                | clamps back to 0           | -maxScroll (-800)                 | 0                                          | native = logical - maxScroll   |
// | "default"  | POSITIVE (~maxScroll, ~800)      | (not reached — already >0) | 0                                  | maxScroll (800)                            | native = logical (pass-through)|
// | "reverse"  | 0                                | STICKS, reads back as 1     | maxScroll (800)                    | 0                                          | native = maxScroll - logical   |
//
// Reasoning per row (also see toNativeScrollLeft/fromNativeScrollLeft, which
// implement these same three formulas verbatim):
//   - "negative" (standardized behavior in evergreen Chrome/Firefox/Safari):
//     0 is the RTL START (physical right edge, where reading begins);
//     scrolling toward the "end" of the content (physically left, later
//     dates) makes scrollLeft NEGATIVE, down to -(scrollWidth-clientWidth).
//     Natural/rest state is already 0 (no adjustment needed to show the RTL
//     start), and a POSITIVE assignment is out of range so it clamps back to
//     0 — the ONLY one of the three where that clamp happens, which is
//     exactly what the `probe.scrollLeft === 0` check after assigning +1
//     detects.
//   - "default" (old WebKit/Chrome, pre-RTL-scroll-remapping): scrollLeft
//     keeps its LTR-identical numbering even under dir=rtl — 0 is the
//     physical LEFT edge, scrollWidth-clientWidth is the physical RIGHT edge,
//     completely unaffected by direction. Since numbering is LTR-identical,
//     converting our logical axis (which ALSO defines 0=phys-left) needs NO
//     transform at all: native = logical, a straight pass-through. The engine
//     still wants a freshly-rendered, never-touched RTL container to VISUALLY
//     default to showing its RTL reading-start (physical right) — since
//     showing the right edge under this LTR-identical numbering REQUIRES
//     scrollLeft ≈ maxScroll, the natural/rest value here is POSITIVE. This
//     is the ONLY one of the three conventions with a positive natural
//     initial value, which is exactly what `probe.scrollLeft > 0` (checked
//     BEFORE any assignment ever touches it) detects.
//   - "reverse" (old IE/Edge): 0 is ALSO the RTL start (physical right edge,
//     same starting point as "negative" — no adjustment needed at rest, so
//     natural initial is 0 here too) but INCREASES — rather than going
//     negative — as the viewport moves toward showing more physical-left
//     (later) content, topping out at scrollLeft=maxScroll for the physical
//     left edge. A positive assignment therefore STICKS (reads back as
//     whatever was assigned, e.g. 1) instead of clamping — the discriminator
//     between this and "negative" once the natural-initial check has already
//     ruled out "default".
//
// PREVIOUS (round 4) code had the "default" and "reverse" LABELS swapped
// relative to this table — the natural-initial-positive branch was labelled
// 'reverse' (which pairs with the maxScroll-minus-logical formula: WRONG,
// that branch needs the pass-through) and the zero-initial-sticks branch was
// labelled 'default' (which falls through to pass-through in
// toNativeScrollLeft: ALSO wrong, that branch needs maxScroll-minus-logical).
// Both real legacy engines this probe targets are effectively unreachable in
// this environment (no CI/local test runner using genuinely pre-2015 WebKit
// or any IE/Edge-Legacy build exists to reproduce the wrong-formula symptom
// directly), so this was verified by re-deriving the three rows above from
// first principles with concrete numbers rather than against a live legacy
// engine — see the unit tests mirroring toNativeScrollLeft/fromNativeScrollLeft
// in C# (GanttScaleTests) for the checkable, independently-run version of the
// same three formulas.
//
// Detected once via a throwaway probe (the well-known "detectRTLScrollType"
// pattern used by several JS grid libraries, e.g.
// github.com/othree/jquery.rtl-scroll-type) and cached — the convention is a
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

    // Bug fix (Codex round 4, P2 #4): the PREVIOUS-PREVIOUS probe assigned
    // scrollLeft=1 FIRST and inspected the result — but "default" and
    // "reverse" both accept non-negative values in the SAME [0, maxScroll]
    // numeric range (only "negative" rejects/clamps a positive assignment),
    // so a positive assignment read back as 1 for BOTH — the natural-initial
    // check below (checking BEFORE any assignment) is what actually
    // distinguishes them, per the table above.
    if (probe.scrollLeft > 0) {
        // Bug fix (Codex round 6, P2 #3): was mislabelled 'reverse' — see the
        // table above. A positive NATURAL (untouched) initial value is
        // "default"'s own signature; "default" needs the pass-through
        // formula, which is what the 'default' label routes to in
        // toNativeScrollLeft/fromNativeScrollLeft (the fallthrough case).
        _rtlScrollConvention = 'default';
    } else {
        probe.scrollLeft = 1;
        // Bug fix (Codex round 6, P2 #3): the non-clamping ("sticks") branch
        // was mislabelled 'default' — see the table above. This is
        // "reverse"'s own signature (zero natural initial, but a positive
        // assignment sticks instead of clamping), and "reverse" needs the
        // maxScroll-minus-logical formula.
        _rtlScrollConvention = probe.scrollLeft === 0 ? 'negative' : 'reverse';
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
