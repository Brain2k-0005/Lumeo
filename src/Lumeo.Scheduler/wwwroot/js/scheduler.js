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

// FullCalendar locale packs ship as files inside @fullcalendar/core itself
// (core/locales/{code}.js) rather than as a separate npm package. Registering one
// cdn-deps.json key per locale (FullCalendar ships ~150) would be pure manifest
// bloat for files that are already reachable through the ONE key that exists for
// core — `fullCalendarCore` (see tools/Lumeo.RegistryGen/CdnDeps.cs, "the only
// Scheduler keys that exist"). So instead of a new registered key, locale packs
// are resolved RELATIVE TO wherever FC_CORE itself resolved to — honouring both
// the `fullCalendarBase` convenience override and an individual `fullCalendarCore`
// override, exactly like every other Scheduler module URL above.
//
// Previously this only ever checked `fullCalendarBase` directly and otherwise fell
// straight through to the public esm.sh URL — so a deployment that self-hosted
// FullCalendar via the registered `fullCalendarCore` key (without ALSO setting the
// separate `fullCalendarBase` convenience key) still leaked a public request for
// every locale pack on a strict-CSP/offline deployment, and silently fell back to
// English when that request was blocked. "en" is FullCalendar's own built-in
// default and needs no extra file/import at all.
const localeModules = new Map();
function _fcLocaleUrl(code) {
    // FC_CORE is already the fully-resolved core URL (fullCalendarBase, then the
    // registered fullCalendarCore override, then the public esm.sh default — see
    // _fcUrl above). Two shapes to resolve relative to it:
    //  - A concrete self-hosted file (e.g. ".../core.js"): locale packs sit next to
    //    it, so take its directory and append locales/{code}.js as a sibling file.
    //  - A bare ESM package specifier (the public esm.sh default, e.g.
    //    ".../@fullcalendar/core@6" — note the version pin can itself contain dots,
    //    e.g. "@6.1.10", so this is NOT simply "has a dot"): esm.sh serves package
    //    sub-paths directly off that specifier, so append locales/{code}.js as-is.
    const lastSegment = FC_CORE.slice(FC_CORE.lastIndexOf('/') + 1);
    const isConcreteFile = /\.(m?js|cjs)$/i.test(lastSegment);
    const base = isConcreteFile ? FC_CORE.slice(0, FC_CORE.lastIndexOf('/')) : FC_CORE;
    return `${base}/locales/${code}.js`;
}
async function loadLocale(code) {
    if (!code || code === 'en') return null;
    if (localeModules.has(code)) return localeModules.get(code);
    try {
        const mod = await import(/* @vite-ignore */ _fcLocaleUrl(code));
        const localeObj = mod.default || null;
        localeModules.set(code, localeObj);
        return localeObj;
    } catch (e) {
        console.error('[Lumeo Scheduler] Failed to load locale pack "' + code + '": ' + e.message);
        return null;
    }
}

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

// Lumeo design tokens that ship a matching "-foreground" pair (see
// src/Lumeo/wwwroot/css/lumeo.css) — i.e. tokens it's safe to auto-derive
// readable event text/dot color for. `primary` is deliberately excluded:
// it's already lumeo-scheduler.css's DEFAULT (--fc-event-text-color), so
// events with no Color override need no extra class.
const FOREGROUND_PAIRED_TOKENS = new Set([
    'secondary', 'destructive', 'accent', 'muted', 'success', 'warning', 'info',
]);

