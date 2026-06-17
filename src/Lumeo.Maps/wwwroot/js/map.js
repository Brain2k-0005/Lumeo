// Lumeo.Maps — MapLibre GL wrapper module.
//
// MapLibre GL JS + its stylesheet are loaded from CDN on first use, then
// cached on the window. The default basemap is CARTO's free Positron
// (light) / Dark Matter (dark) GL vector style — no API key required,
// attributed to OpenStreetMap + CARTO. This mirrors mapcn.dev's default.
//
// Coordinate convention: MapLibre takes [lng, lat] arrays. The C# side
// passes (Lat, Lon) tuples; we flip at the interop boundary so component
// authors keep using the natural (lat, lon) order. Internally everything
// after `toLngLat()` is [lng, lat].
//
// We also inject our own `map.css` (shadcn-style chrome on top of MapLibre's
// defaults). It's idempotent: one <link data-lumeo-map> per document.

// CDN URLs — can be overridden globally by setting
//   window.lumeoCdn = { mapLibreJs: '/lib/maplibre/maplibre-gl.js',
//                       mapLibreCss: '/lib/maplibre/maplibre-gl.css' };
// before the first Map mounts.  Lets airgapped / strict-CSP / CLI-installed
// consumers self-host the lib without forking the satellite.  MapLibre v5+
// ships the globe projection as a stable feature (it was experimental in v4).
function _cdn(key, fallback) {
    return (typeof window !== 'undefined' && window.lumeoCdn && window.lumeoCdn[key]) || fallback;
}
const MAPLIBRE_JS = _cdn('mapLibreJs', 'https://unpkg.com/maplibre-gl@5.7.1/dist/maplibre-gl.js');
const MAPLIBRE_CSS = _cdn('mapLibreCss', 'https://unpkg.com/maplibre-gl@5.7.1/dist/maplibre-gl.css');
const LUMEO_MAP_CSS = '_content/Lumeo.Maps/css/map.css';

// CARTO basemaps (free, no API key, OSM + CARTO attribution).
// `Auto` is special — resolved per call based on the host page's theme.
const STYLE_PRESETS = {
    Positron:   'https://basemaps.cartocdn.com/gl/positron-gl-style/style.json',
    DarkMatter: 'https://basemaps.cartocdn.com/gl/dark-matter-gl-style/style.json',
    Voyager:    'https://basemaps.cartocdn.com/gl/voyager-gl-style/style.json',
    // Backwards-compatible aliases — pre-MapLibre consumers passed these
    // raster preset names. Map them to the closest vector equivalents so
    // upgrading the engine doesn't break any existing <Map TileLayer="…"> calls.
    CartoLight:    'https://basemaps.cartocdn.com/gl/positron-gl-style/style.json',
    CartoDark:     'https://basemaps.cartocdn.com/gl/dark-matter-gl-style/style.json',
    OpenStreetMap: 'https://basemaps.cartocdn.com/gl/positron-gl-style/style.json',
    Satellite:     'https://basemaps.cartocdn.com/gl/voyager-gl-style/style.json',
    Terrain:       'https://basemaps.cartocdn.com/gl/voyager-gl-style/style.json',
};

const instances = new Map();
let maplibreLoadPromise = null;

// Single shared MutationObserver + media-query listener for theme changes.
// Watching per-document (not per-map) to avoid N observers for N maps.
let _themeObserver = null;
let _themeMq = null;
let _lastDark = null; // cached state so we only react on actual toggles

function toLngLat(lat, lon) { return [lon, lat]; }

function ensureLumeoMapCss() {
    if (document.querySelector('link[data-lumeo-map]')) return;
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = LUMEO_MAP_CSS;
    link.setAttribute('data-lumeo-map', '');
    document.head.appendChild(link);
}

function loadMapLibre() {
    if (typeof window !== 'undefined' && window.maplibregl) return Promise.resolve(window.maplibregl);
    if (maplibreLoadPromise) return maplibreLoadPromise;

    maplibreLoadPromise = new Promise((resolve, reject) => {
        if (!document.querySelector('link[data-lumeo-maplibre]')) {
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = MAPLIBRE_CSS;
            link.setAttribute('data-lumeo-maplibre', '');
            document.head.appendChild(link);
        }

        if (window.maplibregl) { resolve(window.maplibregl); return; }

        const existing = document.querySelector('script[data-lumeo-maplibre]');
        if (existing) {
            existing.addEventListener('load', () => resolve(window.maplibregl));
            existing.addEventListener('error', () => reject(new Error('Failed to load MapLibre GL')));
            return;
        }

        const script = document.createElement('script');
        script.src = MAPLIBRE_JS;
        script.async = true;
        script.setAttribute('data-lumeo-maplibre', '');
        script.onload = () => resolve(window.maplibregl);
        script.onerror = () => reject(new Error('Failed to load MapLibre GL'));
        document.head.appendChild(script);
    });
    return maplibreLoadPromise;
}

// ----------------------------------------------------------------------
// Theme detection (Auto basemap)
// ----------------------------------------------------------------------

function isDarkTheme() {
    // Priority 1: explicit class on <html> (Lumeo's themeManager adds/removes 'dark').
    // This is the most reliable signal — it reflects whatever the user has chosen.
    const root = document.documentElement;
    if (root.classList.contains('dark')) return true;

    // Priority 2: data-theme="dark" attribute (non-Lumeo hosts).
    if (root.dataset?.theme === 'dark') return true;

    // Priority 3: if the theme was explicitly set to light via localStorage
    // (Lumeo's themeManager stores 'theme-mode'), don't fall through to matchMedia.
    // Without this guard, toggling to 'light' while the OS is in dark mode would
    // still return true here — meaning our observer wouldn't detect the toggle.
    const storedMode = (typeof localStorage !== 'undefined')
        ? (localStorage.getItem('theme-mode') || localStorage.getItem('theme'))
        : null;
    if (storedMode === 'light') return false;

    // Priority 4: OS / browser preference (only when no explicit override).
    return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
}

function resolveStyleUrl(name) {
    if (!name || name === 'Auto') {
        return isDarkTheme() ? STYLE_PRESETS.DarkMatter : STYLE_PRESETS.Positron;
    }
    if (STYLE_PRESETS[name]) return STYLE_PRESETS[name];
    // Anything else: assume it's already a style.json URL (or a raw tile URL,
    // in which case MapLibre will reject — that's on the caller).
    return name;
}

