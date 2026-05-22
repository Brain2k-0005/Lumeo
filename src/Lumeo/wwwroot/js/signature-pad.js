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
            const dataUrl = pad.isEmpty ? null : canvas.toDataURL(pad.mimeType);
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
    clearCanvas(pad);
}

export function getDataUrl(elementId, mimeType) {
    const pad = pads.get(elementId);
    if (!pad) return null;
    if (pad.isEmpty) return null;
    return pad.canvas.toDataURL(mimeType || pad.mimeType);
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
