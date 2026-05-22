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

const MAPLIBRE_JS = 'https://unpkg.com/maplibre-gl@4.7.1/dist/maplibre-gl.js';
const MAPLIBRE_CSS = 'https://unpkg.com/maplibre-gl@4.7.1/dist/maplibre-gl.css';
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
    const root = document.documentElement;
    if (root.classList.contains('dark')) return true;
    if (root.dataset?.theme === 'dark') return true;
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

function watchThemeChanges(map, requestedName, getInstance) {
    if (requestedName !== 'Auto') return () => {};
    let last = isDarkTheme();
    const apply = () => {
        const dark = isDarkTheme();
        if (dark === last) return;
        last = dark;
        const newStyle = resolveStyleUrl('Auto');
        // setStyle wipes all user-added sources/layers; we re-add them after
        // the new style finishes loading (see attachStyleReloadHandler below).
        map.setStyle(newStyle);
    };
    const mo = new MutationObserver(apply);
    mo.observe(document.documentElement, { attributes: true, attributeFilter: ['class', 'data-theme'] });
    const mq = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;
    const mqHandler = () => apply();
    mq?.addEventListener?.('change', mqHandler);
    return () => {
        mo.disconnect();
        mq?.removeEventListener?.('change', mqHandler);
    };
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
function buildMarkerVariant(variant, color, label, iconHtml) {
    const v = (variant || 'Default');
    const fill = resolveColor(color);

    let innerStyle = '';
    let labelHtml = '';
    let anchor = 'center';

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
            break;
        case 'Pin':
            anchor = 'bottom';
            return {
                html: `${labelHtml}<svg xmlns="http://www.w3.org/2000/svg" width="28" height="40" viewBox="0 0 24 36" fill="${fill}" stroke="hsl(var(--background, 0 0% 100%))" stroke-width="1.5">
                    <path d="M12 0C5.4 0 0 5.4 0 12c0 9 12 24 12 24s12-15 12-24c0-6.6-5.4-12-12-12z"/>
                    <circle cx="12" cy="12" r="4.5" fill="hsl(var(--background, 0 0% 100%))"/>
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
    const parts = buildMarkerVariant(m.variant, m.color, m.label, m.iconHtml);
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

    const map = new maplibregl.Map({
        container: el,
        style: resolveStyleUrl(options.tileLayer),
        center: toLngLat(options.lat, options.lon),
        zoom: options.zoom,
        attributionControl: options.attribution !== false,
    });

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
        clusterEnabled: !!options.cluster,
        controls,
        themeCleanup: null,
        requestedStyle: options.tileLayer,
    };

    // After a setStyle() call, MapLibre wipes user-added sources/layers.
    // Re-apply our cluster source and shape layers when the new style finishes
    // loading. DOM markers (non-cluster mode) survive automatically.
    map.on('styledata', () => {
        if (!map.isStyleLoaded()) return;
        if (inst.clusterEnabled) applyClusterMarkers(inst);
        applyShapes(inst);
    });

    inst.themeCleanup = watchThemeChanges(map, options.tileLayer, () => inst);

    instances.set(elementId, inst);
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
        const marker = new maplibregl.Marker({ element: el, anchor })
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

        inst.markers.push({ marker, popup, id: m.id, hasClick: m.hasClick, listener });
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
            'circle-color': 'hsl(var(--primary, 222 47% 11%))',
            'circle-stroke-color': 'hsl(var(--background, 0 0% 100%))',
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
        paint: { 'text-color': 'hsl(var(--primary-foreground, 0 0% 100%))' },
    });
    map.addLayer({
        id: 'lumeo-unclustered-point',
        type: 'circle',
        source: sourceId,
        filter: ['!', ['has', 'point_count']],
        paint: {
            'circle-color': ['get', 'color'],
            'circle-radius': 8,
            'circle-stroke-color': 'hsl(var(--background, 0 0% 100%))',
            'circle-stroke-width': 2,
        },
    });

    // Click handlers — clusters zoom in; individual points fire OnMarkerClick.
    map.on('click', 'lumeo-clusters', (e) => {
        const feature = e.features?.[0];
        if (!feature) return;
        const clusterId = feature.properties.cluster_id;
        map.getSource(sourceId).getClusterExpansionZoom(clusterId, (err, zoom) => {
            if (err) return;
            map.easeTo({ center: feature.geometry.coordinates, zoom });
        });
    });
    map.on('click', 'lumeo-unclustered-point', (e) => {
        const feature = e.features?.[0];
        if (!feature || !feature.properties.hasClick) return;
        try { inst.dotNetRef.invokeMethodAsync('OnMarkerClick', feature.properties.id); } catch (_) {}
    });
    const setCursor = (cur) => () => { map.getCanvas().style.cursor = cur; };
    map.on('mouseenter', 'lumeo-clusters', setCursor('pointer'));
    map.on('mouseleave', 'lumeo-clusters', setCursor(''));
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

        if (built.kind === 'line') {
            map.addSource(sourceId, { type: 'geojson', data: built.feature });
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

export async function destroy(elementId) {
    const inst = instances.get(elementId);
    if (!inst) return;
    try {
        if (inst.themeCleanup) inst.themeCleanup();
        clearDomMarkers(inst);
        inst.map.remove();
    } catch (_) {}
    instances.delete(elementId);
}
