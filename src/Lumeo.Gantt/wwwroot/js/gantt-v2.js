// Lumeo Gantt — custom SVG-based renderer.
// Drop-in replacement for the Frappe Gantt wrapper. Deterministic, themeable,
// and feature-complete: drag-to-move, drag-to-resize, drag-to-set-progress,
// hover tooltip, dependency arrows, today indicator, generous past/future scroll.
//
// API surface (matches the previous wrapper):
//   gantt.init(el, dotNetRef, options) -> string instanceId
//   gantt.refresh(id, options)
//   gantt.destroy(id)
//
// Options:
//   tasks:    [{ id, name, start, end, progress, dependencies, customClass }]
//   viewMode: 'QuarterDay' | 'HalfDay' | 'Day' | 'Week' | 'Month' | 'Year'
//   readonly: bool
//
// .NET callbacks (best-effort, swallowed on failure):
//   JsOnTaskClick(taskJson)
//   JsOnDateChange(taskJson { Start, End })
//   JsOnProgressChange(taskJson { Progress })

const SVG_NS = 'http://www.w3.org/2000/svg';
const instances = new Map();
let nextId = 1;

const VIEW_MODES = {
    QuarterDay: { columnWidth: 38,  unit: 'hour',  step: 6,  padBefore: 24, padAfter: 24, headerFmt: { upper: 'day',   lower: 'time6h' } },
    HalfDay:    { columnWidth: 38,  unit: 'hour',  step: 12, padBefore: 24, padAfter: 24, headerFmt: { upper: 'day',   lower: 'time12h' } },
    Day:        { columnWidth: 38,  unit: 'day',   step: 1,  padBefore: 60, padAfter: 60, headerFmt: { upper: 'month', lower: 'dayNum' } },
    Week:       { columnWidth: 140, unit: 'day',   step: 7,  padBefore: 16, padAfter: 16, headerFmt: { upper: 'month', lower: 'weekRange' } },
    Month:      { columnWidth: 120, unit: 'month', step: 1,  padBefore: 12, padAfter: 12, headerFmt: { upper: 'year',  lower: 'monthName' } },
    Year:       { columnWidth: 120, unit: 'year',  step: 1,  padBefore: 4,  padAfter: 6,  headerFmt: { upper: '',      lower: 'yearNum' } },
};

const ROW_HEIGHT = 36;
const BAR_HEIGHT = 22;
const HEADER_HEIGHT = 56;
const PADDING_X = 8;
const RESIZE_HANDLE_W = 8;

// ── Helpers ────────────────────────────────────────────────────────────────

function el(tag, attrs, parent) {
    const node = document.createElementNS(SVG_NS, tag);
    if (attrs) for (const k in attrs) node.setAttribute(k, attrs[k]);
    if (parent) parent.appendChild(node);
    return node;
}

function parseDate(d) {
    if (!d) return null;
    if (d instanceof Date) return new Date(d.getFullYear(), d.getMonth(), d.getDate());
    if (typeof d === 'string') {
        const m = d.match(/^(\d{4})-(\d{2})-(\d{2})/);
        if (m) return new Date(+m[1], +m[2] - 1, +m[3]);
        const x = new Date(d);
        return isNaN(x.getTime()) ? null : new Date(x.getFullYear(), x.getMonth(), x.getDate());
    }
    return null;
}

const dayDiff = (a, b) => Math.round((b - a) / 86_400_000);
const addDays = (d, n) => { const x = new Date(d); x.setDate(x.getDate() + n); return x; };
const addMonths = (d, n) => { const x = new Date(d); x.setMonth(x.getMonth() + n); return x; };
const fmtDayNum = d => String(d.getDate()).padStart(2, '0');
const fmtMonth = d => d.toLocaleString(undefined, { month: 'long' });
const fmtMonthShort = d => d.toLocaleString(undefined, { month: 'short' });
const fmtYear = d => String(d.getFullYear());
const fmtDate = d => `${fmtMonthShort(d)} ${d.getDate()}, ${d.getFullYear()}`;

