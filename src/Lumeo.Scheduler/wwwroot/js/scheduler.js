// Lumeo Scheduler interop — wraps FullCalendar v6 loaded from an ESM CDN.
// One module-level registry keyed by a generated instance id lets Blazor
// address instances across the JSInvokable boundary without holding JS
// references directly.

const instances = new Map();
let fcLoaded = false;
let fcLoadPromise = null;
let fcModules = null;

// Auto-repaint all active calendars when the app theme changes.
// theme.js fires this event after any theme mutation (dark/light toggle, palette swap,
// radius change, etc.) so FullCalendar's CSS-var-driven overrides are re-resolved.
// Deferred with queueMicrotask so DOM class / CSS-var updates are already
// committed before FullCalendar re-reads them via calendar.render().
if (typeof document !== 'undefined' && !window.__lumeoSchedulerThemeListener) {
    document.addEventListener('lumeo:theme-changed', () => {
        if (instances.size === 0) return;
        queueMicrotask(() => {
            try { refreshAllCalendars(); } catch (_) { /* ignore */ }
        });
    });
    window.__lumeoSchedulerThemeListener = true;
}

function refreshAllCalendars() {
    for (const [, inst] of instances) {
        try { inst.calendar.render(); } catch (_) { /* ignore stale instances */ }
    }
}

// CDN URLs — overridable via the standard `window.lumeoCdn` config:
//   window.lumeoCdn = { fullCalendarBase: '/lib/fullcalendar/' };
// Individual modules can also be overridden one-by-one (fullCalendarCore, etc.)
// for fine-grained self-hosting via the Lumeo CLI deps installer.
function _cdn(key, fallback) {
    return (typeof window !== 'undefined' && window.lumeoCdn && window.lumeoCdn[key]) || fallback;
}
function _fcUrl(pkg, fallback) {
    const base = _cdn('fullCalendarBase', null);
    return base ? `${base.replace(/\/$/, '')}/${pkg}.js` : _cdn(`fullCalendar${pkg[0].toUpperCase()}${pkg.slice(1)}`, fallback);
}
const FC_CORE = _fcUrl('core', 'https://esm.sh/@fullcalendar/core@6');
const FC_DAYGRID = _fcUrl('daygrid', 'https://esm.sh/@fullcalendar/daygrid@6');
const FC_TIMEGRID = _fcUrl('timegrid', 'https://esm.sh/@fullcalendar/timegrid@6');
const FC_LIST = _fcUrl('list', 'https://esm.sh/@fullcalendar/list@6');
const FC_INTERACTION = _fcUrl('interaction', 'https://esm.sh/@fullcalendar/interaction@6');

function injectLumeoSchedulerOverrides() {
    if (document.querySelector('[data-lumeo-scheduler-overrides]')) return;
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.setAttribute('data-lumeo-scheduler-overrides', '');
    link.href = '/_content/Lumeo.Scheduler/css/lumeo-scheduler.css';
    document.head.appendChild(link);
}

async function loadFullCalendar() {
    if (fcLoaded) return fcModules;
    if (fcLoadPromise) return fcLoadPromise;

    fcLoadPromise = (async () => {
        // Inject Lumeo theme overrides before any Calendar instance is created.
        injectLumeoSchedulerOverrides();
        try {
            const [core, dayGrid, timeGrid, listPlugin, interaction] = await Promise.all([
                import(/* @vite-ignore */ FC_CORE),
                import(/* @vite-ignore */ FC_DAYGRID),
                import(/* @vite-ignore */ FC_TIMEGRID),
                import(/* @vite-ignore */ FC_LIST),
                import(/* @vite-ignore */ FC_INTERACTION),
            ]);
            fcModules = {
                Calendar: core.Calendar,
                dayGridPlugin: dayGrid.default,
                timeGridPlugin: timeGrid.default,
                listPlugin: listPlugin.default,
                interactionPlugin: interaction.default,
            };
            fcLoaded = true;
            return fcModules;
        } catch (e) {
            fcLoadPromise = null; // allow retry on next init call
            throw new Error('[Lumeo Scheduler] FullCalendar bundle failed to load: ' + e.message);
        }
    })();

    return fcLoadPromise;
}