// Called whenever the host page's dark/light class toggles. Iterates every
// live map instance and:
//   • Auto-tile instances: calls setStyle() with the new basemap URL. MapLibre
//     wipes user sources/layers on setStyle; the 'styledata' handler in init()
//     re-adds cluster layers (with freshly resolved CSS-var colors) and shapes.
//   • All cluster instances: if the source is already present (non-Auto maps),
//     re-resolve CSS vars and push new colors via setPaintProperty so cluster
//     bubbles match the new theme without a full style reload.
function _onThemeToggle() {
    const dark = isDarkTheme();
    if (dark === _lastDark) return; // spurious mutation (class change unrelated to theme)
    _lastDark = dark;

    const newAutoStyle = resolveStyleUrl('Auto');
    for (const inst of instances.values()) {
        try { applyThemeToInstance(inst, dark, newAutoStyle); } catch (_) {}
    }
}

function applyThemeToInstance(inst, dark, newAutoStyle) {
    const { map } = inst;
    if (inst.requestedStyle === 'Auto' || inst.requestedStyle == null || inst.requestedStyle === '') {
        // setStyle triggers 'styledata' → applyClusterMarkers + applyShapes run
        // with fresh getComputedStyle colors. DOM markers use var() refs so they
        // update automatically.
        map.setStyle(newAutoStyle);
    } else if (inst.clusterEnabled && map.getSource('lumeo-markers')) {
        // Non-Auto map: tile does not change, but cluster paint colors should
        // track the theme. Re-resolve and push paint properties directly.
        const clusterColor    = resolveCssVar('--primary',            '#3b82f6');
        const clusterBgColor  = resolveCssVar('--background',         '#ffffff');
        const clusterTextColor = resolveCssVar('--primary-foreground', '#ffffff');
        try { map.setPaintProperty('lumeo-clusters',          'circle-color',        clusterColor);    } catch (_) {}
        try { map.setPaintProperty('lumeo-clusters',          'circle-stroke-color', clusterBgColor);  } catch (_) {}
        try { map.setPaintProperty('lumeo-unclustered-point', 'circle-stroke-color', clusterBgColor);  } catch (_) {}
        try { map.setPaintProperty('lumeo-cluster-count',     'text-color',          clusterTextColor);} catch (_) {}
    }
}

// Start the shared observer if it isn't running yet.
function ensureThemeWatcher() {
    if (_themeObserver) return;
    _lastDark = isDarkTheme();
    _themeObserver = new MutationObserver(_onThemeToggle);
    _themeObserver.observe(document.documentElement, {
        attributes: true,
        attributeFilter: ['class', 'data-theme'],
    });
    _themeMq = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;
    _themeMq?.addEventListener?.('change', _onThemeToggle);
}

// Tear down the shared observer when no map instances are left.
function maybeStopThemeWatcher() {
    if (instances.size > 0) return;
    if (_themeObserver) { _themeObserver.disconnect(); _themeObserver = null; }
    _themeMq?.removeEventListener?.('change', _onThemeToggle);
    _themeMq = null;
    _lastDark = null;
}

// ----------------------------------------------------------------------
// Colors + marker HTML (shared with Leaflet build's visual contract)
// ----------------------------------------------------------------------

const COLOR_MAP = {
    red:    '#ef4444',
    green:  '#22c55e',
    yellow: '#eab308',
    blue:   '#3b82f6',
    purple: '#a855f7',
    pink:   '#ec4899',
    orange: '#f97316',
    teal:   '#14b8a6',
    gray:   '#6b7280',
    slate:  '#64748b',
};

function resolveColor(color) {
    if (!color) return 'hsl(var(--primary, 222 47% 11%))';
    const key = String(color).toLowerCase();
    if (COLOR_MAP[key]) return COLOR_MAP[key];
    return color;
}

// Returns { html, anchor } for a marker variant. `anchor` is the MapLibre
// anchor string — center for circular variants, bottom for the Pin teardrop.
function buildMarkerVariant(variant, color, label, iconHtml, labelHoverOnly) {
    const v = (variant || 'Default');
    const fill = resolveColor(color);

    let innerStyle = '';
    let labelHtml = '';
    let anchor = 'center';

    if (label) {
        const labelClass = labelHoverOnly
            ? 'lumeo-map-marker-label lumeo-map-marker-label--hover'
            : 'lumeo-map-marker-label';
        labelHtml = `<span class="${labelClass}">${escapeHtml(label)}</span>`;
    }

    switch (v) {
        case 'Outline':
            innerStyle = `
                width:32px;height:32px;border-radius:9999px;
                background:hsl(var(--background, 0 0% 100%));
                border:2px solid ${fill};
                color:${fill};
                box-shadow:0 2px 6px rgba(0,0,0,.18);
            `;
            break;
        case 'Dot':
            innerStyle = `
                width:12px;height:12px;border-radius:9999px;
                background:${fill};
                box-shadow:0 0 0 4px ${fill}33, 0 1px 3px rgba(0,0,0,.2);
            `;
            break;
        case 'Pin':
            anchor = 'bottom';
            return {
                // Use style="" not the fill= presentation attribute so that
                // CSS variable references (e.g. hsl(var(--primary,...))) are
                // resolved by the CSS engine and update on theme change.
                html: `${labelHtml}<svg xmlns="http://www.w3.org/2000/svg" width="28" height="40" viewBox="0 0 24 36" stroke-width="1.5" style="stroke:hsl(var(--background, 0 0% 100%))">
                    <path d="M12 0C5.4 0 0 5.4 0 12c0 9 12 24 12 24s12-15 12-24c0-6.6-5.4-12-12-12z" style="fill:${fill}"/>
                    <circle cx="12" cy="12" r="4.5" style="fill:hsl(var(--background, 0 0% 100%))"/>
                </svg>`,
                anchor,
            };
        case 'Default':
        default:
            innerStyle = `
                width:32px;height:32px;border-radius:9999px;
                background:${fill};
                color:hsl(var(--primary-foreground, 0 0% 100%));
                border:2px solid hsl(var(--background, 0 0% 100%));
                box-shadow:0 4px 12px rgba(0,0,0,.22);
            `;
            break;
    }

    return {
        html: `${labelHtml}<div class="lumeo-map-marker-inner" style="${innerStyle.replace(/\s+/g, ' ').trim()}">${iconHtml || ''}</div>`,
        anchor,
    };
}

