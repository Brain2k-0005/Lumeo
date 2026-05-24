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
//   search(canvasId, query)          → { totalCount, pages: [{ pageNum, matches: [{x,y,width,height}] }] }
//   getMatchOverlays(canvasId, pageNum, query) → [{ x, y, width, height }] in CSS px for current zoom
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
// CDN URLs — can be overridden globally via the standard `window.lumeoCdn`
// config object (preferred), or via the legacy `window.lumeoPdfJsUrl` /
// `window.lumeoPdfJsWorkerUrl` (kept for back-compat).  Lets airgapped /
// strict-CSP consumers self-host without forking the satellite.
function _cdn(key, fallback) {
    return (typeof window !== 'undefined' && window.lumeoCdn && window.lumeoCdn[key]) || fallback;
}
const DEFAULT_PDFJS_URL = _cdn('pdfJs', 'https://cdn.jsdelivr.net/npm/pdfjs-dist@4.0.379/build/pdf.mjs');
const DEFAULT_WORKER_URL = _cdn('pdfJsWorker', 'https://cdn.jsdelivr.net/npm/pdfjs-dist@4.0.379/build/pdf.worker.mjs');

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
        pageTexts: null,    // lazily populated on first search: array of string per page (1-indexed)
        pageTextItems: null, // lazily populated: array of [{str, x, y, w, h}] per page (1-indexed)
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
    inst.pageTexts = null;
    inst.pageTextItems = null;
    _instances.delete(canvasId);
}

// Lazily build per-page text content caches (both flat string and positioned items).
async function ensurePageData(inst) {
    if (inst.pageTexts && inst.pageTextItems) return;

    const texts = [];
    const items = [];
    for (let i = 1; i <= inst.doc.numPages; i++) {
        const page = await inst.doc.getPage(i);
        const content = await page.getTextContent();
        // Build a flat string for each page (used for quick count).
        // Separate items with a space so words at item boundaries are not merged.
        texts.push(content.items.map(it => it.str || '').join(' '));

        // Extract positioned items for overlay computation.
        // pdf.js transform: [scaleX, skewY, skewX, scaleY, tx, ty]
        // tx/ty are the TEXT BASELINE in PDF user units (Y origin at bottom-left of page).
        // We also need the page height to flip the Y axis to top-left CSS coords later.
        const viewport = page.getViewport({ scale: 1 });
        const pageHeight = viewport.height;
        // IMPORTANT: include ALL items (even empty-string ones) so that buildCharMap
        // produces a charMap whose indices align 1-to-1 with the flatText built by
        // join(' ') above.  Each item — empty or not — contributes its characters
        // plus one null separator.  Filtering out empty items here caused the charMap
        // to be shorter than flatText by one slot per empty item, so matches landed
        // on the wrong text item (typically the line below the intended one).
        const pageItems = content.items.map(it => {
            const [, , , scaleY, tx, ty] = it.transform;
            const w = it.width || 0;
            // it.height from pdf.js is the ascent (height above the baseline) of the
            // rendered glyphs in PDF user units.  Fall back to 0.8×|scaleY| if absent.
            const ascent = it.height > 0 ? it.height : Math.abs(scaleY) * 0.8;
            // Small descent below baseline (roughly 0.2× font size) so descenders
            // like 'g','p','y' are fully covered.
            const descent = Math.abs(scaleY) * 0.2;
            const h = ascent + descent;
            // ty is the baseline.  Top-of-glyph in CSS (Y down from page top) =
            // pageHeight - ty - ascent.
            const x = tx;
            const y = pageHeight - ty - ascent;
            return { str: it.str || '', x, y, w, h };
        });
        items.push({ pageHeight, items: pageItems });
    }
    inst.pageTexts = texts;
    inst.pageTextItems = items;
}

// Build a charMap for a page's items: charMap[charIndex] = { item, localOffset }
// Mirrors the join(' ') logic used to build flatText.
// Empty-string items contribute no character entries but DO contribute the null
// separator (matching the space that join(' ') inserts for them) so that charMap
// indices stay in sync with flatText positions.
function buildCharMap(pageItems) {
    const charMap = [];
    for (const item of pageItems) {
        // Only push character entries for items that have actual text; empty items
        // still get the trailing null separator so flatText positions stay aligned.
        for (let c = 0; c < item.str.length; c++) {
            charMap.push({ item, localOffset: c });
        }
        // Space separator between items (matches the join(' ') in ensurePageData).
        charMap.push(null);
    }
    return charMap;
}

