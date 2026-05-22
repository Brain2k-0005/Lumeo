// Lumeo.Maps — Leaflet wrapper module.
//
// Leaflet itself + its stylesheet are loaded from CDN on first use, then
// cached on the window. Tile-layer URL templates for the named presets
// match the docs at https://leafletjs.com/examples/quick-start/ — consumers
// can also pass a raw URL template (anything containing "{z}/{x}/{y}").
//
// We also inject our own `map.css` (shadcn-style popup + DivIcon marker
// overrides). It's idempotent: a single <link data-lumeo-map> per document.

const LEAFLET_JS = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.js';
const LEAFLET_CSS = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.css';
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
    // Esri's World Imagery tile service — free for non-commercial use without
    // an API key. Attribution string follows Esri's published requirement.
    Satellite: {
        url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
        attribution: 'Tiles &copy; Esri &mdash; Source: Esri, Maxar, Earthstar Geographics, and the GIS User Community',
        maxZoom: 19,
    },
    // Stadia hosts the Stamen Terrain tiles since 2023. Free for non-commercial
    // use; commercial users should sign up for a Stadia API key.
    Terrain: {
        url: 'https://tiles.stadiamaps.com/tiles/stamen_terrain/{z}/{x}/{y}.png',
        attribution: '&copy; <a href="https://stadiamaps.com/">Stadia Maps</a>, &copy; <a href="https://stamen.com/">Stamen Design</a>, &copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        maxZoom: 18,
    },
};

const instances = new Map();
let leafletLoadPromise = null;

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
        // Stylesheet — append only once even if multiple maps mount.
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

function resolveTileLayer(L, name) {
    if (!name) name = 'OpenStreetMap';
    const preset = TILE_PRESETS[name];
    if (preset) {
        return L.tileLayer(preset.url, { attribution: preset.attribution, maxZoom: preset.maxZoom });
    }
    // Treat anything else as a raw URL template.
    return L.tileLayer(name, { maxZoom: 19 });
}

// ----------------------------------------------------------------------
// Marker rendering — DivIcon variants.
// ----------------------------------------------------------------------

// Map a friendly color name (or pass-through CSS) to a CSS color string. When
// `null`/undefined the caller gets the theme primary via CSS variables.
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
    // Treat anything else as a raw CSS color (hex, hsl(), etc.).
    return color;
}

// Returns { html, iconSize, iconAnchor, popupAnchor } for a variant.
function buildDivIconParts(variant, color, label, iconHtml) {
    const v = (variant || 'Default');
    const fill = resolveColor(color);

    // Inner element receives the visual styling; the wrapper gets the
    // `lumeo-map-marker` class so our hover transition kicks in. Style is
    // inlined so the DivIcon is self-contained — no extra CSS per variant.
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
            // Smaller, no border, soft halo via box-shadow.
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
            // Classic teardrop — SVG embedded so the variant doesn't depend on
            // Leaflet's bitmap default. Anchor at the tip.
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
    // If the consumer passed an explicit IconUrl, stay on the bitmap path —
    // they've opted out of the DivIcon styling entirely.
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

// ----------------------------------------------------------------------
// Public API
// ----------------------------------------------------------------------

export async function init(elementId, options, dotNetRef) {
    ensureLumeoMapCss();
    const L = await loadLeaflet();
    const el = document.getElementById(elementId);
    if (!el) return;

    // Idempotent: if a previous instance survived a re-render, tear it down
    // before re-initializing on the same element id.
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

    const tile = resolveTileLayer(L, options.tileLayer);
    tile.addTo(map);

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

    instances.set(elementId, { L, map, tile, markers: [], dotNetRef });
}

export async function setMarkers(elementId, markers) {
    const inst = instances.get(elementId);
    if (!inst) return;
    const { L, map, markers: existing, dotNetRef } = inst;

    // Wipe previous markers — declarative model: the C# side owns the full list
    // and re-sends it on any change. Cheap because marker counts are bounded
    // by how many <MapMarker> tags the consumer wrote.
    for (const m of existing) map.removeLayer(m);
    existing.length = 0;

    for (const m of markers) {
        // Resolve the inner icon HTML before building the DivIcon — if the
        // consumer supplied an <Icon> RenderFragment, Map.razor sent us a
        // hidden host id and we splice its innerHTML in.
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

        leafletMarker.addTo(map);
        existing.push(leafletMarker);
    }
}

export async function setCenter(elementId, lat, lon, zoom) {
    const inst = instances.get(elementId);
    if (!inst) return;
    inst.map.setView([lat, lon], zoom);
}

export async function destroy(elementId) {
    const inst = instances.get(elementId);
    if (!inst) return;
    try { inst.map.remove(); } catch (_) {}
    instances.delete(elementId);
}
