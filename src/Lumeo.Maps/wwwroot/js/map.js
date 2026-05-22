// Lumeo.Maps — Leaflet wrapper module.
//
// Leaflet (+ leaflet.markercluster when Cluster=true) and its stylesheets
// are loaded from CDN on first use, then cached on the window. Tile-layer
// presets mirror the well-known providers; consumers can also pass a raw
// URL template ("https://your.tiles/{z}/{x}/{y}.png") or a list of
// switchable layers for the built-in layer switcher control.
//
// We also inject our own `map.css` (shadcn-style chrome on top of Leaflet's
// defaults). It's idempotent: one <link data-lumeo-map> per document.

const LEAFLET_JS = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.js';
const LEAFLET_CSS = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.css';
const MARKERCLUSTER_JS = 'https://unpkg.com/leaflet.markercluster@1.5.3/dist/leaflet.markercluster.js';
const MARKERCLUSTER_CSS = 'https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.css';
const LUMEO_MAP_CSS = '_content/Lumeo.Maps/css/map.css';

const TILE_PRESETS = {
    OpenStreetMap: {
        url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        maxZoom: 19,
    },
    CartoLight: {
        url: 'https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}.png',
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> &copy; <a href="https://carto.com/attributions">CARTO</a>',
        maxZoom: 20,
    },
    CartoDark: {
        url: 'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}.png',
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> &copy; <a href="https://carto.com/attributions">CARTO</a>',
        maxZoom: 20,
    },
    Satellite: {
        url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
        attribution: 'Tiles &copy; Esri &mdash; Source: Esri, Maxar, Earthstar Geographics, and the GIS User Community',
        maxZoom: 19,
    },
    Terrain: {
        url: 'https://tiles.stadiamaps.com/tiles/stamen_terrain/{z}/{x}/{y}.png',
        attribution: '&copy; <a href="https://stadiamaps.com/">Stadia Maps</a>, &copy; <a href="https://stamen.com/">Stamen Design</a>, &copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        maxZoom: 18,
    },
};

const instances = new Map();
let leafletLoadPromise = null;
let clusterLoadPromise = null;

function ensureLumeoMapCss() {
    if (document.querySelector('link[data-lumeo-map]')) return;
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = LUMEO_MAP_CSS;
    link.setAttribute('data-lumeo-map', '');
    document.head.appendChild(link);
}

function loadLeaflet() {
    if (typeof window !== 'undefined' && window.L) return Promise.resolve(window.L);
    if (leafletLoadPromise) return leafletLoadPromise;

    leafletLoadPromise = new Promise((resolve, reject) => {
        if (!document.querySelector('link[data-lumeo-leaflet]')) {
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = LEAFLET_CSS;
            link.setAttribute('data-lumeo-leaflet', '');
            document.head.appendChild(link);
        }

        if (window.L) { resolve(window.L); return; }

        const existing = document.querySelector('script[data-lumeo-leaflet]');
        if (existing) {
            existing.addEventListener('load', () => resolve(window.L));
            existing.addEventListener('error', () => reject(new Error('Failed to load Leaflet')));
            return;
        }

        const script = document.createElement('script');
        script.src = LEAFLET_JS;
        script.async = true;
        script.setAttribute('data-lumeo-leaflet', '');
        script.onload = () => resolve(window.L);
        script.onerror = () => reject(new Error('Failed to load Leaflet'));
        document.head.appendChild(script);
    });
    return leafletLoadPromise;
}

