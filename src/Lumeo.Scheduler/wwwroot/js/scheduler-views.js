// Lumeo Scheduler — first-party view engine interaction module (wave 1b).
//
// Reuses the three hard-won Gantt v3 patterns this task explicitly calls out
// (see docs/superpowers/specs/2026-08-10-scheduler-first-party.md §3.1, and
// src/Lumeo.Gantt/wwwroot/js/gantt-v3.js's own `registerDrag`, which this module
// mirrors rather than re-deriving):
//
//   1. GHOST-ONLY dragging. The real Blazor-rendered chip is NEVER mutated by
//      this module — only a cloned ghost element (created on drag start, removed
//      on drop/cancel) moves. Blazor's diff has nothing to fight on commit, and a
//      vetoed/failed drop has nothing to visually resync (the real chip was never
//      touched — see gantt-v3.js's own `makeGhost` remarks for the full argument).
//   2. FAIL-CLOSED async validation. `ValidateDrop` is awaited BEFORE the commit
//      interop call; a rejected/thrown/timed-out invocation resolves to `false`
//      via `.catch(() => false)`, never `true`. There is no code path where a
//      drop commits while the predicate is still pending or has failed.
//   3. Browser-local "now". The now-indicator line is positioned and refreshed
//      ENTIRELY in this module via `new Date()` — never round-tripped through
//      .NET as a DateTime (Blazor Server's server clock may be in a different
//      timezone than the browser — see gantt-v3.js:49-66's `getLocalDateIso`
//      for the identical, already-shipped fix this ports one level further).

const monthRegistrations = new Map(); // hostEl -> { dotNetRef, options, onPointerDown }
const timeGridRegistrations = new Map();
const nowIndicators = new Map(); // containerEl -> { intervalId, lineEl }

const DRAG_THRESHOLD_PX = 4;
const RESIZE_HANDLE_PX = 6;

function toIsoDate(d) {
    // Calendar-field read, never toISOString() (which converts to UTC first and
    // can roll the calendar day backward in a positive-UTC-offset zone — the
    // exact class of bug gantt-v3.js's getLocalDateIso already documents).
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
}

function makeGhost(sourceEl) {
    const ghost = sourceEl.cloneNode(true);
    ghost.removeAttribute('data-event-instance');
    ghost.removeAttribute('id');
    ghost.style.pointerEvents = 'none';
    ghost.style.opacity = '0.75';
    ghost.style.zIndex = '60';
    ghost.setAttribute('data-scheduler-ghost', 'true');
    return ghost;
}

function setGhostInvalid(ghost, invalid) {
    if (!ghost) return;
    if (invalid) {
        ghost.setAttribute('data-invalid', 'true');
        ghost.style.outline = '2px solid var(--color-destructive)';
    } else {
        ghost.removeAttribute('data-invalid');
        ghost.style.outline = '';
    }
}

// ============================================================
// Month view — whole-day move only (spec §3.4: month has no edge-resize).
// ============================================================

