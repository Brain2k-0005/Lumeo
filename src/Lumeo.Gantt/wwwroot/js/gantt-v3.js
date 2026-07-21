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

    registerDrag,
    unregisterDrag,
};

// ── Drag engine (Phase 2, T1) ───────────────────────────────────────────────
//
// v3's bars are plain absolutely-positioned <div>s inside the row-canvas div
// (the "relative" element Virtualize's items render into — see
// GanttTimeline.razor's RowItems/RowsContainerStyle remarks), each carrying
// data-task-id/data-task-start/data-task-end/data-milestone (see GanttBar.razor's
// WrapperAttributes). Rather than attaching a listener per bar (which Blazor's
// Virtualize would force us to re-attach on every recycle), ONE pointerdown
// listener is delegated on the scroll-host element GanttTimeline passes to
// registerDrag (the same _scrollHostRef centerOn() above already targets) —
// e.target.closest('[data-task-id]') finds which bar (if any) was hit.
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

const RESIZE_HANDLE_PX = 6;
// gantt-v2.js:610 `if (Math.abs(dx) > 3) dragInitiated = true;` — pixels of
// pointer travel before a mousedown-on-a-bar counts as a drag rather than a
// click. v3 has no click event to fall back to yet (OnTaskClick is not wired
// on Gantt3 as of this task — out of scope), so falling BELOW this threshold
// simply cancels the drag with no commit (see onPointerUp below).
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
        if (!barEl || !el.contains(barEl)) return;

        const taskId = barEl.getAttribute('data-task-id');
        const isMilestone = barEl.getAttribute('data-milestone') === 'true';
        const origStartIso = barEl.getAttribute('data-task-start');
        const origEndIso = barEl.getAttribute('data-task-end');
        const origStart = parseIsoDate(origStartIso);
        const origEnd = parseIsoDate(origEndIso);
        if (!origStart || !origEnd) return; // malformed data-* — nothing sane to drag

        // gantt-v2.js:593 `e.preventDefault();` — stops the browser's native text
        // selection / drag-image gesture from fighting the pointer drag.
        e.preventDefault();

        const mode = resolveHitMode(barEl, e.clientX, isMilestone);
        const geo = readBarGeometry(barEl);
        const startClientX = e.clientX;
        let dragInitiated = false;
        let ghost = null;

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
            // grows/shrinks from the left (right edge fixed).
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
            }
        };

        const onPointerUp = (up) => {
            cleanup();
            if (!dragInitiated) return; // below threshold — no commit (v2 parity: gantt-v2.js:617-622 treats this as a click instead; v3 has no click path yet, see DRAG_THRESHOLD_PX's note)

            const dx = up.clientX - startClientX;
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

            const dotNet = reg.dotNetRef;
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

function unregisterDrag(el) {
    if (!el) return;
    const reg = dragRegistrations.get(el);
    if (!reg) return;
    el.removeEventListener('pointerdown', reg.onPointerDown);
    dragRegistrations.delete(el);
}

export default ganttV3;