function normalizeTasks(rawTasks) {
    if (!Array.isArray(rawTasks)) return [];
    return rawTasks.map((t, i) => {
        const id = t.id ?? t.Id ?? `task-${i}`;
        const name = t.name ?? t.Name ?? '';
        const start = parseDate(t.start ?? t.Start);
        const end = parseDate(t.end ?? t.End);
        const progress = Math.max(0, Math.min(100, Number(t.progress ?? t.Progress ?? 0)));
        const depsRaw = t.dependencies ?? t.Dependencies ?? [];
        const dependencies = Array.isArray(depsRaw)
            ? depsRaw
            : String(depsRaw).split(',').map(s => s.trim()).filter(Boolean);
        return { id: String(id), name, start, end, progress, dependencies };
    }).filter(t => t.start && t.end && t.end >= t.start);
}

function readTokens(el) {
    const cs = getComputedStyle(el);
    const get = (name, fallback) => (cs.getPropertyValue(name) || fallback).trim();
    return {
        primary:   get('--color-primary', '#7c3aed'),
        primaryFg: get('--color-primary-foreground', '#ffffff'),
        muted:     get('--color-muted-foreground', '#888'),
        fg:        get('--color-foreground', '#fff'),
        border:    get('--color-border', '#444'),
        accent:    get('--color-accent', '#333'),
        card:      get('--color-card', '#0a0a0a'),
        popover:   get('--color-popover', '#0a0a0a'),
        popoverFg: get('--color-popover-foreground', '#fff'),
    };
}

function taskToJson(task) {
    return {
        Id: task.id,
        Name: task.name,
        Start: task.start ? task.start.toISOString() : null,
        End: task.end ? task.end.toISOString() : null,
        Progress: task.progress,
        Dependencies: task.dependencies,
        CustomClass: null,
    };
}

// ── Render ─────────────────────────────────────────────────────────────────