function mapView(v) {
    switch ((v || '').toString().toLowerCase()) {
        case 'week': return 'timeGridWeek';
        case 'day': return 'timeGridDay';
        case 'list': return 'listWeek';
        case 'month':
        default: return 'dayGridMonth';
    }
}

function eventToJson(ev) {
    if (!ev) return null;
    const start = ev.start ? ev.start.toISOString() : null;
    const end = (ev.end || ev.start) ? (ev.end || ev.start).toISOString() : null;
    return {
        id: ev.id || '',
        title: ev.title || '',
        start: start,
        end: end,
        allDay: !!ev.allDay,
        color: ev.backgroundColor || null,
        url: ev.url || null,
        extendedProps: ev.extendedProps ? { ...ev.extendedProps } : null,
    };
}

function normalizeEvent(e) {
    // Accept either camelCase or PascalCase keys (JSON from .NET can ship either).
    const id = e.id ?? e.Id;
    const title = e.title ?? e.Title;
    const allDay = e.allDay ?? e.AllDay ?? false;
    const color = e.color ?? e.Color ?? null;
    const url = e.url ?? e.Url ?? null;
    const extendedProps = e.extendedProps ?? e.ExtendedProps ?? null;
    const classNames = e.classNames ?? e.ClassNames ?? null;

    // ── Simple recurrence (free FullCalendar model, no rrule premium plugin) ──
    const daysOfWeek = e.daysOfWeek ?? e.DaysOfWeek ?? null;
    if (Array.isArray(daysOfWeek) && daysOfWeek.length > 0) {
        const obj = {
            id: id != null ? String(id) : undefined,
            title: title || '',
            daysOfWeek: daysOfWeek,
            allDay: !!allDay,
        };
        const startTime = e.startTime ?? e.StartTime ?? null;
        const endTime = e.endTime ?? e.EndTime ?? null;
        if (startTime) obj.startTime = startTime;
        if (endTime) obj.endTime = endTime;
        const startRecur = e.startRecur ?? e.StartRecur ?? null;
        const endRecur = e.endRecur ?? e.EndRecur ?? null;
        if (startRecur) obj.startRecur = startRecur;
        if (endRecur) obj.endRecur = endRecur;
        // exdate: array of ISO date strings to skip (exception dates).
        const exdate = e.exdate ?? e.Exdate ?? null;
        if (Array.isArray(exdate) && exdate.length > 0) obj.exdate = exdate;
        if (color) obj.backgroundColor = color, obj.borderColor = color;
        if (url) obj.url = url;
        if (classNames) obj.classNames = typeof classNames === 'string'
            ? classNames.split(/\s+/).filter(Boolean)
            : classNames;
        if (extendedProps) obj.extendedProps = extendedProps;
        return obj;
    }

    // ── Standard (non-recurring) event ────────────────────────────────────
    const start = e.start ?? e.Start;
    const end = e.end ?? e.End;
    const obj = {
        id: id != null ? String(id) : undefined,
        title: title || '',
        start: start,
        end: end,
        allDay: !!allDay,
    };
    if (color) obj.backgroundColor = color, obj.borderColor = color;
    if (url) obj.url = url;
    if (classNames) obj.classNames = typeof classNames === 'string'
        ? classNames.split(/\s+/).filter(Boolean)
        : classNames;
    if (extendedProps) obj.extendedProps = extendedProps;
    return obj;
}