function escapeHtml(s) {
    return String(s)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

function buildMarkerElement(m) {
    if (m.iconHostId) {
        const host = document.getElementById(m.iconHostId);
        if (host) m.iconHtml = host.innerHTML;
    }
    if (m.iconUrl) {
        const el = document.createElement('div');
        el.className = 'lumeo-map-marker';
        el.style.cssText = `width:32px;height:32px;background:url(${m.iconUrl}) center/contain no-repeat;`;
        return { el, anchor: 'bottom' };
    }
    const parts = buildMarkerVariant(m.variant, m.color, m.label, m.iconHtml, !!m.labelHoverOnly);
    const el = document.createElement('div');
    el.className = 'lumeo-map-marker';
    el.innerHTML = parts.html;
    return { el, anchor: parts.anchor };
}

// ----------------------------------------------------------------------
// Shape → GeoJSON
// ----------------------------------------------------------------------

function shapeToFeature(s) {
    switch (s.type) {
        case 'polyline':
            return {
                kind: 'line',
                feature: {
                    type: 'Feature',
                    geometry: { type: 'LineString', coordinates: s.points.map(p => [p.lon, p.lat]) },
                },
            };
        case 'polygon':
            return {
                kind: 'polygon',
                feature: {
                    type: 'Feature',
                    geometry: { type: 'Polygon', coordinates: [s.points.map(p => [p.lon, p.lat])] },
                },
            };
        case 'rectangle': {
            const b = s.bounds;
            return {
                kind: 'polygon',
                feature: {
                    type: 'Feature',
                    geometry: {
                        type: 'Polygon',
                        coordinates: [[
                            [b.west, b.south], [b.east, b.south],
                            [b.east, b.north], [b.west, b.north],
                            [b.west, b.south],
                        ]],
                    },
                },
            };
        }
        case 'circle': {
            // Approximate the meter-radius circle as a 64-point polygon so it
            // scales correctly with zoom (MapLibre's native `circle` layer
            // takes a pixel radius, which would shrink as the user zooms out).
            const samples = 64;
            const earthRadius = 6378137;
            const latRad = s.center.lat * Math.PI / 180;
            const coords = [];
            for (let i = 0; i <= samples; i++) {
                const bearing = (i / samples) * 2 * Math.PI;
                const dLat = (s.radiusMeters * Math.cos(bearing)) / earthRadius;
                const dLng = (s.radiusMeters * Math.sin(bearing)) / (earthRadius * Math.cos(latRad));
                coords.push([
                    s.center.lon + dLng * 180 / Math.PI,
                    s.center.lat + dLat * 180 / Math.PI,
                ]);
            }
            return {
                kind: 'polygon',
                feature: { type: 'Feature', geometry: { type: 'Polygon', coordinates: [coords] } },
            };
        }
        case 'arc': {
            // Quadratic Bezier in (lng, lat) space, sampled into a LineString.
            const A = [s.from.lon, s.from.lat];
            const B = [s.to.lon, s.to.lat];
            const mid = [(A[0] + B[0]) / 2, (A[1] + B[1]) / 2];
            const dx = B[0] - A[0];
            const dy = B[1] - A[1];
            const len = Math.sqrt(dx * dx + dy * dy);
            const curv = s.curvature ?? 0.25;
            // Perpendicular offset to raise the midpoint into an arc.
            const cx = mid[0] + (-dy / (len || 1)) * len * curv;
            const cy = mid[1] + ( dx / (len || 1)) * len * curv;
            const samples = 48;
            const pts = [];
            for (let i = 0; i <= samples; i++) {
                const t = i / samples;
                const lng = (1 - t) * (1 - t) * A[0] + 2 * (1 - t) * t * cx + t * t * B[0];
                const lat = (1 - t) * (1 - t) * A[1] + 2 * (1 - t) * t * cy + t * t * B[1];
                pts.push([lng, lat]);
            }
            return {
                kind: 'line',
                feature: { type: 'Feature', geometry: { type: 'LineString', coordinates: pts } },
            };
        }
        case 'geojson':
            return { kind: 'geojson', feature: s.geojson };
        case 'heatmap': {
            // Build a GeoJSON FeatureCollection where each point carries an
            // `intensity` property for the MapLibre heatmap weight expression.
            const features = (s.points || []).map(p => ({
                type: 'Feature',
                properties: { intensity: p.weight != null ? p.weight : 1 },
                geometry: { type: 'Point', coordinates: [p.lon, p.lat] },
            }));
            return {
                kind: 'heatmap',
                feature: { type: 'FeatureCollection', features },
                radius: s.radius ?? 20,
                opacity: s.opacity ?? 0.8,
                colorRamp: s.colorRamp ?? null,
            };
        }
        default:
            return null;
    }
}

// Convert the dash array string ("5 6") to MapLibre's numeric array, where
// values are multiples of the line width.
function parseDashArray(da, weight) {
    if (!da) return null;
    const parts = String(da).split(/[\s,]+/).map(parseFloat).filter(n => !isNaN(n));
    if (!parts.length) return null;
    const w = weight || 1;
    return parts.map(n => n / w);
}

// ----------------------------------------------------------------------
// Custom search control — Nominatim, debounced. Same UX as the Leaflet build.
// ----------------------------------------------------------------------

const ICON_SEARCH = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg>`;

class LumeoSearchControl {
    constructor(map) { this._map = map; }
    onAdd() {
        const container = document.createElement('div');
        container.className = 'maplibregl-ctrl lumeo-map-search';
        container.innerHTML = `
            <div class="lumeo-map-search-input-wrap">
                ${ICON_SEARCH}
                <input type="search" placeholder="Search places…" aria-label="Search places" />
            </div>
            <div class="lumeo-map-search-results" hidden></div>
        `;
        const input = container.querySelector('input');
        const results = container.querySelector('.lumeo-map-search-results');
        let debounceTimer = null;
        let lastQuery = '';
        const map = this._map;

        async function doSearch(q) {
            const url = `https://nominatim.openstreetmap.org/search?format=json&limit=5&q=${encodeURIComponent(q)}`;
            try {
                const res = await fetch(url, { headers: { 'Accept-Language': navigator.language || 'en' } });
                if (!res.ok) throw new Error('nominatim http ' + res.status);
                const data = await res.json();
                renderResults(data);
            } catch {
                renderResults([]);
            }
        }
        function renderResults(data) {
            results.innerHTML = '';
            if (!data.length) {
                results.innerHTML = '<div class="lumeo-map-search-empty">No results</div>';
                results.hidden = false;
                return;
            }
            data.forEach((r) => {
                const btn = document.createElement('button');
                btn.type = 'button';
                btn.textContent = r.display_name;
                btn.addEventListener('click', () => {
                    map.flyTo({ center: [parseFloat(r.lon), parseFloat(r.lat)], zoom: Math.max(map.getZoom(), 13) });
                    input.value = r.display_name.split(',')[0];
                    results.hidden = true;
                });
                results.appendChild(btn);
            });
            results.hidden = false;
        }
        input.addEventListener('input', () => {
            const q = input.value.trim();
            if (q === lastQuery) return;
            lastQuery = q;
            if (debounceTimer) clearTimeout(debounceTimer);
            if (q.length < 3) { results.hidden = true; return; }
            debounceTimer = setTimeout(() => doSearch(q), 400);
        });
        input.addEventListener('blur', () => { setTimeout(() => { results.hidden = true; }, 150); });
        input.addEventListener('focus', () => { if (results.children.length) results.hidden = false; });
        container.addEventListener('click', (e) => e.stopPropagation());
        this._container = container;
        return container;
    }
    onRemove() { this._container?.parentNode?.removeChild(this._container); this._map = null; }
}

