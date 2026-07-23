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
// RTL note (phase-2/phase-1 reconciliation): NONE of the drag math below needs
// the RTL scrollLeft-convention machinery above. CSS `left`/`width` (what
// readBarGeometry reads) are PHYSICAL properties — always physical-left-
// relative regardless of `dir` — and a pointer event's `clientX` is likewise
// always a physical page coordinate. Both therefore already live in the same
// "logical" axis the RTL comment block above describes (0 = physical-left =
// earliest date, never mirrored for RTL), so a drag's pixel delta (dx) is
// correct under RTL with NO conversion: dragging physically right always
// means "later dates," exactly as under LTR. `toNativeScrollLeft`/
// `fromNativeScrollLeft` exist ONLY to translate a LOGICAL position into/out
// of the RTL-convention-dependent NATIVE `scrollLeft` property — an entirely
// different quantity that the drag engine never reads or writes (drag-create's
// `startCreateDrag` likewise anchors off a track element's own
// getBoundingClientRect + inline `top`/`left:0` style, both physical, both
// already row-canvas-space-aligned — see its own remarks). Verified by
// inspection rather than a dedicated RTL-drag Playwright spec; flagged to the
// team lead as a candidate follow-up if an actual RTL+drag regression is ever
// observed, rather than adding untested conversion logic to a codepath that
// doesn't need it.
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
// custom property from whatever theme root is already in scope. data-invalid
// is the stable hook (E2E selector / consumer override), the inline style is
// what actually paints.
function setGhostInvalid(ghost, invalid) {
    if (!ghost) return;
    if (invalid) {
        ghost.setAttribute('data-invalid', 'true');
        ghost.classList.add('lumeo-gantt-v3-drag-ghost-invalid');
        ghost.style.outline = '2px solid var(--color-destructive)';
        ghost.style.backgroundColor = 'var(--color-destructive)';
    } else {
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
function makeGhost(barEl) {
    const ghost = barEl.cloneNode(true);
    ghost.classList.add('lumeo-gantt-v3-drag-ghost');
    ghost.removeAttribute('data-task-id'); // never itself a hit-test target for a second, nested pointerdown
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

    const reg = { dotNetRef, options, onPointerDown: null };

    reg.onPointerDown = (e) => {
        if (e.button !== 0) return; // left mouse / primary touch-pen contact only
        const barEl = e.target.closest('[data-task-id]');
        if (!barEl || !el.contains(barEl)) {
            // Phase 2, T3 — no bar was hit. Only look for a create-track hit
            // when the caller opted in (reg.options.allowCreate — see
            // GanttTimeline.BuildDragOptions' own remarks: the row-track
            // elements themselves also only exist in the DOM when this is
            // true, so this check is defense-in-depth, not the only gate).
            if (reg.options && reg.options.allowCreate) {
                const trackEl = e.target.closest('[data-gantt-row-track]');
                if (trackEl && el.contains(trackEl)) startCreateDrag(reg, trackEl, e);
            }
            return;
        }

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
        let dragInitiated = false;
        let ghost = null;

        // Phase 2, T2 — CanDrop live validation (move/resize only, never
        // progress — GanttScheduleDropContext's own remarks). Scoped to THIS
        // drag session (not module-level), so it never outlives the drag and
        // never collides with a concurrent drag on a different bar.
        const validationCache = new Map(); // snapped-position key -> Promise<bool>
        let lastValidatedKey = null;

        function checkCanDrop(dx) {
            const dayPx = reg.options && reg.options.pixelsPerDay > 0 ? reg.options.pixelsPerDay : 1;
            const movedDays = Math.round(dx / dayPx);
            const { newStart: candStart, newEnd: candEnd } = computeSnappedDates(mode, movedDays, origStart, origEnd);
            const key = `${mode}|${toLocalDateString(candStart)}|${toLocalDateString(candEnd)}`;
            if (key === lastValidatedKey) return; // same snapped position as last check — no new call
            lastValidatedKey = key;

            let promise = validationCache.get(key);
            if (!promise) {
                const dotNet = reg.dotNetRef;
                promise = dotNet
                    ? dotNet.invokeMethodAsync('ValidateDrop', taskId, mode, toLocalDateString(candStart), toLocalDateString(candEnd)).catch(() => true)
                    : Promise.resolve(true);
                validationCache.set(key, promise);
            }
            promise.then((valid) => {
                // Only repaint if the drag hasn't already moved on to a DIFFERENT
                // snapped position by the time this (possibly-async-over-SignalR)
                // verdict comes back.
                if (lastValidatedKey === key) setGhostInvalid(ghost, !valid);
            });
        }

        barEl.setPointerCapture(e.pointerId);

        const onPointerMove = (mv) => {
            const dx = mv.clientX - startClientX;
            if (!dragInitiated) {
                if (Math.abs(dx) < DRAG_THRESHOLD_PX) return;
                dragInitiated = true;
                ghost = makeGhost(barEl);
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
                const newProgress = clampProgress(origProgress + (dx / geo.width) * 100);
                const fill = ghost.querySelector('.lumeo-gantt-v3-bar-progress');
                if (fill) fill.style.width = newProgress + '%';
            }

            if (mode !== 'progress' && reg.options && reg.options.hasCanDrop) {
                checkCanDrop(dx);
            }
        };

        const onPointerUp = async (up) => {
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
                    const dotNet = reg.dotNetRef;
                    if (dotNet) dotNet.invokeMethodAsync('NotifyTaskClick', taskId).catch(() => {});
                }
                return;
            }

            const dx = up.clientX - startClientX;
            const dotNet = reg.dotNetRef;

            if (mode === 'progress') {
                // gantt-v2.js:758 `Math.round(origProgress + (dx / barW) * 100)`,
                // clamped exactly like normalizeTasks' own progress clamp
                // (gantt-v2.js:81). No CanDrop validation for progress (plan:
                // "Progress drag is NOT validated — CanDrop is about scheduling").
                const newProgress = Math.round(clampProgress(origProgress + (dx / geo.width) * 100));
                if (newProgress === origProgress) return; // gantt-v2.js:759 no-op, no commit
                if (dotNet) dotNet.invokeMethodAsync('CommitProgress', taskId, newProgress).catch(() => {});
                return;
            }

            // gantt-v2.js:743 `const dayPx = pixelsPerDay(inst.viewMode);` — here
            // pixelsPerDay comes from .NET (reg.options.pixelsPerDay), never
            // re-derived: GanttScale.ViewModes is the single source of truth.
            const dayPx = reg.options && reg.options.pixelsPerDay > 0 ? reg.options.pixelsPerDay : 1;
            // gantt-v2.js:746/752 `Math.round(dx / dayPx)` — Math.round, not a
            // custom tie-break: unlike GanttScale.PixelToDate (which mirrors
            // Math.round's negative-tie behavior in C# via RoundToInt), this
            // literally IS the JS Math.round v2 used, so no port is needed.
            const movedDays = Math.round(dx / dayPx);
            if (movedDays === 0) return; // gantt-v2.js:747/753 no-op re-render, no commit

            const { newStart, newEnd } = computeSnappedDates(mode, movedDays, origStart, origEnd);

            if (reg.options && reg.options.hasCanDrop) {
                const key = `${mode}|${toLocalDateString(newStart)}|${toLocalDateString(newEnd)}`;
                let promise = validationCache.get(key);
                if (!promise) {
                    // Not already checked during the move (e.g. threshold crossed and
                    // released in the same snap step) — one fresh call, cached like any other.
                    promise = dotNet
                        ? dotNet.invokeMethodAsync('ValidateDrop', taskId, mode, toLocalDateString(newStart), toLocalDateString(newEnd)).catch(() => true)
                        : Promise.resolve(true);
                    validationCache.set(key, promise);
                }
                const valid = await promise;
                if (!valid) return; // invalid drop position — revert silently, no commit, no events
            }

            if (dotNet) {
                dotNet.invokeMethodAsync('CommitDrag', taskId, mode, toLocalDateString(newStart), toLocalDateString(newEnd))
                    .catch(() => {});
            }
        };

        const onPointerCancel = () => { cleanup(); };

        function cleanup() {
            barEl.removeEventListener('pointermove', onPointerMove);
            barEl.removeEventListener('pointerup', onPointerUp);
            barEl.removeEventListener('pointercancel', onPointerCancel);
            try { barEl.releasePointerCapture(e.pointerId); } catch (_) { /* already released */ }
            if (ghost && ghost.parentNode) ghost.parentNode.removeChild(ghost);
        }

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
function startCreateDrag(reg, trackEl, e) {
    const rowKey = trackEl.getAttribute('data-row-key');
    if (!rowKey) return;

    e.preventDefault();
    trackEl.setPointerCapture(e.pointerId);

    const rect = trackEl.getBoundingClientRect();
    const startLocalX = e.clientX - rect.left;
    const rowTop = parseFloat(trackEl.style.top) || 0;
    const rowHeight = parseFloat(trackEl.style.height) || 0;
    const startClientX = e.clientX;
    let dragInitiated = false;
    let ghost = null;

    function dayColumnRange(clientX) {
        const dayPx = reg.options && reg.options.pixelsPerDay > 0 ? reg.options.pixelsPerDay : 1;
        const localX = startLocalX + (clientX - startClientX);
        const dayA = Math.floor(startLocalX / dayPx);
        const dayB = Math.floor(localX / dayPx);
        return { fromDay: Math.min(dayA, dayB), toDay: Math.max(dayA, dayB), dayPx };
    }

    const onPointerMove = (mv) => {
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
        cleanup();
        if (!dragInitiated) return; // below threshold — no ghost residue, no call (plan requirement)

        const { fromDay, toDay } = dayColumnRange(up.clientX);
        const originIso = reg.options && reg.options.originIso;
        const origin = originIso ? parseIsoDate(originIso) : null;
        if (!origin) return; // no anchor date — nothing sane to commit

        const startIso = toLocalDateString(addDays(origin, fromDay));
        const endIso = toLocalDateString(addDays(origin, toDay));

        const dotNet = reg.dotNetRef;
        if (dotNet) dotNet.invokeMethodAsync('CommitCreate', rowKey, startIso, endIso).catch(() => {});
    };

    const onPointerCancel = () => { cleanup(); };

    function cleanup() {
        trackEl.removeEventListener('pointermove', onPointerMove);
        trackEl.removeEventListener('pointerup', onPointerUp);
        trackEl.removeEventListener('pointercancel', onPointerCancel);
        try { trackEl.releasePointerCapture(e.pointerId); } catch (_) { /* already released */ }
        if (ghost && ghost.parentNode) ghost.parentNode.removeChild(ghost);
    }

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
}

export default ganttV3;
