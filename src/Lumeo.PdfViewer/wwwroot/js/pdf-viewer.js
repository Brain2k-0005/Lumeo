// Lumeo.PdfViewer — pdf.js wrapper.
//
// Loads Mozilla's pdf.js v4+ ESM build from a CDN on first use, then renders
// PDF documents page-by-page onto a per-instance <canvas>.  The module is
// loaded directly by the PdfViewer Razor component (not via the core
// ComponentInteropService) so apps that don't install Lumeo.PdfViewer never
// pay the import cost or trigger 404s for the pdf.js worker.
//
// Public surface (matches the named exports):
//   load(canvasId, src)              → { totalPages }
//   renderPage(canvasId, pageNum, zoom)
//   destroy(canvasId)
//   search(canvasId, query)          → match count
//   getDocumentUrl(canvasId)         → string (for download)

// pdf.js v4 ships as native ESM.  The worker MUST come from the same
// major.minor as the main bundle or pdf.js throws a version-mismatch error.
//
// We pin to 4.0.379 (the version this satellite was built against) and use
// the unminified `pdf.mjs` / `pdf.worker.mjs` filenames — these are the
// canonical names per pdfjs-dist's package.json `main` field and are
// guaranteed to exist across the major.  The `.min.mjs` variants exist on
// jsdelivr today but their availability is not contractual.
//
// Consumers who want a newer build can override window.lumeoPdfJsUrl /
// window.lumeoPdfJsWorkerUrl before the first PdfViewer instance loads.
const DEFAULT_PDFJS_URL = 'https://cdn.jsdelivr.net/npm/pdfjs-dist@4.0.379/build/pdf.mjs';
const DEFAULT_WORKER_URL = 'https://cdn.jsdelivr.net/npm/pdfjs-dist@4.0.379/build/pdf.worker.mjs';

let _pdfjsPromise = null;

// pdf.js v4 instantiates its worker via `new Worker(workerSrc, { type: 'module' })`.
// When workerSrc is on a different origin (jsdelivr) some browsers refuse the
// module-worker fetch with an opaque CORS error.  Pre-fetching the worker
// source and constructing the URL via `URL.createObjectURL(new Blob([...]))`
// sidesteps the issue — the worker becomes same-origin to the page.
async function resolveWorkerSrc(workerUrl) {
    try {
        const res = await fetch(workerUrl, { mode: 'cors' });
        if (!res.ok) throw new Error(`worker fetch ${res.status}`);
        const code = await res.text();
        const blob = new Blob([code], { type: 'application/javascript' });
        return URL.createObjectURL(blob);
    } catch (e) {
        // Fall back to the raw URL — works when the CDN's CORS headers are
        // permissive for module workers (Chromium-based browsers usually do).
        console.warn('Lumeo.PdfViewer: worker blob shim failed, using raw URL', e);
        return workerUrl;
    }
}

async function loadPdfJs() {
    if (_pdfjsPromise) return _pdfjsPromise;
    _pdfjsPromise = (async () => {
        const url = (typeof window !== 'undefined' && window.lumeoPdfJsUrl)
            ? window.lumeoPdfJsUrl
            : DEFAULT_PDFJS_URL;
        const workerUrl = (typeof window !== 'undefined' && window.lumeoPdfJsWorkerUrl)
            ? window.lumeoPdfJsWorkerUrl
            : DEFAULT_WORKER_URL;
        let m;
        try {
            m = await import(/* @vite-ignore */ url);
        } catch (e) {
            console.error('Lumeo.PdfViewer: failed to import pdf.js from', url, e);
            throw new Error(`Lumeo.PdfViewer: could not load pdf.js from ${url}`);
        }
        // pdf.js exports either as default or named bindings depending on build.
        const lib = m.GlobalWorkerOptions ? m : (m.default ?? m);
        if (!lib || !lib.GlobalWorkerOptions || typeof lib.getDocument !== 'function') {
            throw new Error('Lumeo.PdfViewer: pdf.js module shape unexpected — GlobalWorkerOptions/getDocument missing');
        }
        // MUST be set before the first getDocument() call.
        lib.GlobalWorkerOptions.workerSrc = await resolveWorkerSrc(workerUrl);
        return lib;
    })();
    // If the load fails, blow away the cached rejection so the next mount can retry.
    _pdfjsPromise.catch(() => { _pdfjsPromise = null; });
    return _pdfjsPromise;
}

// Per-canvas instance state.  Keyed by the canvasId the Razor component
// generates ($"lumeo-pdf-{Guid.NewGuid():N}").
const _instances = new Map();

function getCanvas(canvasId) {
    const el = (typeof document !== 'undefined') ? document.getElementById(canvasId) : null;
    if (!el) throw new Error(`Lumeo.PdfViewer: canvas '${canvasId}' not found in DOM`);
    if (!(el instanceof HTMLCanvasElement)) {
        throw new Error(`Lumeo.PdfViewer: element '${canvasId}' is not a <canvas>`);
    }
    return el;
}

export async function load(canvasId, src) {
    if (!src) throw new Error('Lumeo.PdfViewer: Src is required');
    if (!canvasId) throw new Error('Lumeo.PdfViewer: canvasId is required');
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
        renderLock: Promise.resolve(),
        pageTexts: null, // lazily populated on first search
    });
    return { totalPages: doc.numPages };
}

export async function renderPage(canvasId, pageNum, zoom) {
    const inst = _instances.get(canvasId);
    if (!inst) throw new Error(`Lumeo.PdfViewer: no document loaded for '${canvasId}'`);

    // Serialize render calls so a rapid page/zoom change can't start a new
    // render before the previous one has finished tearing down.  Each call
    // chains onto the previous one's completion (success OR failure).
    const next = inst.renderLock.then(() => doRenderPage(inst, canvasId, pageNum, zoom));
    // Track the chain but swallow failures so a busted render doesn't poison
    // subsequent calls — the awaited promise still surfaces the error.
    inst.renderLock = next.catch(() => { /* keep the chain alive */ });
    return next;
}

async function doRenderPage(inst, canvasId, pageNum, zoom) {
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
    const dpr = (typeof window !== 'undefined' && window.devicePixelRatio) || 1;
    const viewport = page.getViewport({ scale: safeZoom * dpr });
    const cssViewport = page.getViewport({ scale: safeZoom });

    canvas.width = Math.floor(viewport.width);
    canvas.height = Math.floor(viewport.height);
    canvas.style.width = `${Math.floor(cssViewport.width)}px`;
    canvas.style.height = `${Math.floor(cssViewport.height)}px`;

    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error(`Lumeo.PdfViewer: 2D canvas context unavailable for '${canvasId}'`);
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