function render(inst) {
    const { host, tasks, viewMode, readonly, dotNetRef } = inst;
    const tokens = readTokens(host);
    const cfg = VIEW_MODES[viewMode] || VIEW_MODES.Day;

    // Preserve scroll position across re-renders so user's pan doesn't get
    // wiped every time refresh() runs. innerHTML='' resets scrollLeft to 0.
    const preservedScrollLeft = host.scrollLeft;
    const preservedScrollTop = host.scrollTop;

    // Compute date range with generous padding so users can scroll back/forward.
    let minDate, maxDate;
    if (tasks.length > 0) {
        minDate = tasks.reduce((m, t) => (m && m < t.start ? m : t.start), null);
        maxDate = tasks.reduce((m, t) => (m && m > t.end ? m : t.end), null);
    } else {
        const today = new Date();
        minDate = addDays(today, -7);
        maxDate = addDays(today, 14);
    }

    let dateUnits = [];
    if (cfg.unit === 'day') {
        const startDate = addDays(minDate, -cfg.padBefore * cfg.step);
        const endDate = addDays(maxDate, cfg.padAfter * cfg.step);
        const totalDays = dayDiff(startDate, endDate) + 1;
        const totalColumns = Math.ceil(totalDays / cfg.step);
        for (let i = 0; i < totalColumns; i++) dateUnits.push(addDays(startDate, i * cfg.step));
    } else if (cfg.unit === 'month') {
        const startDate = new Date(minDate.getFullYear(), minDate.getMonth() - cfg.padBefore, 1);
        const endDate = new Date(maxDate.getFullYear(), maxDate.getMonth() + cfg.padAfter, 1);
        for (let d = startDate; d <= endDate; d = addMonths(d, 1)) dateUnits.push(new Date(d));
    } else if (cfg.unit === 'year') {
        const startYear = minDate.getFullYear() - cfg.padBefore;
        const endYear = maxDate.getFullYear() + cfg.padAfter;
        for (let y = startYear; y <= endYear; y++) dateUnits.push(new Date(y, 0, 1));
    } else if (cfg.unit === 'hour') {
        const startDate = addDays(minDate, -Math.ceil(cfg.padBefore * cfg.step / 24));
        const endDate = addDays(maxDate, Math.ceil(cfg.padAfter * cfg.step / 24));
        const totalHours = (endDate - startDate) / 3_600_000;
        for (let i = 0; i < totalHours; i += cfg.step) {
            const d = new Date(startDate);
            d.setHours(d.getHours() + i);
            dateUnits.push(d);
        }
    }

    const colW = cfg.columnWidth;
    const totalWidth = dateUnits.length * colW;
    const totalHeight = HEADER_HEIGHT + Math.max(1, tasks.length) * ROW_HEIGHT;

    // Date <-> X mapping
    function dateToX(d) {
        if (cfg.unit === 'day') {
            const days = dayDiff(dateUnits[0], d);
            return (days / cfg.step) * colW;
        }
        if (cfg.unit === 'month') {
            const months = (d.getFullYear() - dateUnits[0].getFullYear()) * 12
                         + (d.getMonth() - dateUnits[0].getMonth());
            const dayFraction = (d.getDate() - 1) / 30;
            return (months + dayFraction) * colW;
        }
        if (cfg.unit === 'year') {
            const years = d.getFullYear() - dateUnits[0].getFullYear();
            const dayFraction = (d.getMonth() * 30 + d.getDate()) / 365;
            return (years + dayFraction) * colW;
        }
        if (cfg.unit === 'hour') {
            const hours = (d - dateUnits[0]) / 3_600_000;
            return (hours / cfg.step) * colW;
        }
        return 0;
    }

    function xToDate(x) {
        if (cfg.unit === 'day') {
            const days = Math.round((x / colW) * cfg.step);
            return addDays(dateUnits[0], days);
        }
        if (cfg.unit === 'month') {
            const months = Math.round(x / colW);
            return addMonths(dateUnits[0], months);
        }
        if (cfg.unit === 'year') {
            const years = Math.round(x / colW);
            return new Date(dateUnits[0].getFullYear() + years, 0, 1);
        }
        if (cfg.unit === 'hour') {
            const hours = Math.round((x / colW) * cfg.step);
            const d = new Date(dateUnits[0]);
            d.setHours(d.getHours() + hours);
            return d;
        }
        return new Date();
    }

    inst._dateToX = dateToX;
    inst._xToDate = xToDate;
    inst._snapStep = cfg.step;
    inst._unitMs = cfg.unit === 'day' ? 86_400_000
                 : cfg.unit === 'hour' ? 3_600_000
                 : cfg.unit === 'month' ? 30 * 86_400_000
                 : 365 * 86_400_000;

    // Wipe and rebuild
    host.innerHTML = '';
    host.style.position = 'relative';
    host.style.overflow = 'auto';
    // CRITICAL: flex items default to min-width:min-content, so any inline
    // min-width on a child SVG would expand the host element to the SVG's
    // full width, eliminating horizontal scroll and pushing the chart out
    // of its parent card. Forcing min-width:0 lets the host shrink to its
    // parent's available width and lets overflow-x:auto actually scroll.
    host.style.minWidth = '0';
    host.style.maxWidth = '100%';

    const svg = el('svg', {
        width: totalWidth,
        height: totalHeight,
        viewBox: `0 0 ${totalWidth} ${totalHeight}`,
        class: 'lumeo-gantt-svg',
        // Fixed pixel dimensions via inline CSS to defeat any ancestor
        // svg{width:100%} rule. NOTE: do NOT set min-width here — that
        // bubbles up through flex containers and forces the host to grow
        // to the SVG's full width, killing horizontal scroll.
        style: `display:block; user-select:none; width:${totalWidth}px; height:${totalHeight}px; max-width:none; flex-shrink:0;`,
    }, host);

    // Diagnostic so we can SEE what range was actually rendered.
    // Uses a temp 'now' since 'today' const is declared further below.
    if (typeof console !== 'undefined' && console.warn) {
        const _now = new Date();
        _now.setHours(0, 0, 0, 0);
        console.warn('[lumeo-gantt-v2-custom] dimensions ' + JSON.stringify({
            viewMode,
            columns: dateUnits.length,
            totalWidth,
            firstColDate: dateUnits[0] ? dateUnits[0].toISOString().slice(0, 10) : null,
            lastColDate: dateUnits[dateUnits.length - 1] ? dateUnits[dateUnits.length - 1].toISOString().slice(0, 10) : null,
            hostClientWidth: host.clientWidth,
            hostScrollWidth: host.scrollWidth,
            todayDate: _now.toISOString().slice(0, 10),
            todayX: dateToX(_now),
            padBefore: cfg.padBefore,
        }));
    }

    // Header background
    el('rect', { x: 0, y: 0, width: totalWidth, height: HEADER_HEIGHT, fill: tokens.card }, svg);

    // Vertical grid lines
    for (let i = 0; i <= dateUnits.length; i++) {
        el('line', {
            x1: i * colW, y1: 0, x2: i * colW, y2: totalHeight,
            stroke: tokens.border, 'stroke-width': '0.5', opacity: '0.4',
        }, svg);
    }

    // Horizontal row lines
    for (let i = 0; i <= tasks.length; i++) {
        const y = HEADER_HEIGHT + i * ROW_HEIGHT;
        el('line', {
            x1: 0, y1: y, x2: totalWidth, y2: y,
            stroke: tokens.border, 'stroke-width': '0.5', opacity: '0.3',
        }, svg);
    }

    // Today indicator
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const todayX = dateToX(today);
    const todayInRange = todayX >= 0 && todayX <= totalWidth;
    if (todayInRange) {
        el('line', {
            x1: todayX, y1: HEADER_HEIGHT - 4, x2: todayX, y2: totalHeight,
            stroke: tokens.primary, 'stroke-width': '2', opacity: '0.8',
        }, svg);
        el('circle', { cx: todayX, cy: HEADER_HEIGHT - 4, r: 4, fill: tokens.primary }, svg);
    }

    // Header labels (per-column)
    let lastUpperLabel = '';
    dateUnits.forEach((d, i) => {
        const xCenter = i * colW + colW / 2;
        const xLeft = i * colW;

        // Highlight today's column
        const isToday = cfg.unit === 'day' && dayDiff(d, today) === 0;
        if (isToday) {
            el('rect', {
                x: xLeft + 2, y: HEADER_HEIGHT - 32,
                width: colW - 4, height: 22, rx: 4,
                fill: tokens.primary,
            }, svg);
        }

        // Lower row text
        let lowerText = '';
        switch (cfg.headerFmt.lower) {
            case 'dayNum':    lowerText = fmtDayNum(d); break;
            case 'weekRange': lowerText = `${d.getDate()}/${d.getMonth() + 1}`; break;
            case 'monthName': lowerText = fmtMonthShort(d); break;
            case 'yearNum':   lowerText = fmtYear(d); break;
            case 'time6h':
            case 'time12h':   lowerText = `${d.getHours()}:00`; break;
        }
        const lower = el('text', {
            x: xCenter, y: HEADER_HEIGHT - 18,
            'text-anchor': 'middle',
            'font-size': '12',
            'font-family': 'system-ui,sans-serif',
            fill: isToday ? tokens.primaryFg : tokens.muted,
            'pointer-events': 'none',
        }, svg);
        lower.textContent = lowerText;

        // Upper row label (only when it changes)
        let upperText = '';
        switch (cfg.headerFmt.upper) {
            case 'month': upperText = fmtMonth(d); break;
            case 'year':  upperText = fmtYear(d); break;
            case 'day':   upperText = `${fmtMonth(d)} ${d.getDate()}`; break;
        }
        if (upperText && upperText !== lastUpperLabel) {
            const upper = el('text', {
                x: xLeft + 8, y: 18,
                'font-size': '13',
                'font-family': 'system-ui,sans-serif',
                'font-weight': '600',
                fill: tokens.fg,
                'pointer-events': 'none',
            }, svg);
            upper.textContent = upperText;
            lastUpperLabel = upperText;
        }
    });

    // Empty state
    if (tasks.length === 0) {
        const emptyT = el('text', {
            x: totalWidth / 2, y: HEADER_HEIGHT + 50,
            'text-anchor': 'middle',
            'font-size': '13',
            'font-family': 'system-ui,sans-serif',
            fill: tokens.muted,
        }, svg);
        emptyT.textContent = 'No tasks to display';
        return;
    }

    // Task bars
    const taskById = new Map();
    tasks.forEach((task, idx) => {
        const rowY = HEADER_HEIGHT + idx * ROW_HEIGHT;
        const barY = rowY + (ROW_HEIGHT - BAR_HEIGHT) / 2;
        const x1 = dateToX(task.start);
        const x2 = dateToX(addDays(task.end, 1));
        const barW = Math.max(8, x2 - x1);

        taskById.set(task.id, { task, x: x1, y: barY, w: barW, idx });

        const group = el('g', {
            class: 'lumeo-gantt-bar-wrapper',
            'data-task-id': task.id,
            style: readonly ? 'cursor:pointer' : 'cursor:grab',
        }, svg);

        // Background bar
        const bgRect = el('rect', {
            class: 'lumeo-gantt-bar-bg',
            x: x1, y: barY,
            width: barW, height: BAR_HEIGHT,
            rx: 4, ry: 4,
            fill: tokens.primary,
            'fill-opacity': '0.30',
        }, group);

        // Progress overlay
        const progressW = barW * (task.progress / 100);
        const progressRect = el('rect', {
            class: 'lumeo-gantt-bar-progress',
            x: x1, y: barY,
            width: progressW, height: BAR_HEIGHT,
            rx: 4, ry: 4,
            fill: tokens.primary,
        }, group);

        // Label (clipped to bar)
        const label = el('text', {
            class: 'lumeo-gantt-bar-label',
            x: x1 + PADDING_X, y: barY + BAR_HEIGHT / 2 + 4,
            'font-size': '12',
            'font-family': 'system-ui,sans-serif',
            'font-weight': '500',
            fill: tokens.fg,
            'pointer-events': 'none',
        }, group);
        label.textContent = task.name;

        // Resize handle (right edge) — invisible hit zone for cursor change + drag
        let resizeHandle = null;
        let progressHandle = null;
        if (!readonly) {
            resizeHandle = el('rect', {
                class: 'lumeo-gantt-resize',
                x: x1 + barW - RESIZE_HANDLE_W, y: barY,
                width: RESIZE_HANDLE_W, height: BAR_HEIGHT,
                fill: 'transparent',
                style: 'cursor:ew-resize',
            }, group);

            // Progress handle — small circle at the right edge of progress overlay
            progressHandle = el('circle', {
                class: 'lumeo-gantt-progress-handle',
                cx: x1 + progressW, cy: barY + BAR_HEIGHT,
                r: 4,
                fill: tokens.primaryFg,
                stroke: tokens.primary,
                'stroke-width': '2',
                style: 'cursor:ns-resize',
                opacity: '0',
            }, group);
        }

        // Hover feedback
        group.addEventListener('mouseenter', () => {
            bgRect.setAttribute('fill-opacity', '0.45');
            if (progressHandle) progressHandle.setAttribute('opacity', '1');
            showTooltip(host, task, group);
        });
        group.addEventListener('mouseleave', () => {
            bgRect.setAttribute('fill-opacity', '0.30');
            if (progressHandle) progressHandle.setAttribute('opacity', '0');
            hideTooltip(host);
        });

        // Click handler (only fires if drag wasn't initiated)
        let mouseDownX = 0, dragInitiated = false, dragMode = null;
        const onMouseDown = (e, mode) => {
            if (readonly && mode !== 'click') return;
            e.preventDefault();
            mouseDownX = e.clientX;
            dragInitiated = false;
            dragMode = mode;
            inst._dragState = {
                taskId: task.id,
                mode,
                startX: e.clientX,
                origStart: task.start,
                origEnd: task.end,
                origProgress: task.progress,
                bgRect, progressRect, label, resizeHandle, progressHandle,
                x1, barW, barY,
            };

            const onMove = (mv) => {
                const dx = mv.clientX - mouseDownX;
                if (Math.abs(dx) > 3) dragInitiated = true;
                applyDragVisual(inst, dx);
            };
            const onUp = (mu) => {
                document.removeEventListener('mousemove', onMove);
                document.removeEventListener('mouseup', onUp);
                if (dragInitiated) {
                    commitDrag(inst, mu.clientX - mouseDownX);
                } else if (mode === 'move') {
                    // Treat as click
                    dotNetRef.invokeMethodAsync('JsOnTaskClick', taskToJson(task)).catch(() => {});
                }
                inst._dragState = null;
                dragInitiated = false;
            };
            document.addEventListener('mousemove', onMove);
            document.addEventListener('mouseup', onUp);
        };

        bgRect.addEventListener('mousedown', e => onMouseDown(e, 'move'));
        progressRect.addEventListener('mousedown', e => onMouseDown(e, 'move'));
        label.addEventListener('mousedown', e => onMouseDown(e, 'move'));
        if (resizeHandle) resizeHandle.addEventListener('mousedown', e => onMouseDown(e, 'resize'));
        if (progressHandle) progressHandle.addEventListener('mousedown', e => onMouseDown(e, 'progress'));
    });

    // Dependency arrows
    tasks.forEach((task) => {
        if (!task.dependencies || task.dependencies.length === 0) return;
        const target = taskById.get(task.id);
        if (!target) return;
        for (const depId of task.dependencies) {
            const source = taskById.get(depId);
            if (!source) continue;
            const sx = source.x + source.w;
            const sy = source.y + BAR_HEIGHT / 2;
            const tx = target.x;
            const ty = target.y + BAR_HEIGHT / 2;
            const midX = sx + 12;
            const path = `M ${sx} ${sy} L ${midX} ${sy} L ${midX} ${ty} L ${tx - 4} ${ty}`;
            el('path', {
                d: path, fill: 'none',
                stroke: tokens.muted, 'stroke-width': '1.2', opacity: '0.6',
            }, svg);
            el('polygon', {
                points: `${tx - 6},${ty - 4} ${tx},${ty} ${tx - 6},${ty + 4}`,
                fill: tokens.muted, opacity: '0.6',
            }, svg);
        }
    });

    inst._taskById = taskById;

    // Scroll handling:
    //  - First render: center today in viewport once host has real width.
    //  - Subsequent renders (refresh): restore the user's pan position.
    if (inst._initialScrolled) {
        host.scrollLeft = preservedScrollLeft;
        host.scrollTop = preservedScrollTop;
    } else {
        const tryScroll = (attempt) => {
            const w = host.clientWidth;
            if (w > 50) {
                const todayPx = dateToX(today);
                if (todayPx > 0) {
                    host.scrollLeft = Math.max(0, todayPx - w / 2);
                }
                inst._initialScrolled = true;
            } else if (attempt < 30) {
                requestAnimationFrame(() => tryScroll(attempt + 1));
            }
        };
        requestAnimationFrame(() => tryScroll(0));
    }
}