function registerMonthDrag(hostEl, dotNetRef, options) {
    if (!hostEl) return;
    const existing = monthRegistrations.get(hostEl);
    if (existing) { existing.dotNetRef = dotNetRef; existing.options = options; return; }

    const reg = { dotNetRef, options, onPointerDown: null };

    reg.onPointerDown = (e) => {
        if (e.button !== 0) return;
        const dragOptions = reg.options;
        const dragDotNet = reg.dotNetRef;
        if (!dragOptions) return;

        const pillEl = e.target.closest('[data-event-instance]');
        if (pillEl && hostEl.contains(pillEl) && dragOptions.editable) {
            startMonthEventMove(pillEl, e, dragOptions, dragDotNet);
            return;
        }

        // A pointerdown on any OTHER real control inside the cell (the "+N more" popover
        // trigger, or an event chip inside its popover) must never start a range-select
        // gesture: setPointerCapture() on the cell would redirect the resulting synthetic
        // "click" event to the CELL instead of the button the user actually pressed, so
        // the button's own @onclick would silently never fire — found via this task's own
        // Playwright run (a plain mouse ClickAsync on "+N more" did nothing at all, while
        // a keyboard Enter on the same focused button worked, isolating it to exactly this
        // pointer-capture interception, not a C#/Blazor wiring problem).
        const controlEl = e.target.closest('button, [role="button"]');
        if (controlEl && hostEl.contains(controlEl)) return;

        const cellEl = e.target.closest('[data-cell-date]');
        if (cellEl && hostEl.contains(cellEl) && dragOptions.selectable) {
            startMonthRangeSelect(cellEl, e, dragDotNet);
        }
    };

    function startMonthEventMove(pillEl, e, dragOptions, dragDotNet) {
        const eventId = pillEl.getAttribute('data-event-id');
        if (!eventId) return;

        const startClientX = e.clientX;
        const startClientY = e.clientY;
        const pointerId = e.pointerId;
        let dragInitiated = false;
        let ghost = null;
        let lastCellEl = null;
        let lastDateIso = null;
        const validationCache = new Map();

        // Captured UNCONDITIONALLY at gesture start, before any move — not lazily once
        // the drag threshold is crossed. A single large/fast pointer move (as Playwright's
        // Mouse.MoveAsync issues by default — one jump, not incremental steps) can land the
        // pointer over a completely different element before the threshold check ever runs;
        // without capture already in place, that pointermove's natural bubble target is
        // whatever element is now under the cursor, which pillEl may not be an ancestor of,
        // so pillEl's own listener would never fire and the drag would silently never start.
        // Mirrors gantt-v3.js's own `barEl.setPointerCapture(pointerId)` call, which happens
        // immediately after resolving pointerId too, for the identical reason.
        pillEl.setPointerCapture(pointerId);

        // A completed drag must never ALSO fire a native click on the pill (which would
        // wrongly invoke OnEventClick right after a genuine move) — pointer capture can
        // redirect the browser's own post-mouseup click to the captor element regardless of
        // where the pointer visually ended up. Swallow exactly one click, only when a real
        // drag happened; a below-threshold press (dragInitiated stays false) leaves the
        // native click alone so a genuine click still reaches Blazor's own @onclick handler.
        const onClickCapture = (ce) => {
            pillEl.removeEventListener('click', onClickCapture, true);
            if (dragInitiated) { ce.stopPropagation(); ce.preventDefault(); }
        };
        pillEl.addEventListener('click', onClickCapture, true);

        function checkCanDrop(dateIso) {
            if (!dragOptions.hasCanDrop) return;
            let promise = validationCache.get(dateIso);
            if (!promise) {
                promise = dragDotNet.invokeMethodAsync('ValidateDrop', eventId, dateIso).catch(() => false);
                validationCache.set(dateIso, promise);
            }
            promise.then((valid) => {
                if (lastDateIso === dateIso) setGhostInvalid(ghost, !valid);
            });
        }

        const onPointerMove = (mv) => {
            if (mv.pointerId !== pointerId) return;
            const dx = mv.clientX - startClientX;
            const dy = mv.clientY - startClientY;
            if (!dragInitiated) {
                if (Math.abs(dx) < DRAG_THRESHOLD_PX && Math.abs(dy) < DRAG_THRESHOLD_PX) return;
                dragInitiated = true;
                ghost = makeGhost(pillEl);
                ghost.style.position = 'fixed';
                ghost.style.width = pillEl.getBoundingClientRect().width + 'px';
                document.body.appendChild(ghost);
                pillEl.style.opacity = '0.4';
            }

            ghost.style.left = (mv.clientX + 8) + 'px';
            ghost.style.top = (mv.clientY + 8) + 'px';

            const under = document.elementFromPoint(mv.clientX, mv.clientY);
            const cellEl = under ? under.closest('[data-cell-date]') : null;
            if (cellEl !== lastCellEl) {
                if (lastCellEl) lastCellEl.removeAttribute('data-drop-target');
                lastCellEl = cellEl;
                if (cellEl) cellEl.setAttribute('data-drop-target', 'true');
            }
            if (cellEl) {
                const dateIso = cellEl.getAttribute('data-cell-date');
                if (dateIso !== lastDateIso) {
                    lastDateIso = dateIso;
                    checkCanDrop(dateIso);
                }
            }
        };

        const onPointerUp = async (up) => {
            if (up.pointerId !== pointerId) return;
            cleanup();
            if (!dragInitiated) return; // below threshold — native click already handles this

            if (!lastDateIso) return;

            if (dragOptions.hasCanDrop) {
                let promise = validationCache.get(lastDateIso);
                if (!promise) {
                    promise = dragDotNet.invokeMethodAsync('ValidateDrop', eventId, lastDateIso).catch(() => false);
                }
                const valid = await promise;
                if (!valid) {
                    // Tell .NET the drop was refused so it can announce it
                    // (Codex review of the live-region PR, P1). CommitDrag is
                    // never reached for a rejection, so an announce placed
                    // there could not fire for the very case it existed for.
                    dragDotNet.invokeMethodAsync('NotifyDropRejected', eventId).catch(() => {});
                    return; // fail-closed: no commit on invalid/unconfirmed drop
                }
            }

            dragDotNet.invokeMethodAsync('CommitDrag', eventId, lastDateIso).catch(() => {});
        };

        const onPointerCancel = (cn) => {
            if (cn.pointerId !== pointerId) return;
            cleanup();
        };

        function cleanup() {
            pillEl.removeEventListener('pointermove', onPointerMove);
            pillEl.removeEventListener('pointerup', onPointerUp);
            pillEl.removeEventListener('pointercancel', onPointerCancel);
            try { pillEl.releasePointerCapture(pointerId); } catch (_) { /* already released */ }
            if (ghost && ghost.parentNode) ghost.parentNode.removeChild(ghost);
            if (lastCellEl) lastCellEl.removeAttribute('data-drop-target');
            pillEl.style.opacity = '';
        }

        pillEl.addEventListener('pointermove', onPointerMove);
        pillEl.addEventListener('pointerup', onPointerUp);
        pillEl.addEventListener('pointercancel', onPointerCancel);
    }

    // Drag-across-cells date-range selection on empty grid background (spec §3.4's Month
    // row — previously entirely absent, only a click/dblclick on a single cell existed).
    // Same ghost-free (attribute-only highlight, not a cloned element) + pointer-capture +
    // drag-threshold + click-after-drag-swallow pattern as startMonthEventMove above.
    function startMonthRangeSelect(startCellEl, e, dragDotNet) {
        const startDateIso = startCellEl.getAttribute('data-cell-date');
        if (!startDateIso) return;

        const startClientX = e.clientX;
        const startClientY = e.clientY;
        const pointerId = e.pointerId;
        let dragInitiated = false;
        let lastDateIso = startDateIso;
        let highlighted = [];

        startCellEl.setPointerCapture(pointerId);

        const onClickCapture = (ce) => {
            startCellEl.removeEventListener('click', onClickCapture, true);
            if (dragInitiated) { ce.stopPropagation(); ce.preventDefault(); }
        };
        startCellEl.addEventListener('click', onClickCapture, true);

        function paintRange(toIso) {
            for (const el of highlighted) el.removeAttribute('data-select-target');
            highlighted = [];
            const lo = startDateIso <= toIso ? startDateIso : toIso; // yyyy-MM-dd compares lexicographically = chronologically
            const hi = startDateIso <= toIso ? toIso : startDateIso;
            hostEl.querySelectorAll('[data-cell-date]').forEach((el) => {
                const d = el.getAttribute('data-cell-date');
                if (d >= lo && d <= hi) {
                    el.setAttribute('data-select-target', 'true');
                    highlighted.push(el);
                }
            });
        }

        const onPointerMove = (mv) => {
            if (mv.pointerId !== pointerId) return;
            const dx = mv.clientX - startClientX;
            const dy = mv.clientY - startClientY;
            if (!dragInitiated) {
                if (Math.abs(dx) < DRAG_THRESHOLD_PX && Math.abs(dy) < DRAG_THRESHOLD_PX) return;
                dragInitiated = true;
            }

            const under = document.elementFromPoint(mv.clientX, mv.clientY);
            const cellEl = under ? under.closest('[data-cell-date]') : null;
            if (cellEl && hostEl.contains(cellEl)) {
                const dateIso = cellEl.getAttribute('data-cell-date');
                if (dateIso !== lastDateIso) {
                    lastDateIso = dateIso;
                    paintRange(dateIso);
                }
            }
        };

        const onPointerUp = (up) => {
            if (up.pointerId !== pointerId) return;
            cleanup();
            if (!dragInitiated) return; // below threshold — native click/dblclick already handle this
            if (!lastDateIso) return;
            dragDotNet.invokeMethodAsync('CommitDateRangeSelect', startDateIso, lastDateIso).catch(() => {});
        };

        const onPointerCancel = (cn) => {
            if (cn.pointerId !== pointerId) return;
            cleanup();
        };

        function cleanup() {
            startCellEl.removeEventListener('pointermove', onPointerMove);
            startCellEl.removeEventListener('pointerup', onPointerUp);
            startCellEl.removeEventListener('pointercancel', onPointerCancel);
            try { startCellEl.releasePointerCapture(pointerId); } catch (_) { /* already released */ }
            for (const el of highlighted) el.removeAttribute('data-select-target');
            highlighted = [];
        }

        startCellEl.addEventListener('pointermove', onPointerMove);
        startCellEl.addEventListener('pointerup', onPointerUp);
        startCellEl.addEventListener('pointercancel', onPointerCancel);
    }

    hostEl.addEventListener('pointerdown', reg.onPointerDown);
    monthRegistrations.set(hostEl, reg);
}