// Layer-switcher control — chip-style, mirrors the previous Leaflet UX.
class LumeoLayerSwitcher {
    constructor(layers, currentName, onChange) {
        this._layers = layers;
        this._currentName = currentName;
        this._onChange = onChange;
    }
    onAdd() {
        const container = document.createElement('div');
        container.className = 'maplibregl-ctrl lumeo-map-layers';
        this._layers.forEach((entry) => {
            const name = typeof entry === 'string' ? entry : entry.name;
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.textContent = name;
            if (name === this._currentName) btn.setAttribute('data-active', 'true');
            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                container.querySelectorAll('button').forEach(b => b.removeAttribute('data-active'));
                btn.setAttribute('data-active', 'true');
                this._currentName = name;
                this._onChange(entry);
            });
            container.appendChild(btn);
        });
        this._container = container;
        return container;
    }
    onRemove() { this._container?.parentNode?.removeChild(this._container); }
}

// ----------------------------------------------------------------------
// Public API
// ----------------------------------------------------------------------

export async function init(elementId, options, dotNetRef) {
    ensureLumeoMapCss();
    const maplibregl = await loadMapLibre();
    const el = document.getElementById(elementId);
    if (!el) return;

    if (instances.has(elementId)) {
        try { instances.get(elementId).map.remove(); } catch (_) {}
        instances.delete(elementId);
    }

    const mapOptions = {
        container: el,
        style: resolveStyleUrl(options.tileLayer),
        center: toLngLat(options.lat, options.lon),
        zoom: options.zoom,
        // Compact attribution — collapsed to a small (i) chip in the bottom-right,
        // expands to a pill on click. Custom CSS in map.css theme the
        // <details>/<summary> element.
        attributionControl: options.attribution !== false ? { compact: true } : false,
        // Required so getCanvas().toDataURL() returns a non-empty image —
        // WebGL clears the back buffer after each frame by default, which
        // causes PNG export to produce a black canvas. Small perf cost.
        preserveDrawingBuffer: true,
    };
    // Globe projection — supported in MapLibre GL ≥ 3.0.
    // We set it after creation via setProjection so the map still initialises
    // even if an older CDN bundle is loaded (safe no-op degradation).
    const map = new maplibregl.Map(mapOptions);

    // Force the compact attribution to start CLOSED (just the (i) chip).
    // MapLibre v5.7.1's compact=true initialises the <details> with the open
    // attribute set, which means the attribution pill is fully expanded on
    // first paint. We want the collapsed (i)-only state by default — users
    // click to reveal — so we strip `open` and the compact-show class on the
    // first `load` event (and again on `idle` as a safety net for race
    // conditions where MapLibre re-applies the class).
    const closeAttribOnce = () => {
        try {
            const details = el.querySelector('.maplibregl-ctrl-attrib.maplibregl-compact');
            if (!details) return;
            details.removeAttribute('open');
            details.classList.remove('maplibregl-compact-show');
        } catch { /* ignore */ }
    };
    map.once('load', closeAttribOnce);
    map.once('idle', closeAttribOnce);

    // Globe projection — set after the style loads. With MapLibre 5+ this is
    // a stable API; we still wrap defensively because older bundles may be
    // cached and we don't want a console-level failure to break init.
    if (options.projection && options.projection !== 'mercator') {
        const applyProjection = () => {
            try {
                map.setProjection({ type: options.projection });
            } catch (err) {
                console.warn('Lumeo.Maps: setProjection failed', err);
            }
        };
        if (map.isStyleLoaded()) {
            applyProjection();
        } else {
            map.once('style.load', applyProjection);
        }
    }

    // Built-in chrome — MapLibre handles zoom +/- via NavigationControl.
    if (options.zoomControl !== false) {
        map.addControl(new maplibregl.NavigationControl({ showCompass: false, visualizePitch: false }), 'top-right');
    }
    if (options.scrollWheelZoom === false) {
        map.scrollZoom.disable();
    }

    const controls = [];
    if (options.geolocate) {
        const c = new maplibregl.GeolocateControl({
            positionOptions: { enableHighAccuracy: false, timeout: 6000, maximumAge: 60000 },
            trackUserLocation: false,
        });
        map.addControl(c, 'top-right');
        controls.push(c);
    }
    if (options.fullscreen) {
        const c = new maplibregl.FullscreenControl();
        map.addControl(c, 'top-right');
        controls.push(c);
    }
    if (options.scale) {
        const c = new maplibregl.ScaleControl({ maxWidth: 120, unit: 'metric' });
        map.addControl(c, 'bottom-left');
        controls.push(c);
    }
    if (options.search) {
        const c = new LumeoSearchControl(map);
        map.addControl(c, 'top-left');
        controls.push(c);
    }

    let layerSwitcher = null;
    if (Array.isArray(options.layers) && options.layers.length > 0) {
        let currentName = options.tileLayer || 'Auto';
        layerSwitcher = new LumeoLayerSwitcher(options.layers, currentName, (entry) => {
            const name = typeof entry === 'string' ? entry : entry.name;
            const url = (typeof entry === 'string') ? resolveStyleUrl(name) : (entry.url || resolveStyleUrl(name));
            map.setStyle(url);
        });
        map.addControl(layerSwitcher, 'top-right');
        controls.push(layerSwitcher);
    }

    map.on('click', (e) => {
        try { dotNetRef.invokeMethodAsync('OnMapClick', e.lngLat.lat, e.lngLat.lng); } catch (_) {}
    });
    const fireViewChanged = () => {
        const c = map.getCenter();
        try { dotNetRef.invokeMethodAsync('OnViewChanged', c.lat, c.lng, Math.round(map.getZoom())); } catch (_) {}
    };
    map.on('moveend', fireViewChanged);
    map.on('zoomend', fireViewChanged);

    const inst = {
        maplibregl, map, dotNetRef,
        markers: [],          // [{ marker, popup, id, hasClick, listener }]
        markerSpecs: [],      // last spec list (so we can re-render after a style swap in cluster mode)
        shapeSpecs: [],       // last shape spec list (re-applied after style swap)
        standalonePopups: [], // [{ maplibrePopup }] — programmatic popups not tied to markers
        clusterEnabled: !!options.cluster,
        autoFitBounds: !!options.autoFitBounds,
        controls,
        requestedStyle: options.tileLayer,
        animatingShapes: new Set(), // track active animation timers
    };

    // After a setStyle() call, MapLibre wipes user-added sources/layers.
    // Re-apply our cluster source and shape layers when the new style finishes
    // loading. DOM markers (non-cluster mode) survive automatically.
    map.on('styledata', () => {
        if (!map.isStyleLoaded()) return;
        if (inst.clusterEnabled) applyClusterMarkers(inst);
        applyShapes(inst);
    });

    instances.set(elementId, inst);
    ensureThemeWatcher();
}

