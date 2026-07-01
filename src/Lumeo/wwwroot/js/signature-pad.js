// Lumeo SignaturePad — vanilla canvas, pointer-event driven.
//
// Pointer events unify mouse / touch / pen on every modern browser. We rely
// on setPointerCapture so a stroke that begins inside the canvas keeps
// tracking even when the finger / mouse leaves the canvas bounds — without
// capture, drag-out would silently end the stroke mid-character. CSS
// `touch-action: none` (applied by the .razor) is what stops Safari from
// hijacking single-finger drags as page-scroll while signing.

const pads = new Map();

function getCtx(pad) {
    return pad.canvas.getContext('2d');
}

function applyStrokeStyle(pad) {
    const ctx = getCtx(pad);
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    ctx.strokeStyle = pad.strokeColor;
    ctx.lineWidth = pad.strokeWidth;
}

function pointFromEvent(pad, e) {
    const rect = pad.canvas.getBoundingClientRect();
    // Map CSS-pixel coordinates onto the canvas backing-store size — the canvas
    // element's width/height attributes are CSS pixels too in our layout, so
    // no DPR scaling is needed. (If we later add DPR-aware rendering we'd
    // multiply here by `pad.canvas.width / rect.width`.)
    return {
        x: e.clientX - rect.left,
        y: e.clientY - rect.top,
    };
}

function loadDataUrlInto(pad, dataUrl) {
    if (!dataUrl) return;
    const img = new Image();
    img.onload = () => {
        const ctx = getCtx(pad);
        ctx.drawImage(img, 0, 0, pad.canvas.width, pad.canvas.height);
    };
    img.src = dataUrl;
}

function clearCanvas(pad) {
    const ctx = getCtx(pad);
    ctx.clearRect(0, 0, pad.canvas.width, pad.canvas.height);
    pad.isEmpty = true;
    pad.strokes = [];
    pad.currentStroke = null;
}

// Builds an SVG document from the recorded vector strokes and returns it as a
// base64 data URL. Each stroke becomes a quadratic-smoothed <path> matching the
// on-canvas rendering. Returns null when there's nothing drawn (so the .NET
// side can treat empty the same as the PNG path).
function buildSvgDataUrl(pad) {
    if (!pad.strokes || pad.strokes.length === 0) return null;
    const w = pad.canvas.width;
    const h = pad.canvas.height;
    const round = (n) => Math.round(n * 100) / 100;
    const paths = [];
    for (const stroke of pad.strokes) {
        const pts = stroke && stroke.points;
        if (!pts || pts.length === 0) continue;
        // Each stroke carries the color/width active when it was drawn, so a
        // mid-signature setStrokeStyle change is preserved in the export rather
        // than every stroke inheriting the final global style.
        const color = stroke.color;
        const width = stroke.width;
        if (pts.length === 1) {
            // A lone tap: render a filled dot of the stroke radius.
            const p = pts[0];
            paths.push(`<circle cx="${round(p.x)}" cy="${round(p.y)}" r="${round(width / 2)}" fill="${color}" />`);
            continue;
        }
        let d = `M ${round(pts[0].x)} ${round(pts[0].y)}`;
        // Quadratic smoothing through midpoints, mirroring the canvas path.
        for (let i = 1; i < pts.length; i++) {
            const prev = pts[i - 1];
            const cur = pts[i];
            const midX = (prev.x + cur.x) / 2;
            const midY = (prev.y + cur.y) / 2;
            d += ` Q ${round(prev.x)} ${round(prev.y)} ${round(midX)} ${round(midY)}`;
        }
        paths.push(`<path d="${d}" fill="none" stroke="${color}" stroke-width="${width}" stroke-linecap="round" stroke-linejoin="round" />`);
    }
    if (paths.length === 0) return null;
    const svg =
        `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">` +
        `${paths.join('')}</svg>`;
    // Encode as a UTF-8-safe base64 data URL.
    const b64 = btoa(unescape(encodeURIComponent(svg)));
    return `data:image/svg+xml;base64,${b64}`;
}