// Compute a tight bounding rect for the character range [pos, pos+len) in charMap.
// Uses per-character proportional widths within each item (uniform-width approximation)
// so the highlight covers only the matched characters, not the entire item bbox.
function computeMatchRect(charMap, pos, len) {
    const end = pos + len;

    // Collect the first and last character entries for each item involved.
    // itemSpans: Map<item, { firstLocal, lastLocal }>
    const itemSpans = new Map();
    for (let ci = pos; ci < end && ci < charMap.length; ci++) {
        const entry = charMap[ci];
        if (!entry) continue;
        const { item, localOffset } = entry;
        if (!itemSpans.has(item)) {
            itemSpans.set(item, { firstLocal: localOffset, lastLocal: localOffset });
        } else {
            const span = itemSpans.get(item);
            if (localOffset < span.firstLocal) span.firstLocal = localOffset;
            if (localOffset > span.lastLocal) span.lastLocal = localOffset;
        }
    }
    if (itemSpans.size === 0) return null;

    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const [item, { firstLocal, lastLocal }] of itemSpans) {
        const charCount = item.str.length || 1;
        const charW = item.w / charCount; // uniform-width approximation

        const itemStartX = item.x + firstLocal * charW;
        // +1 so we include the full last character's width.
        const itemEndX = item.x + (lastLocal + 1) * charW;

        minX = Math.min(minX, itemStartX);
        maxX = Math.max(maxX, itemEndX);
        minY = Math.min(minY, item.y);
        maxY = Math.max(maxY, item.y + item.h);
    }
    return { x: minX, y: minY, width: maxX - minX, height: maxY - minY };
}

// Returns { totalCount, pages: [{ pageNum, matches: [{x,y,width,height}] }] }
// Coordinates in PDF user-unit space (scale=1, Y from top).
export async function search(canvasId, query) {
    const inst = _instances.get(canvasId);
    if (!inst) return { totalCount: 0, pages: [] };
    if (!query || !query.trim()) return { totalCount: 0, pages: [] };

    await ensurePageData(inst);

    const q = query.toLowerCase();
    let totalCount = 0;
    const pages = [];

    for (let pageIndex = 0; pageIndex < inst.pageTexts.length; pageIndex++) {
        const pageNum = pageIndex + 1;
        const flatText = inst.pageTexts[pageIndex].toLowerCase();
        const pageMatches = [];

        // Find all occurrences in the flat text string.
        let from = 0;
        const positions = [];
        while ((from = flatText.indexOf(q, from)) !== -1) {
            positions.push(from);
            totalCount++;
            from += q.length;
        }

        if (positions.length > 0) {
            const { items: pageItems } = inst.pageTextItems[pageIndex];
            const charMap = buildCharMap(pageItems);

            for (const pos of positions) {
                const rect = computeMatchRect(charMap, pos, q.length);
                if (rect) pageMatches.push(rect);
            }

            if (pageMatches.length > 0) {
                pages.push({ pageNum, matches: pageMatches });
            }
        }
    }

    return { totalCount, pages };
}

// Returns match overlay rects scaled to CSS pixels for the current zoom of canvasId.
// Rects are in the coordinate space of the canvas's CSS dimensions (top-left origin).
export async function getMatchOverlays(canvasId, pageNum, query) {
    const inst = _instances.get(canvasId);
    if (!inst || !query || !query.trim()) return [];

    await ensurePageData(inst);

    const pageIndex = pageNum - 1;
    if (pageIndex < 0 || pageIndex >= inst.pageTextItems.length) return [];

    const zoom = inst.currentZoom || 1;
    const { items: pageItems } = inst.pageTextItems[pageIndex];
    const q = query.toLowerCase();

    // Use the flat text for position finding.
    const flatText = inst.pageTexts[pageIndex].toLowerCase();

    const positions = [];
    let from = 0;
    while ((from = flatText.indexOf(q, from)) !== -1) {
        positions.push(from);
        from += q.length;
    }
    if (positions.length === 0) return [];

    const charMap = buildCharMap(pageItems);

    const overlays = [];
    for (const pos of positions) {
        const rect = computeMatchRect(charMap, pos, q.length);
        if (!rect) continue;
        overlays.push({
            x: rect.x * zoom,
            y: rect.y * zoom,
            width: rect.width * zoom,
            height: rect.height * zoom,
        });
    }
    return overlays;
}

export function getDocumentUrl(canvasId) {
    const inst = _instances.get(canvasId);
    return inst ? inst.src : null;
}