// ── Drag visual update (during mousemove) ──────────────────────────────────

function applyDragVisual(inst, dx) {
    const s = inst._dragState;
    if (!s) return;
    const { mode, x1, barW, bgRect, progressRect, label, resizeHandle, progressHandle, origProgress } = s;

    if (mode === 'move') {
        bgRect.setAttribute('x', x1 + dx);
        progressRect.setAttribute('x', x1 + dx);
        label.setAttribute('x', x1 + dx + PADDING_X);
        if (resizeHandle) resizeHandle.setAttribute('x', x1 + dx + barW - RESIZE_HANDLE_W);
        if (progressHandle) progressHandle.setAttribute('cx', x1 + dx + (barW * origProgress / 100));
    } else if (mode === 'resize') {
        const newW = Math.max(8, barW + dx);
        bgRect.setAttribute('width', newW);
        progressRect.setAttribute('width', newW * origProgress / 100);
        if (resizeHandle) resizeHandle.setAttribute('x', x1 + newW - RESIZE_HANDLE_W);
        if (progressHandle) progressHandle.setAttribute('cx', x1 + (newW * origProgress / 100));
    } else if (mode === 'progress') {
        const newProgress = Math.max(0, Math.min(100, origProgress + (dx / barW) * 100));
        progressRect.setAttribute('width', barW * newProgress / 100);
        if (progressHandle) progressHandle.setAttribute('cx', x1 + (barW * newProgress / 100));
    }
}

