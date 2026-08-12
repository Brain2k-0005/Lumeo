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

    // Wheel-zoom's own recenter (GanttTimeline.ScrollTargetOffsetOverride) —
    // the SAME retry-until-measurable shape as centerOn above, but places
    // targetX at an ARBITRARY viewport offset (offsetPx, pixels from el's own
    // left edge) instead of centerOn's hardcoded w/2. A separate function
    // (not a centerOn(el, targetX, offsetPx = w/2) overload) so centerOn's
    // own existing behavior/E2E assertions are untouched by construction —
    // no shared code path for a new caller to accidentally perturb.
    scrollToOffset(el, targetX, offsetPx) {
        if (!el) return;
        const tryScroll = (attempt) => {
            const w = el.clientWidth;
            if (w > 50) {
                const logicalTarget = Math.max(0, targetX - offsetPx);
                el.scrollLeft = toNativeScrollLeft(el, logicalTarget);
                el.setAttribute('data-gantt-v3-initial-scroll', 'done');
            } else if (attempt < 30) {
                requestAnimationFrame(() => tryScroll(attempt + 1));
            }
        };
        requestAnimationFrame(() => tryScroll(0));
    },

    // Roving-tabindex focus target (arrow-key navigation's own DOM-side
    // half — see GanttTimeline.MoveBarFocusAsync's own remarks). Scoped
    // queries by [data-task-id] within containerEl, mirroring every other
    // bar-scoped lookup in this file (registerDrag's own
    // e.target.closest('[data-task-id]'), registerBarContextMenu) rather
    // than a global document.getElementById — a bar's own _barId
    // (GanttBar.razor) is a per-COMPONENT-INSTANCE random guid, not
    // deterministic from a task id, so data-task-id (already rendered on
    // every bar's wrapper, deterministic and stable) is the only reliable
    // handle GanttTimeline has to "the bar for task X" from outside GanttBar
    // itself. CSS.escape guards a task id containing quotes/brackets/etc.
    // from breaking the attribute selector. The actual tabindex="0" element
    // is a DESCENDANT of the [data-task-id] wrapper (GanttBar's own inner
    // content div, not the wrapper itself — see GanttBar.razor's
    // InnerAttributes/WrapperAttributes split), so this focuses the first
    // [tabindex] descendant, falling back to the wrapper itself if somehow
    // absent. A plain element.focus() call auto-scrolls an in-DOM-but-
    // visually-clipped target into view natively — see
    // GanttTimeline.MoveBarFocusAsync's own remarks for why that's enough
    // to reach every row inside Virtualize's own overscan buffer without
    // this needing any scroll-into-view logic of its own.
    focusBar(containerEl, taskId) {
        if (!containerEl || typeof taskId !== 'string') return;
        const selector = `[data-task-id="${CSS.escape(taskId)}"]`;
        const barEl = containerEl.querySelector(selector);
        if (!barEl) return;
        const focusable = barEl.querySelector('[tabindex]') || barEl;
        focusable.focus();
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

    // Browser-local "now" as yyyy-MM-ddTHH:mm:ss (design spec Phase 3, T2 —
    // NowIndicator's precise current-TIME line in sub-day view modes). Extends
    // the SAME browser-clock family getLocalDateIso above already established
    // rather than a parallel channel — see GanttV3GetLocalDateTimeAsync's own
    // remarks. Same local-field construction (never toISOString) as
    // getLocalDateIso, for the same UTC-conversion-would-roll-the-day reason.
    getLocalDateTimeIso() {
        const d = new Date();
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, '0');
        const day = String(d.getDate()).padStart(2, '0');
        const h = String(d.getHours()).padStart(2, '0');
        const min = String(d.getMinutes()).padStart(2, '0');
        const s = String(d.getSeconds()).padStart(2, '0');
        return `${y}-${m}-${day}T${h}:${min}:${s}`;
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
    //
    // Bug fix (Codex round 16 review, P2 finding #5): accepts an optional
    // `direction` ('ltr'/'rtl') forwarded straight to fromNativeScrollLeft.
    // Gantt3's ThemeService-driven reconcile passes the OLD (pre-flip)
    // direction explicitly here, since by the time that capture runs,
    // document.documentElement's own `dir` (and so getComputedStyle(el).direction)
    // may already reflect the NEW direction — ThemeService's own flip mutates
    // the DOM synchronously, independent of Blazor's render pipeline, unlike a
    // DirectionProvider-cascading-parameter change (which repaints only AFTER
    // Blazor's async lifecycle, including this same capture, has already run).
    // Every OTHER caller omits it, keeping the live-DOM-read behavior.
    getScrollCenterX(el, direction) {
        if (!el || el.clientWidth <= 50) return null;
        const logical = fromNativeScrollLeft(el, el.scrollLeft, direction);
        return logical + el.clientWidth / 2;
    },

    registerHeaderScrollSync,
    unregisterHeaderScrollSync,
    registerVerticalScrollTracking,
    unregisterVerticalScrollTracking,

    registerDrag,
    unregisterDrag,

    registerSplitterDrag,
    unregisterSplitterDrag,
    resetSplitterWidth,

    registerRowReorderDrag,
    unregisterRowReorderDrag,

    registerBarContextMenu,
    unregisterBarContextMenu,

    registerWheelZoom,
    unregisterWheelZoom,

    hasActiveDrag,
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
    // Bug fix (Codex round 9 review, P2 #4): the LAST onScroll call left a
    // translateX(...) frozen on the header inline style - unregistering
    // (e.g. a standalone timeline transitioning to Gantt3's shared-pane
    // mode, where the header goes back to natural DOM flow with no offset
    // needed at all - see GanttTimeline's own remarks) never cleared it, so
    // the header stayed visually shifted by whatever the last scroll
    // position happened to be. Cleared here so a re-registration later
    // (the reverse transition) also starts from a clean baseline instead of
    // briefly showing the stale offset for one frame before its own first
    // onScroll() call overwrites it.
    reg.headerInnerEl.style.transform = '';
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
const verticalScrollTrackers = new Map(); // el -> { dotNetRef, onScroll, pendingFrame, lastScrollTop, lastClientHeight, lastScrollLeft, lastClientWidth, resizeObserver }

function registerVerticalScrollTracking(el, dotNetRef) {
    if (!el || verticalScrollTrackers.has(el)) return;
    const report = () => {
        tracker.pendingFrame = null;
        // Design spec Phase 3, T7 — off-screen indicators need the shared
        // pane's HORIZONTAL extent too; fromNativeScrollLeft (this file's own
        // RTL-normalization helper, already used by registerHeaderScrollSync
        // for the identical "live native 'scroll' event -> logical offset"
        // conversion) un-mirrors it into the SAME never-mirrored coordinate
        // space GanttScale.DateToPixel/every bar's own X already live in, so
        // the .NET side needs no further conversion.
        const scrollLeft = fromNativeScrollLeft(el, el.scrollLeft);
        const clientWidth = el.clientWidth;
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
        //
        // Design spec Phase 3, T7: scrollLeft/clientWidth joined the SAME
        // dedup — critically, this is what makes a PURE horizontal pan (the
        // exact scenario off-screen indicators exist for) actually report at
        // all: before T7, scrollTop/clientHeight alone would have kept
        // matching their cached values throughout a horizontal-only drag,
        // silently swallowing every such report the same class of bug the
        // round-6 clientHeight fix already closed for resize.
        if (el.scrollTop === tracker.lastScrollTop && el.clientHeight === tracker.lastClientHeight &&
            scrollLeft === tracker.lastScrollLeft && clientWidth === tracker.lastClientWidth) return;
        tracker.lastScrollTop = el.scrollTop;
        tracker.lastClientHeight = el.clientHeight;
        tracker.lastScrollLeft = scrollLeft;
        tracker.lastClientWidth = clientWidth;
        // Debug/test-observability counter (Codex round 5, P2 #8): the
        // invokeMethodAsync call below crosses a Blazor Server SignalR
        // round-trip with no console/network signal an E2E test could
        // observe directly — this data attribute, incremented ONLY on an
        // actual (post-dedup) report, gives Playwright a deterministic count
        // to assert "no report fired" against, matching the existing
        // data-gantt-v3-initial-scroll latch's own reasoning (centerOn's remarks).
        el.dataset.ganttV3VerticalReportCount = String((Number(el.dataset.ganttV3VerticalReportCount) || 0) + 1);
        dotNetRef.invokeMethodAsync('OnGanttV3VerticalScroll', el.scrollTop, el.clientHeight, scrollLeft, clientWidth);
    };
    const onScroll = () => {
        if (tracker.pendingFrame) return; // already scheduled for this frame
        tracker.pendingFrame = requestAnimationFrame(report);
    };
    const tracker = { dotNetRef, onScroll, pendingFrame: null, lastScrollTop: null, lastClientHeight: null, lastScrollLeft: null, lastClientWidth: null, resizeObserver: null };
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
//
// Bug fix (Codex round 16 review, P2 finding #5): accepts an optional
// directionOverride ('ltr'/'rtl') — see getScrollCenterX's own remarks for
// why a caller (Gantt3's ThemeService-driven reconcile) needs to force the
// conversion to a KNOWN-old direction instead of trusting
// getComputedStyle(el).direction, which can already reflect a NEW direction
// by the time that caller's own capture runs. The header-scroll-sync call
// site below never passes one — it always wants the CURRENT live direction,
// since it runs on every native 'scroll' event, always reflecting whatever
// the DOM is under right now.
function fromNativeScrollLeft(el, nativeValue, directionOverride) {
    const direction = directionOverride ?? getComputedStyle(el).direction;
    if (direction !== 'rtl') return nativeValue;
    const maxScroll = Math.max(0, el.scrollWidth - el.clientWidth);
    const convention = detectRtlScrollConvention();
    if (convention === 'negative') return nativeValue + maxScroll;
    if (convention === 'reverse') return maxScroll - nativeValue;
    return nativeValue;
}

// ── Drag engine (Phase 2, T1) ───────────────────────────────────────────────
//
// v3's bars are plain absolutely-positioned <div>s inside the row-canvas div
// (the "relative" element Virtualize's items render into — see
// GanttTimeline.razor's RowItems/RowsContainerStyle remarks), each carrying
// data-task-id/data-task-start/data-task-end/data-milestone (see GanttBar.razor's
// WrapperAttributes). Rather than attaching a listener per bar (which Blazor's
// Virtualize would force us to re-attach on every recycle), ONE pointerdown
// listener is delegated on the scroll-host element GanttTimeline passes to
// registerDrag (GanttTimeline's own row-canvas `_scrollHostRef` — the element
// bars/tracks actually live in, regardless of which element the RTL/scroll-sync
// machinery above treats as the scroll owner) — e.target.closest('[data-task-id]')
// finds which bar (if any) was hit.
//
// Coordinate space (carry-forward watch item (b) from the phase-2 plan — the
// T4 arrow-layer bug must not repeat): a bar's rendered `left`/`width` (read via
// getComputedStyle, which resolves the --lumeo-gantt-bar-x/-w custom properties
// GanttBar.razor's WrapperStyle sets) are relative to the ROW-CANVAS div, i.e.
// the same origin GanttScale.BarGeometry computes X/Width in. Since a drag here
// is HORIZONTAL-ONLY (dates, never a row/vertical change), no Y math or
// scrollLeft compensation is needed at all: computedStyle.left/width already ARE
// the row-canvas-space numbers, unaffected by the scroll-host's scrollLeft (both
// the bar and its row-canvas ancestor move together under scroll).
//
// RTL note (phase-2/phase-1 reconciliation, corrected post-Codex-review — see
// reg.onPointerDown's own `isRtl` remarks for the one exception): move/resize
// drag math below needs NONE of the RTL scrollLeft-convention machinery
// above. CSS `left`/`width` (what readBarGeometry reads) are PHYSICAL
// properties — always physical-left-relative regardless of `dir` — and a
// pointer event's `clientX` is likewise always a physical page coordinate.
// Both therefore already live in the same "logical" axis the RTL comment
// block above describes (0 = physical-left = earliest date, never mirrored
// for RTL), so a MOVE/RESIZE drag's pixel delta (dx) is correct under RTL
// with NO conversion: dragging physically right always means "later dates,"
// exactly as under LTR. `toNativeScrollLeft`/`fromNativeScrollLeft` exist
// ONLY to translate a LOGICAL position into/out of the RTL-convention-
// dependent NATIVE `scrollLeft` property — an entirely different quantity
// the drag engine never reads or writes (drag-create's `startCreateDrag`
// likewise anchors off a track element's own getBoundingClientRect + inline
// `top`/`left:0` style, both physical, both already row-canvas-space-aligned
// — see its own remarks).
//
// PROGRESS is the one exception this "verified by inspection" note originally
// missed (Codex review — "Reverse progress deltas in RTL"): the fill/handle
// (GanttBar.razor's `.lumeo-gantt-v3-bar-progress`, `start-0`) anchors at the
// LOGICAL inline start, which is the PHYSICAL RIGHT edge under RTL, so its
// width grows AWAY from that edge (leftward) instead of rightward — the one
// place a physical dx needs an RTL-aware sign flip. Fixed at the two call
// sites (`onPointerMove`'s progress branch and `onPointerUp`'s progress
// commit) via the `isRtl` flag computed in reg.onPointerDown, rather than
// here in shared module-level math, since only that one mode is affected.
//
// Every rule below is a deliberate port of gantt-v2.js's pointer/drag handling
// (lines 590-763) — ported faithfully with the ORIGINAL line numbers cited
// per-rule so a future reader can diff intent, not just behavior:
//   - hit zones + drag-vs-click threshold: gantt-v2.js:590-643
//   - live visual update during move: gantt-v2.js:698-720 (applyDragVisual)
//   - day-snapped commit + end/start clamp: gantt-v2.js:736-764 (commitDrag)
//   - date parse/format helpers: gantt-v2.js:53-63 (parseDate), 66 (addDays),
//     117-122 (toLocalDateString)
// Deltas not ported: v2's RESIZE_HANDLE_W is 8px and right-edge only (v2 has no
// left-edge resize at all — REUI parity added resize-left here); this port uses
// a 6px hit zone on BOTH edges (RESIZE_HANDLE_PX below), a deliberate v3 design
// choice, not a v2 constant.
//
// Phase 2, T2 additions (progress drag, click, CanDrop) — same file, same
// registerDrag/onPointerDown closure, three new v2-parity/REUI-analog behaviors:
//   - progress-handle drag + commit: gantt-v2.js:564-574 (handle geometry),
//     715-719/758 (applyDragVisual/commitDrag progress branches)
//   - click-vs-drag: gantt-v2.js:617-622 (a below-threshold 'move'-mode
//     mousedown falls back to a click; 'resize'/'progress' modes do not)
//   - CanDrop live validation has NO v2 equivalent (REUI canDropEvent analog) —
//     see GanttTimeline.ValidateDrop's remarks for the .NET side.
//
// Phase 2, T3 addition (drag-create) — ALSO no v2 equivalent (REUI parity: a
// pointer-down on empty row-canvas TRACK background, never a bar, followed by
// a horizontal drag). Handled by a SEPARATE entry point (startCreateDrag,
// below) rather than folded into the bar-drag closure above: there is no
// source bar element to clone a ghost from, no data-task-start/-end to anchor
// against, and no CanDrop concern (T2's plan: "CanDrop is about scheduling
// EXISTING tasks"), so the two code paths share only the module-level
// constants (RESIZE_HANDLE_PX doesn't apply; DRAG_THRESHOLD_PX/GHOST_MIN_WIDTH_PX
// do) and the date-format helpers below.

const RESIZE_HANDLE_PX = 6;
// gantt-v2.js:610 `if (Math.abs(dx) > 3) dragInitiated = true;` — pixels of
// pointer travel before a mousedown-on-a-bar counts as a drag rather than a
// click. Falling BELOW this threshold fires a click instead when mode ===
// 'move' (Phase 2, T2 — NotifyTaskClick — see onPointerUp), matching
// gantt-v2.js:617-622; for 'resize'/'progress' modes it simply cancels with no
// commit and no click (v2 parity — v2 has no click fallback for those modes
// either).
const DRAG_THRESHOLD_PX = 3;
// Purely a visual floor for the ghost's rendered width during an active resize
// (never lets the ghost collapse to something unreadable/inverted on screen).
// Distinct from the DAY-based minimum-duration clamp applied at COMMIT time
// (mirrors gantt-v2.js:710 `Math.max(8, barW + dx)`, which is likewise a
// visual-only floor — v2's actual commit-time duration clamp is line 755's
// `if (task.end < task.start) task.end = task.start`).
const GHOST_MIN_WIDTH_PX = 8;

const dragRegistrations = new Map(); // scrollHostEl -> { dotNetRef, options, onPointerDown }

// Codex P2 finding ("Isolate each drag to its initiating pointer"): a bar's
// pointermove/pointerup/pointercancel listeners are attached PER-DRAG (inside
// reg.onPointerDown, below) rather than once at registerDrag time, so a
// SECOND pointerdown landing on the same barEl while a drag is already in
// flight — a second touch/pen contact on multi-pointer hardware — would
// otherwise install a second, independent set of handlers on top of the
// first. Tracked here (keyed by barEl, not globally) so two DIFFERENT bars
// can still each run their own legitimate concurrent drag; only a second
// contact on the SAME bar is rejected. See reg.onPointerDown's own remarks
// for the pointerId filter this pairs with (defense-in-depth against the
// same class of cross-pointer contamination for any drag that DOES get
// past this gate, e.g. a pen+touch combo where both count as "primary" for
// their own pointer type).
const activeBarDrags = new WeakSet(); // barEl -> currently being dragged by some pointer

// Bug fix (Codex P2 finding "Filter drag-create events to their initiating
// pointer") — the create-drag analog of activeBarDrags above, keyed by
// trackEl instead of barEl: a second contact landing on a track already
// being create-dragged must not start a second, independent handler set on
// top of the first. See startCreateDrag's own remarks for the pointerId
// filter this pairs with.
const activeCreateDrags = new WeakSet(); // trackEl -> currently being create-dragged by some pointer

// Design spec Phase 3, T9 — infinite scroll's own gesture-suppression signal
// (decision 3: "suppress extension while a drag is in flight"). A plain
// COUNTER, not a boolean: the same module comment above notes concurrent
// drags on different bars/pointers are explicitly permitted (activeBarDrags/
// activeCreateDrags are WeakSets precisely because MULTIPLE sessions can be
// live at once), so a boolean flipped false by the FIRST gesture to end would
// wrongly un-suppress extension while a SECOND, unrelated gesture is still in
// flight. Incremented/decremented at the EXACT same two moments
// activeBarDrags/activeCreateDrags themselves are populated/cleared (see
// reg.onPointerDown's own `activeBarDrags.add`/cleanup's `.delete` below, and
// startCreateDrag's identical pair) — eagerly at pointerdown, not only once
// the drag threshold is crossed, matching those WeakSets' own timing exactly
// (a below-threshold pointer that resolves to a plain click still counts as
// "in flight" for the brief window it's down, which is the conservative,
// correct choice here: better to defer one extension attempt by a frame than
// risk one landing under a gesture that turns out to be a drag after all).
// MODULE-LEVEL (not scoped per Gantt instance/scroll-host), same as
// activeBarDrags/activeCreateDrags themselves already are — see
// hasActiveDrag's own remarks for why this is a deliberate, low-risk
// simplification rather than an oversight.
let activeDragGestureCount = 0;

// Design spec Phase 3, T9 — queried by Gantt3/GanttTimeline (via
// ComponentInteropService.GanttV3HasActiveDragAsync, a plain pull, no element
// argument) at exactly two points: (1) GanttTimeline's own gate before ever
// asking Gantt3 to extend VisibleRange, and (2) Gantt3's OWN re-check
// immediately before it actually commits an extension — closing the narrow
// race where a NEW gesture starts DURING the one await
// (ResolveCurrentCenterDateAsync's live-scroll-center read) that sits between
// those two points. See Gantt3.HandleRangeExtensionRequestAsync's own remarks
// for why only the LEADING-edge path needs the second check at all (trailing
// extension never shifts the coordinate origin, so it has nothing for a
// concurrent gesture's ghost math to desync against).
//
// MODULE-LEVEL, not scoped per scroll-host element (unlike registerDrag's own
// per-`el` registrations): activeBarDrags/activeCreateDrags — the sets this
// counter mirrors — are ALREADY module-level/page-global, not per-Gantt-
// instance, so a page hosting multiple simultaneous Gantt3 charts already
// shares drag-isolation state across all of them at the JS layer; scoping
// JUST this counter per-instance would not remove that pre-existing sharing,
// only make this ONE signal inconsistent with the WeakSets it mirrors. The
// worst outcome of this simplification is a purely CONSERVATIVE one: an
// unrelated chart's active drag can briefly defer another chart's otherwise-
// eligible extension by a frame or two — never a wrong commit, since a
// suppressed extension simply retries on the NEXT qualifying scroll report.
function hasActiveDrag() {
    return activeDragGestureCount > 0;
}

// gantt-v2.js:53-63 (parseDate) — v3 only ever receives its own "yyyy-MM-dd"
// data-task-start/-end attributes (see GanttBar.razor), never a free-form
// string or Date, so this is the regex branch only, trimmed accordingly.
function parseIsoDate(s) {
    const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(s);
    if (!m) return null;
    return new Date(+m[1], +m[2] - 1, +m[3]);
}

// gantt-v2.js:66 (addDays) — local-midnight calendar arithmetic, DST-safe the
// same way v2's is (JS Date setters roll the calendar day forward/back using
// the LOCAL timezone, which is exactly what a "shift by whole days" drag needs
// — see GanttScale's own TZ/DST-safety note for why the C# side never touches
// timezone conversion either).
function addDays(d, n) {
    const x = new Date(d);
    x.setDate(x.getDate() + n);
    return x;
}

// gantt-v2.js:117-122 (toLocalDateString) — LOCAL calendar fields, never
// toISOString() (which converts to UTC and can roll the date across midnight
// in a positive-UTC-offset timezone). C# parses this with
// DateTime.TryParseExact("yyyy-MM-dd", ...), so the two sides agree on format
// with no timezone conversion anywhere in the round trip.
function toLocalDateString(d) {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
}

// Resolved bar geometry, in row-canvas pixels — see the coordinate-space note
// above for why getComputedStyle's left/width need no further adjustment.
function readBarGeometry(barEl) {
    const cs = getComputedStyle(barEl);
    return { left: parseFloat(cs.left) || 0, width: parseFloat(cs.width) || 0 };
}

// GanttBar.razor's data-task-progress (Phase 2, T2) — the ORIGINAL progress
// percent at pointerdown time, read once so the progress-drag math never needs
// a mid-drag JS->.NET round trip (same rationale as data-task-start/-end).
function readBarProgress(barEl) {
    const raw = parseFloat(barEl.getAttribute('data-task-progress'));
    return Number.isFinite(raw) ? raw : 0;
}

function clampProgress(p) {
    return Math.max(0, Math.min(100, p));
}

// gantt-v2.js:745-755 commitDrag's move/resize-end/resize-start branches,
// EXTRACTED so onPointerMove's CanDrop-candidate preview and onPointerUp's
// actual commit compute the identical date for "the same" snapped drag
// position — a live-preview/commit mismatch would let the ghost show one
// verdict and the commit silently land on a DIFFERENT (unvalidated) date pair.
function computeSnappedDates(mode, movedDays, origStart, origEnd) {
    let newStart = origStart;
    let newEnd = origEnd;
    if (mode === 'move') {
        newStart = addDays(origStart, movedDays);
        newEnd = addDays(origEnd, movedDays);
    } else if (mode === 'resize-end') {
        newEnd = addDays(origEnd, movedDays);
        // gantt-v2.js:755 `if (task.end < task.start) task.end = task.start;`
        if (newEnd < origStart) newEnd = origStart;
    } else if (mode === 'resize-start') {
        // REUI-parity addition (v2 has no left-edge resize to mirror) —
        // symmetric clamp to gantt-v2.js:755, against the FIXED end.
        newStart = addDays(origStart, movedDays);
        if (newStart > origEnd) newStart = origEnd;
    }
    return { newStart, newEnd };
}

// Paints/clears the CanDrop-invalid visual on a move/resize ghost (REUI
// canDropEvent analog — no v2 equivalent). CSS-vars-only per house rules: an
// inline style referencing var(--color-destructive) needs no stylesheet rule
// of its own (unlike a Tailwind utility class, which would need the v3 CSS
// build to have ever seen that class string) — the browser resolves the
// custom property from whatever theme root is already in scope.
//
// data-drop-invalid is the ReUI-parity hook (the styling-hooks audit — ReUI
// names this exact attribute), the inline style is what actually paints.
// Presence-only (an empty-string value, never "true"/"false") — same
// boolean-attribute convention every OTHER new styling hook in this pass uses
// (see GanttBar.razor's WrapperAttributes remarks): `[data-drop-invalid]` is a
// valid CSS selector this way.
//
// data-invalid="true" is kept ALONGSIDE it, not replaced (Codex review of this
// PR, P2). The first pass renamed it, but this file's own prior comment called
// data-invalid "the stable hook (E2E selector / consumer override)" — i.e. it
// was explicitly promised to consumers, so dropping it would silently break
// every existing CanDrop override, in a PR whose whole point is to ADD styling
// hooks. It is also the house convention beyond this component: Lumeo.Scheduler
// paints its own drag ghost with the identical attribute and ships a
// `[data-scheduler-ghost][data-invalid]` rule, so a Gantt-only rename would
// have split a cross-component convention too. Both are set and cleared
// together; the value shapes differ deliberately (the legacy alias keeps its
// original "true" value so existing `[data-invalid="true"]` selectors — not
// just `[data-invalid]` — keep matching).
function setGhostInvalid(ghost, invalid) {
    if (!ghost) return;
    if (invalid) {
        ghost.setAttribute('data-drop-invalid', '');
        ghost.setAttribute('data-invalid', 'true');
        ghost.classList.add('lumeo-gantt-v3-drag-ghost-invalid');
        ghost.style.outline = '2px solid var(--color-destructive)';
        ghost.style.backgroundColor = 'var(--color-destructive)';
    } else {
        ghost.removeAttribute('data-drop-invalid');
        ghost.removeAttribute('data-invalid');
        ghost.classList.remove('lumeo-gantt-v3-drag-ghost-invalid');
        ghost.style.outline = '';
        ghost.style.backgroundColor = '';
    }
}

// gantt-v2.js:591-596 hit-zone dispatch, generalized to BOTH edges (v2 only
// ever had a right-edge resizeHandle, gantt-v2.js:556-562) and forced to
// 'move' for a milestone (v2 draws milestones with no resize/progress
// handles at all, gantt-v2.js:472-505 — the milestone <g> only ever gets
// mouseenter/mouseleave/click listeners, never mousedown; v3's move-only
// milestone drag is a deliberate v3 ADDITION consistent with that "no resize"
// half of v2's behavior, not a straight port of a v2 drag path — v2 never
// drags milestones at all).
function resolveHitMode(barEl, clientX, isMilestone) {
    if (isMilestone) return 'move';
    const rect = barEl.getBoundingClientRect();
    const localX = clientX - rect.left;
    if (localX <= RESIZE_HANDLE_PX) return 'resize-start';
    if (rect.width - localX <= RESIZE_HANDLE_PX) return 'resize-end';
    return 'move';
}

// "ghost element (clone bar, opacity, painted via CSS vars)" — the phase-2
// plan's explicit T1 deliverable: drag is ghost-only, the REAL Blazor-owned
// bar div is never mutated by JS (only re-rendered once by .NET after
// CommitDrag), so there is nothing for Blazor's diff to fight or leave stale
// on an aborted/failed drag.
//
// Design spec Phase 3, T8 — DragGhostTemplate: when GanttBar.razor rendered a
// custom ghost template for THIS task (see GanttBar's own remarks — a HIDDEN
// sibling div, `[data-gantt-ghost-template]`, positioned with the EXACT SAME
// --lumeo-gantt-bar-x/-w/-row + left/top/width/height WrapperStyle the real
// bar itself carries, since both come from the same X/Width/RowIndex/BarHeight
// props on the same GanttBar instance), clone THAT node instead of barEl —
// same coordinate space (a sibling of barEl inside the SAME row-canvas parent,
// per the pinned Phase-2 "bars are nested, arrows are outer-canvas" coordinate
// rule — nothing here changes WHICH element the ghost is appended to, only
// WHICH element supplies its cloned content), so the ghost's initial position
// is byte-identical to cloning the bar itself. The move/resize/progress preview
// logic below only ever WRITES ghost.style.left/width/etc — it never reads
// them back off the clone — and its own delta math (dx, geo) is always derived
// from barEl's OWN readBarGeometry/data-task-* attributes, never from the
// template — so which element was cloned cannot skew any drag math, only the
// ghost's visual content. A progress-drag preview specifically looks for
// `.lumeo-gantt-v3-bar-progress` inside the ghost (see onPointerMove's own
// progress branch) — a custom template that doesn't include one simply skips
// that live repaint (guarded by `if (fill)` there already); the ghost still
// tracks position/size correctly either way.
function makeGhost(barEl) {
    const templateEl = barEl.parentElement
        ? barEl.parentElement.querySelector('[data-gantt-ghost-template]')
        : null;
    const ghost = (templateEl || barEl).cloneNode(true);
    ghost.classList.add('lumeo-gantt-v3-drag-ghost');
    ghost.removeAttribute('data-task-id'); // never itself a hit-test target for a second, nested pointerdown
    ghost.removeAttribute('data-gantt-ghost-template');
    ghost.classList.remove('hidden');
    ghost.style.display = ''; // clear the template's own inline display:none (see GanttBar.razor's remarks) — the class above already handles Tailwind's `hidden` utility; this covers a template whose visibility came from the inline style instead
    ghost.style.opacity = '0.6';
    ghost.style.pointerEvents = 'none';
    ghost.style.zIndex = '50';
    barEl.parentNode.appendChild(ghost);
    return ghost;
}

function registerDrag(el, dotNetRef, options) {
    if (!el) return;
    const existing = dragRegistrations.get(el);
    if (existing) {
        // Idempotent re-registration (view-mode/ColumnWidth change): swap the
        // stored dotNetRef/options in place — "JS never re-derives" the snap
        // config, so a fresher columnWidth/pixelsPerDay from .NET must always
        // win without requiring a separate unregister/register round trip.
        existing.dotNetRef = dotNetRef;
        existing.options = options;
        return;
    }

    // Bug fix (Codex P2 finding "Cancel active drags when unregistering"):
    // every currently-running drag SESSION's own cleanup() — bar-drag AND
    // create-drag alike — is tracked here so unregisterDrag can tear all of
    // them down externally (see its own remarks). Keyed on the registration
    // itself (per scroll-host), not per-element, since a host can have
    // multiple concurrent sessions (different bars/tracks, or different
    // pointers — see activeBarDrags/activeCreateDrags below).
    const reg = { dotNetRef, options, onPointerDown: null, activeCleanups: new Set() };

    reg.onPointerDown = (e) => {
        if (e.button !== 0) return; // left mouse / primary touch-pen contact only

        // Bug fix (Codex P2 finding "Snapshot drag options at pointerdown"):
        // reg.options/reg.dotNetRef are mutated IN PLACE by a later
        // registerDrag call (the idempotent re-registration branch above) —
        // a ViewMode/ColumnWidth/etc. change mid-drag would otherwise change
        // what THIS already-running gesture reads (stale bar geometry,
        // fresh pixelsPerDay), corrupting movedDays math partway through a
        // single drag. Captured ONCE here, at the moment the gesture
        // actually begins, and used everywhere below instead of reading
        // reg.* live — the snapshot is deliberately owned by THIS pointerdown
        // closure, not reg itself, so a later registerDrag swap can never
        // reach it.
        const dragOptions = reg.options;
        const dragDotNet = reg.dotNetRef;

        const barEl = e.target.closest('[data-task-id]');
        if (!barEl || !el.contains(barEl)) {
            // Phase 2, T3 — no bar was hit. Only look for a create-track hit
            // when the caller opted in (dragOptions.allowCreate — see
            // GanttTimeline.BuildDragOptions' own remarks: the row-track
            // elements themselves also only exist in the DOM when this is
            // true, so this check is defense-in-depth, not the only gate).
            if (dragOptions && dragOptions.allowCreate) {
                const trackEl = e.target.closest('[data-gantt-row-track]');
                // Bug fix (Codex P2 finding "Filter drag-create events to
                // their initiating pointer"): mirrors activeBarDrags below —
                // a second contact (multi-touch/pen+touch) landing on a
                // track already being create-dragged must not install a
                // second handler set on top of the first.
                if (trackEl && el.contains(trackEl) && !activeCreateDrags.has(trackEl)) {
                    startCreateDrag(dragOptions, dragDotNet, reg, trackEl, e);
                }
            }
            return;
        }

        // Codex P2 finding ("Isolate each drag to its initiating pointer"):
        // reject a second pointerdown on a bar that already has a drag in
        // flight (see activeBarDrags' own remarks) rather than layering a
        // second handler set on top of the first one.
        if (activeBarDrags.has(barEl)) return;

        const taskId = barEl.getAttribute('data-task-id');
        const isMilestone = barEl.getAttribute('data-milestone') === 'true';
        const origStartIso = barEl.getAttribute('data-task-start');
        const origEndIso = barEl.getAttribute('data-task-end');
        const origStart = parseIsoDate(origStartIso);
        const origEnd = parseIsoDate(origEndIso);
        if (!origStart || !origEnd) return; // malformed data-* — nothing sane to drag
        const origProgress = readBarProgress(barEl);

        // gantt-v2.js:593 `e.preventDefault();` — stops the browser's native text
        // selection / drag-image gesture from fighting the pointer drag.
        e.preventDefault();

        // Phase 2, T2 — a hit on the progress handle wins over resolveHitMode's
        // edge/move dispatch (milestones never render one — see GanttBar.razor's
        // `@if (!Task.IsMilestone && !Readonly)` guard — so `isMilestone` alone is
        // enough to keep this branch unreachable for them without a second check).
        const progressHandleEl = !isMilestone ? e.target.closest('[data-gantt-progress-handle]') : null;
        const mode = (progressHandleEl && barEl.contains(progressHandleEl))
            ? 'progress'
            : resolveHitMode(barEl, e.clientX, isMilestone);
        const geo = readBarGeometry(barEl);
        const startClientX = e.clientX;
        // Codex P2 finding ("Reverse progress deltas in RTL"): the progress
        // fill/handle (GanttBar.razor's `.lumeo-gantt-v3-bar-progress`) is
        // positioned with `start-0` — a LOGICAL inset that resolves to the
        // PHYSICAL RIGHT edge once the bar's own computed `direction` is
        // `rtl` (Lumeo's DirectionProvider). Growing that box's `width` then
        // extends it AWAY from its anchored (right) edge, i.e. LEFTWARD, so
        // the fill's leading (handle) edge moves opposite a plain physical
        // clientX delta: a rightward drag would shrink the anchored-right
        // box instead of growing it. Unlike move/resize (see the "RTL note"
        // above readBarGeometry's own remarks — pure physical left/width,
        // genuinely direction-agnostic), progress is the one drag mode whose
        // visual growth direction flips with the bar's own direction, so
        // ONLY its delta needs an RTL-aware sign flip. Gated on mode ===
        // 'progress' so a plain move/resize drag never pays for the
        // getComputedStyle() call (a potential forced style recalc) at all.
        const isRtl = mode === 'progress' && getComputedStyle(barEl).direction === 'rtl';
        let dragInitiated = false;
        let ghost = null;

        // Phase 2, T2 — CanDrop live validation (move/resize only, never
        // progress — GanttScheduleDropContext's own remarks). Scoped to THIS
        // drag session (not module-level), so it never outlives the drag and
        // never collides with a concurrent drag on a different bar.
        const validationCache = new Map(); // snapped-position key -> Promise<bool>
        // Keys whose cached promise has not settled yet. Needed because a Map
        // of promises cannot answer "is this one still in flight?" — see the
        // revisit branch in checkCanDrop for why that question matters.
        const pendingKeys = new Set();
        let lastValidatedKey = null;

        // Bug fix (Codex review of this PR, P2): makeGhost CLONES the bar, so
        // the ghost carried the ORIGINAL task's data-past for the whole
        // gesture — drag a finished task into the future and the preview
        // stayed styled as past until the drop committed. Same class as the
        // progress-hook staleness already fixed above, for the date-changing
        // modes. Mirrors GanttBar.IsPast exactly: whole-day comparison against
        // the LAST RENDERED day (candidate Start for a milestone, whose
        // geometry ignores End; candidate End otherwise), never constructing
        // the day after it — see that property's own remarks for why the
        // "+1 day" form overflowed at DateTime.MaxValue.
        function refreshGhostPast(dx) {
            if (!ghost) return;
            const dayPx = dragOptions && dragOptions.pixelsPerDay > 0 ? dragOptions.pixelsPerDay : 0;
            if (!dayPx) return; // no day scale to snap with — leave the cloned value rather than guess
            const movedDays = Math.round(dx / dayPx);
            const { newStart: candStart, newEnd: candEnd } = computeSnappedDates(mode, movedDays, origStart, origEnd);
            const endInclusive = isMilestone ? candStart : candEnd;
            // Missing/unparseable data-task-start|end (parseIsoDate returns
            // null, and addDays on it yields an Invalid Date) — leave the
            // cloned attribute alone rather than paint from NaN.
            if (!endInclusive || Number.isNaN(endInclusive.getTime())) return;
            // The TIMELINE's effective now (dragOptions.nowDate), never this
            // engine's own new Date() (Codex review of this PR, P2): GanttBar
            // computes data-past from GanttTimeline.Now when a consumer supplies
            // one — a historical or simulated timeline — so reading the real
            // browser clock here made the ghost contradict the very bar it was
            // cloned from. No fallback to new Date(): if .NET did not send a
            // date, leave the cloned attribute rather than invent a clock the
            // bars are not using.
            const today = dragOptions && dragOptions.nowDate ? parseIsoDate(dragOptions.nowDate) : null;
            if (!today || Number.isNaN(today.getTime())) return;
            const end = new Date(endInclusive.getFullYear(), endInclusive.getMonth(), endInclusive.getDate());
            if (today > end) ghost.setAttribute('data-past', '');
            else ghost.removeAttribute('data-past');
        }

        function checkCanDrop(dx) {
            const dayPx = dragOptions && dragOptions.pixelsPerDay > 0 ? dragOptions.pixelsPerDay : 1;
            const movedDays = Math.round(dx / dayPx);
            const { newStart: candStart, newEnd: candEnd } = computeSnappedDates(mode, movedDays, origStart, origEnd);
            const key = `${mode}|${toLocalDateString(candStart)}|${toLocalDateString(candEnd)}`;
            if (key === lastValidatedKey) return; // same snapped position as last check — no new call
            lastValidatedKey = key;

            let promise = validationCache.get(key);
            if (!promise) {
                // Bug fix (Codex P1 finding "Fail closed when CanDrop
                // invocation rejects"): a REJECTED invocation (the
                // consumer's own CanDrop predicate throwing, a transient
                // interop failure) used to resolve to `true` — a permission
                // check that fails OPEN. This promise is CACHED and reused
                // verbatim by onPointerUp's own commit-time await below when
                // the position was already checked during the move, so this
                // catch handler is not merely cosmetic (repainting the
                // ghost) — it can be the ACTUAL verdict CommitDrag gates on.
                // `false` here is deliberately NOT the same code path as "no
                // validator configured" — that case never reaches this
                // function at all (see checkCanDrop's/onPointerUp's own
                // `dragOptions.hasCanDrop` gate), so a chart with no CanDrop
                // still commits unconditionally, exactly as before.
                //
                // Styling-hooks audit (data-drop-invalid, fail-closed while
                // pending): an unresolved verdict for a BRAND NEW snapped
                // position used to leave the ghost showing whatever its PRIOR
                // position's verdict happened to be — optimistically "valid"
                // more often than not, since most positions along a drag are.
                // That is the same fail-OPEN shape the P1 fix above already
                // closed for a REJECTED invocation, just for the in-flight
                // window instead of the rejected-outcome case: painting the
                // ghost invalid before the async call is even dispatched
                // means an unresolved predicate reads as invalid, matching
                // this drag's own fail-closed COMMIT gate (onPointerUp below
                // awaits the identical promise and requires `valid === true`)
                // rather than a stale, possibly-wrong "valid" flashing between
                // repaints.
                setGhostInvalid(ghost, true);
                pendingKeys.add(key);
                promise = dragDotNet
                    ? dragDotNet.invokeMethodAsync('ValidateDrop', taskId, mode, toLocalDateString(candStart), toLocalDateString(candEnd)).catch(() => false)
                    : Promise.resolve(false);
                // Registered BEFORE the repaint .then below, so by the time
                // that one runs the key is already off the pending set.
                promise.then(() => pendingKeys.delete(key));
                validationCache.set(key, promise);
            } else if (pendingKeys.has(key)) {
                // Bug fix (Codex review of this PR, P2): the pessimistic
                // repaint above used to be gated on `!promise` alone, on the
                // reasoning that revisiting an already-cached key "has nothing
                // new to hide behind a pessimistic repaint". That holds for a
                // SETTLED verdict, but not for one still in flight: drag to a
                // slow-validating position A (ghost correctly painted
                // invalid-while-pending), move to a fast/valid position B
                // (ghost repainted valid), then move back to A before A's
                // verdict lands — the cache hit skipped the repaint and left
                // the ghost showing B's "valid" for a position whose verdict
                // is still unknown. Same fail-OPEN shape as the two cases
                // above, reached through the cache instead of a fresh call.
                setGhostInvalid(ghost, true);
            }
            promise.then((valid) => {
                // Only repaint if the drag hasn't already moved on to a DIFFERENT
                // snapped position by the time this (possibly-async-over-SignalR)
                // verdict comes back.
                if (lastValidatedKey === key) setGhostInvalid(ghost, !valid);
            });
        }

        const pointerId = e.pointerId;
        barEl.setPointerCapture(pointerId);
        activeBarDrags.add(barEl);
        activeDragGestureCount++; // design spec Phase 3, T9 — see its own declaration for why this is a counter, not a boolean

        const onPointerMove = (mv) => {
            // Codex P2 finding ("Isolate each drag to its initiating
            // pointer"): a second pointer's move events must never drive
            // THIS closure's ghost — see activeBarDrags' own remarks for the
            // companion gate that stops a second closure from ever being
            // created on the same bar in the first place.
            if (mv.pointerId !== pointerId) return;
            const dx = mv.clientX - startClientX;
            if (!dragInitiated) {
                if (Math.abs(dx) < DRAG_THRESHOLD_PX) return;
                dragInitiated = true;
                ghost = makeGhost(barEl); // clone FIRST — see data-dragging's own remarks below for why order matters
                // Styling-hooks audit (data-dragging): set on barEl, never
                // Blazor-rendered — this whole engine runs a live gesture with
                // NO Blazor round trip until CommitDrag on drop (see this
                // function's own class remarks: "the REAL Blazor-owned bar div
                // is never mutated by JS" predates this attribute; a render
                // can't reach a mid-gesture truth only JS holds). Presence-only,
                // same convention as data-drop-invalid/every other new hook.
                // Set AFTER makeGhost (which clones barEl) so the clone does
                // NOT inherit it — a consumer rule mirroring ReUI's own
                // "data-dragging hides the original, the ghost stands in for
                // it" intent (e.g. `[data-dragging] { opacity: 0 }`) must hide
                // ONLY the original, never the ghost that is the live preview.
                barEl.setAttribute('data-dragging', '');
            }
            // gantt-v2.js:698-720 (applyDragVisual) — the ghost-only v3
            // equivalent: 'move' translates the whole ghost, 'resize-end'
            // grows/shrinks from the right (left edge fixed), 'resize-start'
            // grows/shrinks from the left (right edge fixed), 'progress'
            // (Phase 2, T2) resizes just the cloned progress-fill child.
            if (mode === 'move') {
                ghost.style.left = (geo.left + dx) + 'px';
            } else if (mode === 'resize-end') {
                const newWidth = Math.max(GHOST_MIN_WIDTH_PX, geo.width + dx);
                ghost.style.width = newWidth + 'px';
            } else if (mode === 'resize-start') {
                const maxLeft = geo.left + geo.width - GHOST_MIN_WIDTH_PX;
                const newLeft = Math.min(geo.left + dx, maxLeft);
                ghost.style.left = newLeft + 'px';
                ghost.style.width = (geo.left + geo.width - newLeft) + 'px';
            } else if (mode === 'progress') {
                // gantt-v2.js:716 `Math.max(0, Math.min(100, origProgress + (dx / barW) * 100))`.
                // RTL note: `isRtl` negates dx so the fill's leading edge
                // (anchored at the LOGICAL start — see isRtl's own remarks)
                // keeps tracking the pointer instead of moving opposite it.
                const newProgress = clampProgress(origProgress + ((isRtl ? -dx : dx) / geo.width) * 100);
                const fill = ghost.querySelector('.lumeo-gantt-v3-bar-progress');
                if (fill) fill.style.width = newProgress + '%';
                // Bug fix (Codex review of the styling-hooks PR, P2): makeGhost
                // CLONES the original bar, so the ghost inherited a frozen
                // data-progress (and possibly data-completed) from before the
                // drag. The preview then matched consumer selectors for the OLD
                // percentage while visibly showing a different one — and a bar
                // dragged down from 100% kept [data-completed] the whole time.
                // Keep both hooks on the ghost in step with the width above.
                const rounded = Math.round(newProgress);
                ghost.setAttribute('data-progress', String(rounded));
                if (rounded === 100) ghost.setAttribute('data-completed', '');
                else ghost.removeAttribute('data-completed');
            }

            if (mode !== 'progress') {
                refreshGhostPast(dx);
                if (dragOptions && dragOptions.hasCanDrop) checkCanDrop(dx);
            }
        };

        const onPointerUp = async (up) => {
            // Codex P2 finding ("Isolate each drag to its initiating
            // pointer"): a second pointer's release must never resolve/
            // commit THIS closure's drag (or fire its click fallback).
            if (up.pointerId !== pointerId) return;
            cleanup();
            if (!dragInitiated) {
                // gantt-v2.js:617-622 — below the drag threshold, a 'move'-mode
                // mousedown falls back to a click. Only 'move' has this fallback in
                // v2 (a below-threshold 'resize'/'progress'-mode mousedown is NOT a
                // click there either), so this port narrows the same way. Milestones
                // always resolve to 'move' (resolveHitMode), so they get this for
                // free — see NotifyTaskClick's own remarks for the readonly-parity
                // deviation from v2's separate, unconditional milestone click listener.
                if (mode === 'move') {
                    if (dragDotNet) dragDotNet.invokeMethodAsync('NotifyTaskClick', taskId).catch(() => {});
                }
                return;
            }

            const dx = up.clientX - startClientX;

            if (mode === 'progress') {
                // gantt-v2.js:758 `Math.round(origProgress + (dx / barW) * 100)`,
                // clamped exactly like normalizeTasks' own progress clamp
                // (gantt-v2.js:81). No CanDrop validation for progress (plan:
                // "Progress drag is NOT validated — CanDrop is about scheduling").
                // RTL note: same sign flip as the move-preview branch above —
                // the pointer-up commit must agree with what the ghost last showed.
                const newProgress = Math.round(clampProgress(origProgress + ((isRtl ? -dx : dx) / geo.width) * 100));
                if (newProgress === origProgress) return; // gantt-v2.js:759 no-op, no commit
                if (dragDotNet) dragDotNet.invokeMethodAsync('CommitProgress', taskId, newProgress).catch(() => {});
                return;
            }

            // gantt-v2.js:743 `const dayPx = pixelsPerDay(inst.viewMode);` — here
            // pixelsPerDay comes from .NET (dragOptions.pixelsPerDay), never
            // re-derived: GanttScale.ViewModes is the single source of truth.
            const dayPx = dragOptions && dragOptions.pixelsPerDay > 0 ? dragOptions.pixelsPerDay : 1;
            // gantt-v2.js:746/752 `Math.round(dx / dayPx)` — Math.round, not a
            // custom tie-break: unlike GanttScale.PixelToDate (which mirrors
            // Math.round's negative-tie behavior in C# via RoundToInt), this
            // literally IS the JS Math.round v2 used, so no port is needed.
            const movedDays = Math.round(dx / dayPx);
            if (movedDays === 0) return; // gantt-v2.js:747/753 no-op re-render, no commit

            const { newStart, newEnd } = computeSnappedDates(mode, movedDays, origStart, origEnd);

            if (dragOptions && dragOptions.hasCanDrop) {
                const key = `${mode}|${toLocalDateString(newStart)}|${toLocalDateString(newEnd)}`;
                let promise = validationCache.get(key);
                if (!promise) {
                    // Not already checked during the move (e.g. threshold crossed and
                    // released in the same snap step) — one fresh call, cached like any
                    // other. Bug fix (Codex P1 "Fail closed when CanDrop invocation
                    // rejects") — see checkCanDrop's own remarks; identical reasoning.
                    promise = dragDotNet
                        ? dragDotNet.invokeMethodAsync('ValidateDrop', taskId, mode, toLocalDateString(newStart), toLocalDateString(newEnd)).catch(() => false)
                        : Promise.resolve(false);
                    validationCache.set(key, promise);
                }
                const valid = await promise;
                if (!valid) return; // invalid (or unconfirmable) drop position — revert silently, no commit, no events
            }

            if (dragDotNet) {
                dragDotNet.invokeMethodAsync('CommitDrag', taskId, mode, toLocalDateString(newStart), toLocalDateString(newEnd))
                    .catch(() => {});
            }
        };

        const onPointerCancel = (cn) => {
            // Codex P2 finding ("Isolate each drag to its initiating
            // pointer"): same pointerId gate as onPointerMove/onPointerUp —
            // a different pointer's cancel must not tear THIS drag down.
            if (cn.pointerId !== pointerId) return;
            cleanup();
        };

        function cleanup() {
            barEl.removeEventListener('pointermove', onPointerMove);
            barEl.removeEventListener('pointerup', onPointerUp);
            barEl.removeEventListener('pointercancel', onPointerCancel);
            try { barEl.releasePointerCapture(pointerId); } catch (_) { /* already released */ }
            if (ghost && ghost.parentNode) ghost.parentNode.removeChild(ghost);
            barEl.removeAttribute('data-dragging'); // styling-hooks audit — harmless no-op below the drag threshold (never set)
            activeBarDrags.delete(barEl);
            activeDragGestureCount--; // design spec Phase 3, T9 — mirrors the activeBarDrags.add above
            // Bug fix (Codex P2 finding "Cancel active drags when
            // unregistering") — see unregisterDrag's own remarks: this
            // session no longer needs external cancellation once it has
            // torn itself down via its own pointerup/pointercancel.
            reg.activeCleanups.delete(cleanup);
        }

        // Bug fix (Codex P2 finding "Cancel active drags when
        // unregistering"): tracked from the moment listeners actually
        // attach, unconditionally — a below-threshold (not yet
        // dragInitiated) pointer is still a live session with real
        // listeners/pointer-capture that a Readonly flip must be able to
        // tear down, not only a session that has already produced a ghost.
        reg.activeCleanups.add(cleanup);

        barEl.addEventListener('pointermove', onPointerMove);
        barEl.addEventListener('pointerup', onPointerUp);
        barEl.addEventListener('pointercancel', onPointerCancel);
    };

    el.addEventListener('pointerdown', reg.onPointerDown);
    dragRegistrations.set(el, reg);
}

// Phase 2, T3 — drag-create on an empty row track (REUI parity addition, no v2
// equivalent — v2 has no drag-create at all). Entered ONLY from onPointerDown's
// "no bar was hit" branch above, so a genuine bar click/drag can never reach
// here. trackEl is one of GanttTimeline's per-row `[data-gantt-row-track]`
// divs (own remarks: rendered BEFORE the bars in DOM order so a bar always
// wins the hit-test first) — its own inline `top`/`height` ARE the row's
// row-canvas-space geometry (no CSS-var indirection to resolve, unlike a
// bar's --lumeo-gantt-bar-x/-w — see readBarGeometry's own comment for why
// THAT needs getComputedStyle), and its `data-row-key` is the stable row
// identity GanttTimeline.CommitCreate resolves back against EffectiveRows.
//
// Unlike a bar drag, there is no existing element to clone a ghost from and no
// original Start/End to anchor deltas against — the ghost is built from
// scratch, and the pointer's OWN local X position (relative to the track,
// which starts at row-canvas x=0) is converted to an absolute day-COLUMN index
// via Math.floor (which grid column contains this pixel), not the delta-based
// Math.round the move/resize paths use for a RELATIVE shift.
function startCreateDrag(dragOptions, dragDotNet, reg, trackEl, e) {
    const rowKey = trackEl.getAttribute('data-row-key');
    if (!rowKey) return;

    e.preventDefault();
    // Bug fix (Codex P2 finding "Filter drag-create events to their
    // initiating pointer"): captured once, compared in every move/up/cancel
    // handler below — mirrors activeBarDrags/the bar-drag closure's own
    // identical pointerId gate (see its own remarks). Without this, a
    // second contact (multi-touch, or pen+touch) landing on the SAME track
    // while this drag is in flight would run BOTH this closure's handlers
    // AND the second contact's own, since both listen on the same trackEl —
    // a release from either could commit a task built from the WRONG
    // contact's geometry. activeCreateDrags (the onPointerDown-side half of
    // this fix) rejects a second pointerdown on the same track outright, so
    // this pointerId check only ever has to defend against the SAME track's
    // own already-rejected second contact reaching move/up/cancel some
    // other way (e.g. a pointer that started BEFORE AllowCreate/handler
    // registration changed).
    const pointerId = e.pointerId;
    trackEl.setPointerCapture(pointerId);
    activeCreateDrags.add(trackEl);
    activeDragGestureCount++; // design spec Phase 3, T9 — see its own declaration for why this is a counter, not a boolean

    const rect = trackEl.getBoundingClientRect();
    const startLocalX = e.clientX - rect.left;
    const rowTop = parseFloat(trackEl.style.top) || 0;
    const rowHeight = parseFloat(trackEl.style.height) || 0;
    const startClientX = e.clientX;
    let dragInitiated = false;
    let ghost = null;

    function dayColumnRange(clientX) {
        const dayPx = dragOptions && dragOptions.pixelsPerDay > 0 ? dragOptions.pixelsPerDay : 1;
        const localX = startLocalX + (clientX - startClientX);
        const dayA = Math.floor(startLocalX / dayPx);
        const dayB = Math.floor(localX / dayPx);
        return { fromDay: Math.min(dayA, dayB), toDay: Math.max(dayA, dayB), dayPx };
    }

    // Bug fix (Codex P2 finding "Map drag-create pixels through the active
    // calendar scale") — see GanttTimeline.BuildDragOptions' own remarks for
    // scaleUnit. Day/Hour/Week (all GanttScaleUnit.Day internally — Week is
    // just Step=7) keep the dayPx-based linear math above: a "day" is a
    // fixed-duration unit, so addDays(origin, floor(pixel/dayPx)) is EXACT
    // there, not approximate. Month/Year are REAL calendar units of
    // VARIABLE length — GanttScale.PixelToDate's own Month/Year branches
    // never divide by an approximate 30/365-day constant; they resolve a
    // COLUMN INDEX (pixel / columnWidth — uniform regardless of how many
    // real days that particular month/year happens to have) and step the
    // calendar unit itself by that index. Mirrored here (not round-tripped
    // to .NET per pixel — T3's create-drag is a ghost-only, JS-local
    // preview by design) for the ONE place precision actually matters: the
    // final commit date, not the ghost's own cosmetic width/position (which
    // still uses dayColumnRange's approximation above — purely visual, no
    // calendar meaning).
    function resolveColumnDate(localX) {
        const unit = dragOptions && dragOptions.scaleUnit;
        // Bug fix (Codex review, P2 #5): Quarter (design spec Phase 3, T2 —
        // v3-only, no v2 counterpart) is ALSO a real calendar unit of
        // variable length (91/92/92/92 days, and a leap-year Q1 differs
        // again) — it belongs in this branch alongside Month/Year, not the
        // fixed-day-count fallback below. Column index -> calendar date the
        // SAME way the Month branch does, just stepping 3 months per column
        // instead of 1 — mirrors GanttScale.PixelToDate's own
        // `GanttScaleUnit.Quarter => origin.AddMonths(... * 3)` branch
        // (C# side), so drag-create snaps to the calendar-quarter start
        // exactly like every other v3-only-scale JS/C# pair in this file
        // already keeps in lockstep.
        if (unit === 'Month' || unit === 'Year' || unit === 'Quarter') {
            const colW = dragOptions && dragOptions.columnWidth > 0 ? dragOptions.columnWidth : 1;
            const idx = Math.floor(localX / colW);
            if (unit === 'Month') return new Date(origin.getFullYear(), origin.getMonth() + idx, 1);
            if (unit === 'Quarter') return new Date(origin.getFullYear(), origin.getMonth() + idx * 3, 1);
            return new Date(origin.getFullYear() + idx, 0, 1);
        }
        const dayPx = dragOptions && dragOptions.pixelsPerDay > 0 ? dragOptions.pixelsPerDay : 1;
        return addDays(origin, Math.floor(localX / dayPx));
    }

    const originIso = dragOptions && dragOptions.originIso;
    const origin = originIso ? parseIsoDate(originIso) : null;

    const onPointerMove = (mv) => {
        if (mv.pointerId !== pointerId) return;
        const dx = mv.clientX - startClientX;
        if (!dragInitiated) {
            // gantt-v2.js:610-style threshold (DRAG_THRESHOLD_PX) — the actual
            // "below-threshold release -> no ghost residue, no call" gate the
            // plan asks for; once past it, the resulting snapped range is
            // guaranteed at least one day (span >= 1 snap unit) by construction.
            if (Math.abs(dx) < DRAG_THRESHOLD_PX) return;
            dragInitiated = true;
            ghost = document.createElement('div');
            ghost.className = 'lumeo-gantt-v3-drag-ghost lumeo-gantt-v3-create-ghost rounded';
            ghost.style.position = 'absolute';
            ghost.style.top = rowTop + 'px';
            ghost.style.height = rowHeight + 'px';
            ghost.style.opacity = '0.6';
            ghost.style.pointerEvents = 'none';
            ghost.style.zIndex = '50';
            ghost.style.backgroundColor = 'var(--color-primary)';
            trackEl.parentNode.appendChild(ghost);
        }

        const { fromDay, toDay, dayPx } = dayColumnRange(mv.clientX);
        ghost.style.left = (fromDay * dayPx) + 'px';
        ghost.style.width = Math.max(GHOST_MIN_WIDTH_PX, (toDay - fromDay + 1) * dayPx) + 'px';
    };

    const onPointerUp = (up) => {
        if (up.pointerId !== pointerId) return;
        cleanup();
        if (!dragInitiated) return; // below threshold — no ghost residue, no call (plan requirement)
        if (!origin) return; // no anchor date — nothing sane to commit

        const endLocalX = startLocalX + (up.clientX - startClientX);
        const dateA = resolveColumnDate(startLocalX);
        const dateB = resolveColumnDate(endLocalX);
        const [start, end] = dateA <= dateB ? [dateA, dateB] : [dateB, dateA];

        const startIso = toLocalDateString(start);
        const endIso = toLocalDateString(end);

        if (dragDotNet) dragDotNet.invokeMethodAsync('CommitCreate', rowKey, startIso, endIso).catch(() => {});
    };

    const onPointerCancel = (cn) => {
        if (cn.pointerId !== pointerId) return;
        cleanup();
    };

    function cleanup() {
        trackEl.removeEventListener('pointermove', onPointerMove);
        trackEl.removeEventListener('pointerup', onPointerUp);
        trackEl.removeEventListener('pointercancel', onPointerCancel);
        try { trackEl.releasePointerCapture(pointerId); } catch (_) { /* already released */ }
        if (ghost && ghost.parentNode) ghost.parentNode.removeChild(ghost);
        activeCreateDrags.delete(trackEl);
        activeDragGestureCount--; // design spec Phase 3, T9 — mirrors the activeCreateDrags.add above
        // Bug fix (Codex P2 finding "Cancel active drags when
        // unregistering") — see unregisterDrag's own remarks; identical
        // reasoning to the bar-drag closure's own reg.activeCleanups use.
        reg.activeCleanups.delete(cleanup);
    }

    reg.activeCleanups.add(cleanup);
    trackEl.addEventListener('pointermove', onPointerMove);
    trackEl.addEventListener('pointerup', onPointerUp);
    trackEl.addEventListener('pointercancel', onPointerCancel);
}

function unregisterDrag(el) {
    if (!el) return;
    const reg = dragRegistrations.get(el);
    if (!reg) return;
    el.removeEventListener('pointerdown', reg.onPointerDown);
    dragRegistrations.delete(el);

    // Bug fix (Codex P2 finding "Cancel active drags when unregistering"):
    // removing the delegated pointerdown listener above stops any NEW
    // gesture from starting, but a drag ALREADY in flight (its own
    // pointermove/pointerup/pointercancel listeners, pointer capture, and
    // ghost — bar-drag or create-drag alike) is entirely independent of
    // that listener and survives it untouched. If the caller's own Readonly
    // guard flips back to false before that pointer releases, the
    // surviving closure would still reach CommitDrag/CommitCreate — a
    // gesture that should have been cancelled the instant Readonly went
    // true still mutating the task. Running every active session's own
    // cleanup() (registered in reg.activeCleanups the moment each one's
    // listeners attached — see registerDrag/startCreateDrag's own remarks)
    // tears each down through the EXACT same path its own pointerup/
    // pointercancel already uses, deliberately WITHOUT ever reaching the
    // commit code that only runs AFTER a cleanup() call inside those
    // handlers — so this cancels, it never commits. Array.from snapshots
    // the set first since each cleanup() call mutates it (deletes itself)
    // while this loop is still iterating.
    for (const cleanup of Array.from(reg.activeCleanups)) cleanup();
}

// ── Splitter drag (design spec Phase 3, T5) ─────────────────────────────────
//
// The tree/timeline splitter — a SEPARATE registration channel from
// registerDrag above (different element shape: ONE dedicated handle rather
// than a delegated listener over many recycled bars), but the SAME conventions
// throughout, per the T5 dispatch's explicit "introduce no second drag idiom":
// pointer capture on the handle at pointerdown, a per-gesture options snapshot
// taken at that same moment (dragOptions/dragPaneEl below — reg.options/
// reg.paneEl are mutated in place by a later idempotent re-registration, same
// hazard registerDrag's own "Snapshot drag options at pointerdown" fix
// documents), pointer-id isolation on every subsequent event, and every
// gesture's cleanup() tracked in reg.activeCleanups so unregisterSplitterDrag
// can cancel an in-flight drag exactly like unregisterDrag already does for a
// bar drag.
//
// Live visual: DIRECT DOM mutation of dragPaneEl's own `width` inline style
// plus the --lumeo-gantt-tree-name-width custom property (inherited by every
// name-cell — see GanttTree.razor's own remarks), never a per-pointermove
// Blazor round-trip. The ONE JSInvokable call (CommitSplitterWidth) fires
// exactly once, at pointerup, mirroring registerDrag's own bar-drag commit
// discipline (ghost/live-visual during the gesture, one commit at the end) —
// there is no ghost element here because, unlike a bar move/resize, a
// splitter resize has no rejectable/invalid position to visually preview
// separately from the real element.
const splitterRegistrations = new Map(); // handleEl -> { dotNetRef, options, paneEl, onPointerDown, activeCleanups }

function registerSplitterDrag(handleEl, paneEl, dotNetRef, options) {
    if (!handleEl || !paneEl) return;
    const existing = splitterRegistrations.get(handleEl);
    if (existing) {
        // Idempotent re-registration (a controlled TreePaneWidth push, or this
        // component's own prior commit landing back through Gantt3) — swap the
        // stored dotNetRef/options/paneEl in place, same as registerDrag's own
        // idempotent branch.
        existing.dotNetRef = dotNetRef;
        existing.options = options;
        existing.paneEl = paneEl;
        return;
    }

    const reg = { dotNetRef, options, paneEl, onPointerDown: null, activeCleanups: new Set() };

    reg.onPointerDown = (e) => {
        if (e.button !== 0) return; // left mouse / primary touch-pen contact only

        // Snapshot at pointerdown (registerDrag's own "Snapshot drag options at
        // pointerdown" fix, applied here identically) — a later idempotent
        // re-registration mutates reg.* in place and must not reach an
        // already-running gesture.
        const dragOptions = reg.options;
        const dragDotNet = reg.dotNetRef;
        const dragPaneEl = reg.paneEl;

        e.preventDefault();
        const pointerId = e.pointerId;
        handleEl.setPointerCapture(pointerId);

        const startClientX = e.clientX;
        const startNameWidth = dragOptions && typeof dragOptions.width === 'number' ? dragOptions.width : 0;
        const startTotalWidth = dragPaneEl.getBoundingClientRect().width;
        const minWidth = dragOptions && dragOptions.minWidth > 0 ? dragOptions.minWidth : 0;
        const maxWidth = dragOptions && dragOptions.maxWidth > 0 ? dragOptions.maxWidth : Infinity;
        // Same RTL sign-flip convention as the progress-handle's own `isRtl`
        // (see registerDrag's own remarks) — under RTL this pane's free
        // (resizable) edge is physically LEFT, so growing it means moving the
        // pointer LEFT, not right.
        const isRtl = getComputedStyle(dragPaneEl).direction === 'rtl';
        let nameWidth = startNameWidth;

        const onPointerMove = (mv) => {
            if (mv.pointerId !== pointerId) return;
            const rawDx = mv.clientX - startClientX;
            const dx = isRtl ? -rawDx : rawDx;
            nameWidth = Math.min(maxWidth, Math.max(minWidth, startNameWidth + dx));
            // The extra (fixed-width) TreeColumns move 1:1 with the CLAMPED
            // name-width delta — once nameWidth hits a bound, the applied
            // delta (and so the total width) stops growing too.
            const totalWidth = startTotalWidth + (nameWidth - startNameWidth);
            dragPaneEl.style.width = totalWidth + 'px';
            dragPaneEl.style.setProperty('--lumeo-gantt-tree-name-width', nameWidth + 'px');
        };

        const onPointerUp = (up) => {
            if (up.pointerId !== pointerId) return;
            cleanup();
            if (Math.abs(nameWidth - startNameWidth) < 0.5) return; // no-op, no commit — mirrors registerDrag's own 0-delta no-op
            if (dragDotNet) dragDotNet.invokeMethodAsync('CommitSplitterWidth', nameWidth).catch(() => {});
        };

        const onPointerCancel = (cn) => {
            if (cn.pointerId !== pointerId) return;
            cleanup();
            // No commit happened — the DOM must not be left showing a width
            // .NET never adopted (there is no ghost to simply discard here;
            // the real element was mutated directly, so reverting it IS the
            // "discard the aborted gesture" step).
            dragPaneEl.style.width = startTotalWidth + 'px';
            dragPaneEl.style.setProperty('--lumeo-gantt-tree-name-width', startNameWidth + 'px');
        };

        function cleanup() {
            handleEl.removeEventListener('pointermove', onPointerMove);
            handleEl.removeEventListener('pointerup', onPointerUp);
            handleEl.removeEventListener('pointercancel', onPointerCancel);
            try { handleEl.releasePointerCapture(pointerId); } catch (_) { /* already released */ }
            reg.activeCleanups.delete(cleanup);
        }

        reg.activeCleanups.add(cleanup);
        handleEl.addEventListener('pointermove', onPointerMove);
        handleEl.addEventListener('pointerup', onPointerUp);
        handleEl.addEventListener('pointercancel', onPointerCancel);
    };

    handleEl.addEventListener('pointerdown', reg.onPointerDown);
    splitterRegistrations.set(handleEl, reg);
}

function unregisterSplitterDrag(handleEl) {
    if (!handleEl) return;
    const reg = splitterRegistrations.get(handleEl);
    if (!reg) return;
    handleEl.removeEventListener('pointerdown', reg.onPointerDown);
    splitterRegistrations.delete(handleEl);
    // Cancel (never commit) any drag still in flight — same reasoning as
    // unregisterDrag's own identical loop (a Dispose/re-render racing a live
    // gesture must not let it reach CommitSplitterWidth afterward).
    for (const cleanup of Array.from(reg.activeCleanups)) cleanup();
}

// Bug fix (Codex review, P2 #9): registerSplitterDrag's own onPointerMove
// mutates paneEl's inline width/--lumeo-gantt-tree-name-width DIRECTLY
// during the live gesture — the pointerCANCEL path already reverts that (see
// its own remarks: "there is no ghost to simply discard here; the real
// element was mutated directly, so reverting it IS the discard step"), but
// pointer-UP had no equivalent for a CONTROLLED caller that VETOES the
// resulting CommitSplitterWidth request: if the parent keeps TreePaneWidth
// unchanged, Blazor's next render computes the SAME style string it
// rendered before this drag ever started, its diff sees no change, and the
// JS-mutated (rejected) width is left in the DOM indefinitely. Called
// UNCONDITIONALLY from GanttTree.CommitSplitterWidth right after its own
// round trip resolves (accepted or vetoed) to force-sync the DOM back to
// whatever the resolved, authoritative width actually is — same values,
// same two lines, as the pointercancel revert above, just parameterized
// for the post-commit case instead of the mid-gesture-abort one.
function resetSplitterWidth(paneEl, totalWidth, nameWidth) {
    if (!paneEl) return;
    paneEl.style.width = totalWidth + 'px';
    paneEl.style.setProperty('--lumeo-gantt-tree-name-width', nameWidth + 'px');
}

// ── Row-reorder drag (design spec Phase 3, T6) ──────────────────────────────
//
// A THIRD registration channel, reusing the SAME conventions registerDrag/
// registerSplitterDrag already established (pointer capture at pointerdown,
// a per-gesture snapshot of reg.dotNetRef taken at that same moment, pointer-
// id isolation, every gesture's cleanup() tracked in reg.activeCleanups so
// unregisterRowReorderDrag can cancel an in-flight drag exactly like the
// other two channels' own unregister functions do). Shape-wise it sits
// between the two: a DELEGATED pointerdown listener (like registerDrag — GanttTree's
// rows are Virtualize-recycled, so a listener per row would need constant
// re-attachment), but filtered to a dedicated grip element per row (like
// registerSplitterDrag's dedicated handle, NOT registerDrag's whole-bar hit
// area — a tree row's whole surface is a click target for its OWN
// toggle/checkbox/RowTemplate content, so the grip is the only unambiguous
// drag initiator, same reasoning Lumeo.DataGrid's own row-reorder grip
// documents).
//
// No options bag: unlike registerDrag/registerSplitterDrag (which need .NET
// to push live config — columnWidth/pixelsPerDay, min/max), a candidate row's
// bucket/index identity is read directly off its OWN `data-reorder-bucket`/
// `data-reorder-index` DOM attributes — GanttTree re-renders those fresh
// every pass, so there is nothing to snapshot or go stale.
//
// Ghost + drop-index line (design spec Phase 3, T6 — "drag-engine JS family:
// ghost row, drop-index line"): the ghost is a translucent clone of the
// dragged row (mirrors makeGhost's bar-ghost idiom, vertically-only —
// "drag tree rows vertically"); the drop-index line is a thin horizontal
// indicator positioned at the boundary between two candidate sibling rows,
// re-validated continuously against ValidateRowDrop exactly like
// registerDrag's own checkCanDrop (cached per candidate index, repainted
// invalid via the SAME CSS-var-only convention setGhostInvalid already
// established — see setDropLineInvalid).
const rowReorderRegistrations = new Map(); // paneEl -> { dotNetRef, onPointerDown, activeCleanups }
const activeRowReorderDrags = new WeakSet(); // gripEl -> currently being dragged by some pointer

function makeRowReorderGhost(rowEl, paneEl) {
    const rect = rowEl.getBoundingClientRect();
    const ghost = rowEl.cloneNode(true);
    ghost.classList.add('lumeo-gantt-v3-drag-ghost', 'lumeo-gantt-v3-row-reorder-ghost');
    ghost.removeAttribute('data-task-id'); // never itself a hit-test target for a second, nested pointerdown
    ghost.removeAttribute('data-reorder-bucket');
    ghost.removeAttribute('data-reorder-index');
    ghost.style.position = 'absolute';
    ghost.style.left = '0px';
    ghost.style.width = rect.width + 'px';
    ghost.style.opacity = '0.85';
    ghost.style.pointerEvents = 'none';
    ghost.style.zIndex = '60';
    ghost.style.boxShadow = '0 2px 8px rgba(0, 0, 0, 0.15)';
    paneEl.appendChild(ghost);
    return ghost;
}

function makeRowReorderDropLine(paneEl) {
    const line = document.createElement('div');
    line.className = 'lumeo-gantt-v3-row-reorder-drop-line';
    line.setAttribute('data-row-reorder-drop-line', 'true');
    line.style.position = 'absolute';
    line.style.left = '0px';
    line.style.right = '0px';
    line.style.height = '2px';
    line.style.backgroundColor = 'var(--color-primary)';
    line.style.pointerEvents = 'none';
    line.style.zIndex = '55';
    line.style.display = 'none';
    paneEl.appendChild(line);
    return line;
}

// CSS-vars-only invalid-drop paint (house rules) — same idiom as
// setGhostInvalid, applied to the drop-index line rather than a bar ghost:
// the line itself is the affordance that reads as "you can drop HERE", so a
// rejected candidate position repaints the LINE, not the ghost (the ghost
// represents the dragged content, unaffected by whether ITS CURRENT target
// is valid).
function setRowReorderDropLineInvalid(line, invalid) {
    if (!line) return;
    line.style.backgroundColor = invalid ? 'var(--color-destructive)' : 'var(--color-primary)';
}

function registerRowReorderDrag(paneEl, dotNetRef) {
    if (!paneEl) return;
    const existing = rowReorderRegistrations.get(paneEl);
    if (existing) {
        // Idempotent re-registration — swap the stored dotNetRef in place,
        // same as registerDrag/registerSplitterDrag's own idempotent branch.
        existing.dotNetRef = dotNetRef;
        return;
    }

    const reg = { dotNetRef, onPointerDown: null, activeCleanups: new Set() };

    reg.onPointerDown = (e) => {
        if (e.button !== 0) return; // left mouse / primary touch-pen contact only

        const gripEl = e.target.closest('[data-row-reorder-grip]');
        if (!gripEl || !paneEl.contains(gripEl)) return;
        if (activeRowReorderDrags.has(gripEl)) return; // isolate to the initiating pointer, mirrors activeBarDrags

        const rowEl = gripEl.closest('[data-task-id]');
        if (!rowEl || !paneEl.contains(rowEl)) return;
        const taskId = rowEl.getAttribute('data-task-id');
        const bucket = rowEl.getAttribute('data-reorder-bucket');
        const originalIndex = parseInt(rowEl.getAttribute('data-reorder-index'), 10);
        if (!taskId || bucket === null || Number.isNaN(originalIndex)) return;

        // Snapshot at pointerdown (registerDrag's own "Snapshot drag options
        // at pointerdown" fix, applied identically here) — a later idempotent
        // re-registration mutates reg.dotNetRef in place and must not reach
        // an already-running gesture.
        const dragDotNet = reg.dotNetRef;

        e.preventDefault();
        const pointerId = e.pointerId;
        gripEl.setPointerCapture(pointerId);
        activeRowReorderDrags.add(gripEl);

        const startClientY = e.clientY;
        let dragInitiated = false;
        let ghost = null;
        let dropLine = null;
        let lastTargetIndex = originalIndex;
        const validationCache = new Map(); // targetIndex -> Promise<bool>

        // Every OTHER currently-rendered row sharing this drag's own bucket
        // (equality on the raw attribute string — never a CSS selector built
        // from it, so an arbitrary consumer-supplied task id/GroupLabel can
        // never need escaping). Recomputed live each move — Virtualize can
        // recycle rows mid-scroll-drag, so a cached element list would go stale.
        function siblingRows() {
            const all = paneEl.querySelectorAll('[data-reorder-bucket]');
            const result = [];
            for (const el of all) {
                if (el !== rowEl && el.getAttribute('data-reorder-bucket') === bucket) result.push(el);
            }
            return result;
        }

        // Nearest-candidate hit test: the sibling whose vertical CENTER is
        // closest to the pointer wins; above its center = insert BEFORE it
        // (target index = its own current index), below = insert AFTER
        // (index + 1). edgeY is where the drop-line paints (that candidate's
        // own top or bottom edge, whichever half of it was measured closest).
        function resolveTarget(clientY) {
            const candidates = siblingRows();
            if (candidates.length === 0) {
                const rect = rowEl.getBoundingClientRect();
                return { index: 0, edgeY: rect.top + rect.height / 2 };
            }
            let best = null;
            let bestDist = Infinity;
            for (const el of candidates) {
                const rect = el.getBoundingClientRect();
                const centerY = rect.top + rect.height / 2;
                const dist = Math.abs(clientY - centerY);
                if (dist < bestDist) {
                    bestDist = dist;
                    const idx = parseInt(el.getAttribute('data-reorder-index'), 10);
                    const after = clientY > centerY;
                    best = { index: after ? idx + 1 : idx, edgeY: after ? rect.bottom : rect.top };
                }
            }
            return best;
        }

        function checkValid(targetIndex) {
            let promise = validationCache.get(targetIndex);
            if (!promise) {
                // Fail closed on a rejected invocation — same reasoning as
                // registerDrag's own checkCanDrop ("Fail closed when CanDrop
                // invocation rejects"): a thrown consumer predicate or a
                // transient interop failure must not silently permit the drop.
                promise = dragDotNet
                    ? dragDotNet.invokeMethodAsync('ValidateRowDrop', taskId, targetIndex).catch(() => false)
                    : Promise.resolve(true);
                validationCache.set(targetIndex, promise);
            }
            promise.then((valid) => {
                if (lastTargetIndex !== targetIndex) return; // superseded by a later hovered position already
                setRowReorderDropLineInvalid(dropLine, !valid);
            });
        }

        const onPointerMove = (mv) => {
            if (mv.pointerId !== pointerId) return;
            const dy = mv.clientY - startClientY;
            if (!dragInitiated) {
                if (Math.abs(dy) < DRAG_THRESHOLD_PX) return;
                dragInitiated = true;
                ghost = makeRowReorderGhost(rowEl, paneEl);
                dropLine = makeRowReorderDropLine(paneEl);
                rowEl.style.opacity = '0.3'; // dim the real row while its ghost follows the pointer — same "one visible representation" intent as the bar-drag ghost
            }

            const paneRect = paneEl.getBoundingClientRect();
            const rowRect = rowEl.getBoundingClientRect();
            ghost.style.top = (rowRect.top - paneRect.top + dy) + 'px';

            const target = resolveTarget(mv.clientY);
            lastTargetIndex = target.index;
            dropLine.style.top = (target.edgeY - paneRect.top) + 'px';
            dropLine.style.display = 'block';
            checkValid(target.index);
        };

        const onPointerUp = async (up) => {
            if (up.pointerId !== pointerId) return;
            cleanup();
            if (!dragInitiated) return; // below threshold — no ghost residue, no call
            if (lastTargetIndex === originalIndex) return; // no-op position, no commit — mirrors registerDrag's own 0-delta no-op

            // Bug fix (Codex review, P1 #2): this used to read `lastValid`
            // (a plain closure variable, initialized `true` and only ever
            // flipped by checkValid's fire-and-forget `.then()`) SYNCHRONOUSLY
            // here — a release landing before the ValidateRowDrop round trip
            // for the CURRENT lastTargetIndex resolves committed on whatever
            // `lastValid` happened to be left at (possibly still its initial
            // `true`), failing OPEN. Mirrors registerDrag's own onPointerUp
            // fail-closed pattern exactly: await the SAME cached promise
            // checkValid keys validationCache by (already in flight if this
            // position was hovered during the move; freshly created — same
            // fail-closed `.catch(() => false)` — if pointer-up is reached
            // before checkValid ever got called for it, e.g. threshold
            // crossed and released in one step).
            let promise = validationCache.get(lastTargetIndex);
            if (!promise) {
                promise = dragDotNet
                    ? dragDotNet.invokeMethodAsync('ValidateRowDrop', taskId, lastTargetIndex).catch(() => false)
                    : Promise.resolve(true);
                validationCache.set(lastTargetIndex, promise);
            }
            const valid = await promise;
            if (!valid) return; // invalid (or unconfirmable) drop position — revert silently, no commit

            if (dragDotNet) dragDotNet.invokeMethodAsync('CommitRowReorder', taskId, lastTargetIndex).catch(() => {});
        };

        const onPointerCancel = (cn) => {
            if (cn.pointerId !== pointerId) return;
            cleanup();
        };

        function cleanup() {
            gripEl.removeEventListener('pointermove', onPointerMove);
            gripEl.removeEventListener('pointerup', onPointerUp);
            gripEl.removeEventListener('pointercancel', onPointerCancel);
            try { gripEl.releasePointerCapture(pointerId); } catch (_) { /* already released */ }
            if (ghost && ghost.parentNode) ghost.parentNode.removeChild(ghost);
            if (dropLine && dropLine.parentNode) dropLine.parentNode.removeChild(dropLine);
            rowEl.style.opacity = '';
            activeRowReorderDrags.delete(gripEl);
            reg.activeCleanups.delete(cleanup);
        }

        reg.activeCleanups.add(cleanup);
        gripEl.addEventListener('pointermove', onPointerMove);
        gripEl.addEventListener('pointerup', onPointerUp);
        gripEl.addEventListener('pointercancel', onPointerCancel);
    };

    paneEl.addEventListener('pointerdown', reg.onPointerDown);
    rowReorderRegistrations.set(paneEl, reg);
}

function unregisterRowReorderDrag(paneEl) {
    if (!paneEl) return;
    const reg = rowReorderRegistrations.get(paneEl);
    if (!reg) return;
    paneEl.removeEventListener('pointerdown', reg.onPointerDown);
    rowReorderRegistrations.delete(paneEl);
    // Cancel (never commit) any drag still in flight — same reasoning as
    // unregisterDrag/unregisterSplitterDrag's own identical loops (a
    // Dispose/re-render racing a live gesture must not let it reach
    // CommitRowReorder afterward).
    for (const cleanup of Array.from(reg.activeCleanups)) cleanup();
}

// ── Bar context menu (design spec Phase 3, T8) ──────────────────────────────
//
// A FOURTH, SEPARATE registration channel alongside registerDrag/
// registerSplitterDrag/registerRowReorderDrag — deliberately NOT folded into
// registerDrag's own registration object, for one concrete reason:
// registerDrag is torn down ENTIRELY whenever Readonly is true
// (SyncDragRegistrationAsync's own "no listeners at all when Readonly"
// contract), but BarContextMenu is a VIEW action (right-click -> caller-
// supplied menu content), not an edit — it must stay available on a Readonly
// chart (a consumer's own menu content can still offer non-mutating actions
// like "view details"/"copy" there, exactly the same way GanttBar's tooltip
// and keyboard focus stay available when Readonly). Sharing registerDrag's
// registration would tie this feature's lifecycle to Readonly for no reason.
//
// Delegated on the SAME scroll-host element registerDrag uses (bars/tracks
// live there) — a native 'contextmenu' event, not a pointer event, so it
// needs no pointer-capture/pointer-id isolation of its own; the isolation
// this needs is from a CONCURRENT bar drag, not from a second contextmenu
// gesture (there is only ever one contextmenu event per right-click).
//
// Drag isolation (design spec Phase 3, T8, decision 4): checks the SAME
// module-level `activeBarDrags` WeakSet registerDrag's own onPointerDown
// populates/clears — true for exactly the span of a real, currently-in-flight
// pointer session on that bar (from pointerdown, whether or not the
// DRAG_THRESHOLD_PX has been crossed yet, through pointerup/pointercancel's
// own synchronous cleanup() — see registerDrag's remarks). A right-click
// landing on a bar mid-session is swallowed entirely (preventDefault, no
// NotifyBarContextMenu call, no native menu either) rather than opening a
// menu that could then race a still-in-flight move/resize commit. This is
// symmetric with the OTHER direction (a real drag never starts from a
// right-button pointerdown at all — registerDrag's own `if (e.button !== 0)
// return;` gate, unconditional and unrelated to this channel).
const barContextMenuRegistrations = new Map(); // el -> { dotNetRef, onContextMenu }

function registerBarContextMenu(el, dotNetRef) {
    if (!el) return;
    const existing = barContextMenuRegistrations.get(el);
    if (existing) {
        // Idempotent re-registration — mirrors registerDrag's own dotNetRef-swap-
        // in-place precedent (a re-render handing a fresh DotNetObjectReference
        // must not attach a second listener).
        existing.dotNetRef = dotNetRef;
        return;
    }

    const onContextMenu = (e) => {
        const barEl = e.target.closest('[data-task-id]');
        if (!barEl || !el.contains(barEl)) return; // not a bar — leave the native/default context menu alone
        if (activeBarDrags.has(barEl)) {
            e.preventDefault(); // a real drag/pending gesture already owns this bar — swallow, no menu, no native fallback either
            return;
        }
        e.preventDefault();
        const reg = barContextMenuRegistrations.get(el);
        if (!reg || !reg.dotNetRef) return;
        const taskId = barEl.getAttribute('data-task-id');
        // Keyboard-invoked contextmenu (Shift+F10 / the Menu key, with focus on
        // the bar) reports (0, 0) in most engines — fall back to the bar's own
        // bottom-left corner, the same convention Lumeo's own
        // ContextMenuTrigger.HandleKeyDown already uses for its keyboard path.
        let x = e.clientX;
        let y = e.clientY;
        if (x === 0 && y === 0) {
            const rect = barEl.getBoundingClientRect();
            x = rect.left;
            y = rect.bottom;
        }
        reg.dotNetRef.invokeMethodAsync('NotifyBarContextMenu', taskId, x, y).catch(() => {});
    };
    el.addEventListener('contextmenu', onContextMenu);
    barContextMenuRegistrations.set(el, { dotNetRef, onContextMenu });
}

function unregisterBarContextMenu(el) {
    if (!el) return;
    const reg = barContextMenuRegistrations.get(el);
    if (!reg) return;
    el.removeEventListener('contextmenu', reg.onContextMenu);
    barContextMenuRegistrations.delete(el);
}

// ── Wheel zoom (Ctrl/Cmd + wheel over the timeline — REUI parity,
// GanttTimeline.WheelZoom) ──────────────────────────────────────────────────
//
// A SIXTH, separate registration channel (alongside registerDrag/
// registerSplitterDrag/registerRowReorderDrag/registerBarContextMenu),
// targeting EffectiveScrollHost — the SAME element centerOn/scrollToOffset
// already scroll (see GanttTimeline.CommitWheelZoom's own remarks for why
// THIS element, not the row-canvas registerDrag itself delegates pointer
// events on: offsetPx below needs el's own CLIPPED viewport rect, which the
// always-full-width row-canvas element can't provide).
//
// The listener is unconditional on every native 'wheel' event over that
// element ({ passive: false } — required to ever call preventDefault at
// all), but does NOTHING for a bare wheel: no ctrl/meta key means an
// immediate return with no side effect, no preventDefault, no .NET call —
// the page/pane scrolls exactly as if this listener didn't exist. That
// early, unconditional check is the entire mechanism behind "a bare wheel
// keeps scrolling the page."
//
// Whether a Ctrl/Cmd+wheel actually zooms (vs. being left alone for the
// browser's own native page-zoom) is decided ENTIRELY synchronously, here,
// from the OPTIONS bag .NET last pushed (levels/currentMode) — no .NET round
// trip precedes that decision, so JS can preventDefault (or not) in the SAME
// tick the event fires. That is what makes "at the zoom limits the gesture
// returns to the browser" possible at all: an async decision could not do
// this — by the time any Promise resolved, the browser would already have
// run (or skipped) its own default action for the wheel event.
const wheelZoomRegistrations = new Map(); // el -> { dotNetRef, options, onWheel }

function registerWheelZoom(el, dotNetRef, options) {
    if (!el) return;
    const existing = wheelZoomRegistrations.get(el);
    if (existing) {
        // Idempotent re-registration (a ViewMode/ZoomLevels change) — same
        // "swap dotNetRef/options in place" contract every other registerX
        // in this file already has.
        existing.dotNetRef = dotNetRef;
        existing.options = options;
        return;
    }

    const reg = { dotNetRef, options, onWheel: null };

    reg.onWheel = (e) => {
        if (!(e.ctrlKey || e.metaKey)) return; // bare wheel — never touched, see this block's own remarks

        const opts = reg.options;
        const levels = opts && Array.isArray(opts.levels) ? opts.levels : [];
        const currentIndex = levels.indexOf(opts && opts.currentMode);
        if (currentIndex < 0) return; // current mode isn't steppable at all — defer to the browser, same as an exhausted limit below

        // Standard wheel convention: deltaY < 0 ("scroll up" / pinch-out)
        // zooms IN. Levels are ordered coarsest-last (GanttZoomLevelModel's
        // own Day..Year order — the same convention GanttZoomControl's own
        // +/- stepper documents), so zooming IN steps TOWARD index 0.
        const zoomingIn = e.deltaY < 0;
        const nextIndex = zoomingIn ? currentIndex - 1 : currentIndex + 1;
        if (nextIndex < 0 || nextIndex >= levels.length) return; // at a limit — let the browser's own ctrl/cmd+wheel page-zoom take over instead of swallowing the gesture

        e.preventDefault();

        // contentX/offsetPx — see GanttTimeline.CommitWheelZoom's own remarks
        // for exactly what each feeds into on the .NET side. fromNativeScrollLeft
        // is this file's own existing RTL-normalization helper (already used
        // by registerHeaderScrollSync/getScrollCenterX for the identical
        // "live native scrollLeft -> logical offset" conversion).
        const rect = el.getBoundingClientRect();
        const offsetPx = e.clientX - rect.left;
        const logical = fromNativeScrollLeft(el, el.scrollLeft);
        const contentX = logical + offsetPx;

        if (reg.dotNetRef) {
            reg.dotNetRef.invokeMethodAsync('CommitWheelZoom', levels[nextIndex], contentX, offsetPx).catch(() => {});
        }
    };

    el.addEventListener('wheel', reg.onWheel, { passive: false });
    wheelZoomRegistrations.set(el, reg);
}

function unregisterWheelZoom(el) {
    if (!el) return;
    const reg = wheelZoomRegistrations.get(el);
    if (!reg) return;
    el.removeEventListener('wheel', reg.onWheel);
    wheelZoomRegistrations.delete(el);
}

export default ganttV3;
