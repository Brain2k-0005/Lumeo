// Lumeo.PdfViewer — pdf.js wrapper.
//
// Loads Mozilla's pdf.js v4+ ESM build from a CDN on first use, then renders
// PDF documents page-by-page onto a per-instance <canvas>.  The module is
// loaded directly by the PdfViewer Razor component (not via the core
// ComponentInteropService) so apps that don't install Lumeo.PdfViewer never
// pay the import cost or trigger 404s for the pdf.js worker.
//
// Public surface (matches the IIFE namespace + named exports):
//   load(canvasId, src)              → { totalPages }
//   renderPage(canvasId, pageNum, zoom)
//   destroy(canvasId)
//   search(canvasId, query)          → match count
//   getDocumentUrl(canvasId)         → string (for download)

// pdf.js v4+ ships as native ESM. The worker MUST be loaded as a module from
// the same major.minor as the main bundle or pdf.js throws a version-mismatch
// error.  Pinning both to 4.0.379 keeps consumers reproducible; consumers who
// want a newer build can override window.lumeoPdfJsUrl / window.lumeoPdfJsWorkerUrl
// before the first PdfViewer instance loads.
const DEFAULT_PDFJS_URL = 'https://cdn.jsdelivr.net/npm/pdfjs-dist@4.0.379/build/pdf.min.mjs';
const DEFAULT_WORKER_URL = 'https://cdn.jsdelivr.net/npm/pdfjs-dist@4.0.379/build/pdf.worker.min.mjs';

let _pdfjsPromise = null;

async function loadPdfJs() {
    if (_pdfjsPromise) return _pdfjsPromise;
    _pdfjsPromise = (async () => {
        const url = (typeof window !== 'undefined' && window.lumeoPdfJsUrl)
            ? window.lumeoPdfJsUrl
            : DEFAULT_PDFJS_URL;
        const workerUrl = (typeof window !== 'undefined' && window.lumeoPdfJsWorkerUrl)
            ? window.lumeoPdfJsWorkerUrl
            : DEFAULT_WORKER_URL;
        const m = await import(/* @vite-ignore */ url);
        // pdf.js exports either as default or named bindings depending on build.
        const lib = m.GlobalWorkerOptions ? m : (m.default ?? m);
        if (lib.GlobalWorkerOptions) {
            lib.GlobalWorkerOptions.workerSrc = workerUrl;
        }
        return lib;
    })();
    return _pdfjsPromise;
}

// Per-canvas instance state.  Keyed by the canvasId the Razor component
// generates ($"lumeo-pdf-{Guid.NewGuid():N}").
const _instances = new Map();

function getCanvas(canvasId) {
    const el = document.getElementById(canvasId);
    if (!el) throw new Error(`Lumeo.PdfViewer: canvas '${canvasId}' not found in DOM`);
    if (!(el instanceof HTMLCanvasElement)) {
        throw new Error(`Lumeo.PdfViewer: element '${canvasId}' is not a <canvas>`);
    }
    return el;
}

export async function load(canvasId, src) {
    if (!src) throw new Error('Lumeo.PdfViewer: Src is required');
    const pdfjs = await loadPdfJs();
    // Destroy any previous instance bound to the same canvas (Src changed).
    await destroy(canvasId);

    const task = pdfjs.getDocument({ url: src });
    const doc = await task.promise;
    _instances.set(canvasId, {
        doc,
        src,
        currentPage: 0,
        currentZoom: 0,
        renderTask: null,
        pageTexts: null, // lazily populated on first search
    });
    return { totalPages: doc.numPages };
}

export async function renderPage(canvasId, pageNum, zoom) {
    const inst = _instances.get(canvasId);
    if (!inst) throw new Error(`Lumeo.PdfViewer: no document loaded for '${canvasId}'`);
    const canvas = getCanvas(canvasId);

    // Clamp page index to [1, totalPages].
    const safePage = Math.max(1, Math.min(pageNum | 0, inst.doc.numPages));
    const safeZoom = typeof zoom === 'number' && isFinite(zoom) && zoom > 0 ? zoom : 1.0;

    // Cancel any in-flight render so rapid zoom/page changes don't pile up.
    if (inst.renderTask) {
        try { inst.renderTask.cancel(); } catch { /* ignore */ }
        inst.renderTask = null;
    }

    const page = await inst.doc.getPage(safePage);
    // Account for HiDPI screens so the canvas is crisp without inflating CSS px.
    const dpr = window.devicePixelRatio || 1;
    const viewport = page.getViewport({ scale: safeZoom * dpr });
    const cssViewport = page.getViewport({ scale: safeZoom });

    canvas.width = Math.floor(viewport.width);
    canvas.height = Math.floor(viewport.height);
    canvas.style.width = `${Math.floor(cssViewport.width)}px`;
    canvas.style.height = `${Math.floor(cssViewport.height)}px`;

    const ctx = canvas.getContext('2d');
    inst.renderTask = page.render({ canvasContext: ctx, viewport });
    try {
        await inst.renderTask.promise;
    } catch (e) {
        // Cancelled renders throw a "RenderingCancelledException" — swallow it.
        if (e && e.name === 'RenderingCancelledException') return;
        throw e;
    } finally {
        inst.renderTask = null;
    }
    inst.currentPage = safePage;
    inst.currentZoom = safeZoom;
}

export async function destroy(canvasId) {
    const inst = _instances.get(canvasId);
    if (!inst) return;
    try {
        if (inst.renderTask) { try { inst.renderTask.cancel(); } catch { /* ignore */ } }
        if (inst.doc) { await inst.doc.destroy(); }
    } catch { /* swallow */ }
    _instances.delete(canvasId);
}

export async function search(canvasId, query) {
    const inst = _instances.get(canvasId);
    if (!inst) return 0;
    if (!query || !query.trim()) return 0;

    // Lazily extract per-page text once per document; subsequent searches are O(n).
    if (!inst.pageTexts) {
        const texts = [];
        for (let i = 1; i <= inst.doc.numPages; i++) {
            const page = await inst.doc.getPage(i);
            const content = await page.getTextContent();
            texts.push(content.items.map(it => it.str || '').join(' '));
        }
        inst.pageTexts = texts;
    }
    const q = query.toLowerCase();
    let count = 0;
    for (const text of inst.pageTexts) {
        const lower = text.toLowerCase();
        let from = 0;
        while ((from = lower.indexOf(q, from)) !== -1) {
            count++;
            from += q.length;
        }
    }
    return count;
}

export function getDocumentUrl(canvasId) {
    const inst = _instances.get(canvasId);
    return inst ? inst.src : null;
}