// ── Drag commit (mouseup) ──────────────────────────────────────────────────

// Pixels per day for the current view mode — used to snap drag deltas
// to single-day increments regardless of column granularity. In Week view
// each column is 140px wide and represents 7 days, so 1 day = 20px.
function pixelsPerDay(viewMode) {
    const cfg = VIEW_MODES[viewMode] || VIEW_MODES.Day;
    if (cfg.unit === 'day') return cfg.columnWidth / cfg.step;       // Day:1, Week:20
    if (cfg.unit === 'hour') return (cfg.columnWidth * 24) / cfg.step;
    if (cfg.unit === 'month') return cfg.columnWidth / 30;
    if (cfg.unit === 'year') return cfg.columnWidth / 365;
    return cfg.columnWidth;
}

function commitDrag(inst, dx) {
    const s = inst._dragState;
    if (!s) return;
    const { mode, taskId, origStart, origEnd, origProgress, barW } = s;
    const task = inst.tasks.find(t => t.id === taskId);
    if (!task) return;

    const dayPx = pixelsPerDay(inst.viewMode);

    if (mode === 'move') {
        const movedDays = Math.round(dx / dayPx);
        if (movedDays === 0) { render(inst); return; }
        task.start = addDays(origStart, movedDays);
        task.end = addDays(origEnd, movedDays);
        inst.dotNetRef.invokeMethodAsync('JsOnDateChange', taskToJson(task)).catch(() => {});
    } else if (mode === 'resize') {
        const movedDays = Math.round(dx / dayPx);
        if (movedDays === 0) { render(inst); return; }
        task.end = addDays(origEnd, movedDays);
        if (task.end < task.start) task.end = task.start;
        inst.dotNetRef.invokeMethodAsync('JsOnDateChange', taskToJson(task)).catch(() => {});
    } else if (mode === 'progress') {
        const newProgress = Math.max(0, Math.min(100, Math.round(origProgress + (dx / barW) * 100)));
        if (newProgress === origProgress) { render(inst); return; }
        task.progress = newProgress;
        inst.dotNetRef.invokeMethodAsync('JsOnProgressChange', taskToJson(task)).catch(() => {});
    }
    render(inst);
}

