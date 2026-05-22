// Lumeo.Maps — Leaflet wrapper module.
//
// Leaflet itself + its stylesheet are loaded from CDN on first use, then
// cached on the window. Tile-layer URL templates for the named presets
// match the docs at https://leafletjs.com/examples/quick-start/ — consumers
// can also pass a raw URL template (anything containing "{z}/{x}/{y}").

const LEAFLET_JS = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.js';
const LEAFLET_CSS = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.css';

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
};

const instances = new Map();
let leafletLoadPromise = null;

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

export async function init(elementId, options, dotNetRef) {
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
        const opts = {};
        if (m.iconUrl) {
            opts.icon = L.icon({
                iconUrl: m.iconUrl,
                iconSize: [32, 32],
                iconAnchor: [16, 32],
                popupAnchor: [0, -32],
            });
        }
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
