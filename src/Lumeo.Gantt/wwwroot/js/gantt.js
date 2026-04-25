// Lumeo Gantt interop — wraps Frappe Gantt loaded from an ESM CDN.
// Mirrors the Scheduler pattern: a module-level registry keyed by a generated
// instance id, lazy-loaded library, and DotNet callbacks invoked via the
// registered DotNetObjectReference.

const instances = new Map();
let gLoaded = false;
let gLoadPromise = null;
let gModule = null;
let cssInjected = false;

const GANTT_ESM = 'https://esm.sh/frappe-gantt@1';
const GANTT_CSS = 'https://esm.sh/frappe-gantt@1/dist/frappe-gantt.css';

async function injectCssOnce() {
    if (cssInjected) return;
    cssInjected = true;
    try {
        // Fetch + inject as <style> — most reliable cross-browser approach
        // (CSS import assertions aren't universally supported yet).
        const res = await fetch(GANTT_CSS);
        if (!res.ok) return;
        const css = await res.text();
        const style = document.createElement('style');
        style.setAttribute('data-lumeo-gantt-css', '');
        style.textContent = css;
        document.head.appendChild(style);
    } catch (_) {
        // Non-fatal: chart still renders, just without default Frappe styles.
        // Lumeo theme overrides in lumeo.css cover the essentials.
    }
}

async function loadGantt() {
    if (gLoaded) return gModule;
    if (gLoadPromise) return gLoadPromise;

    gLoadPromise = (async () => {
        await injectCssOnce();
        const mod = await import(/* @vite-ignore */ GANTT_ESM);
        // Frappe Gantt default export is the Gantt class.
        gModule = { Gantt: mod.default || mod.Gantt || mod };
        gLoaded = true;
        return gModule;
    })();

    return gLoadPromise;
}

function mapViewMode(v) {
    switch ((v || '').toString()) {
        case 'QuarterDay': return 'Quarter Day';
        case 'HalfDay': return 'Half Day';
        case 'Day': return 'Day';
        case 'Week': return 'Week';
        case 'Month': return 'Month';
        case 'Year': return 'Year';
        default: return 'Day';
    }
}

function toYmd(d) {
    if (!d) return null;
    const date = (d instanceof Date) ? d : new Date(d);
    if (isNaN(date.getTime())) return null;
    // YYYY-MM-DD in local time to avoid TZ drift.
    const y = date.getFullYear();
    const m = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
}

function normalizeTask(t) {
    // Accept camelCase (JS) or PascalCase (.NET) keys.
    const id = t.id ?? t.Id;
    const name = t.name ?? t.Name;
    const start = t.start ?? t.Start;
    const end = t.end ?? t.End;
    const progress = t.progress ?? t.Progress ?? 0;
    const deps = t.dependencies ?? t.Dependencies ?? [];
    const custom = t.customClass ?? t.CustomClass ?? null;

    const obj = {
        id: id != null ? String(id) : undefined,
        name: name || '',
        start: toYmd(start),
        end: toYmd(end),
        progress: Number(progress) || 0,
        dependencies: Array.isArray(deps) ? deps.join(',') : (deps || ''),
    };
    if (custom) obj.custom_class = custom;
    return obj;
}

function taskToJson(task, overrides) {
    // Shape matches GanttTask record on the .NET side — PascalCase with ISO
    // date strings so System.Text.Json binds them straight onto DateTime.
    const src = task || {};
    const start = overrides?.start ?? src.start ?? src._start;
    const end = overrides?.end ?? src.end ?? src._end;
    const progress = overrides?.progress ?? src.progress ?? 0;
    const deps = (src.dependencies || '')
        .toString()
        .split(',')
        .map(s => s.trim())
        .filter(Boolean);

    const toIso = (d) => {
        if (!d) return null;
        const date = (d instanceof Date) ? d : new Date(d);
        if (isNaN(date.getTime())) return null;
        return date.toISOString();
    };

    return {
        Id: String(src.id || ''),
        Name: src.name || '',
        Start: toIso(start),
        End: toIso(end),
        Progress: Number(progress) || 0,
        Dependencies: deps,
        CustomClass: src.custom_class || null,
    };
}

function buildInstance(el, dotNetRef, options) {
    const opts = options || {};
    const tasks = Array.isArray(opts.tasks) ? opts.tasks.map(normalizeTask) : [];
    const viewMode = mapViewMode(opts.viewMode);
    const readOnly = !!opts.readonly;

    // Frappe Gantt mutates the container (injects an <svg>). Clear any
    // previous render before instantiating a replacement.
    while (el.firstChild) el.removeChild(el.firstChild);

    const { Gantt } = gModule;
    const g = new Gantt(el, tasks, {
        view_mode: viewMode,
        read_only: readOnly,
        // Frappe Gantt ≥1.0 uses these option names for drag guards.
        bar_edit: !readOnly,
        bar_drag: !readOnly,
        bar_progress_drag: !readOnly,
        on_click(task) {
            try { dotNetRef.invokeMethodAsync('JsOnTaskClick', taskToJson(task)); } catch (_) { }
        },
        on_date_change(task, start, end) {
            try { dotNetRef.invokeMethodAsync('JsOnDateChange', taskToJson(task, { start, end })); } catch (_) { }
        },
        on_progress_change(task, progress) {
            try { dotNetRef.invokeMethodAsync('JsOnProgressChange', taskToJson(task, { progress })); } catch (_) { }
        },
    });

    return g;
}

export const gantt = {
    async init(el, dotNetRef, options) {
        await loadGantt();
        if (!el) throw new Error('Gantt: root element missing');

        const g = buildInstance(el, dotNetRef, options);

        const id = 'lumeo-gantt-' + Math.random().toString(36).slice(2, 10);
        instances.set(id, { gantt: g, el, dotNetRef, options });
        return id;
    },

    setTasks(id, tasks) {
        const inst = instances.get(id);
        if (!inst) return;
        // Frappe Gantt doesn't expose a reliable live-refresh that keeps the
        // timeline axis accurate across very different task ranges, so we
        // rebuild the instance. The container + callbacks are preserved.
        const nextOptions = { ...(inst.options || {}), tasks };
        inst.options = nextOptions;
        try {
            inst.gantt = buildInstance(inst.el, inst.dotNetRef, nextOptions);
        } catch (e) {
            console.error('[Lumeo Gantt] setTasks failed', e);
        }
    },

    changeViewMode(id, mode) {
        const inst = instances.get(id);
        if (!inst) return;
        try {
            inst.gantt.change_view_mode(mapViewMode(mode));
            inst.options = { ...(inst.options || {}), viewMode: mode };
        } catch (e) {
            console.error('[Lumeo Gantt] changeViewMode failed', e);
        }
    },

    destroy(id) {
        const inst = instances.get(id);
        if (!inst) return;
        try {
            // Frappe Gantt has no public destroy — just clear the DOM subtree.
            if (inst.el) {
                while (inst.el.firstChild) inst.el.removeChild(inst.el.firstChild);
            }
        } catch (_) { /* ignore */ }
        instances.delete(id);
    },
};

export default gantt;