// SchedulerEvent.Color (see SchedulerTypes.cs) is documented as "a CSS color
// or variable reference, e.g. var(--color-primary)". When a per-event Color
// names one of Lumeo's semantic tokens, FullCalendar's rendered background
// changes but — until scheduler.js tags the event — its title/time text and
// dot indicator stayed hardcoded to var(--color-primary-foreground) (see the
// lumeo-fc-event-color-* rules in lumeo-scheduler.css), which is only
// guaranteed to contrast against var(--color-primary) itself. That silently
// produced unreadable combinations for any OTHER Color (e.g. white-on-white
// with --color-accent in light mode; near-black-on-dark-grey with
// --color-accent in dark mode). Returns the matching CSS class (which the
// stylesheet maps back to that SAME token's own "-foreground" pair) or null
// when Color isn't one of Lumeo's known paired tokens (raw hex/rgb, an
// unpaired token like the chart-N series colors, or no Color at all) — those
// keep today's default (--color-primary-foreground) unchanged.
function colorForegroundClass(color) {
    if (typeof color !== 'string') return null;
    const match = color.trim().match(/^var\(\s*--color-([a-z0-9-]+)\s*\)$/i);
    if (!match) return null;
    const token = match[1].toLowerCase();
    return FOREGROUND_PAIRED_TOKENS.has(token) ? `lumeo-fc-event-color-${token}` : null;
}

function mergeClassNames(existing, extra) {
    const base = !existing ? [] : (typeof existing === 'string' ? existing.split(/\s+/).filter(Boolean) : existing);
    return extra ? [...base, extra] : base;
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
    const fgClass = colorForegroundClass(color);

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
        const mergedRecurClassNames = mergeClassNames(classNames, fgClass);
        if (mergedRecurClassNames.length > 0) obj.classNames = mergedRecurClassNames;
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
    const mergedClassNames = mergeClassNames(classNames, fgClass);
    if (mergedClassNames.length > 0) obj.classNames = mergedClassNames;
    if (extendedProps) obj.extendedProps = extendedProps;
    return obj;
}

export const scheduler = {
    async init(el, dotNetRef, options) {
        const { Calendar, dayGridPlugin, timeGridPlugin, listPlugin, interactionPlugin } = await loadFullCalendar();
        if (!el) throw new Error('Scheduler: root element missing');

        const opts = options || {};
        const events = Array.isArray(opts.events) ? opts.events.map(normalizeEvent) : [];

        // Bug fix: previously no locale was ever passed to FullCalendar at all, so even
        // fully-translated Lumeo locales had zero effect on FullCalendar's own rendered
        // chrome (day/month names, its internal button text). Load the matching locale
        // pack (if any — "en" needs none) before constructing the Calendar so it boots
        // already localized instead of flashing English first.
        const localeCode = opts.locale || 'en';
        const localeObj = await loadLocale(localeCode);

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
            locale: localeCode,
            locales: localeObj ? [localeObj] : [],
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
            // Month/week day cells are routinely too narrow for a full event
            // title (lumeo-scheduler.css now ellipsis-truncates instead of
            // FullCalendar's default hard `clip`) — a native title attribute
            // is the simplest way to surface the FULL text on hover/focus
            // without a custom tooltip widget/dependency, and works for
            // keyboard users too since the event chip is itself focusable.
            eventDidMount(info) {
                const timePrefix = info.timeText ? `${info.timeText} — ` : '';
                info.el.title = `${timePrefix}${info.event.title}`;
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

    // Bug fix companion to init()'s locale wiring: pushes an UPDATED locale onto an
    // already-live Calendar instance so a runtime UI-culture change (host app flips
    // CurrentUICulture and re-renders) actually reaches FullCalendar's chrome instead
    // of the locale only ever being read once at init.
    async setLocale(id, locale) {
        const inst = instances.get(id);
        if (!inst) return;
        const code = locale || 'en';
        const localeObj = await loadLocale(code);
        if (localeObj) {
            const existing = inst.calendar.getOption('locales') || [];
            if (!existing.some(l => l && l.code === localeObj.code)) {
                inst.calendar.setOption('locales', [...existing, localeObj]);
            }
        }
        inst.calendar.setOption('locale', code);
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

// Test-only seam: the repo has no JS unit-test harness for wwwroot interop files
// (bUnit tests mock the whole module instead of executing it), so the CDN-override
// URL builder — pure logic with no DOM/FullCalendar dependency — is exposed here
// for a standalone `node --test` script (tests/js/scheduler-locale-url.test.mjs) to
// call directly. Not part of the Blazor interop surface.
export const __testing = { fcLocaleUrl: _fcLocaleUrl };