function loadMarkerCluster(L) {
    if (L && L.markerClusterGroup) return Promise.resolve();
    if (clusterLoadPromise) return clusterLoadPromise;

    clusterLoadPromise = new Promise((resolve, reject) => {
        if (!document.querySelector('link[data-lumeo-markercluster]')) {
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = MARKERCLUSTER_CSS;
            link.setAttribute('data-lumeo-markercluster', '');
            document.head.appendChild(link);
        }
        const existing = document.querySelector('script[data-lumeo-markercluster]');
        if (existing) {
            existing.addEventListener('load', resolve);
            existing.addEventListener('error', () => reject(new Error('Failed to load leaflet.markercluster')));
            return;
        }
        const script = document.createElement('script');
        script.src = MARKERCLUSTER_JS;
        script.async = true;
        script.setAttribute('data-lumeo-markercluster', '');
        script.onload = resolve;
        script.onerror = () => reject(new Error('Failed to load leaflet.markercluster'));
        document.head.appendChild(script);
    });
    return clusterLoadPromise;
}

function isDarkTheme() {
    // Lumeo's theme toggle adds `class="dark"` (or `data-theme="dark"`) on
    // <html>. Fall back to the OS preference for hosts that don't manage a
    // dedicated theme — matches mapcn.dev's auto-switch behavior.
    const root = document.documentElement;
    if (root.classList.contains('dark')) return true;
    if (root.dataset?.theme === 'dark') return true;
    return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
}

function resolveTileLayer(L, name) {
    if (!name || name === 'Auto') {
        name = isDarkTheme() ? 'CartoDark' : 'CartoLight';
    }
    const preset = TILE_PRESETS[name];
    if (preset) {
        return L.tileLayer(preset.url, { attribution: preset.attribution, maxZoom: preset.maxZoom });
    }
    return L.tileLayer(name, { maxZoom: 19 });
}

// Wire a MutationObserver on <html> so the Auto tile layer flips between
// CartoLight and CartoDark in lockstep with the host page's theme toggle —
// no consumer code needed. Returns a cleanup function.
function watchThemeChanges(L, map, currentName, getCurrentLayer, onSwap) {
    if (currentName !== 'Auto') return () => {};
    const root = document.documentElement;
    let last = isDarkTheme();
    const apply = () => {
        const dark = isDarkTheme();
        if (dark === last) return;
        last = dark;
        const prev = getCurrentLayer();
        const next = resolveTileLayer(L, 'Auto');
        if (prev) map.removeLayer(prev);
        next.addTo(map);
        onSwap(next);
    };
    const mo = new MutationObserver(apply);
    mo.observe(root, { attributes: true, attributeFilter: ['class', 'data-theme'] });
    const mq = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;
    const mqHandler = () => apply();
    mq?.addEventListener?.('change', mqHandler);
    return () => {
        mo.disconnect();
        mq?.removeEventListener?.('change', mqHandler);
    };
}

// ----------------------------------------------------------------------
// Marker rendering — DivIcon variants.
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