export function init(elementId, options, dotnetRef) {
    const canvas = document.getElementById(elementId);
    if (!canvas) return;

    // If the canvas was re-initialised (e.g. component re-render under
    // server-prerender), tear down the previous pad cleanly before wiring
    // up a fresh one to avoid duplicate listeners.
    if (pads.has(elementId)) destroy(elementId);

    const pad = {
        canvas,
        dotnetRef,
        strokeColor: options?.strokeColor ?? 'currentColor',
        strokeWidth: Number(options?.strokeWidth ?? 2),
        mimeType: options?.mimeType ?? 'image/png',
        debounceMs: Number(options?.debounceMs ?? 200),
        disabled: !!options?.disabled,
        isDrawing: false,
        isEmpty: true,
        lastPoint: null,
        debounceTimer: 0,
        handlers: {},
        // Recorded vector strokes for real SVG export. Each stroke is
        // { points: [{x,y}…], color, width } capturing the style active when it
        // was drawn; replayed as <path>/<circle> elements in getSvgDataUrl.
        // Raster (PNG) export still reads the canvas directly.
        strokes: [],
        currentStroke: null,
    };

    // Resolve `currentColor` to the computed text color of the canvas so the
    // stroke actually paints visibly. Pointer events themselves don't care
    // about CSS color, only the 2D context's strokeStyle.
    if (pad.strokeColor === 'currentColor') {
        pad.strokeColor = getComputedStyle(canvas).color || '#000';
    }

    applyStrokeStyle(pad);

    if (options?.initialDataUrl) {
        loadDataUrlInto(pad, options.initialDataUrl);
        pad.isEmpty = false;
    }

    const onPointerDown = (e) => {
        if (pad.disabled) return;
        // Ignore non-primary mouse buttons so right-click / middle-click never
        // start a stroke.
        if (e.pointerType === 'mouse' && e.button !== 0) return;
        e.preventDefault();
        try { canvas.setPointerCapture(e.pointerId); } catch { /* unsupported, ignore */ }
        pad.isDrawing = true;
        const p = pointFromEvent(pad, e);
        pad.lastPoint = p;
        // Start recording a new vector stroke for SVG export.
        pad.currentStroke = { points: [{ x: p.x, y: p.y }], color: pad.strokeColor, width: pad.strokeWidth };
        pad.strokes.push(pad.currentStroke);
        // A tap-only dot is still a real signature — mark non-empty now so it
        // exports (move events would otherwise be the only thing clearing this).
        pad.isEmpty = false;
        const ctx = getCtx(pad);
        ctx.beginPath();
        ctx.moveTo(p.x, p.y);
        // Dot for taps that never move (e.g. punctuation).
        ctx.arc(p.x, p.y, pad.strokeWidth / 2, 0, Math.PI * 2);
        ctx.fillStyle = pad.strokeColor;
        ctx.fill();
        ctx.beginPath();
        ctx.moveTo(p.x, p.y);
    };

    const onPointerMove = (e) => {
        if (!pad.isDrawing || pad.disabled) return;
        e.preventDefault();
        const p = pointFromEvent(pad, e);
        const ctx = getCtx(pad);
        // Quadratic smoothing between last and current point for a more
        // natural-looking handwritten line than raw lineTo segments.
        const mid = {
            x: (pad.lastPoint.x + p.x) / 2,
            y: (pad.lastPoint.y + p.y) / 2,
        };
        ctx.quadraticCurveTo(pad.lastPoint.x, pad.lastPoint.y, mid.x, mid.y);
        ctx.stroke();
        ctx.beginPath();
        ctx.moveTo(mid.x, mid.y);
        pad.lastPoint = p;
        pad.isEmpty = false;
        if (pad.currentStroke) pad.currentStroke.points.push({ x: p.x, y: p.y });
    };

    const endStroke = (e) => {
        if (!pad.isDrawing) return;
        pad.isDrawing = false;
        pad.lastPoint = null;
        try { canvas.releasePointerCapture(e.pointerId); } catch { /* ignore */ }

        // Debounce the .NET roundtrip so a multi-stroke signature doesn't
        // fire a serialise + invokeMethodAsync per stroke.
        clearTimeout(pad.debounceTimer);
        pad.debounceTimer = setTimeout(() => {
            if (!pad.dotnetRef) return;
            const dataUrl = pad.isEmpty ? null : exportDataUrl(pad, pad.mimeType);
            try {
                pad.dotnetRef.invokeMethodAsync('OnStrokeEnded', dataUrl);
            } catch { /* circuit torn down — swallow */ }
        }, pad.debounceMs);
    };

    pad.handlers = {
        pointerdown: onPointerDown,
        pointermove: onPointerMove,
        pointerup: endStroke,
        pointercancel: endStroke,
        pointerleave: endStroke,
    };

    for (const [type, handler] of Object.entries(pad.handlers)) {
        canvas.addEventListener(type, handler);
    }

    pads.set(elementId, pad);
}