// ── Tooltip ────────────────────────────────────────────────────────────────

function showTooltip(host, task, group) {
    hideTooltip(host);
    const tokens = readTokens(host);
    const tt = document.createElement('div');
    tt.className = 'lumeo-gantt-tooltip';
    tt.style.cssText = [
        'position:absolute', 'pointer-events:none', 'z-index:1000',
        `background:${tokens.popover}`, `color:${tokens.popoverFg}`,
        `border:1px solid ${tokens.border}`,
        'padding:8px 10px', 'border-radius:6px',
        'font-family:system-ui,sans-serif', 'font-size:12px',
        'box-shadow:0 4px 12px rgb(0 0 0 / 0.2)',
        'min-width:180px',
    ].join(';');
    const dateRange = `${fmtDate(task.start)} → ${fmtDate(task.end)}`;
    const days = dayDiff(task.start, task.end) + 1;
    tt.innerHTML = `
        <div style="font-weight:600;margin-bottom:4px">${escapeHtml(task.name)}</div>
        <div style="opacity:0.75;margin-bottom:2px">${dateRange}</div>
        <div style="opacity:0.75">${days} day${days !== 1 ? 's' : ''} · ${task.progress}% complete</div>
    `;
    host.appendChild(tt);

    const groupRect = group.getBoundingClientRect();
    const hostRect = host.getBoundingClientRect();
    tt.style.left = (groupRect.left - hostRect.left + host.scrollLeft) + 'px';
    tt.style.top = (groupRect.top - hostRect.top + host.scrollTop - tt.offsetHeight - 8) + 'px';
}