function buildDivIconParts(variant, color, label, iconHtml) {
    const v = (variant || 'Default');
    const fill = resolveColor(color);

    let innerStyle = '';
    let labelHtml = '';
    let iconSize = [32, 32];
    let iconAnchor = [16, 16];
    let popupAnchor = [0, -18];

    if (label) {
        labelHtml = `<span class="lumeo-map-marker-label">${escapeHtml(label)}</span>`;
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
            iconSize = [12, 12];
            iconAnchor = [6, 6];
            popupAnchor = [0, -8];
            break;
        case 'Pin':
            return {
                html: `${labelHtml}<svg xmlns="http://www.w3.org/2000/svg" width="28" height="40" viewBox="0 0 24 36" fill="${fill}" stroke="hsl(var(--background, 0 0% 100%))" stroke-width="1.5">
                    <path d="M12 0C5.4 0 0 5.4 0 12c0 9 12 24 12 24s12-15 12-24c0-6.6-5.4-12-12-12z"/>
                    <circle cx="12" cy="12" r="4.5" fill="hsl(var(--background, 0 0% 100%))"/>
                </svg>`,
                iconSize: [28, 40],
                iconAnchor: [14, 40],
                popupAnchor: [0, -36],
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
        iconSize,
        iconAnchor,
        popupAnchor,
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

function createMarkerIcon(L, m) {
    if (m.iconUrl) {
        return L.icon({
            iconUrl: m.iconUrl,
            iconSize: [32, 32],
            iconAnchor: [16, 32],
            popupAnchor: [0, -32],
        });
    }
    const parts = buildDivIconParts(m.variant, m.color, m.label, m.iconHtml);
    return L.divIcon({
        html: parts.html,
        iconSize: parts.iconSize,
        iconAnchor: parts.iconAnchor,
        popupAnchor: parts.popupAnchor,
        className: 'lumeo-map-marker',
    });
}

// Cluster DivIcon — mirrors the marker visuals so clusters read as part of
// the same family. Size scales with the child count (16/12 per tier).
function makeClusterIcon(L, count) {
    let size = 36;
    let className = 'lumeo-map-cluster';
    if (count >= 100) size = 52;
    else if (count >= 25) size = 44;
    return L.divIcon({
        html: `<div>${count}</div>`,
        className,
        iconSize: [size, size],
    });
}

// ----------------------------------------------------------------------
// Custom controls — shadcn-styled floating panels.
// ----------------------------------------------------------------------

function makeCustomControlButton(L, opts) {
    // Returns a Leaflet Control with one button — same look as a single-row
    // .leaflet-bar but built from scratch so we can attach our own icons.
    const ctl = L.control({ position: opts.position || 'topleft' });
    ctl.onAdd = function () {
        const container = L.DomUtil.create('div', 'leaflet-bar lumeo-map-panel');
        const btn = L.DomUtil.create('a', 'lumeo-map-btn', container);
        btn.href = '#';
        btn.title = opts.title;
        btn.setAttribute('role', 'button');
        btn.setAttribute('aria-label', opts.title);
        btn.innerHTML = opts.svg;
        L.DomEvent.on(btn, 'click', (e) => {
            L.DomEvent.stop(e);
            opts.onClick();
        });
        L.DomEvent.disableClickPropagation(container);
        return container;
    };
    return ctl;
}

// Inline Lucide SVGs — embedded so we don't depend on Blazicons at the JS layer.
const ICON_LOCATE = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="2" x2="5" y1="12" y2="12"/><line x1="19" x2="22" y1="12" y2="12"/><line x1="12" x2="12" y1="2" y2="5"/><line x1="12" x2="12" y1="19" y2="22"/><circle cx="12" cy="12" r="7"/><circle cx="12" cy="12" r="3"/></svg>`;
const ICON_EXPAND = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="15 3 21 3 21 9"/><polyline points="9 21 3 21 3 15"/><line x1="21" x2="14" y1="3" y2="10"/><line x1="3" x2="10" y1="21" y2="14"/></svg>`;
const ICON_COMPRESS = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="4 14 10 14 10 20"/><polyline points="20 10 14 10 14 4"/><line x1="14" x2="21" y1="10" y2="3"/><line x1="3" x2="10" y1="21" y2="14"/></svg>`;
const ICON_SEARCH = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg>`;

function addGeolocateControl(L, map) {
    const ctl = makeCustomControlButton(L, {
        position: 'topleft',
        title: 'My location',
        svg: ICON_LOCATE,
        onClick: () => {
            if (!('geolocation' in navigator)) return;
            navigator.geolocation.getCurrentPosition(
                (pos) => map.setView([pos.coords.latitude, pos.coords.longitude], Math.max(map.getZoom(), 14)),
                () => { /* permission denied / no fix — silently */ },
                { enableHighAccuracy: false, timeout: 6000, maximumAge: 60000 }
            );
        },
    });
    ctl.addTo(map);
    return ctl;
}

function addFullscreenControl(L, map) {
    const el = map.getContainer();
    const ctl = makeCustomControlButton(L, {
        position: 'topleft',
        title: 'Fullscreen',
        svg: ICON_EXPAND,
        onClick: () => {
            if (document.fullscreenElement) {
                document.exitFullscreen().catch(() => {});
            } else {
                el.requestFullscreen().catch(() => {});
            }
        },
    });
    ctl.addTo(map);
    const updateIcon = () => {
        const btn = ctl.getContainer()?.querySelector('a');
        if (btn) btn.innerHTML = document.fullscreenElement ? ICON_COMPRESS : ICON_EXPAND;
        // Leaflet stalls on size changes during fullscreen entry/exit; nudge it.
        setTimeout(() => map.invalidateSize(), 250);
    };
    document.addEventListener('fullscreenchange', updateIcon);
    ctl._lumeoCleanup = () => document.removeEventListener('fullscreenchange', updateIcon);
    return ctl;
}

function addScaleControl(L, map) {
    const scale = L.control.scale({ position: 'bottomleft', imperial: false, maxWidth: 120 });
    scale.addTo(map);
    return scale;
}

function addLayerSwitcher(L, map, layers, currentName, onChange) {
    // `layers` is an array of preset names or { name, url, attribution } records.
    const ctl = L.control({ position: 'topright' });
    ctl.onAdd = function () {
        const container = L.DomUtil.create('div', 'lumeo-map-layers');
        layers.forEach((entry) => {
            const name = typeof entry === 'string' ? entry : entry.name;
            const btn = L.DomUtil.create('button', '', container);
            btn.type = 'button';
            btn.textContent = name;
            if (name === currentName) btn.setAttribute('data-active', 'true');
            L.DomEvent.on(btn, 'click', (e) => {
                L.DomEvent.stop(e);
                container.querySelectorAll('button').forEach(b => b.removeAttribute('data-active'));
                btn.setAttribute('data-active', 'true');
                onChange(entry);
            });
        });
        L.DomEvent.disableClickPropagation(container);
        return container;
    };
    ctl.addTo(map);
    return ctl;
}

function addSearchControl(L, map, dotNetRef) {
    // Nominatim-backed search. Debounced; bounded to 5 results; respect their
    // usage policy (no parallel calls, low rate, attribution implicit via OSM
    // attribution since we hand back lat/lon and the user moves the map).
    const ctl = L.control({ position: 'topleft' });
    let debounceTimer = null;
    let lastQuery = '';

    ctl.onAdd = function () {
        const container = L.DomUtil.create('div', 'lumeo-map-search');
        container.innerHTML = `
            <div class="lumeo-map-search-input-wrap">
                ${ICON_SEARCH}
                <input type="search" placeholder="Search places…" aria-label="Search places" />
            </div>
            <div class="lumeo-map-search-results" hidden></div>
        `;
        const input = container.querySelector('input');
        const results = container.querySelector('.lumeo-map-search-results');

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
                    map.setView([parseFloat(r.lat), parseFloat(r.lon)], 14);
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
            if (q.length < 3) {
                results.hidden = true;
                return;
            }
            debounceTimer = setTimeout(() => doSearch(q), 400);
        });
        input.addEventListener('blur', () => {
            // delay so a click on a result still fires before we hide
            setTimeout(() => { results.hidden = true; }, 150);
        });
        input.addEventListener('focus', () => {
            if (results.children.length) results.hidden = false;
        });

        L.DomEvent.disableClickPropagation(container);
        L.DomEvent.disableScrollPropagation(container);
        return container;
    };
    ctl.addTo(map);
    return ctl;
}

// ----------------------------------------------------------------------
// Shapes (polylines, polygons, circles, rectangles, GeoJSON).
// ----------------------------------------------------------------------

function resolveShapeStyle(s) {
    return {
        color: resolveColor(s.color),
        weight: s.weight ?? 3,
        opacity: s.opacity ?? 0.9,
        fillColor: resolveColor(s.fillColor || s.color),
        fillOpacity: s.fillOpacity ?? 0.18,
        dashArray: s.dashArray || undefined,
    };
}

function createShape(L, s) {
    const style = resolveShapeStyle(s);
    switch (s.type) {
        case 'polyline':
            return L.polyline(s.points.map(p => [p.lat, p.lon]), style);
        case 'polygon':
            return L.polygon(s.points.map(p => [p.lat, p.lon]), style);
        case 'rectangle':
            return L.rectangle([[s.bounds.south, s.bounds.west], [s.bounds.north, s.bounds.east]], style);
        case 'circle':
            return L.circle([s.center.lat, s.center.lon], { ...style, radius: s.radiusMeters });
        case 'arc': {
            // Quadratic Bezier between two points, sampled into a polyline.
            // The control point sits perpendicular to the midpoint at a height
            // of `curvature * chord-length` (so arcs scale with distance).
            const A = [s.from.lat, s.from.lon];
            const B = [s.to.lat, s.to.lon];
            const mid = [(A[0] + B[0]) / 2, (A[1] + B[1]) / 2];
            const dx = B[1] - A[1];
            const dy = B[0] - A[0];
            const len = Math.sqrt(dx * dx + dy * dy);
            const curv = s.curvature ?? 0.25;
            const cx = mid[1] + (-dy / len) * len * curv;
            const cy = mid[0] + (dx / len) * len * curv;
            const samples = 48;
            const pts = [];
            for (let i = 0; i <= samples; i++) {
                const t = i / samples;
                const lat = (1 - t) * (1 - t) * A[0] + 2 * (1 - t) * t * cy + t * t * B[0];
                const lon = (1 - t) * (1 - t) * A[1] + 2 * (1 - t) * t * cx + t * t * B[1];
                pts.push([lat, lon]);
            }
            return L.polyline(pts, style);
        }
        case 'geojson':
            return L.geoJSON(s.geojson, { style });
        default:
            return null;
    }
}

// ----------------------------------------------------------------------
// Public API
// ----------------------------------------------------------------------

export async function init(elementId, options, dotNetRef) {
    ensureLumeoMapCss();
    const L = await loadLeaflet();
    const el = document.getElementById(elementId);
    if (!el) return;

    if (instances.has(elementId)) {
        try { instances.get(elementId).map.remove(); } catch (_) {}
        instances.delete(elementId);
    }

    const map = L.map(el, {
        center: [options.lat, options.lon],
        zoom: options.zoom,
        zoomControl: options.zoomControl !== false,
        attributionControl: options.attribution !== false,
        scrollWheelZoom: options.scrollWheelZoom !== false,
    });

    let currentTile = resolveTileLayer(L, options.tileLayer);
    currentTile.addTo(map);

    // Theme-aware swap for TileLayer="Auto" — flips between CartoLight and
    // CartoDark when the host page toggles its theme. No-op for explicit
    // preset names or raw URL templates.
    const themeCleanup = watchThemeChanges(
        L, map, options.tileLayer,
        () => currentTile,
        (next) => { currentTile = next; },
    );

    let markerGroup = null;
    let clusterEnabled = !!options.cluster;
    if (clusterEnabled) {
        try {
            await loadMarkerCluster(L);
            markerGroup = L.markerClusterGroup({
                showCoverageOnHover: false,
                spiderfyOnMaxZoom: true,
                iconCreateFunction: (cluster) => makeClusterIcon(L, cluster.getChildCount()),
            });
            map.addLayer(markerGroup);
        } catch {
            // fall back to plain group if cluster failed to load
            clusterEnabled = false;
        }
    }

    map.on('click', (e) => {
        try { dotNetRef.invokeMethodAsync('OnMapClick', e.latlng.lat, e.latlng.lng); }
        catch (_) {}
    });

    const fireViewChanged = () => {
        const c = map.getCenter();
        try { dotNetRef.invokeMethodAsync('OnViewChanged', c.lat, c.lng, map.getZoom()); }
        catch (_) {}
    };
    map.on('moveend', fireViewChanged);
    map.on('zoomend', fireViewChanged);

    const controls = [];
    if (options.geolocate)   controls.push(addGeolocateControl(L, map));
    if (options.fullscreen)  controls.push(addFullscreenControl(L, map));
    if (options.scale)       controls.push(addScaleControl(L, map));
    if (options.search)      controls.push(addSearchControl(L, map, dotNetRef));

    let layerSwitcher = null;
    if (Array.isArray(options.layers) && options.layers.length > 0) {
        let currentTile = tile;
        let currentName = options.tileLayer || 'OpenStreetMap';
        layerSwitcher = addLayerSwitcher(L, map, options.layers, currentName, (entry) => {
            const name = typeof entry === 'string' ? entry : entry.name;
            const url = typeof entry === 'string' ? null : entry.url;
            map.removeLayer(currentTile);
            currentTile = url ? L.tileLayer(url, { maxZoom: entry.maxZoom || 19, attribution: entry.attribution || '' })
                              : resolveTileLayer(L, name);
            currentTile.addTo(map);
            currentName = name;
        });
        controls.push(layerSwitcher);
    }

    instances.set(elementId, {
        L, map, tile: currentTile, markers: [], shapes: [], dotNetRef,
        markerGroup, clusterEnabled, controls, themeCleanup,
    });
}

export async function setMarkers(elementId, markers) {
    const inst = instances.get(elementId);
    if (!inst) return;
    const { L, map, markers: existing, dotNetRef, markerGroup, clusterEnabled } = inst;

    const layerHost = clusterEnabled ? markerGroup : map;

    for (const m of existing) {
        if (clusterEnabled) markerGroup.removeLayer(m); else map.removeLayer(m);
    }
    existing.length = 0;

    for (const m of markers) {
        if (m.iconHostId) {
            const host = document.getElementById(m.iconHostId);
            if (host) m.iconHtml = host.innerHTML;
        }
        const opts = { icon: createMarkerIcon(L, m) };
        const leafletMarker = L.marker([m.lat, m.lon], opts);
        if (m.title) leafletMarker.bindTooltip(m.title);

        let popupHtml = m.popupHtml;
        if (m.popupHostId) {
            const host = document.getElementById(m.popupHostId);
            if (host) popupHtml = host.innerHTML;
        }
        if (popupHtml) leafletMarker.bindPopup(popupHtml);

        if (m.hasClick) {
            const id = m.id;
            leafletMarker.on('click', () => {
                try { dotNetRef.invokeMethodAsync('OnMarkerClick', id); }
                catch (_) {}
            });
        }

        if (clusterEnabled) markerGroup.addLayer(leafletMarker);
        else leafletMarker.addTo(map);
        existing.push(leafletMarker);
    }
}

export async function setShapes(elementId, shapes) {
    const inst = instances.get(elementId);
    if (!inst) return;
    const { L, map, shapes: existing } = inst;

    for (const s of existing) map.removeLayer(s);
    existing.length = 0;

    for (const s of shapes) {
        const layer = createShape(L, s);
        if (!layer) continue;
        if (s.tooltip) layer.bindTooltip(s.tooltip);
        if (s.popupHtml) layer.bindPopup(s.popupHtml);
        layer.addTo(map);
        existing.push(layer);
    }
}

export async function setCenter(elementId, lat, lon, zoom) {
    const inst = instances.get(elementId);
    if (!inst) return;
    inst.map.setView([lat, lon], zoom);
}

export async function fitBounds(elementId, south, west, north, east, paddingPx) {
    const inst = instances.get(elementId);
    if (!inst) return;
    const pad = paddingPx ?? 30;
    inst.map.fitBounds([[south, west], [north, east]], { padding: [pad, pad] });
}

export async function destroy(elementId) {
    const inst = instances.get(elementId);
    if (!inst) return;
    try {
        if (inst.themeCleanup) inst.themeCleanup();
        for (const c of inst.controls) {
            if (c && c._lumeoCleanup) c._lumeoCleanup();
        }
        inst.map.remove();
    } catch (_) {}
    instances.delete(elementId);
}