// ----------------------------------------------------------------------
// Markers
// ----------------------------------------------------------------------

function clearDomMarkers(inst) {
    for (const m of inst.markers) {
        if (m.listener) m.marker.getElement().removeEventListener('click', m.listener);
        m.marker.remove();
    }
    inst.markers = [];
}

function applyDomMarkers(inst) {
    const { maplibregl, map, dotNetRef, markerSpecs } = inst;
    clearDomMarkers(inst);

    for (const m of markerSpecs) {
        const { el, anchor } = buildMarkerElement(m);
        const marker = new maplibregl.Marker({ element: el, anchor, draggable: !!m.draggable })
            .setLngLat(toLngLat(m.lat, m.lon))
            .addTo(map);

        if (m.title) el.setAttribute('title', m.title);

        let popup = null;
        let popupHtml = m.popupHtml;
        if (m.popupHostId) {
            const host = document.getElementById(m.popupHostId);
            if (host) popupHtml = host.innerHTML;
        }
        if (popupHtml) {
            popup = new maplibregl.Popup({ offset: 24, closeButton: true, closeOnClick: true }).setHTML(popupHtml);
            marker.setPopup(popup);
        }

        let listener = null;
        if (m.hasClick) {
            const id = m.id;
            listener = (e) => {
                e.stopPropagation();
                try { dotNetRef.invokeMethodAsync('OnMarkerClick', id); } catch (_) {}
            };
            el.addEventListener('click', listener);
        }

        if (m.draggable && m.hasDragEnd) {
            const id = m.id;
            marker.on('dragend', () => {
                const pos = marker.getLngLat();
                try { dotNetRef.invokeMethodAsync('OnMarkerDragEnd', id, pos.lat, pos.lng); } catch (_) {}
            });
        }

        inst.markers.push({ marker, popup, id: m.id, hasClick: m.hasClick, listener });
    }
}

// Resolve a CSS variable name to its current computed value as a color string.
// MapLibre GL JS paint properties cannot use CSS `hsl(var(--x))` syntax —
// they require actual color values resolved at the time the layer is added.
function resolveCssVar(varName, fallback) {
    try {
        const raw = getComputedStyle(document.documentElement)
            .getPropertyValue(varName)
            .trim();
        if (!raw) return fallback;
        // CSS variables for HSL channels are stored as "222 47% 11%" — wrap them.
        // If the value already starts with '#' or 'rgb' it's a full color; return as-is.
        if (raw.startsWith('#') || raw.startsWith('rgb') || raw.startsWith('hsl(')) return raw;
        return `hsl(${raw})`;
    } catch (_) {
        return fallback;
    }
}

function applyClusterMarkers(inst) {
    const { map, markerSpecs } = inst;
    const sourceId = 'lumeo-markers';

    // Build a FeatureCollection from the marker specs — each point carries
    // the original spec id so click handlers can fire OnMarkerClick.
    const features = markerSpecs.map(m => ({
        type: 'Feature',
        properties: { id: m.id, hasClick: !!m.hasClick, color: resolveColor(m.color) },
        geometry: { type: 'Point', coordinates: toLngLat(m.lat, m.lon) },
    }));

    if (map.getSource(sourceId)) {
        map.getSource(sourceId).setData({ type: 'FeatureCollection', features });
        return;
    }

    // Resolve CSS variables to actual color values — MapLibre GL JS paint
    // properties do not support `hsl(var(--x))` CSS syntax.
    const clusterColor = resolveCssVar('--primary', '#3b82f6');
    const clusterBgColor = resolveCssVar('--background', '#ffffff');
    const clusterTextColor = resolveCssVar('--primary-foreground', '#ffffff');

    map.addSource(sourceId, {
        type: 'geojson',
        data: { type: 'FeatureCollection', features },
        cluster: true,
        clusterMaxZoom: 14,
        clusterRadius: 50,
    });

    map.addLayer({
        id: 'lumeo-clusters',
        type: 'circle',
        source: sourceId,
        filter: ['has', 'point_count'],
        paint: {
            'circle-color': clusterColor,
            'circle-stroke-color': clusterBgColor,
            'circle-stroke-width': 2,
            'circle-radius': ['step', ['get', 'point_count'], 18, 25, 24, 100, 30],
        },
    });
    map.addLayer({
        id: 'lumeo-cluster-count',
        type: 'symbol',
        source: sourceId,
        filter: ['has', 'point_count'],
        layout: {
            'text-field': '{point_count_abbreviated}',
            'text-size': 12,
            'text-font': ['Open Sans Semibold', 'Arial Unicode MS Bold'],
        },
        paint: { 'text-color': clusterTextColor },
    });
    map.addLayer({
        id: 'lumeo-unclustered-point',
        type: 'circle',
        source: sourceId,
        filter: ['!', ['has', 'point_count']],
        paint: {
            'circle-color': ['get', 'color'],
            'circle-radius': 8,
            'circle-stroke-color': clusterBgColor,
            'circle-stroke-width': 2,
        },
    });

    // Click handlers — clusters zoom in; individual points fire OnMarkerClick.
    // Bind to BOTH the circle layer AND the count-text layer — otherwise a click
    // landing on the number label is swallowed by the symbol layer and never
    // reaches the circle handler. Also query both layers when looking up the
    // feature, so we always find the cluster_id property.
    const handleClusterClick = async (e) => {
        const features = map.queryRenderedFeatures(e.point, { layers: ['lumeo-clusters', 'lumeo-cluster-count'] });
        const f = features.find(ft => ft.properties && ft.properties.cluster_id != null);
        if (!f) return;
        const clusterId = f.properties.cluster_id;
        const source = map.getSource(sourceId);
        try {
            const zoom = await source.getClusterExpansionZoom(clusterId);
            map.easeTo({ center: f.geometry.coordinates, zoom, duration: 500 });
        } catch (_) {}
    };
    map.on('click', 'lumeo-clusters', handleClusterClick);
    map.on('click', 'lumeo-cluster-count', handleClusterClick);
    map.on('click', 'lumeo-unclustered-point', (e) => {
        const feature = e.features?.[0];
        if (!feature || !feature.properties.hasClick) return;
        try { inst.dotNetRef.invokeMethodAsync('OnMarkerClick', feature.properties.id); } catch (_) {}
    });
    const setCursor = (cur) => () => { map.getCanvas().style.cursor = cur; };
    map.on('mouseenter', 'lumeo-clusters', setCursor('pointer'));
    map.on('mouseleave', 'lumeo-clusters', setCursor(''));
    map.on('mouseenter', 'lumeo-cluster-count', setCursor('pointer'));
    map.on('mouseleave', 'lumeo-cluster-count', setCursor(''));
    map.on('mouseenter', 'lumeo-unclustered-point', setCursor('pointer'));
    map.on('mouseleave', 'lumeo-unclustered-point', setCursor(''));
}