function hideTooltip(host) {
    const tt = host.querySelector('.lumeo-gantt-tooltip');
    if (tt) tt.remove();
}

function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[c]));
}

// ── Public API ─────────────────────────────────────────────────────────────

export const gantt = {
    async init(elOrId, dotNetRef, options) {
        const host = typeof elOrId === 'string' ? document.getElementById(elOrId) : elOrId;
        if (!host) throw new Error('Gantt: root element missing');

        // CRITICAL: lock the host to a stable container width BEFORE adding
        // any wide content. Once SVG with width=5814 is added, min-width:auto
        // cascades up through every flex/block ancestor and expands the host
        // to 5814 — killing horizontal scroll.
        //
        // Tricky: when the Gantt is the ONLY child of its ComponentDemo
        // preview (no surrounding text/cards), the preview shrinks to fit
        // the toolbar (~350px) and parentElement.clientWidth is misleading.
        // So walk UP and pick the WIDEST ancestor's clientWidth as the
        // available container width. That gives us the actual layout column
        // width regardless of which sibling-content the demo has.
        const lockHostWidth = () => {
            const parentEl = host.parentElement;
            if (!parentEl) return;

            // Find the widest ancestor (up to body) — that's the natural
            // layout column the Gantt should fill.
            let widest = 0;
            let p = parentEl;
            for (let i = 0; i < 10 && p && p !== document.documentElement; i++) {
                const w = p.clientWidth;
                if (w > widest) widest = w;
                p = p.parentElement;
            }

            // Subtract padding of the immediate parent so the host fits
            // INSIDE the card chrome rather than overlapping its border.
            const cs = window.getComputedStyle(parentEl);
            const padL = parseFloat(cs.paddingLeft) || 0;
            const padR = parseFloat(cs.paddingRight) || 0;
            const lockW = Math.max(50, widest - padL - padR);

            host.style.width = lockW + 'px';
            host.style.maxWidth = lockW + 'px';
            host.style.minWidth = '0';
            host.style.boxSizing = 'border-box';
        };
        lockHostWidth();

        const id = `gantt-${nextId++}`;
        const inst = {
            id,
            host,
            dotNetRef,
            tasks: normalizeTasks(options.tasks),
            viewMode: options.viewMode || 'Day',
            readonly: !!options.readonly,
        };
        instances.set(id, inst);

        render(inst);

        // When the window resizes, re-measure the parent and update host.
        // Need to TEMPORARILY unlock host (set width to 0) to let the parent
        // collapse back to its natural size, then re-measure, then re-lock.
        const onWindowResize = () => {
            const parentEl = host.parentElement;
            if (!parentEl) return;
            host.style.width = '0';
            host.style.maxWidth = '0';
            // Force layout
            void parentEl.offsetWidth;
            const w = parentEl.clientWidth;
            if (w > 0) {
                host.style.width = w + 'px';
                host.style.maxWidth = w + 'px';
            }
        };
        window.addEventListener('resize', onWindowResize, { passive: true });
        inst._onResize = onWindowResize;

        // No ResizeObserver here on purpose: the SVG has a fixed width (the
        // total of all date columns) and a fixed height (header + rows). The
        // host element resizes around it without affecting the chart's own
        // coordinate space, so we don't need to re-render on every layout
        // wobble. Re-rendering would also wipe host.scrollLeft and reset the
        // user's pan position.

        return id;
    },

    refresh(id, options) {
        const inst = instances.get(id);
        if (!inst) return;
        if (options.tasks !== undefined) inst.tasks = normalizeTasks(options.tasks);
        if (options.viewMode !== undefined) inst.viewMode = options.viewMode;
        if (options.readonly !== undefined) inst.readonly = !!options.readonly;
        render(inst);
    },

    // Aliases matching the original Frappe wrapper API surface, so the
    // Razor component (which calls gantt.setTasks / gantt.changeViewMode)
    // keeps working without C# changes.
    setTasks(id, tasks) {
        this.refresh(id, { tasks });
    },

    changeViewMode(id, mode) {
        this.refresh(id, { viewMode: mode });
    },

    destroy(id) {
        const inst = instances.get(id);
        if (!inst) return;
        if (inst._onResize) window.removeEventListener('resize', inst._onResize);
        if (inst.host) inst.host.innerHTML = '';
        instances.delete(id);
    },
};

export default gantt;
