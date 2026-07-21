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
//
// Phase 2, T2 additions (progress drag, click, CanDrop) — same file, same
// registerDrag/onPointerDown closure, three new v2-parity/REUI-analog behaviors:
//   - progress-handle drag + commit: gantt-v2.js:564-574 (handle geometry),
//     715-719/758 (applyDragVisual/commitDrag progress branches)
//   - click-vs-drag: gantt-v2.js:617-622 (a below-threshold 'move'-mode
//     mousedown falls back to a click; 'resize'/'progress' modes do not)
//   - CanDrop live validation has NO v2 equivalent (REUI canDropEvent analog) —
//     see GanttTimeline.ValidateDrop's remarks for the .NET side.

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
        if (!barEl || !el.contains(barEl)) return;

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

function unregisterDrag(el) {
    if (!el) return;
    const reg = dragRegistrations.get(el);
    if (!reg) return;
    el.removeEventListener('pointerdown', reg.onPointerDown);
    dragRegistrations.delete(el);
}

export default ganttV3;