export async function setMarkers(elementId, markers) {
    const inst = instances.get(elementId);
    if (!inst) return;
    inst.markerSpecs = markers || [];

    if (inst.clusterEnabled) {
        // Wait for the style to be ready before adding the cluster source.
        if (inst.map.isStyleLoaded()) applyClusterMarkers(inst);
        else inst.map.once('load', () => applyClusterMarkers(inst));
    } else {
        applyDomMarkers(inst);
    }
    maybeAutoFitBounds(inst);
}

// ----------------------------------------------------------------------
// Shapes — polylines, polygons, circles, arcs, raw GeoJSON.
// ----------------------------------------------------------------------

function clearShapes(inst) {
    const { map } = inst;
    // Iterate by ids we own (everything prefixed `lumeo-shape-`).
    // Layers must be removed before their source.
    const style = map.getStyle();
    if (!style || !style.layers) return;
    style.layers
        .filter(l => l.id.startsWith('lumeo-shape-'))
        .forEach(l => { try { map.removeLayer(l.id); } catch (_) {} });
    for (const sid of Object.keys(style.sources || {})) {
        if (sid.startsWith('lumeo-shape-')) {
            try { map.removeSource(sid); } catch (_) {}
        }
    }
}

function applyShapes(inst) {
    const { map, shapeSpecs } = inst;
    if (!map.isStyleLoaded()) return;
    clearShapes(inst);

    shapeSpecs.forEach((s, idx) => {
        const built = shapeToFeature(s);
        if (!built) return;
        const sourceId = `lumeo-shape-${idx}`;
        const color = resolveColor(s.color);
        const fillColor = resolveColor(s.fillColor || s.color);
        const weight = s.weight ?? 2;
        const opacity = s.opacity ?? 0.9;
        const fillOpacity = s.fillOpacity ?? 0.18;
        const dash = parseDashArray(s.dashArray, weight);

        const hoverColor = s.hoverColor ? resolveColor(s.hoverColor) : null;
        const hoverWeight = s.hoverWeight ?? null;

        // Helper: add hover state to a line layer (feature-state based).
        const attachHoverToLine = (layerId, srcId) => {
            if (!hoverColor && !hoverWeight) return;
            let hoverFeatureId = null;
            const hoverIn = (e) => {
                if (e.features && e.features.length > 0) {
                    if (hoverFeatureId !== null) {
                        try { map.setFeatureState({ source: srcId, id: hoverFeatureId }, { hover: false }); } catch(_) {}
                    }
                    hoverFeatureId = e.features[0].id;
                    try { map.setFeatureState({ source: srcId, id: hoverFeatureId }, { hover: true }); } catch(_) {}
                }
                map.getCanvas().style.cursor = 'pointer';
            };
            const hoverOut = () => {
                if (hoverFeatureId !== null) {
                    try { map.setFeatureState({ source: srcId, id: hoverFeatureId }, { hover: false }); } catch(_) {}
                    hoverFeatureId = null;
                }
                map.getCanvas().style.cursor = '';
            };
            map.on('mouseenter', layerId, hoverIn);
            map.on('mouseleave', layerId, hoverOut);
        };

        // Helper: animate line drawing (draws from start to end over ~1s).
        const animateLine = (layerId, srcId, coords) => {
            if (!s.animate || !coords || coords.length < 2) return;
            const total = coords.length;
            let current = 1;
            const step = () => {
                current = Math.min(current + Math.max(1, Math.floor(total / 60)), total);
                const partial = { type: 'Feature', geometry: { type: 'LineString', coordinates: coords.slice(0, current) } };
                try { map.getSource(srcId)?.setData(partial); } catch(_) {}
                if (current < total) requestAnimationFrame(step);
            };
            requestAnimationFrame(step);
        };

        if (built.kind === 'line') {
            const coords = built.feature.geometry.coordinates;
            const startFeature = s.animate
                ? { type: 'Feature', geometry: { type: 'LineString', coordinates: [coords[0], coords[0]] } }
                : built.feature;
            map.addSource(sourceId, { type: 'geojson', data: startFeature, generateId: true });
            const linePaint = {
                'line-color': hoverColor
                    ? ['case', ['boolean', ['feature-state', 'hover'], false], hoverColor, color]
                    : color,
                'line-width': hoverWeight
                    ? ['case', ['boolean', ['feature-state', 'hover'], false], hoverWeight, weight]
                    : weight,
                'line-opacity': opacity,
                ...(dash ? { 'line-dasharray': dash } : {}),
            };
            map.addLayer({
                id: `${sourceId}-line`,
                type: 'line',
                source: sourceId,
                layout: { 'line-cap': 'round', 'line-join': 'round' },
                paint: linePaint,
            });
            attachHoverToLine(`${sourceId}-line`, sourceId);
            if (s.animate) animateLine(`${sourceId}-line`, sourceId, coords);
        } else if (built.kind === 'polygon') {
            map.addSource(sourceId, { type: 'geojson', data: built.feature });
            map.addLayer({
                id: `${sourceId}-fill`,
                type: 'fill',
                source: sourceId,
                paint: { 'fill-color': fillColor, 'fill-opacity': fillOpacity },
            });
            map.addLayer({
                id: `${sourceId}-line`,
                type: 'line',
                source: sourceId,
                layout: { 'line-cap': 'round', 'line-join': 'round' },
                paint: {
                    'line-color': color,
                    'line-width': weight,
                    'line-opacity': opacity,
                    ...(dash ? { 'line-dasharray': dash } : {}),
                },
            });
        } else if (built.kind === 'geojson') {
            map.addSource(sourceId, { type: 'geojson', data: built.feature });
            map.addLayer({
                id: `${sourceId}-fill`,
                type: 'fill',
                source: sourceId,
                paint: { 'fill-color': fillColor, 'fill-opacity': fillOpacity },
            });
            map.addLayer({
                id: `${sourceId}-line`,
                type: 'line',
                source: sourceId,
                paint: { 'line-color': color, 'line-width': weight, 'line-opacity': opacity },
            });
        } else if (built.kind === 'heatmap') {
            map.addSource(sourceId, { type: 'geojson', data: built.feature });
            // Default color ramp: transparent → blue → cyan → yellow → orange → red
            const defaultRamp = [
                0,      'rgba(0,0,255,0)',
                0.2,    'rgba(65,105,225,0.7)',
                0.4,    'rgba(0,200,200,0.8)',
                0.6,    'rgba(100,200,0,0.9)',
                0.8,    'rgba(255,165,0,1)',
                1,      'rgba(255,30,0,1)',
            ];
            const rampStops = built.colorRamp && built.colorRamp.length >= 4
                ? built.colorRamp
                : defaultRamp;
            // Build a MapLibre interpolate expression from the flat stop/color array.
            const colorExpr = ['interpolate', ['linear'], ['heatmap-density']];
            for (let i = 0; i < rampStops.length; i += 2) {
                colorExpr.push(rampStops[i], rampStops[i + 1]);
            }
            map.addLayer({
                id: `${sourceId}-heatmap`,
                type: 'heatmap',
                source: sourceId,
                paint: {
                    'heatmap-weight': ['interpolate', ['linear'], ['get', 'intensity'], 0, 0, 1, 1],
                    'heatmap-intensity': ['interpolate', ['linear'], ['zoom'], 0, 1, 18, 3],
                    'heatmap-radius': ['interpolate', ['linear'], ['zoom'], 0, built.radius / 2, 18, built.radius * 4],
                    'heatmap-opacity': built.opacity,
                    'heatmap-color': colorExpr,
                },
            });
        }

        // Popup / tooltip on shapes — bind via click/mouseenter on the line
        // (and fill, where present) layer.
        const targets = built.kind === 'line'
            ? [`${sourceId}-line`]
            : [`${sourceId}-fill`, `${sourceId}-line`];

        if (s.popupHtml) {
            targets.forEach(layerId => {
                map.on('click', layerId, (e) => {
                    new inst.maplibregl.Popup({ closeButton: true, closeOnClick: true })
                        .setLngLat(e.lngLat)
                        .setHTML(s.popupHtml)
                        .addTo(map);
                });
                map.on('mouseenter', layerId, () => { map.getCanvas().style.cursor = 'pointer'; });
                map.on('mouseleave', layerId, () => { map.getCanvas().style.cursor = ''; });
            });
        }
        if (s.tooltip) {
            let tip = null;
            targets.forEach(layerId => {
                map.on('mouseenter', layerId, (e) => {
                    tip = new inst.maplibregl.Popup({ closeButton: false, closeOnClick: false, offset: 8 })
                        .setLngLat(e.lngLat)
                        .setText(s.tooltip)
                        .addTo(map);
                });
                map.on('mousemove', layerId, (e) => { if (tip) tip.setLngLat(e.lngLat); });
                map.on('mouseleave', layerId, () => { tip?.remove(); tip = null; });
            });
        }
    });
}

