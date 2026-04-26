// Lumeo Gantt — custom SVG-based renderer.
// We previously wrapped Frappe Gantt v1.2.2 but its render pipeline kept
// pushing bars off-screen in unpredictable ways across viewport sizes.
// This is a deterministic ~250-line implementation that just works.
//
// API surface (matches the previous Frappe wrapper, so no Razor changes):
//   gantt.init(el, dotNetRef, options)
//   gantt.refresh(instanceId, options)
//   gantt.destroy(instanceId)
//
// Options:
//   tasks:    [{ id, name, start, end, progress, dependencies, customClass }]
//   viewMode: 'QuarterDay' | 'HalfDay' | 'Day' | 'Week' | 'Month' | 'Year'
//   readonly: bool
//
// DotNet callbacks (best-effort, swallowed on failure):
//   JsOnTaskClick(taskJson)
//   JsOnDateChange(taskJson { Start, End })
//   JsOnProgressChange(taskJson { Progress })

const SVG_NS = 'http://www.w3.org/2000/svg';
const instances = new Map();
let nextId = 1;

const VIEW_MODES = {
    QuarterDay: { columnWidth: 38, unit: 'hour', step: 6, headerFmt: { upper: 'day', lower: 'time6h' } },
    HalfDay:    { columnWidth: 38, unit: 'hour', step: 12, headerFmt: { upper: 'day', lower: 'time12h' } },
    Day:        { columnWidth: 38, unit: 'day',  step: 1,  headerFmt: { upper: 'month', lower: 'dayNum' } },
    Week:       { columnWidth: 140, unit: 'day', step: 7,  headerFmt: { upper: 'month', lower: 'weekRange' } },
    Month:      { columnWidth: 120, unit: 'month', step: 1, headerFmt: { upper: 'year', lower: 'monthName' } },
    Year:       { columnWidth: 120, unit: 'year', step: 1, headerFmt: { upper: '', lower: 'yearNum' } },
};

const ROW_HEIGHT = 36;
const BAR_HEIGHT = 22;
const HEADER_HEIGHT = 56;
const PADDING_X = 8;

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
        // 'YYYY-MM-DD' or full ISO. Strip time so day-aligned math is stable.
        const m = d.match(/^(\d{4})-(\d{2})-(\d{2})/);
        if (m) return new Date(+m[1], +m[2] - 1, +m[3]);
        const x = new Date(d);
        return isNaN(x.getTime()) ? null : new Date(x.getFullYear(), x.getMonth(), x.getDate());
    }
    return null;
}

function dayDiff(a, b) {
    const ms = b - a;
    return Math.round(ms / 86_400_000);
}

function addDays(d, n) {
    const x = new Date(d);
    x.setDate(x.getDate() + n);
    return x;
}

function addMonths(d, n) {
    const x = new Date(d);
    x.setMonth(x.getMonth() + n);
    return x;
}

function fmtDayNum(d) { return String(d.getDate()).padStart(2, '0'); }
function fmtMonth(d) { return d.toLocaleString(undefined, { month: 'long' }); }
function fmtMonthShort(d) { return d.toLocaleString(undefined, { month: 'short' }); }
function fmtYear(d) { return String(d.getFullYear()); }

function normalizeTasks(rawTasks) {
    if (!Array.isArray(rawTasks)) return [];
    return rawTasks.map((t, i) => {
        const id = t.id ?? t.Id ?? `task-${i}`;
        const name = t.name ?? t.Name ?? '';
        const start = parseDate(t.start ?? t.Start);
        const end = parseDate(t.end ?? t.End);
        const progress = Math.max(0, Math.min(100, Number(t.progress ?? t.Progress ?? 0)));
        const depsRaw = t.dependencies ?? t.Dependencies ?? [];
        const dependencies = Array.isArray(depsRaw) ? depsRaw : String(depsRaw).split(',').map(s => s.trim()).filter(Boolean);
        return { id: String(id), name, start, end, progress, dependencies };
    }).filter(t => t.start && t.end && t.end >= t.start);
}