export function clear(elementId) {
    const pad = pads.get(elementId);
    if (!pad) return;
    // Cancel any stroke-end debounce still pending from the user's last stroke,
    // so a programmatic clear can't be followed by a stale OnStrokeEnded emit
    // that reports the just-cleared (or about-to-be-replaced) content.
    clearTimeout(pad.debounceTimer);
    clearCanvas(pad);
}

// Central export: SVG mime → real vector export from recorded strokes;
// anything else → raster canvas.toDataURL. Falls back to PNG only if there are
// no recorded strokes to vectorise (e.g. an image loaded via loadDataUrl).
function exportDataUrl(pad, mimeType) {
    const mt = mimeType || pad.mimeType;
    if (mt === 'image/svg+xml') {
        const svg = buildSvgDataUrl(pad);
        if (svg) return svg;
        // No vector data (image was loaded, not drawn) — fall back to raster.
        return pad.canvas.toDataURL('image/png');
    }
    return pad.canvas.toDataURL(mt);
}

export function getDataUrl(elementId, mimeType) {
    const pad = pads.get(elementId);
    if (!pad) return null;
    if (pad.isEmpty) return null;
    return exportDataUrl(pad, mimeType);
}

export function setStrokeStyle(elementId, color, width) {
    const pad = pads.get(elementId);
    if (!pad) return;
    pad.strokeColor = color === 'currentColor'
        ? (getComputedStyle(pad.canvas).color || '#000')
        : color;
    pad.strokeWidth = Number(width);
    applyStrokeStyle(pad);
}

export function setDisabled(elementId, disabled) {
    const pad = pads.get(elementId);
    if (!pad) return;
    pad.disabled = !!disabled;
}

export function loadDataUrl(elementId, dataUrl) {
    const pad = pads.get(elementId);
    if (!pad) return;
    // Cancel any stroke-end debounce still pending from the user's last stroke.
    // Otherwise a programmatic load that lands inside the debounce window would
    // be re-emitted via OnStrokeEnded as if the just-loaded image were drawn.
    clearTimeout(pad.debounceTimer);
    clearCanvas(pad);
    if (dataUrl) {
        loadDataUrlInto(pad, dataUrl);
        pad.isEmpty = false;
    }
}

export function destroy(elementId) {
    const pad = pads.get(elementId);
    if (!pad) return;
    clearTimeout(pad.debounceTimer);
    for (const [type, handler] of Object.entries(pad.handlers)) {
        try { pad.canvas.removeEventListener(type, handler); } catch { /* ignore */ }
    }
    pad.dotnetRef = null;
    pads.delete(elementId);
}
