// Lumeo Scheduler interop — wraps FullCalendar v6 loaded from an ESM CDN.
// One module-level registry keyed by a generated instance id lets Blazor
// address instances across the JSInvokable boundary without holding JS
// references directly.

const instances = new Map();
let fcLoaded = false;
let fcLoadPromise = null;
let fcModules = null;

const FC_CORE = 'https://esm.sh/@fullcalendar/core@6';
const FC_DAYGRID = 'https://esm.sh/@fullcalendar/daygrid@6';
const FC_TIMEGRID = 'https://esm.sh/@fullcalendar/timegrid@6';
const FC_LIST = 'https://esm.sh/@fullcalendar/list@6';
const FC_INTERACTION = 'https://esm.sh/@fullcalendar/interaction@6';

async function loadFullCalendar() {
    if (fcLoaded) return fcModules;
    if (fcLoadPromise) return fcLoadPromise;

    fcLoadPromise = (async () => {
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
    const start = e.start ?? e.Start;
    const end = e.end ?? e.End;
    const allDay = e.allDay ?? e.AllDay ?? false;
    const color = e.color ?? e.Color ?? null;
    const url = e.url ?? e.Url ?? null;
    const extendedProps = e.extendedProps ?? e.ExtendedProps ?? null;
    const obj = {
        id: id != null ? String(id) : undefined,
        title: title || '',
        start: start,
        end: end,
        allDay: !!allDay,
    };
    if (color) obj.backgroundColor = color, obj.borderColor = color;
    if (url) obj.url = url;
    if (extendedProps) obj.extendedProps = extendedProps;
    return obj;
}

export const scheduler = {
    async init(el, dotNetRef, options) {
        const { Calendar, dayGridPlugin, timeGridPlugin, listPlugin, interactionPlugin } = await loadFullCalendar();
        if (!el) throw new Error('Scheduler: root element missing');

        const opts = options || {};
        const events = Array.isArray(opts.events) ? opts.events.map(normalizeEvent) : [];

        const calendar = new Calendar(el, {
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
                calendar.unselect();
            },
            eventChange(info) {
                dotNetRef.invokeMethodAsync('JsOnEventChange', eventToJson(info.event));
            },
        });

        calendar.render();

        const id = 'lumeo-scheduler-' + Math.random().toString(36).slice(2, 10);
        instances.set(id, { calendar, dotNetRef });
        return id;
    },

    setEvents(id, events) {
        const inst = instances.get(id);
        if (!inst) return;
        inst.calendar.removeAllEvents();
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