export async function setShapes(elementId, shapes) {
    const inst = instances.get(elementId);
    if (!inst) return;
    inst.shapeSpecs = shapes || [];
    if (inst.map.isStyleLoaded()) applyShapes(inst);
    else inst.map.once('load', () => applyShapes(inst));
}

// ----------------------------------------------------------------------
// Standalone popups (MapPopup component)
// ----------------------------------------------------------------------

function clearStandalonePopups(inst) {
    for (const p of (inst.standalonePopups || [])) {
        // Suppress the close-callback for programmatic removal — only a USER
        // dismiss (× button / map-background click) should notify C#. Without
        // this, re-syncing popups would fire OnPopupClosed and desync IsOpen.
        p._lumeoSuppressClose = true;
        try { p.remove(); } catch (_) {}
    }
    inst.standalonePopups = [];
}

export async function setStandalonePopups(elementId, popups) {
    const inst = instances.get(elementId);
    if (!inst) return;
    clearStandalonePopups(inst);
    for (const p of (popups || [])) {
        if (!p.isOpen) continue;
        let html = p.html;
        if (!html && p.hostId) {
            const host = document.getElementById(p.hostId);
            if (host) html = host.innerHTML;
        }
        if (!html) continue;
        const popup = new inst.maplibregl.Popup({
            closeButton: p.closeButton !== false,
            closeOnClick: p.closeOnClick !== false,
        })
            .setLngLat(toLngLat(p.lat, p.lon))
            .setHTML(html)
            .addTo(inst.map);
        // Wire MapLibre's 'close' event (× button or map-background click) back to
        // the owning MapPopup so its two-way IsOpen binding updates. The ref is
        // marshalled per-popup from C#.
        if (p.dotNetRef) {
            popup.on('close', () => {
                if (popup._lumeoSuppressClose) return;
                try { p.dotNetRef.invokeMethodAsync('OnPopupClosed'); } catch (_) {}
            });
        }
        inst.standalonePopups.push(popup);
    }
}

// ----------------------------------------------------------------------
// Auto-fit bounds helper
// ----------------------------------------------------------------------

function computeBoundsFromMarkers(markerSpecs) {
    if (!markerSpecs || !markerSpecs.length) return null;
    let south = Infinity, west = Infinity, north = -Infinity, east = -Infinity;
    for (const m of markerSpecs) {
        south = Math.min(south, m.lat);
        north = Math.max(north, m.lat);
        west  = Math.min(west,  m.lon);
        east  = Math.max(east,  m.lon);
    }
    if (south === north && west === east) return null; // single point — skip
    return { south, west, north, east };
}