export const scheduler = {
    async init(el, dotNetRef, options) {
        const { Calendar, dayGridPlugin, timeGridPlugin, listPlugin, interactionPlugin } = await loadFullCalendar();
        if (!el) throw new Error('Scheduler: root element missing');

        const opts = options || {};
        const events = Array.isArray(opts.events) ? opts.events.map(normalizeEvent) : [];

        const calOpts = {
            plugins: [dayGridPlugin, timeGridPlugin, listPlugin, interactionPlugin],
            initialView: mapView(opts.view),
            initialDate: opts.initialDate || undefined,
            editable: opts.editable !== false,
            selectable: opts.selectable !== false,
            selectMirror: true,
            dayMaxEvents: true,
            businessHours: !!opts.businessHours,
            height: opts.height || '640px',
            firstDay: typeof opts.firstDay === 'number' ? opts.firstDay : 1,
            headerToolbar: false, // Lumeo supplies its own toolbar
            // ── New: time-grid display options ──────────────────────────────
            nowIndicator: opts.nowIndicator !== false, // default true
            events: events,
            eventClick(info) {
                info.jsEvent?.preventDefault?.();
                dotNetRef.invokeMethodAsync('JsOnEventClick', eventToJson(info.event));
            },
            select(info) {
                dotNetRef.invokeMethodAsync('JsOnDateSelect', {
                    start: info.start.toISOString(),
                    end: info.end.toISOString(),
                    allDay: !!info.allDay,
                });
            },
            eventChange(info) {
                dotNetRef.invokeMethodAsync('JsOnEventChange', eventToJson(info.event));
            },
        };

        // Only set slotMinTime / slotMaxTime / slotDuration when explicitly provided
        // so FullCalendar's built-in defaults (00:00 / 24:00 / 00:30) remain unchanged.
        if (opts.slotMinTime) calOpts.slotMinTime = opts.slotMinTime;
        if (opts.slotMaxTime) calOpts.slotMaxTime = opts.slotMaxTime;
        if (opts.slotDuration) calOpts.slotDuration = opts.slotDuration;

        let calendar;
        try {
            calendar = new Calendar(el, calOpts);
        } catch (e) {
            throw new Error('[Lumeo Scheduler] Calendar initialization failed: ' + e.message);
        }

        calendar.render();

        const id = 'lumeo-scheduler-' + Math.random().toString(36).slice(2, 10);
        instances.set(id, { calendar, dotNetRef });
        return id;
    },

    setEvents(id, events) {
        const inst = instances.get(id);
        if (!inst) return;
        // removeAllEvents() only clears event OBJECTS, not the event SOURCES that
        // hold them. addEventSource() then APPENDS a new source on every data
        // update, so the source list grew unboundedly (memory + duplicate-source
        // overhead). Remove every existing source (including the initial `events`
        // option source) before adding the fresh one.
        inst.calendar.getEventSources().forEach(s => s.remove());
        const arr = Array.isArray(events) ? events.map(normalizeEvent) : [];
        inst.calendar.addEventSource(arr);
    },

    changeView(id, view) {
        const inst = instances.get(id);
        if (!inst) return;
        inst.calendar.changeView(mapView(view));
    },

    gotoDate(id, dateStr) {
        const inst = instances.get(id);
        if (!inst || !dateStr) return;
        inst.calendar.gotoDate(dateStr);
    },

    prev(id) {
        const inst = instances.get(id);
        if (!inst) return;
        inst.calendar.prev();
    },

    next(id) {
        const inst = instances.get(id);
        if (!inst) return;
        inst.calendar.next();
    },

    today(id) {
        const inst = instances.get(id);
        if (!inst) return;
        inst.calendar.today();
    },

    getTitle(id) {
        const inst = instances.get(id);
        if (!inst) return '';
        try { return inst.calendar.view.title || ''; } catch { return ''; }
    },

    destroy(id) {
        const inst = instances.get(id);
        if (!inst) return;
        try { inst.calendar.destroy(); } catch (_) { /* ignore */ }
        instances.delete(id);
    },
};

// Named exports also accessible as `scheduler.*` for simpler interop.
export default scheduler;