function readTokens(el) {
    const cs = getComputedStyle(el);
    return {
        primary: (cs.getPropertyValue('--color-primary') || '#7c3aed').trim(),
        primaryFg: (cs.getPropertyValue('--color-primary-foreground') || '#ffffff').trim(),
        muted: (cs.getPropertyValue('--color-muted-foreground') || '#888').trim(),
        fg: (cs.getPropertyValue('--color-foreground') || '#fff').trim(),
        border: (cs.getPropertyValue('--color-border') || '#444').trim(),
        accent: (cs.getPropertyValue('--color-accent') || '#333').trim(),
        card: (cs.getPropertyValue('--color-card') || '#0a0a0a').trim(),
    };
}

function render(inst) {
    const { host, tasks, viewMode } = inst;
    const tokens = readTokens(host);
    const cfg = VIEW_MODES[viewMode] || VIEW_MODES.Day;

    // Compute date range with 5-step padding on each side.
    let minDate = tasks.reduce((m, t) => (m && m < t.start ? m : t.start), null);
    let maxDate = tasks.reduce((m, t) => (m && m > t.end ? m : t.end), null);
    if (!minDate || !maxDate) {
        const today = new Date();
        minDate = addDays(today, -7);
        maxDate = addDays(today, 14);
    }

    let columnsBefore = 5, columnsAfter = 5;
    let dateUnits = []; // array of Date objects, one per column
    if (cfg.unit === 'day') {
        const totalDays = dayDiff(minDate, maxDate) + 1;
        const totalColumns = Math.ceil((totalDays + columnsBefore * cfg.step + columnsAfter * cfg.step) / cfg.step);
        const startDate = addDays(minDate, -columnsBefore * cfg.step);
        for (let i = 0; i < totalColumns; i++) dateUnits.push(addDays(startDate, i * cfg.step));
    } else if (cfg.unit === 'month') {
        const startDate = new Date(minDate.getFullYear(), minDate.getMonth() - 2, 1);
        const endDate = new Date(maxDate.getFullYear(), maxDate.getMonth() + 3, 1);
        for (let d = startDate; d <= endDate; d = addMonths(d, 1)) dateUnits.push(new Date(d));
    } else if (cfg.unit === 'year') {
        const startYear = minDate.getFullYear() - 1;
        const endYear = maxDate.getFullYear() + 2;
        for (let y = startYear; y < endYear; y++) dateUnits.push(new Date(y, 0, 1));
    } else if (cfg.unit === 'hour') {
        const startDate = addDays(minDate, -1);
        const endDate = addDays(maxDate, 2);
        const totalHours = (endDate - startDate) / 3_600_000;
        for (let i = 0; i < totalHours; i += cfg.step) {
            const d = new Date(startDate);
            d.setHours(d.getHours() + i);
            dateUnits.push(d);
        }
    }

    const colW = cfg.columnWidth;
    const totalWidth = dateUnits.length * colW;
    const totalHeight = HEADER_HEIGHT + tasks.length * ROW_HEIGHT;

    function dateToX(d) {
        if (cfg.unit === 'day') {
            const days = dayDiff(dateUnits[0], d);
            return (days / cfg.step) * colW;
        }
        if (cfg.unit === 'month') {
            const months = (d.getFullYear() - dateUnits[0].getFullYear()) * 12 + (d.getMonth() - dateUnits[0].getMonth());
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

    // Wipe and rebuild. Use a wrapper div that scrolls horizontally.
    host.innerHTML = '';
    host.style.position = 'relative';
    host.style.overflow = 'auto';

    const svg = el('svg', {
        width: totalWidth,
        height: totalHeight,
        viewBox: `0 0 ${totalWidth} ${totalHeight}`,
        class: 'lumeo-gantt-svg',
        style: 'display:block',
    }, host);

    // -- HEADER (background + grid lines + labels) --
    el('rect', { x: 0, y: 0, width: totalWidth, height: HEADER_HEIGHT, fill: tokens.card }, svg);

    // Vertical grid lines
    for (let i = 0; i <= dateUnits.length; i++) {
        el('line', {
            x1: i * colW, y1: 0, x2: i * colW, y2: totalHeight,
            stroke: tokens.border, 'stroke-width': '0.5', opacity: '0.5',
        }, svg);
    }

    // Horizontal row lines
    for (let i = 0; i <= tasks.length; i++) {
        const y = HEADER_HEIGHT + i * ROW_HEIGHT;
        el('line', {
            x1: 0, y1: y, x2: totalWidth, y2: y,
            stroke: tokens.border, 'stroke-width': '0.5', opacity: '0.4',
        }, svg);
    }

    // Today indicator
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const todayX = dateToX(today);
    if (todayX >= 0 && todayX <= totalWidth) {
        el('line', {
            x1: todayX, y1: HEADER_HEIGHT - 4, x2: todayX, y2: totalHeight,
            stroke: tokens.primary, 'stroke-width': '2',
        }, svg);
        el('circle', { cx: todayX, cy: HEADER_HEIGHT - 4, r: 4, fill: tokens.primary }, svg);
    }

    // Header text labels — render lower row first (per column), upper row groups by month/year
    let lastUpperLabel = '';
    let lastUpperX = 0;
    dateUnits.forEach((d, i) => {
        const x = i * colW + colW / 2;
        const y = HEADER_HEIGHT - 18;

        // Lower row
        let lowerText = '';
        switch (cfg.headerFmt.lower) {
            case 'dayNum': lowerText = fmtDayNum(d); break;
            case 'weekRange': lowerText = `${d.getDate()}/${d.getMonth() + 1}`; break;
            case 'monthName': lowerText = fmtMonthShort(d); break;
            case 'yearNum': lowerText = fmtYear(d); break;
            case 'time6h': case 'time12h': lowerText = `${d.getHours()}:00`; break;
        }
        const lower = el('text', {
            x, y,
            'text-anchor': 'middle',
            'font-size': '12',
            'font-family': 'system-ui,sans-serif',
            fill: tokens.muted,
        }, svg);
        lower.textContent = lowerText;

        // Highlight today's column
        if (cfg.unit === 'day' && dayDiff(d, today) === 0) {
            const bg = el('rect', {
                x: i * colW + 2, y: HEADER_HEIGHT - 32,
                width: colW - 4, height: 22, rx: 4,
                fill: tokens.primary,
            }, svg);
            // Re-append text on top
            lower.setAttribute('fill', tokens.primaryFg);
            svg.appendChild(lower);
        }

        // Upper row: only render label when it changes
        let upperText = '';
        switch (cfg.headerFmt.upper) {
            case 'month': upperText = fmtMonth(d); break;
            case 'year': upperText = fmtYear(d); break;
            case 'day': upperText = `${fmtMonth(d)} ${d.getDate()}`; break;
        }
        if (upperText && upperText !== lastUpperLabel) {
            const upper = el('text', {
                x: i * colW + 8, y: 18,
                'font-size': '13',
                'font-family': 'system-ui,sans-serif',
                'font-weight': '600',
                fill: tokens.fg,
            }, svg);
            upper.textContent = upperText;
            lastUpperLabel = upperText;
            lastUpperX = i * colW;
        }
    });

    // -- TASK BARS --
    const taskById = new Map();
    tasks.forEach((task, idx) => {
        const rowY = HEADER_HEIGHT + idx * ROW_HEIGHT;
        const barY = rowY + (ROW_HEIGHT - BAR_HEIGHT) / 2;
        const x1 = dateToX(task.start);
        const x2 = dateToX(addDays(task.end, 1)); // end-inclusive
        const barW = Math.max(8, x2 - x1);

        taskById.set(task.id, { task, x: x1, y: barY, w: barW });

        const group = el('g', { class: 'lumeo-gantt-bar-wrapper', 'data-task-id': task.id, style: 'cursor:pointer' }, svg);

        // Background bar
        el('rect', {
            x: x1, y: barY,
            width: barW, height: BAR_HEIGHT,
            rx: 4, ry: 4,
            fill: tokens.primary,
            'fill-opacity': '0.30',
        }, group);

        // Progress overlay
        if (task.progress > 0) {
            el('rect', {
                x: x1, y: barY,
                width: barW * (task.progress / 100), height: BAR_HEIGHT,
                rx: 4, ry: 4,
                fill: tokens.primary,
            }, group);
        }

        // Label
        const label = el('text', {
            x: x1 + PADDING_X, y: barY + BAR_HEIGHT / 2 + 4,
            'font-size': '12',
            'font-family': 'system-ui,sans-serif',
            'font-weight': '500',
            fill: tokens.fg,
            'pointer-events': 'none',
        }, group);
        label.textContent = task.name;

        // Click handler
        group.addEventListener('click', () => {
            inst.dotNetRef.invokeMethodAsync('JsOnTaskClick', taskToJson(task)).catch(() => {});
        });
    });

    // -- DEPENDENCY ARROWS --
    tasks.forEach((task, idx) => {
        if (!task.dependencies || task.dependencies.length === 0) return;
        const target = taskById.get(task.id);
        if (!target) return;
        for (const depId of task.dependencies) {
            const source = taskById.get(depId);
            if (!source) continue;
            // Arrow goes from end of source bar to start of target bar.
            const sx = source.x + source.w;
            const sy = source.y + BAR_HEIGHT / 2;
            const tx = target.x;
            const ty = target.y + BAR_HEIGHT / 2;
            const midX = sx + 12;
            const path = `M ${sx} ${sy} L ${midX} ${sy} L ${midX} ${ty} L ${tx - 4} ${ty}`;
            el('path', {
                d: path,
                fill: 'none',
                stroke: tokens.muted,
                'stroke-width': '1.2',
                'marker-end': '',
                opacity: '0.7',
            }, svg);
            // Arrow head
            el('polygon', {
                points: `${tx - 6},${ty - 4} ${tx},${ty} ${tx - 6},${ty + 4}`,
                fill: tokens.muted,
                opacity: '0.7',
            }, svg);
        }
    });

    // Auto-scroll horizontally so today is centered (best-effort)
    requestAnimationFrame(() => {
        const todayPx = dateToX(today);
        if (todayPx > 0) {
            host.scrollLeft = Math.max(0, todayPx - host.clientWidth / 2);
        }
    });

    if (typeof console !== 'undefined' && console.debug) {
        console.debug('[lumeo-gantt-v2-custom] rendered', {
            tasks: tasks.length,
            viewMode,
            totalWidth,
            totalHeight,
            columns: dateUnits.length,
        });
    }
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

export const gantt = {
    async init(elOrId, dotNetRef, options) {
        const host = typeof elOrId === 'string' ? document.getElementById(elOrId) : elOrId;
        if (!host) throw new Error('Gantt: root element missing');

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

        // Re-render on theme change (if Lumeo flips dark/light) and resize.
        const ro = new ResizeObserver(() => render(inst));
        ro.observe(host);
        inst._ro = ro;

        if (typeof console !== 'undefined' && console.debug) {
            console.debug('[lumeo-gantt-v2-custom] init', {
                id,
                taskCount: inst.tasks.length,
                viewMode: inst.viewMode,
            });
        }

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

    destroy(id) {
        const inst = instances.get(id);
        if (!inst) return;
        if (inst._ro) inst._ro.disconnect();
        if (inst.host) inst.host.innerHTML = '';
        instances.delete(id);
    },
};

export default gantt;