function maybeAutoFitBounds(inst) {
    if (!inst.autoFitBounds) return;
    const b = computeBoundsFromMarkers(inst.markerSpecs);
    if (!b) return;
    try {
        inst.map.fitBounds([[b.west, b.south], [b.east, b.north]], { padding: 50, maxZoom: 14 });
    } catch (_) {}
}

// ----------------------------------------------------------------------
// Imperative view control
// ----------------------------------------------------------------------

export async function setCenter(elementId, lat, lon, zoom) {
    const inst = instances.get(elementId);
    if (!inst) return;
    inst.map.easeTo({ center: toLngLat(lat, lon), zoom });
}

export async function fitBounds(elementId, south, west, north, east, paddingPx) {
    const inst = instances.get(elementId);
    if (!inst) return;
    const pad = paddingPx ?? 30;
    inst.map.fitBounds([[west, south], [east, north]], { padding: pad });
}

export async function exportPng(elementId) {
    const inst = instances.get(elementId);
    if (!inst) return null;
    try {
        const glCanvas = inst.map.getCanvas();
        const mapEl    = inst.map.getContainer();

        // If there are no DOM markers, return the GL canvas directly — fast path.
        if (!inst.markers || inst.markers.length === 0) {
            return glCanvas.toDataURL('image/png');
        }

        // Composite: draw the GL canvas onto a new canvas, then paint each
        // DOM marker's visual centre at the projected pixel position.
        const w = glCanvas.width;
        const h = glCanvas.height;
        const dpr = window.devicePixelRatio || 1;
        const mapRect = mapEl.getBoundingClientRect();

        const out = document.createElement('canvas');
        out.width  = w;
        out.height = h;
        const ctx = out.getContext('2d');

        // Layer 1: the GL tiles + vector layers.
        ctx.drawImage(glCanvas, 0, 0);

        // Layer 2: one shape per DOM marker, drawn at the projected pixel position.
        for (const entry of inst.markers) {
            const spec = inst.markerSpecs.find(s => s.id === entry.id);
            if (!spec) continue;

            // Project geographic coordinates to pixel offset within the map container.
            const pt = inst.map.project([spec.lon, spec.lat]);
            // pt is in CSS pixels; scale to physical canvas pixels.
            const px = pt.x * dpr;
            const py = pt.y * dpr;

            const fill   = resolveColor(spec.color);
            const v      = spec.variant || 'Default';
            const radius = 8 * dpr;

            ctx.save();
            ctx.shadowColor   = 'rgba(0,0,0,0.25)';
            ctx.shadowBlur    = 6 * dpr;
            ctx.shadowOffsetY = 2 * dpr;

            switch (v) {
                case 'Dot': {
                    ctx.beginPath();
                    ctx.arc(px, py, 6 * dpr, 0, 2 * Math.PI);
                    ctx.fillStyle = fill;
                    ctx.fill();
                    break;
                }
                case 'Outline': {
                    ctx.beginPath();
                    ctx.arc(px, py, radius, 0, 2 * Math.PI);
                    ctx.fillStyle = '#ffffff';
                    ctx.fill();
                    ctx.strokeStyle = fill;
                    ctx.lineWidth   = 2 * dpr;
                    ctx.stroke();
                    break;
                }
                case 'Pin': {
                    // Replicate the exact SVG used in buildMarkerVariant:
                    //   width="28" height="40" viewBox="0 0 24 36"
                    //   path d="M12 0C5.4 0 0 5.4 0 12c0 9 12 24 12 24s12-15 12-24c0-6.6-5.4-12-12-12z"
                    //   circle cx="12" cy="12" r="4.5"
                    // Anchor is 'bottom': the SVG tip is at viewBox (12, 36) →
                    // CSS px (14, 40). So the draw origin top-left in CSS px is:
                    //   left = px - 14,  top = py - 40
                    // Scale to physical pixels via dpr.
                    const svgW  = 28 * dpr;   // physical width
                    const svgH  = 40 * dpr;   // physical height
                    const vbW   = 24;          // viewBox width
                    const vbH   = 36;          // viewBox height
                    const scaleX = svgW / vbW;
                    const scaleY = svgH / vbH;
                    // Top-left corner of the pin in physical canvas pixels.
                    // py is the bottom tip (anchor=bottom), so top = py - svgH.
                    // px is the horizontal center; tip is at viewBox x=12 → px offset = 12*scaleX = 14*dpr.
                    const ox = px - 12 * scaleX;
                    const oy = py - svgH;

                    ctx.save();
                    ctx.translate(ox, oy);
                    ctx.scale(scaleX, scaleY);

                    // Body path (matches SVG d attribute exactly)
                    const bodyPath = new Path2D('M12 0C5.4 0 0 5.4 0 12c0 9 12 24 12 24s12-15 12-24c0-6.6-5.4-12-12-12z');
                    ctx.fillStyle = fill;
                    ctx.fill(bodyPath);
                    // White outline — matches the SVG's stroke-width="1.5" + stroke=background.
                    // Without this the exported pin looks softer than the live one.
                    ctx.lineWidth = 1.5;
                    ctx.strokeStyle = resolveCssVar('--background', '#ffffff');
                    ctx.stroke(bodyPath);

                    // Inner white dot: cx=12 cy=12 r=4.5
                    ctx.shadowColor = 'transparent';
                    ctx.beginPath();
                    ctx.arc(12, 12, 4.5, 0, 2 * Math.PI);
                    ctx.fillStyle = resolveCssVar('--background', '#ffffff');
                    ctx.fill();

                    ctx.restore();
                    break;
                }
                default: { // Default — filled circle with white border
                    ctx.beginPath();
                    ctx.arc(px, py, radius, 0, 2 * Math.PI);
                    ctx.fillStyle = fill;
                    ctx.fill();
                    ctx.shadowColor = 'transparent';
                    ctx.strokeStyle = '#ffffff';
                    ctx.lineWidth   = 2 * dpr;
                    ctx.stroke();
                    break;
                }
            }

            ctx.restore();
        }

        return out.toDataURL('image/png');
    } catch (_) { return null; }
}

export async function destroy(elementId) {
    const inst = instances.get(elementId);
    if (!inst) return;
    try {
        clearDomMarkers(inst);
        inst.map.remove();
    } catch (_) {}
    instances.delete(elementId);
    maybeStopThemeWatcher();
}