function unregisterMonthDrag(hostEl) {
    const reg = monthRegistrations.get(hostEl);
    if (!reg) return;
    hostEl.removeEventListener('pointerdown', reg.onPointerDown);
    monthRegistrations.delete(hostEl);
}

// ============================================================
// Week/Day time-grid — move (day+time), edge-resize, drag-to-create.
// ============================================================

function resolveHitMode(pillEl, clientY, allowResize) {
    if (!allowResize) return 'move';
    const rect = pillEl.getBoundingClientRect();
    const localY = clientY - rect.top;
    if (localY <= RESIZE_HANDLE_PX) return 'resize-start';
    if (rect.height - localY <= RESIZE_HANDLE_PX) return 'resize-end';
    return 'move';
}

function snapMinutes(raw, snap) {
    return Math.round(raw / snap) * snap;
}

function registerTimeGridDrag(hostEl, dotNetRef, options) {
    if (!hostEl) return;
    const existing = timeGridRegistrations.get(hostEl);
    if (existing) { existing.dotNetRef = dotNetRef; existing.options = options; return; }

    const reg = { dotNetRef, options, onPointerDown: null };

    reg.onPointerDown = (e) => {
        if (e.button !== 0) return;
        const dragOptions = reg.options;
        const dragDotNet = reg.dotNetRef;
        if (!dragOptions) return;

        const pillEl = e.target.closest('[data-event-instance]');
        if (pillEl && hostEl.contains(pillEl) && dragOptions.editable) {
            startMovResize(pillEl, e);
            return;
        }

        const cellEl = e.target.closest('[data-slot-hour]');
        if (cellEl && hostEl.contains(cellEl) && dragOptions.selectable) {
            startCreate(cellEl, e);
        }

        function startMovResize(pillEl, e) {
            const eventId = pillEl.getAttribute('data-event-id');
            const instanceKey = pillEl.getAttribute('data-event-instance');
            if (!eventId) return;

            const mode = resolveHitMode(pillEl, e.clientY, dragOptions.allowResize !== false);
            const startClientX = e.clientX;
            const startClientY = e.clientY;
            const pointerId = e.pointerId;
            const pxPerMinute = dragOptions.pixelsPerMinute || 0.8;
            const snap = dragOptions.snapMinutes || 15;
            let dragInitiated = false;
            let ghost = null;
            const validationCache = new Map();
            let lastKey = null;

            // Captured unconditionally at gesture start — see registerMonthDrag's identical
            // fix/remarks above for why a lazy (post-threshold) capture can silently drop
            // the whole gesture when the first qualifying move jumps off the source element.
            pillEl.setPointerCapture(pointerId);

            // Same click-after-drag guard as registerMonthDrag's own onClickCapture — see its
            // remarks. mode 'resize-start'/'resize-end' never leaves the pill's own bounding
            // box, so this matters most for 'move', but is installed unconditionally for all
            // three since the pill is always the pointer-capture target regardless of mode.
            const onClickCapture = (ce) => {
                pillEl.removeEventListener('click', onClickCapture, true);
                if (dragInitiated) { ce.stopPropagation(); ce.preventDefault(); }
            };
            pillEl.addEventListener('click', onClickCapture, true);

            const onPointerMove = (mv) => {
                if (mv.pointerId !== pointerId) return;
                const dx = mv.clientX - startClientX;
                const dy = mv.clientY - startClientY;
                if (!dragInitiated) {
                    if (Math.abs(dx) < DRAG_THRESHOLD_PX && Math.abs(dy) < DRAG_THRESHOLD_PX) return;
                    dragInitiated = true;
                    ghost = makeGhost(pillEl);
                    ghost.style.position = 'fixed';
                    const rect = pillEl.getBoundingClientRect();
                    ghost.style.left = rect.left + 'px';
                    ghost.style.top = rect.top + 'px';
                    ghost.style.width = rect.width + 'px';
                    ghost.style.height = rect.height + 'px';
                    document.body.appendChild(ghost);
                    pillEl.style.opacity = '0.4';
                }

                const rect0 = pillEl.getBoundingClientRect();
                if (mode === 'move') {
                    ghost.style.left = (rect0.left + dx) + 'px';
                    ghost.style.top = (rect0.top + dy) + 'px';
                } else if (mode === 'resize-end') {
                    const newHeight = Math.max(16, rect0.height + dy);
                    ghost.style.height = newHeight + 'px';
                } else if (mode === 'resize-start') {
                    const newTop = rect0.top + dy;
                    const newHeight = Math.max(16, rect0.height - dy);
                    ghost.style.top = newTop + 'px';
                    ghost.style.height = newHeight + 'px';
                }

                const deltaMinutes = snapMinutes(dy / pxPerMinute, snap);
                let dayIso = null;
                if (mode === 'move') {
                    const under = document.elementFromPoint(mv.clientX, mv.clientY);
                    const dayCol = under ? under.closest('[data-daycol]') : null;
                    dayIso = dayCol ? dayCol.getAttribute('data-daycol') : null;
                }
                const key = `${mode}|${dayIso ?? ''}|${deltaMinutes}`;
                if (key === lastKey) return;
                lastKey = key;

                if (dragOptions.hasCanDrop) {
                    let promise = validationCache.get(key);
                    if (!promise) {
                        promise = dragDotNet.invokeMethodAsync('ValidateDrop', instanceKey, mode, dayIso, deltaMinutes).catch(() => false);
                        validationCache.set(key, promise);
                    }
                    promise.then((valid) => {
                        if (lastKey === key) setGhostInvalid(ghost, !valid);
                    });
                }

                reg._lastMoveState = { mode, dayIso, deltaMinutes, key };
            };

            const onPointerUp = async (up) => {
                if (up.pointerId !== pointerId) return;
                // Read state BEFORE cleanup() — cleanup() clears reg._lastMoveState as part
                // of tearing the drag session down, so reading it after would always see
                // null and silently drop every commit (the bug this comment replaces).
                const state = reg._lastMoveState;
                cleanup();
                if (!dragInitiated) return;
                if (!state) return;
                // A MOVE released outside every [data-daycol] has no day to
                // commit to (Codex review of the live-region PR, P2). With no
                // CanDrop configured the validation branch below is skipped
                // entirely, so CommitDrag simply returns and the gesture ends in
                // silence.
                //
                // Gated on the mode (Codex review, P1 — a regression this guard
                // introduced): onPointerMove populates dayIso ONLY for 'move'
                // and deliberately leaves it null for both resize modes, so an
                // unconditional check treated every resize as an out-of-grid
                // drop, announced a rejection and returned before committing —
                // breaking resize outright.
                if (state.mode === 'move' && !state.dayIso) {
                    dragDotNet.invokeMethodAsync('NotifyDropRejected', instanceKey, state.mode).catch(() => {});
                    return;
                }

                if (dragOptions.hasCanDrop) {
                    let promise = validationCache.get(state.key);
                    if (!promise) {
                        promise = dragDotNet.invokeMethodAsync('ValidateDrop', instanceKey, state.mode, state.dayIso, state.deltaMinutes).catch(() => false);
                    }
                    const valid = await promise;
                    if (!valid) {
                        // See the month handler's own remarks. The mode travels
                        // too, so a refused RESIZE is not announced as a refused
                        // move.
                        dragDotNet.invokeMethodAsync('NotifyDropRejected', instanceKey, state.mode).catch(() => {});
                        return; // fail-closed
                    }
                }

                dragDotNet.invokeMethodAsync('CommitDrag', instanceKey, state.mode, state.dayIso, state.deltaMinutes).catch(() => {});
            };

            const onPointerCancel = (cn) => {
                if (cn.pointerId !== pointerId) return;
                cleanup();
            };

            function cleanup() {
                pillEl.removeEventListener('pointermove', onPointerMove);
                pillEl.removeEventListener('pointerup', onPointerUp);
                pillEl.removeEventListener('pointercancel', onPointerCancel);
                try { pillEl.releasePointerCapture(pointerId); } catch (_) { /* already released */ }
                if (ghost && ghost.parentNode) ghost.parentNode.removeChild(ghost);
                pillEl.style.opacity = '';
                reg._lastMoveState = null;
            }

            pillEl.addEventListener('pointermove', onPointerMove);
            pillEl.addEventListener('pointerup', onPointerUp);
            pillEl.addEventListener('pointercancel', onPointerCancel);
        }

        function startCreate(cellEl, e) {
            e.preventDefault();
            const dayCol = cellEl.closest('[data-daycol]');
            const dayIso = dayCol ? dayCol.getAttribute('data-daycol') : null;
            if (!dayIso) return;

            // data-slot-minute is the precise row start; data-slot-hour is kept as the
            // fallback because it is the older contract (E2E selectors and the pointerdown
            // hit-test below still match on it) and stays correct for a whole-hour grid.
            const minuteAttr = cellEl.getAttribute('data-slot-minute');
            const startHour = parseInt(cellEl.getAttribute('data-slot-hour'), 10);
            const pointerId = e.pointerId;
            const startClientY = e.clientY;
            const pxPerMinute = dragOptions.pixelsPerMinute || 0.8;
            const snap = dragOptions.snapMinutes || 15;
            const startMinute = minuteAttr !== null ? parseInt(minuteAttr, 10) : startHour * 60;
            let ghost = null;
            let dragInitiated = false;
            let endMinute = startMinute + snap;

            // Captured unconditionally at gesture start — see registerMonthDrag's identical
            // fix/remarks above.
            cellEl.setPointerCapture(pointerId);

            const onPointerMove = (mv) => {
                if (mv.pointerId !== pointerId) return;
                const dy = mv.clientY - startClientY;
                if (!dragInitiated) {
                    if (Math.abs(dy) < DRAG_THRESHOLD_PX) return;
                    dragInitiated = true;
                    ghost = document.createElement('div');
                    ghost.setAttribute('data-scheduler-ghost', 'true');
                    ghost.style.position = 'absolute';
                    ghost.style.left = '2px';
                    ghost.style.right = '2px';
                    ghost.style.background = 'color-mix(in oklab, var(--color-ring) 25%, transparent)';
                    ghost.style.border = '1px dashed var(--color-ring)';
                    ghost.style.borderRadius = '6px';
                    ghost.style.pointerEvents = 'none';
                    ghost.style.top = ((startMinute - (dragOptions.slotMinMinute || 0)) * pxPerMinute) + 'px';
                    dayCol.appendChild(ghost);
                }

                const rawDelta = snapMinutes(dy / pxPerMinute, snap);
                endMinute = Math.max(startMinute + snap, startMinute + rawDelta);
                const top = (startMinute - (dragOptions.slotMinMinute || 0)) * pxPerMinute;
                const height = Math.max(16, (endMinute - startMinute) * pxPerMinute);
                ghost.style.top = top + 'px';
                ghost.style.height = height + 'px';
            };

            const onPointerUp = (up) => {
                if (up.pointerId !== pointerId) return;
                cleanup();
                if (!dragInitiated) return;
                dragDotNet.invokeMethodAsync('CommitCreate', dayIso, startMinute, endMinute).catch(() => {});
            };

            const onPointerCancel = (cn) => {
                if (cn.pointerId !== pointerId) return;
                cleanup();
            };

            function cleanup() {
                cellEl.removeEventListener('pointermove', onPointerMove);
                cellEl.removeEventListener('pointerup', onPointerUp);
                cellEl.removeEventListener('pointercancel', onPointerCancel);
                try { cellEl.releasePointerCapture(pointerId); } catch (_) { /* already released */ }
                if (ghost && ghost.parentNode) ghost.parentNode.removeChild(ghost);
            }

            cellEl.addEventListener('pointermove', onPointerMove);
            cellEl.addEventListener('pointerup', onPointerUp);
            cellEl.addEventListener('pointercancel', onPointerCancel);
        }
    };

    hostEl.addEventListener('pointerdown', reg.onPointerDown);
    timeGridRegistrations.set(hostEl, reg);
}

function unregisterTimeGridDrag(hostEl) {
    const reg = timeGridRegistrations.get(hostEl);
    if (!reg) return;
    hostEl.removeEventListener('pointerdown', reg.onPointerDown);
    timeGridRegistrations.delete(hostEl);
}

// ============================================================
// Now-indicator — browser-local clock, positioned/refreshed entirely here.
// Spec §2.2: the server NEVER computes "now" for display; this module reads
// new Date() directly (never round-tripped through .NET) and repaints purely
// via DOM style mutation on a 60s setInterval INSIDE this module — never a
// Blazor Timer/PeriodicTimer driving StateHasChanged().
// ============================================================

function registerNowIndicator(containerEl, options) {
    if (!containerEl) return;
    unregisterNowIndicator(containerEl);

    const line = document.createElement('div');
    line.setAttribute('data-scheduler-now-line', 'true');
    line.style.position = 'absolute';
    line.style.left = '0';
    line.style.right = '0';
    line.style.height = '0';
    line.style.borderTop = '2px solid var(--color-destructive)';
    line.style.zIndex = '5';
    line.style.pointerEvents = 'none';
    containerEl.appendChild(line);

    const pxPerMinute = options && options.pixelsPerMinute ? options.pixelsPerMinute : 0.8;
    const slotMinMinute = options && options.slotMinMinute ? options.slotMinMinute : 0;
    const dayIso = options && options.dayIso ? options.dayIso : null;
    const timeZone = options && options.timeZone ? options.timeZone : null;

    // Intl is the only IANA database the browser has, and it is enough: formatToParts gives
    // the zone's own calendar date and clock reading without any date library. An id Intl
    // rejects falls back to the browser's clock rather than throwing inside a timer that
    // would then never be cleaned up.
    let zoneParts = null;
    if (timeZone) {
        try {
            zoneParts = new Intl.DateTimeFormat('en-US', {
                timeZone, year: 'numeric', month: '2-digit', day: '2-digit',
                hour: '2-digit', minute: '2-digit', hour12: false,
            });
        } catch (_) { zoneParts = null; }
    }

    function readClock() {
        const now = new Date();
        if (!zoneParts) {
            return { iso: toIsoDate(now), minutes: now.getHours() * 60 + now.getMinutes() };
        }
        const parts = {};
        for (const part of zoneParts.formatToParts(now)) parts[part.type] = part.value;
        // hour12:false still yields "24" at midnight in some engines.
        const hour = parseInt(parts.hour, 10) % 24;
        return {
            iso: `${parts.year}-${parts.month}-${parts.day}`,
            minutes: hour * 60 + parseInt(parts.minute, 10),
        };
    }

    function update() {
        const now = readClock();
        // dayIso is only supplied for a Day view (1 column) — the line is hidden
        // entirely when "today" isn't the visible day, matching every mainstream
        // calendar's now-indicator (it never draws on a non-today column set it
        // can't place unambiguously without per-day iso comparison from the caller).
        if (dayIso && now.iso !== dayIso) {
            line.style.display = 'none';
            return;
        }
        line.style.display = '';
        const minutes = now.minutes;
        line.style.top = ((minutes - slotMinMinute) * pxPerMinute) + 'px';
    }

    update();
    const intervalId = window.setInterval(update, 60000);
    nowIndicators.set(containerEl, { intervalId, lineEl: line });
}

function unregisterNowIndicator(containerEl) {
    const entry = nowIndicators.get(containerEl);
    if (!entry) return;
    window.clearInterval(entry.intervalId);
    if (entry.lineEl && entry.lineEl.parentNode) entry.lineEl.parentNode.removeChild(entry.lineEl);
    nowIndicators.delete(containerEl);
}

export const schedulerViews = {
    registerMonthDrag,
    unregisterMonthDrag,
    registerTimeGridDrag,
    unregisterTimeGridDrag,
    registerNowIndicator,
    unregisterNowIndicator,
};
