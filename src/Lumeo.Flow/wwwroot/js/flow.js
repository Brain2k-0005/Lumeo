// flow.js — the Lumeo.Flow canvas engine (FlowCanvas). ES module, no dependencies.
//
// Ownership contract (see FlowCanvas.razor for the .NET half):
//   * The viewport transform (the [data-slot="flow-viewport"] layer and every background
//     pattern) is written by THIS file after mount. .NET renders it once, for the first paint,
//     and from then on changes it only through the `data-flow-viewport="x|y|zoom|id"` stamp on the
//     pane, which the MutationObserver below applies in the same microtask checkpoint as the
//     render batch that carried it (the #385 lesson: the geometry and the transform that anchors
//     it must land in the same frame).
//   * Pan and wheel zoom are applied here, synchronously, in the event handler; .NET hears about
//     them afterwards through OnViewportChanged — at most once per animation frame, plus once more
//     with final = true when the gesture ends.
//   * A node drag moves the node element (CSS transform) and rewrites the `d` of every connected
//     edge path live. .NET is told once, on drop (CommitNodeDrag), with the generation the drag
//     started under; it answers whether it accepted the commit. On acceptance the next render
//     writes the same transform/path this file already shows; on rejection this file puts the
//     nodes and edges back where the DOM's data-x/data-y (the .NET truth) says they are.
//
// Path math is a port of FlowGeometry.cs and must stay in lockstep with it — see __testing at
// the bottom and tests/js/flow-geometry-table.mjs.

// ── Diagnostics journal ────────────────────────────────────────────────────
// Opt-in: set `window.__lumeoFlowDiag = true` (or to an array) before the module loads; every
// viewport write, report, drag start and commit is then appended there with a timestamp. Capped at
// 300 entries. Off by default — a single typeof/Array check per call site.
function diag(entry) {
    if (typeof window === 'undefined') return;
    let log = window.__lumeoFlowDiag;
    if (log === true) log = window.__lumeoFlowDiag = [];
    if (!Array.isArray(log)) return;
    entry.t = Math.round(performance.now() * 10) / 10;
    log.push(entry);
    if (log.length > 300) log.shift();
}

// ── Geometry (lockstep with FlowGeometry.cs) ───────────────────────────────
const DEFAULT_NODE_WIDTH = 150;
const DEFAULT_NODE_HEIGHT = 40;
const STEP_OFFSET = 20;
const SMOOTH_STEP_RADIUS = 5;
const BEZIER_CURVATURE = 0.25;

function fmt(v) {
    if (!Number.isFinite(v)) return '0';
    let r = Math.floor(v * 1000 + 0.5) / 1000;
    if (r === 0) r = 0; // normalises -0
    return String(r);
}

function clampZoom(zoom, min, max) {
    if (max < min) max = min;
    if (Number.isNaN(zoom)) return min;
    return Math.min(max, Math.max(min, zoom));
}

function snap(value, grid) {
    return grid > 0 ? Math.floor(value / grid + 0.5) * grid : value;
}

function screenToFlow(x, y, vp) {
    const zoom = vp.zoom > 0 ? vp.zoom : 1;
    return { x: (x - vp.x) / zoom, y: (y - vp.y) / zoom };
}

function zoomAt(vp, zoom, ax, ay) {
    const p = screenToFlow(ax, ay, vp);
    return { x: ax - p.x * zoom, y: ay - p.y * zoom, zoom };
}

function getBounds(rects) {
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity, any = false;
    for (const r of rects) {
        any = true;
        minX = Math.min(minX, r.x);
        minY = Math.min(minY, r.y);
        maxX = Math.max(maxX, r.x + r.width);
        maxY = Math.max(maxY, r.y + r.height);
    }
    return any ? { x: minX, y: minY, width: maxX - minX, height: maxY - minY } : null;
}

// LU-08: `anchor` (optional, a {x, y, width, height} rect) mirrors FlowGeometry.FitView's overload
// — when the plain fit would need a zoom below minZoom, centre on the anchor at minZoom instead of
// on the whole (still-clamped) bounds. Omitted/null keeps the plain behaviour.
function fitView(rects, paneWidth, paneHeight, padding, minZoom, maxZoom, anchor) {
    const b = getBounds(rects);
    if (!b || !(paneWidth > 0) || !(paneHeight > 0)) return null;
    const pad = padding > 0 ? padding : 0;
    const bw = Math.max(b.width, 1);
    const bh = Math.max(b.height, 1);
    const required = Math.min(paneWidth / (bw * (1 + pad)), paneHeight / (bh * (1 + pad)));
    if (anchor && required < minZoom) {
        const az = clampZoom(minZoom, minZoom, maxZoom);
        const acx = anchor.x + anchor.width / 2;
        const acy = anchor.y + anchor.height / 2;
        return { x: paneWidth / 2 - acx * az, y: paneHeight / 2 - acy * az, zoom: az };
    }
    const zoom = clampZoom(required, minZoom, maxZoom);
    const cx = b.x + b.width / 2;
    const cy = b.y + b.height / 2;
    return { x: paneWidth / 2 - cx * zoom, y: paneHeight / 2 - cy * zoom, zoom };
}

function handleAnchor(rect, position) {
    switch (position) {
        case 'left': return { x: rect.x, y: rect.y + rect.height / 2 };
        case 'right': return { x: rect.x + rect.width, y: rect.y + rect.height / 2 };
        case 'top': return { x: rect.x + rect.width / 2, y: rect.y };
        default: return { x: rect.x + rect.width / 2, y: rect.y + rect.height };
    }
}

function controlOffset(distance) {
    return distance >= 0 ? 0.5 * distance : BEZIER_CURVATURE * 25 * Math.sqrt(-distance);
}

function controlPoint(position, x1, y1, x2, y2) {
    switch (position) {
        case 'left': return [x1 - controlOffset(x1 - x2), y1];
        case 'right': return [x1 + controlOffset(x2 - x1), y1];
        case 'top': return [x1, y1 - controlOffset(y1 - y2)];
        default: return [x1, y1 + controlOffset(y2 - y1)];
    }
}

function straightPath(sx, sy, tx, ty) {
    return { d: 'M' + fmt(sx) + ',' + fmt(sy) + ' L' + fmt(tx) + ',' + fmt(ty), labelX: (sx + tx) / 2, labelY: (sy + ty) / 2 };
}

function bezierPath(sx, sy, sPos, tx, ty, tPos) {
    const [c1x, c1y] = controlPoint(sPos, sx, sy, tx, ty);
    const [c2x, c2y] = controlPoint(tPos, tx, ty, sx, sy);
    const d = 'M' + fmt(sx) + ',' + fmt(sy)
        + ' C' + fmt(c1x) + ',' + fmt(c1y)
        + ' ' + fmt(c2x) + ',' + fmt(c2y)
        + ' ' + fmt(tx) + ',' + fmt(ty);
    const labelX = sx * 0.125 + c1x * 0.375 + c2x * 0.375 + tx * 0.125;
    const labelY = sy * 0.125 + c1y * 0.375 + c2y * 0.375 + ty * 0.125;
    return { d, labelX, labelY };
}

function direction(position) {
    switch (position) {
        case 'left': return [-1, 0];
        case 'right': return [1, 0];
        case 'top': return [0, -1];
        default: return [0, 1];
    }
}

function dist(a, b) {
    const dx = b.x - a.x;
    const dy = b.y - a.y;
    return Math.sqrt(dx * dx + dy * dy);
}

function samePoint(a, b) {
    return Math.abs(a.x - b.x) < 1e-9 && Math.abs(a.y - b.y) < 1e-9;
}

function stepPoints(sx, sy, sPos, tx, ty, tPos, offset) {
    const [sdx, sdy] = direction(sPos);
    const [tdx, tdy] = direction(tPos);
    const s0 = { x: sx, y: sy };
    const t0 = { x: tx, y: ty };
    const s1 = { x: sx + sdx * offset, y: sy + sdy * offset };
    const t1 = { x: tx + tdx * offset, y: ty + tdy * offset };
    const sH = sPos === 'left' || sPos === 'right';
    const tH = tPos === 'left' || tPos === 'right';

    const raw = [s0, s1];
    if (sH && tH) {
        if (sPos === tPos) {
            const ex = sdx > 0 ? Math.max(s1.x, t1.x) : Math.min(s1.x, t1.x);
            raw.push({ x: ex, y: s1.y }, { x: ex, y: t1.y });
        } else if (sdx * (t1.x - s1.x) >= 0) {
            const mx = (s1.x + t1.x) / 2;
            raw.push({ x: mx, y: s1.y }, { x: mx, y: t1.y });
        } else {
            const my = (s1.y + t1.y) / 2;
            raw.push({ x: s1.x, y: my }, { x: t1.x, y: my });
        }
    } else if (!sH && !tH) {
        if (sPos === tPos) {
            const ey = sdy > 0 ? Math.max(s1.y, t1.y) : Math.min(s1.y, t1.y);
            raw.push({ x: s1.x, y: ey }, { x: t1.x, y: ey });
        } else if (sdy * (t1.y - s1.y) >= 0) {
            const my = (s1.y + t1.y) / 2;
            raw.push({ x: s1.x, y: my }, { x: t1.x, y: my });
        } else {
            const mx = (s1.x + t1.x) / 2;
            raw.push({ x: mx, y: s1.y }, { x: mx, y: t1.y });
        }
    } else if (sH) {
        raw.push({ x: t1.x, y: s1.y });
    } else {
        raw.push({ x: s1.x, y: t1.y });
    }
    raw.push(t1, t0);

    const dedup = [];
    for (const p of raw) {
        if (dedup.length > 0 && samePoint(dedup[dedup.length - 1], p)) continue;
        dedup.push(p);
    }
    const result = [];
    for (let i = 0; i < dedup.length; i++) {
        if (i > 0 && i < dedup.length - 1) {
            const a = result[result.length - 1];
            const b = dedup[i];
            const c = dedup[i + 1];
            const cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
            const dot = (b.x - a.x) * (c.x - b.x) + (b.y - a.y) * (c.y - b.y);
            if (Math.abs(cross) < 1e-9 && dot >= 0) continue;
        }
        result.push(dedup[i]);
    }
    return result;
}

function polylineMidpoint(pts) {
    let total = 0;
    for (let i = 1; i < pts.length; i++) total += dist(pts[i - 1], pts[i]);
    if (total <= 0) return [pts[0].x, pts[0].y];
    const half = total / 2;
    let walked = 0;
    for (let i = 1; i < pts.length; i++) {
        const seg = dist(pts[i - 1], pts[i]);
        if (walked + seg >= half && seg > 0) {
            const t = (half - walked) / seg;
            return [pts[i - 1].x + (pts[i].x - pts[i - 1].x) * t, pts[i - 1].y + (pts[i].y - pts[i - 1].y) * t];
        }
        walked += seg;
    }
    const last = pts[pts.length - 1];
    return [last.x, last.y];
}

function smoothStepPath(sx, sy, sPos, tx, ty, tPos, borderRadius = SMOOTH_STEP_RADIUS, offset = STEP_OFFSET) {
    const pts = stepPoints(sx, sy, sPos, tx, ty, tPos, offset);
    let d = 'M' + fmt(pts[0].x) + ',' + fmt(pts[0].y);
    for (let i = 1; i < pts.length - 1; i++) {
        const a = pts[i - 1];
        const b = pts[i];
        const c = pts[i + 1];
        const lab = dist(a, b);
        const lbc = dist(b, c);
        const r = Math.min(borderRadius, Math.min(lab / 2, lbc / 2));
        if (r > 0 && lab > 0 && lbc > 0) {
            const inX = b.x - (b.x - a.x) / lab * r;
            const inY = b.y - (b.y - a.y) / lab * r;
            const outX = b.x + (c.x - b.x) / lbc * r;
            const outY = b.y + (c.y - b.y) / lbc * r;
            d += ' L' + fmt(inX) + ',' + fmt(inY);
            d += ' Q' + fmt(b.x) + ',' + fmt(b.y) + ' ' + fmt(outX) + ',' + fmt(outY);
        } else {
            d += ' L' + fmt(b.x) + ',' + fmt(b.y);
        }
    }
    const last = pts[pts.length - 1];
    d += ' L' + fmt(last.x) + ',' + fmt(last.y);
    const [labelX, labelY] = polylineMidpoint(pts);
    return { d, labelX, labelY };
}

function edgePath(type, sx, sy, sPos, tx, ty, tPos) {
    switch (type) {
        case 'straight': return straightPath(sx, sy, tx, ty);
        case 'step': return smoothStepPath(sx, sy, sPos, tx, ty, tPos, 0, STEP_OFFSET);
        case 'smoothstep': return smoothStepPath(sx, sy, sPos, tx, ty, tPos);
        default: return bezierPath(sx, sy, sPos, tx, ty, tPos);
    }
}

// ── Engine ─────────────────────────────────────────────────────────────────
const registrations = new Map(); // pane element -> registration
const DRAG_THRESHOLD = 3;
const WHEEL_END_MS = 160;

function ensureCss() {
    if (typeof document === 'undefined') return;
    if (document.querySelector('link[data-lumeo-flow-css]')) return;
    for (const l of document.querySelectorAll('link[rel="stylesheet"]')) {
        if ((l.getAttribute('href') || '').indexOf('lumeo-flow.css') >= 0) return;
    }
    try {
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = new URL('../css/lumeo-flow.css', import.meta.url).href;
        link.setAttribute('data-lumeo-flow-css', '');
        document.head.appendChild(link);
    } catch { /* non-browser host */ }
}

const DEFAULT_STRINGS = {
    connectingFrom: 'Connecting from {0}. Press Enter on a target handle to connect, Escape to cancel.',
    connected: 'Connected.',
    connectionRejected: 'Connection rejected.',
    connectionCancelled: 'Connection cancelled.',
    reconnected: 'Edge reconnected.',
    reconnectRejected: 'Reconnect rejected.',
};

function normalizeOptions(o) {
    o = o || {};
    const snapArr = Array.isArray(o.snap) && o.snap.length === 2 ? [Number(o.snap[0]) || 0, Number(o.snap[1]) || 0] : null;
    const s = o.strings || {};
    return {
        minZoom: Number.isFinite(o.minZoom) ? o.minZoom : 0.25,
        maxZoom: Number.isFinite(o.maxZoom) ? o.maxZoom : 2,
        snap: snapArr,
        nodesDraggable: o.nodesDraggable !== false,
        panOnDrag: o.panOnDrag !== false,
        zoomOnScroll: o.zoomOnScroll !== false,
        selectionOnShiftDrag: o.selectionOnShiftDrag !== false,
        elementsSelectable: o.elementsSelectable !== false,
        connectable: o.connectable !== false,
        readonly: o.readonly === true,
        rtl: o.rtl === true,
        fitViewOnInit: o.fitViewOnInit === true,
        fitViewPadding: Number.isFinite(o.fitViewPadding) ? o.fitViewPadding : 0.1,
        reconnectable: o.reconnectable !== false,
        // Phase 4:
        connectionMode: o.connectionMode === 'loose' ? 'loose' : 'strict',
        helperLines: o.helperLines === true,
        helperLineThreshold: Number.isFinite(o.helperLineThreshold) && o.helperLineThreshold > 0 ? o.helperLineThreshold : 5,
        validateOnHover: o.validateOnHover === true,
        // Phase 5: OnlyRenderVisibleNodes. The DOM holds only the mounted window, so the engine never
        // fits from the DOM on init (.NET fits from the model instead).
        virtualized: o.virtualized === true,
        strings: {
            connectingFrom: typeof s.connectingFrom === 'string' && s.connectingFrom ? s.connectingFrom : DEFAULT_STRINGS.connectingFrom,
            connected: typeof s.connected === 'string' && s.connected ? s.connected : DEFAULT_STRINGS.connected,
            connectionRejected: typeof s.connectionRejected === 'string' && s.connectionRejected ? s.connectionRejected : DEFAULT_STRINGS.connectionRejected,
            connectionCancelled: typeof s.connectionCancelled === 'string' && s.connectionCancelled ? s.connectionCancelled : DEFAULT_STRINGS.connectionCancelled,
            reconnected: typeof s.reconnected === 'string' && s.reconnected ? s.reconnected : DEFAULT_STRINGS.reconnected,
            reconnectRejected: typeof s.reconnectRejected === 'string' && s.reconnectRejected ? s.reconnectRejected : DEFAULT_STRINGS.reconnectRejected,
        },
    };
}

function fmtTemplate(template, value) {
    return String(template).replace('{0}', value);
}

function call(reg, method, ...args) {
    if (!reg.dotNetRef) return Promise.resolve(undefined);
    try {
        return reg.dotNetRef.invokeMethodAsync(method, ...args);
    } catch (e) {
        return Promise.reject(e);
    }
}

function nodeElements(reg) {
    return reg.pane.querySelectorAll('[data-flow-node]');
}

function findNode(reg, id) {
    if (id == null) return null;
    // A native attribute lookup instead of a JS scan over every node: edge redraws call this twice
    // per connected edge per frame, which went quadratic on large (phase 5) graphs.
    return reg.pane.querySelector('[data-flow-node="' + escAttr(id) + '"]');
}

// The .NET truth for a node's position (data-x/data-y), unless a commit this file sent is still
// waiting for its render — then the committed position.
function nodePos(reg, el) {
    const id = el.getAttribute('data-flow-node');
    const pending = reg.pending.get(id);
    if (pending) return { x: pending.x, y: pending.y };
    return { x: Number(el.getAttribute('data-x')) || 0, y: Number(el.getAttribute('data-y')) || 0 };
}

function truthPos(el) {
    return { x: Number(el.getAttribute('data-x')) || 0, y: Number(el.getAttribute('data-y')) || 0 };
}

function nodeTransform(x, y) {
    return 'translate(' + x + 'px, ' + y + 'px)';
}

function nodeSize(el) {
    return { width: el.offsetWidth || DEFAULT_NODE_WIDTH, height: el.offsetHeight || DEFAULT_NODE_HEIGHT };
}

function measureHandles(reg, el) {
    const handles = [];
    const nodeRect = el.getBoundingClientRect();
    const zoom = reg.vp.zoom > 0 ? reg.vp.zoom : 1;
    for (const h of el.querySelectorAll('[data-flow-handle]')) {
        if (h.closest('[data-flow-node]') !== el) continue;
        const r = h.getBoundingClientRect();
        handles.push({
            id: h.getAttribute('data-handle-id') || null,
            type: h.getAttribute('data-handle-type') === 'target' ? 'target' : 'source',
            position: h.getAttribute('data-position') || 'right',
            x: Math.round(((r.left + r.width / 2 - nodeRect.left) / zoom) * 100) / 100,
            y: Math.round(((r.top + r.height / 2 - nodeRect.top) / zoom) * 100) / 100,
        });
    }
    return handles;
}

function measureNode(reg, el) {
    const id = el.getAttribute('data-flow-node');
    const { width, height } = nodeSize(el);
    const handles = measureHandles(reg, el);
    return { id, width, height, handles };
}

function sameMeasurement(a, b) {
    if (!a || !b || a.width !== b.width || a.height !== b.height || a.handles.length !== b.handles.length) return false;
    for (let i = 0; i < a.handles.length; i++) {
        const x = a.handles[i], y = b.handles[i];
        if (x.id !== y.id || x.type !== y.type || x.position !== y.position || x.x !== y.x || x.y !== y.y) return false;
    }
    return true;
}

// Mirrors FlowState.GetAnchor (LU-11): the handle with the EXPLICIT id, else the default side —
// right for a source, left for a target. A node simply having a handle must not silently change
// where an edge that never named a handle attaches.
function anchorFor(reg, el, handleId, type, pos) {
    const id = el.getAttribute('data-flow-node');
    let m = reg.measured.get(id);
    if (!m) {
        m = measureNode(reg, el);
        reg.measured.set(id, m);
    }
    if (handleId != null) {
        for (const h of m.handles) {
            if (h.type !== type) continue;
            if (h.id === handleId) {
                return { x: pos.x + h.x, y: pos.y + h.y, position: h.position };
            }
        }
    }
    const side = type === 'source' ? 'right' : 'left';
    const a = handleAnchor({ x: pos.x, y: pos.y, width: m.width, height: m.height }, side);
    return { x: a.x, y: a.y, position: side };
}

function edgeElements(reg) {
    // Includes each edge's invisible wide "hit" twin (data-flow-edge-hit) so it stays glued to
    // the visible path while a connected node is dragged, and (LU-04) a solid Animated edge's
    // decorative "flow" overlay (data-flow-edge-flow) so its dash keeps travelling along the
    // correct path during a drag too — see FlowEdgeLayer.razor.
    return reg.pane.querySelectorAll('[data-flow-edge], [data-flow-edge-hit], [data-flow-edge-flow]');
}

// Recomputes one edge path from the given position resolver (live drag positions or the truth).
function computeEdge(reg, edgeEl, posOf) {
    const s = findNode(reg, edgeEl.getAttribute('data-source'));
    const t = findNode(reg, edgeEl.getAttribute('data-target'));
    if (!s && !t) return null;
    // Phase 5 virtualization: an end whose node is not mounted keeps the anchor .NET stamped on the
    // path (data-sa / data-ta = "x|y|position") — it cannot move during this gesture anyway.
    const sa = s ? anchorFor(reg, s, edgeEl.getAttribute('data-source-handle'), 'source', posOf(s)) : stampedAnchor(edgeEl, 'data-sa');
    const ta = t ? anchorFor(reg, t, edgeEl.getAttribute('data-target-handle'), 'target', posOf(t)) : stampedAnchor(edgeEl, 'data-ta');
    if (!sa || !ta) return null;
    return edgePath(edgeEl.getAttribute('data-edge-type') || 'bezier', sa.x, sa.y, sa.position, ta.x, ta.y, ta.position);
}

function stampedAnchor(edgeEl, attr) {
    const raw = edgeEl.getAttribute(attr);
    if (!raw) return null;
    const parts = raw.split('|');
    const x = Number(parts[0]), y = Number(parts[1]);
    if (parts.length !== 3 || !Number.isFinite(x) || !Number.isFinite(y)) return null;
    return { x, y, position: parts[2] };
}

function escAttr(v) {
    return (typeof CSS !== 'undefined' && CSS.escape) ? CSS.escape(v) : String(v).replace(/"/g, '\\"');
}

// ── Handles: connect (pointer + keyboard), and node toolbars following a live drag ─────────────
function handleNode(el) {
    return el.closest('[data-flow-node]');
}

function handleIdOf(el) {
    return el.getAttribute('data-handle-id') || null;
}

function oppositePosition(pos) {
    switch (pos) {
        case 'left': return 'right';
        case 'right': return 'left';
        case 'top': return 'bottom';
        default: return 'top';
    }
}

// Structural compatibility only (Strict: type=target; Loose: any type; either way connectable and
// not the source's own handle) — the full IsValidConnection predicate lives in .NET and is
// evaluated once, on drop/commit, by CommitConnect (or per hover, when ValidateOnHover is on).
function isValidTargetCandidate(reg, sourceHandleEl, candidateEl) {
    if (!candidateEl || candidateEl === sourceHandleEl) return false;
    if (reg.options.connectionMode !== 'loose' && candidateEl.getAttribute('data-handle-type') !== 'target') return false;
    const node = handleNode(candidateEl);
    if (!node || node.getAttribute('data-connectable') === 'false') return false;
    return true;
}

function markValidTargets(reg, sourceHandleEl) {
    const selector = reg.options.connectionMode === 'loose' ? '[data-flow-handle]' : '[data-flow-handle][data-handle-type="target"]';
    for (const h of reg.pane.querySelectorAll(selector)) {
        if (isValidTargetCandidate(reg, sourceHandleEl, h)) h.setAttribute('data-flow-handle-valid', '');
    }
}

function clearValidTargets(reg) {
    for (const h of reg.pane.querySelectorAll('[data-flow-handle-valid]')) h.removeAttribute('data-flow-handle-valid');
    for (const h of reg.pane.querySelectorAll('[data-flow-handle-invalid]')) h.removeAttribute('data-flow-handle-invalid');
    reg.hoverValidateId = null;
    reg.lastHoverInvalidEl = null;
}

function ensureConnLine(reg) {
    const svg = reg.pane.querySelector('[data-slot="flow-edges"]');
    if (!svg) return;
    let el = svg.querySelector('[data-flow-connection-line]');
    if (!el) {
        el = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        el.setAttribute('data-flow-connection-line', '');
        el.setAttribute('fill', 'none');
        el.setAttribute('stroke-width', '1.5');
        el.setAttribute('stroke-dasharray', '4 3');
        el.setAttribute('pointer-events', 'none');
        svg.appendChild(el);
    }
    reg.connLine = el;
}

function removeConnLine(reg) {
    if (reg.connLine) {
        reg.connLine.remove();
        reg.connLine = null;
    }
}

function updateConnectionLine(reg, sourceNodeEl, sourceHandleId, clientX, clientY) {
    if (!reg.connLine) return;
    const srcPos = nodePos(reg, sourceNodeEl);
    const a = anchorFor(reg, sourceNodeEl, sourceHandleId, 'source', srcPos);
    const local = localPoint(reg, clientX, clientY);
    const p = screenToFlow(local.x, local.y, reg.vp);
    const path = edgePath('bezier', a.x, a.y, a.position, p.x, p.y, oppositePosition(a.position));
    reg.connLine.setAttribute('d', path.d);
}

function beginConnectGesture(reg, handleEl, base) {
    const nodeEl = handleNode(handleEl);
    if (!nodeEl) return;
    const g = Object.assign(base, {
        kind: 'connect', sourceHandleEl: handleEl, sourceNodeEl: nodeEl,
        sourceId: nodeEl.getAttribute('data-flow-node'), sourceHandleId: handleIdOf(handleEl),
    });
    reg.gesture = g;
    attachGestureListeners(reg);
    capture(reg, g.pointerId);
    markValidTargets(reg, handleEl);
    ensureConnLine(reg);
    updateConnectionLine(reg, nodeEl, g.sourceHandleId, base.startX, base.startY);
    diag({ ev: 'connect-start', source: g.sourceId, handle: g.sourceHandleId });
}

// Phase 4, ValidateOnHover only: while a connect drag is in flight, on each hovered target
// CHANGE (never redundantly — reg.hoverValidateId dedupes), ask .NET to run IsValidConnection
// and mark the candidate data-flow-handle-invalid when it would be rejected. One round trip per
// hover change, not per pointermove frame; a stale response (the hover moved on again before it
// came back) is dropped by comparing reg.hoverValidateId.
function updateHoverValidation(reg, g, clientX, clientY) {
    if (!reg.options.validateOnHover) return;
    const el = document.elementFromPoint(clientX, clientY);
    const candidate = el ? el.closest('[data-flow-handle]') : null;
    const ok = candidate && isValidTargetCandidate(reg, g.sourceHandleEl, candidate);
    const key = ok ? candidate.getAttribute('data-handle-type') + '|' + (handleIdOf(candidate) || '') + '|' + handleNode(candidate).getAttribute('data-flow-node') : null;
    if (key === reg.hoverValidateId) return;
    if (reg.lastHoverInvalidEl) { reg.lastHoverInvalidEl.removeAttribute('data-flow-handle-invalid'); reg.lastHoverInvalidEl = null; }
    reg.hoverValidateId = key;
    if (!ok) return;
    const targetNode = handleNode(candidate);
    call(reg, 'ValidateHoveredConnection', g.sourceId, g.sourceHandleId, targetNode.getAttribute('data-flow-node'), handleIdOf(candidate))
        .then(valid => {
            if (reg.hoverValidateId !== key) return; // a newer hover already superseded this response
            if (valid !== true) {
                candidate.setAttribute('data-flow-handle-invalid', '');
                reg.lastHoverInvalidEl = candidate;
            }
        }).catch(() => { });
}

function endConnectGesture(reg, g, clientX, clientY) {
    removeConnLine(reg);
    clearValidTargets(reg);
    const el = document.elementFromPoint(clientX, clientY);
    const targetHandle = el ? el.closest('[data-flow-handle]') : null;
    if (!targetHandle || !isValidTargetCandidate(reg, g.sourceHandleEl, targetHandle)) {
        diag({ ev: 'connect-end', hasTarget: false });
        return;
    }
    const targetNode = handleNode(targetHandle);
    const targetId = targetNode.getAttribute('data-flow-node');
    const targetHandleId = handleIdOf(targetHandle);
    diag({ ev: 'connect-end', hasTarget: true, target: targetId });
    call(reg, 'CommitConnect', g.sourceId, g.sourceHandleId, targetId, targetHandleId)
        .then(accepted => diag({ ev: 'connect-result', accepted: accepted === true }))
        .catch(() => { });
}

// ── Reconnect: dragging an existing selected edge's end onto a different handle ─────────────────
// Mirrors the connect gesture above: a temp connection-line from the edge's FIXED end to the
// pointer, valid-target highlighting (this time restricted to handles of the SAME type as the end
// being moved — you move a target end onto a target handle, a source end onto a source handle),
// CommitReconnect on drop, Escape cancels.
function edgeEndOf(reg, edgeId) {
    // The visible path carries the canonical data-* attributes (the hit twin mirrors them).
    for (const el of reg.pane.querySelectorAll('[data-flow-edge][data-edge-id]')) {
        if (el.getAttribute('data-edge-id') === edgeId) return el;
    }
    return null;
}

function markValidReconnectTargets(reg, wantType, excludeHandleEl) {
    for (const h of reg.pane.querySelectorAll('[data-flow-handle][data-handle-type="' + wantType + '"]')) {
        if (h === excludeHandleEl) continue;
        const node = handleNode(h);
        if (!node || node.getAttribute('data-connectable') === 'false') continue;
        h.setAttribute('data-flow-handle-valid', '');
    }
}

function isValidReconnectCandidate(reg, wantType, excludeHandleEl, candidateEl) {
    if (!candidateEl) return false;
    if (candidateEl.getAttribute('data-handle-type') !== wantType) return false;
    const node = handleNode(candidateEl);
    if (!node || node.getAttribute('data-connectable') === 'false') return false;
    return true;
}

function updateReconnectLine(reg, fixedPoint, clientX, clientY) {
    if (!reg.connLine) return;
    const local = localPoint(reg, clientX, clientY);
    const p = screenToFlow(local.x, local.y, reg.vp);
    // The temp line always runs from the fixed end to the pointer, drawn as a straight-ish bezier
    // (the fixed end's own handle position is used for direction; the pointer end has no "position"
    // of its own, so it points back at the fixed end — the same trick the connect preview uses).
    const path = edgePath('bezier', fixedPoint.x, fixedPoint.y, fixedPoint.position, p.x, p.y, oppositePosition(fixedPoint.position));
    reg.connLine.setAttribute('d', path.d);
}

function beginReconnectGesture(reg, endEl, base) {
    const edgeId = endEl.getAttribute('data-edge-id');
    const movingEnd = endEl.getAttribute('data-end'); // 'source' or 'target'
    const edgeEl = edgeEndOf(reg, edgeId);
    if (!edgeEl) return;
    const fixedType = movingEnd === 'source' ? 'target' : 'source';
    const fixedNodeId = edgeEl.getAttribute(fixedType === 'source' ? 'data-source' : 'data-target');
    const fixedHandleId = edgeEl.getAttribute(fixedType === 'source' ? 'data-source-handle' : 'data-target-handle');
    const fixedNodeEl = findNode(reg, fixedNodeId);
    if (!fixedNodeEl) return;
    const fixedAnchor = anchorFor(reg, fixedNodeEl, fixedHandleId, fixedType, nodePos(reg, fixedNodeEl));

    const g = Object.assign(base, {
        kind: 'reconnect', edgeId, movingEnd, wantType: movingEnd, fixedPoint: fixedAnchor,
    });
    reg.gesture = g;
    attachGestureListeners(reg);
    capture(reg, g.pointerId);
    markValidReconnectTargets(reg, g.wantType, endEl);
    ensureConnLine(reg);
    updateReconnectLine(reg, fixedAnchor, base.startX, base.startY);
    diag({ ev: 'reconnect-start', edge: edgeId, movingEnd });
}

function endReconnectGesture(reg, g, clientX, clientY) {
    removeConnLine(reg);
    clearValidTargets(reg);
    const el = document.elementFromPoint(clientX, clientY);
    const targetHandle = el ? el.closest('[data-flow-handle]') : null;
    if (!isValidReconnectCandidate(reg, g.wantType, null, targetHandle)) {
        diag({ ev: 'reconnect-end', hasTarget: false });
        return;
    }
    const targetNode = handleNode(targetHandle);
    const newNodeId = targetNode.getAttribute('data-flow-node');
    const newHandleId = handleIdOf(targetHandle);
    diag({ ev: 'reconnect-end', hasTarget: true, target: newNodeId });
    call(reg, 'CommitReconnect', g.edgeId, g.movingEnd, newNodeId, newHandleId)
        .then(accepted => {
            diag({ ev: 'reconnect-result', accepted: accepted === true });
            announce(reg, accepted === true ? reg.options.strings.reconnected : reg.options.strings.reconnectRejected);
        })
        .catch(() => { });
}

function ensureLive(reg) {
    if (reg.liveEl && reg.liveEl.isConnected) return reg.liveEl;
    const el = document.createElement('div');
    el.setAttribute('data-flow-live', '');
    el.setAttribute('aria-live', 'polite');
    el.style.position = 'absolute';
    el.style.width = '1px';
    el.style.height = '1px';
    el.style.overflow = 'hidden';
    el.style.clip = 'rect(0,0,0,0)';
    el.style.whiteSpace = 'nowrap';
    reg.pane.appendChild(el);
    reg.liveEl = el;
    return el;
}

function announce(reg, text) {
    ensureLive(reg).textContent = text;
}

// Keyboard connect: Enter/Space on a source handle enters "connecting" mode; Enter/Space on a
// compatible target commits. Handled entirely here (a pane-level keydown listener catches the
// bubbled event from any focused handle) so there is no render round trip per keystroke.
function handleConnectKey(reg, handleEl) {
    const o = reg.options;
    if (o.readonly || !o.connectable) return;
    if (!reg.kbConnect) {
        if (o.connectionMode !== 'loose' && handleEl.getAttribute('data-handle-type') !== 'source') return;
        const node = handleNode(handleEl);
        if (!node || node.getAttribute('data-connectable') === 'false') return;
        reg.kbConnect = { sourceHandleEl: handleEl, sourceId: node.getAttribute('data-flow-node'), sourceHandleId: handleIdOf(handleEl) };
        markValidTargets(reg, handleEl);
        handleEl.setAttribute('data-flow-connecting', '');
        announce(reg, fmtTemplate(reg.options.strings.connectingFrom, reg.kbConnect.sourceId));
        diag({ ev: 'kbconnect-start', source: reg.kbConnect.sourceId });
        return;
    }
    if (handleEl === reg.kbConnect.sourceHandleEl) {
        cancelKbConnect(reg, 'reselect-source');
        return;
    }
    if (!isValidTargetCandidate(reg, reg.kbConnect.sourceHandleEl, handleEl)) return;
    const targetNode = handleNode(handleEl);
    const targetId = targetNode.getAttribute('data-flow-node');
    const targetHandleId = handleIdOf(handleEl);
    const src = reg.kbConnect;
    finishKbConnect(reg);
    call(reg, 'CommitConnect', src.sourceId, src.sourceHandleId, targetId, targetHandleId).then(accepted => {
        announce(reg, accepted === true ? reg.options.strings.connected : reg.options.strings.connectionRejected);
        diag({ ev: 'kbconnect-result', accepted: accepted === true });
    }, () => { });
}

function finishKbConnect(reg) {
    if (!reg.kbConnect) return;
    reg.kbConnect.sourceHandleEl.removeAttribute('data-flow-connecting');
    clearValidTargets(reg);
    reg.kbConnect = null;
}

function cancelKbConnect(reg, reason) {
    if (!reg.kbConnect) return;
    finishKbConnect(reg);
    announce(reg, reg.options.strings.connectionCancelled);
    diag({ ev: 'kbconnect-cancel', reason });
}

// ── Marquee (shift-drag) selection ──────────────────────────────────────────
function createMarqueeRect(reg) {
    const el = document.createElement('div');
    el.setAttribute('data-flow-marquee', '');
    el.style.position = 'absolute';
    el.style.zIndex = '999';
    el.style.pointerEvents = 'none';
    reg.pane.appendChild(el);
    diag({ ev: 'marquee-start' });
    return el;
}

function rectFromPoints(reg, x0, y0, x1, y1) {
    const p0 = localPoint(reg, x0, y0);
    const p1 = localPoint(reg, x1, y1);
    return { left: Math.min(p0.x, p1.x), top: Math.min(p0.y, p1.y), width: Math.abs(p1.x - p0.x), height: Math.abs(p1.y - p0.y) };
}

function updateMarqueeRect(reg, g, clientX, clientY) {
    const r = rectFromPoints(reg, g.startX, g.startY, clientX, clientY);
    g.rectEl.style.left = r.left + 'px';
    g.rectEl.style.top = r.top + 'px';
    g.rectEl.style.width = r.width + 'px';
    g.rectEl.style.height = r.height + 'px';
    g.lastRect = r;
}

function rectsIntersect(a, b) {
    return a.x < b.x + b.width && a.x + a.width > b.x && a.y < b.y + b.height && a.y + a.height > b.y;
}

function endMarquee(reg, g) {
    if (!g.rectEl) return;
    const r = g.lastRect || { left: 0, top: 0, width: 0, height: 0 };
    g.rectEl.remove();
    const p0 = screenToFlow(r.left, r.top, reg.vp);
    const p1 = screenToFlow(r.left + r.width, r.top + r.height, reg.vp);
    const flowRect = { x: Math.min(p0.x, p1.x), y: Math.min(p0.y, p1.y), width: Math.abs(p1.x - p0.x), height: Math.abs(p1.y - p0.y) };
    const ids = [];
    for (const el of nodeElements(reg)) {
        if (el.getAttribute('data-selectable') === 'false') continue;
        const p = nodePos(reg, el);
        const { width, height } = nodeSize(el);
        if (rectsIntersect(flowRect, { x: p.x, y: p.y, width, height })) ids.push(el.getAttribute('data-flow-node'));
    }
    diag({ ev: 'marquee-end', count: ids.length });
    call(reg, 'CommitMarquee', ids).catch(() => { });
    try { reg.pane.focus({ preventScroll: true }); } catch { /* ignore */ }
}

// Repositions every node toolbar whose target node is part of the drag in flight — mirrors the
// edge redraw above; FlowNodeToolbar computes its own screen position on every Blazor render, this
// keeps it glued to the node between renders while a pointer drag is live.
function redrawToolbars(reg, g) {
    for (const el of reg.pane.querySelectorAll('[data-flow-toolbar-for]')) {
        const id = el.getAttribute('data-flow-toolbar-for');
        if (!g.ids || !g.ids.has(id)) continue;
        const it = g.items.find(i => i.id === id);
        if (!it) continue;
        const { width, height } = nodeSize(it.el);
        const zoom = reg.vp.zoom > 0 ? reg.vp.zoom : 1;
        const cx = (it.x + width / 2) * zoom + reg.vp.x;
        const isBottom = el.getAttribute('data-flow-toolbar-position') === 'bottom';
        const edgeY = isBottom ? it.y + height : it.y;
        el.style.left = cx + 'px';
        el.style.top = (edgeY * zoom + reg.vp.y) + 'px';
    }
}

// ── Resize (phase 4): pointer-drag grips from FlowNodeResizer ──────────────
// Mirrors the node-drag gesture above (live transform, committed once on drop, restored from
// truth on rejection/cancel) but changes WIDTH/HEIGHT (and, for a west/north-facing grip, X/Y)
// instead of position, and Shift keeps a corner grip's base aspect ratio. Connected edges are
// redrawn against the LIVE rect through anchorForResize/redrawEdgesForResize below rather than
// the generic computeEdge/redrawEdges pair — a resizing node's handles sit at fixed physical
// sides (FlowHandle's Position, never a free offset), so re-deriving their anchor from the live
// (x, y, width, height) via the same handleAnchor() the default side anchor already uses is
// exact, not an approximation, and needs no live re-measurement mid-gesture.
function anchorForResize(reg, g, el, handleId, type) {
    const id = el.getAttribute('data-flow-node');
    if (id !== g.nodeEl.getAttribute('data-flow-node')) return anchorFor(reg, el, handleId, type, nodePos(reg, el));
    let m = reg.measured.get(id);
    if (!m) m = measureNode(reg, el);
    let position = type === 'source' ? 'right' : 'left';
    if (handleId != null) {
        for (const h of m.handles) {
            if (h.type !== type) continue;
            if (h.id === handleId) { position = h.position; break; }
        }
    }
    const a = handleAnchor({ x: g.x, y: g.y, width: g.w, height: g.h }, position);
    return { x: a.x, y: a.y, position };
}

function redrawEdgesForResize(reg, g) {
    for (const e of g.edges) {
        const s = findNode(reg, e.getAttribute('data-source'));
        const t = findNode(reg, e.getAttribute('data-target'));
        const sa = s ? anchorForResize(reg, g, s, e.getAttribute('data-source-handle'), 'source') : stampedAnchor(e, 'data-sa');
        const ta = t ? anchorForResize(reg, g, t, e.getAttribute('data-target-handle'), 'target') : stampedAnchor(e, 'data-ta');
        if (!sa || !ta) continue;
        const p = edgePath(e.getAttribute('data-edge-type') || 'bezier', sa.x, sa.y, sa.position, ta.x, ta.y, ta.position);
        if (e.getAttribute('d') !== p.d) e.setAttribute('d', p.d);
        const id = e.getAttribute('data-edge-id');
        if (!id) continue;
        const label = reg.pane.querySelector('[data-flow-edge-label][data-edge-id="' + escAttr(id) + '"]');
        if (label) { label.style.left = p.labelX + 'px'; label.style.top = p.labelY + 'px'; }
    }
}

function beginResizeGesture(reg, handleEl, base) {
    const resizerEl = handleEl.closest('[data-flow-resizer]');
    const nodeEl = handleNode(handleEl);
    if (!resizerEl || !nodeEl) return;
    const pos = nodePos(reg, nodeEl);
    const { width, height } = nodeSize(nodeEl);
    const maxWAttr = resizerEl.getAttribute('data-max-width');
    const maxHAttr = resizerEl.getAttribute('data-max-height');
    const g = Object.assign(base, {
        kind: 'resize', nodeEl, dir: handleEl.getAttribute('data-resize-dir'),
        baseX: pos.x, baseY: pos.y, baseW: width, baseH: height,
        x: pos.x, y: pos.y, w: width, h: height,
        minW: Number(resizerEl.getAttribute('data-min-width')) || 0,
        minH: Number(resizerEl.getAttribute('data-min-height')) || 0,
        maxW: maxWAttr ? Number(maxWAttr) : null,
        maxH: maxHAttr ? Number(maxHAttr) : null,
        hadWidthStyle: nodeEl.style.width,
        hadHeightStyle: nodeEl.style.height,
        generation: Number(reg.pane.getAttribute('data-flow-generation')) || 0,
        edges: [],
    });
    const id = nodeEl.getAttribute('data-flow-node');
    g.edges = Array.from(edgeElements(reg)).filter(e => e.getAttribute('data-source') === id || e.getAttribute('data-target') === id);
    reg.gesture = g;
    attachGestureListeners(reg);
    capture(reg, g.pointerId);
    nodeEl.setAttribute('data-resizing', '');
    diag({ ev: 'resize-start', id, dir: g.dir });
}

function updateResize(reg, g, clientX, clientY, keepAspect) {
    const zoom = reg.vp.zoom > 0 ? reg.vp.zoom : 1;
    const dx = (clientX - g.startX) / zoom;
    const dy = (clientY - g.startY) / zoom;
    const dir = g.dir;
    const growsRight = dir === 'ne' || dir === 'e' || dir === 'se';
    const growsLeft = dir === 'nw' || dir === 'w' || dir === 'sw';
    const growsDown = dir === 'sw' || dir === 's' || dir === 'se';
    const growsUp = dir === 'nw' || dir === 'n' || dir === 'ne';

    let w = g.baseW + (growsRight ? dx : growsLeft ? -dx : 0);
    let h = g.baseH + (growsDown ? dy : growsUp ? -dy : 0);

    if (keepAspect && g.baseW > 0 && g.baseH > 0 && (growsRight || growsLeft) && (growsUp || growsDown)) {
        // A corner grip with Shift held: keep the base aspect ratio, driven by whichever axis moved further.
        const ratio = g.baseW / g.baseH;
        if (Math.abs(dx) > Math.abs(dy)) h = w / ratio; else w = h * ratio;
    }

    w = Math.max(g.minW || 0, w);
    if (g.maxW != null) w = Math.min(g.maxW, w);
    h = Math.max(g.minH || 0, h);
    if (g.maxH != null) h = Math.min(g.maxH, h);

    g.x = growsLeft ? g.baseX + (g.baseW - w) : g.baseX;
    g.y = growsUp ? g.baseY + (g.baseH - h) : g.baseY;
    g.w = w;
    g.h = h;

    g.nodeEl.style.width = w + 'px';
    g.nodeEl.style.height = h + 'px';
    g.nodeEl.style.transform = nodeTransform(g.x, g.y);
    redrawEdgesForResize(reg, g);
}

function restoreResize(reg, g) {
    g.nodeEl.style.width = g.hadWidthStyle;
    g.nodeEl.style.height = g.hadHeightStyle;
    if (g.nodeEl.isConnected) {
        const p = truthPos(g.nodeEl);
        g.nodeEl.style.transform = nodeTransform(p.x, p.y);
    }
    redrawEdges(reg, g.edges.filter(e => e.isConnected), el => nodePos(reg, el));
}

function commitResize(reg, g) {
    g.nodeEl.removeAttribute('data-resizing');
    const id = g.nodeEl.getAttribute('data-flow-node');
    diag({ ev: 'resize-commit', id, x: g.x, y: g.y, w: g.w, h: g.h, generation: g.generation });
    call(reg, 'CommitNodeResize', id, g.x, g.y, g.w, g.h, g.generation).then(accepted => {
        diag({ ev: 'resize-result', accepted: accepted === true });
        if (accepted !== true) restoreResize(reg, g);
    }, () => restoreResize(reg, g));
}

// ── Helper lines (phase 4): live alignment guides while dragging ONE node ──
// A pure-JS port of FlowGeometry.ComputeHelperLines, kept in lockstep by hand (no automated
// JS/C# table for this one — see the Phase 4 DESIGN.md note). Multi-node drags never compute
// helper lines (which node would be "the" moving one is ambiguous); SnapToGrid, when also on,
// runs first in moveNodes() and this may then override it.
function computeHelperLines(moving, others, threshold) {
    let bestVLine = null, bestHLine = null, snapX = null, snapY = null;
    let bestVDist = Infinity, bestHDist = Infinity;
    const movingXs = [moving.x, moving.x + moving.width / 2, moving.x + moving.width];
    const movingYs = [moving.y, moving.y + moving.height / 2, moving.y + moving.height];
    for (const other of others) {
        for (const ox of [other.x, other.x + other.width / 2, other.x + other.width]) {
            for (const mx of movingXs) {
                const d = Math.abs(mx - ox);
                if (d > threshold || d >= bestVDist) continue;
                bestVDist = d; bestVLine = ox; snapX = moving.x + (ox - mx);
            }
        }
        for (const oy of [other.y, other.y + other.height / 2, other.y + other.height]) {
            for (const my of movingYs) {
                const d = Math.abs(my - oy);
                if (d > threshold || d >= bestHDist) continue;
                bestHDist = d; bestHLine = oy; snapY = moving.y + (oy - my);
            }
        }
    }
    return { snapX, snapY, lines: [bestVLine != null ? { position: bestVLine, axis: 'vertical' } : null, bestHLine != null ? { position: bestHLine, axis: 'horizontal' } : null].filter(Boolean) };
}

function ensureHelperLineEls(reg) {
    const svg = reg.pane.querySelector('[data-slot="flow-edges"]');
    if (!svg) return {};
    let v = svg.querySelector('[data-flow-helper-line="vertical"]');
    let h = svg.querySelector('[data-flow-helper-line="horizontal"]');
    const make = (axis) => {
        const el = document.createElementNS('http://www.w3.org/2000/svg', 'line');
        el.setAttribute('data-flow-helper-line', axis);
        el.setAttribute('stroke', 'var(--color-primary)');
        el.setAttribute('stroke-width', '1');
        el.setAttribute('stroke-dasharray', '4 3');
        el.setAttribute('pointer-events', 'none');
        svg.appendChild(el);
        return el;
    };
    if (!v) v = make('vertical');
    if (!h) h = make('horizontal');
    return { v, h };
}

// A span far larger than any realistic flow-coordinate canvas, so the line always crosses the
// visible pane regardless of pan/zoom — the pane itself clips it (overflow: hidden).
const HELPER_LINE_SPAN = 100000;

function drawHelperLines(reg, result) {
    const { v, h } = ensureHelperLineEls(reg);
    const vLine = result.lines.find(l => l.axis === 'vertical');
    const hLine = result.lines.find(l => l.axis === 'horizontal');
    if (v) {
        if (vLine) {
            v.setAttribute('x1', String(vLine.position)); v.setAttribute('x2', String(vLine.position));
            v.setAttribute('y1', String(-HELPER_LINE_SPAN)); v.setAttribute('y2', String(HELPER_LINE_SPAN));
            v.style.display = '';
        } else v.style.display = 'none';
    }
    if (h) {
        if (hLine) {
            h.setAttribute('y1', String(hLine.position)); h.setAttribute('y2', String(hLine.position));
            h.setAttribute('x1', String(-HELPER_LINE_SPAN)); h.setAttribute('x2', String(HELPER_LINE_SPAN));
            h.style.display = '';
        } else h.style.display = 'none';
    }
}

function clearHelperLines(reg) {
    for (const el of reg.pane.querySelectorAll('[data-flow-helper-line]')) el.remove();
}

// ── Viewport ───────────────────────────────────────────────────────────────
function applyViewport(reg, vp, source) {
    reg.vp = { x: vp.x, y: vp.y, zoom: vp.zoom };
    const layer = reg.pane.querySelector('[data-slot="flow-viewport"]');
    if (layer) layer.style.transform = 'translate(' + vp.x + 'px, ' + vp.y + 'px) scale(' + vp.zoom + ')';
    applyPatterns(reg);
    diag({ ev: 'viewport-apply', src: source, x: vp.x, y: vp.y, zoom: vp.zoom });
}

function applyPatterns(reg) {
    const v = reg.vp;
    const value = 'translate(' + v.x + ',' + v.y + ') scale(' + v.zoom + ')';
    for (const p of reg.pane.querySelectorAll('[data-flow-background-pattern]')) {
        if (p.getAttribute('patternTransform') !== value) p.setAttribute('patternTransform', value);
    }
}

function sendReport(reg, final) {
    const v = reg.vp;
    const last = reg.lastReported;
    if (!final && last && last.x === v.x && last.y === v.y && last.zoom === v.zoom) return;
    reg.lastReported = { x: v.x, y: v.y, zoom: v.zoom };
    diag({ ev: 'report', x: v.x, y: v.y, zoom: v.zoom, final });
    call(reg, 'OnViewportChanged', v.x, v.y, v.zoom, final).catch(() => { });
}

function scheduleReport(reg) {
    if (reg.reportFrame) return;
    reg.reportFrame = requestAnimationFrame(() => {
        reg.reportFrame = 0;
        sendReport(reg, false);
    });
}

function reportFinal(reg) {
    if (reg.reportFrame) {
        cancelAnimationFrame(reg.reportFrame);
        reg.reportFrame = 0;
    }
    sendReport(reg, true);
}

function readStamp(pane) {
    const raw = pane.getAttribute('data-flow-viewport');
    if (!raw) return null;
    const parts = raw.split('|');
    if (parts.length !== 4) return null;
    const x = Number(parts[0]), y = Number(parts[1]), zoom = Number(parts[2]);
    if (!Number.isFinite(x) || !Number.isFinite(y) || !(zoom > 0)) return null;
    return { x, y, zoom, id: parts[3] };
}

function applyStamp(reg) {
    const s = readStamp(reg.pane);
    if (!s || s.id === reg.stampId) return;
    const initial = reg.stampId === null;
    reg.stampId = s.id;
    // An explicit .NET set after mount wins over a still-pending initial fit.
    if (!initial && s.id !== '0') reg.initialFitPending = false;
    applyViewport(reg, s, initial ? 'stamp-initial' : 'stamp');
    const g = reg.gesture;
    if (g && g.kind === 'pan') {
        // Rebase a pan in flight on the new viewport so it continues from there.
        g.vp0 = { x: s.x, y: s.y, zoom: s.zoom };
        g.startX = g.lastX;
        g.startY = g.lastY;
    }
    if (!initial) {
        reg.lastReported = { x: s.x, y: s.y, zoom: s.zoom };
    }
}

function domRects(reg) {
    const rects = [];
    for (const el of nodeElements(reg)) {
        const p = nodePos(reg, el);
        const { width, height } = nodeSize(el);
        rects.push({ x: p.x, y: p.y, width, height });
    }
    return rects;
}

// LU-08: the anchor node's own rect, straight from the DOM, or null when it isn't mounted.
function domRectById(reg, id) {
    if (!id) return null;
    const el = findNode(reg, id);
    if (!el) return null;
    const p = nodePos(reg, el);
    const { width, height } = nodeSize(el);
    return { x: p.x, y: p.y, width, height };
}

function fitFromDom(reg, padding, minZoom, maxZoom, source, anchorNodeId) {
    const anchor = anchorNodeId ? domRectById(reg, anchorNodeId) : null;
    const vp = fitView(domRects(reg), reg.pane.clientWidth, reg.pane.clientHeight, padding, minZoom, maxZoom, anchor);
    if (!vp) return false;
    applyViewport(reg, vp, source);
    reportFinal(reg);
    // LU-08: OnFitView — additive to the ordinary OnViewportChanged(..., final: true) report above,
    // fired for every DOM-measured fit (initial fit-on-load included) so app code can await "the
    // fit actually happened" even when it did not call FitViewAsync itself.
    call(reg, 'FitCompleted', vp.x, vp.y, vp.zoom).catch(() => { });
    return true;
}

function markReady(reg) {
    if (reg.ready) return;
    reg.ready = true;
    reg.root.setAttribute('data-flow-ready', 'done');
    diag({ ev: 'ready' });
}

function tryInitialFit(reg) {
    if (!reg.initialFitPending) return;
    if (nodeElements(reg).length === 0 || !(reg.pane.clientWidth > 0)) {
        markReady(reg); // mounted; the fit waits for the first nodes
        return;
    }
    const o = reg.options;
    if (fitFromDom(reg, o.fitViewPadding, o.minZoom, o.maxZoom, 'fit-init')) {
        reg.initialFitPending = false;
        markReady(reg);
    }
}

// ── Measurement ────────────────────────────────────────────────────────────
function queueMeasure(reg, el) {
    reg.measureQueue.add(el);
    if (reg.measureFrame) return;
    reg.measureFrame = requestAnimationFrame(() => {
        reg.measureFrame = 0;
        flushMeasure(reg);
    });
}

function flushMeasure(reg) {
    const batch = [];
    for (const el of reg.measureQueue) {
        if (!el.isConnected) continue;
        const m = measureNode(reg, el);
        if (!m.id) continue;
        if (sameMeasurement(reg.measured.get(m.id), m) && reg.reportedMeasure.has(m.id)) continue;
        reg.measured.set(m.id, m);
        reg.reportedMeasure.add(m.id);
        batch.push(m);
    }
    reg.measureQueue.clear();
    if (batch.length > 0) {
        diag({ ev: 'measure', count: batch.length });
        call(reg, 'NodesMeasured', batch).catch(() => { });
    }
    tryInitialFit(reg);
}

function observeNodes(reg, root) {
    const list = [];
    if (root.nodeType !== 1) return list;
    if (root.hasAttribute('data-flow-node')) list.push(root);
    for (const el of root.querySelectorAll('[data-flow-node]')) list.push(el);
    for (const el of list) {
        if (reg.observed.has(el)) continue;
        reg.observed.add(el);
        reg.ro.observe(el);
    }
    return list;
}

// ── Gestures ───────────────────────────────────────────────────────────────
function isEditable(target) {
    return !!(target && target.closest && target.closest('input, textarea, select, [contenteditable="true"], [contenteditable=""]'));
}

// A keydown target the canvas' own global shortcuts (undo/redo, delete, arrow-nudge) must leave
// alone: an editable field, or anything the app opted out of gesture handling with
// data-flow-nodrag (the same attribute already used to exempt a region from node-drag/pan —
// reused here for the same "this is the app's own interactive content" meaning).
function isKeyboardExempt(target) {
    return !!(target && target.closest && target.closest('input, textarea, select, [contenteditable="true"], [contenteditable=""], [data-flow-nodrag]'));
}

// What document.activeElement is right now, scoped to this canvas' own pane (so a keydown handled
// by one canvas never looks at a DIFFERENT canvas' focused field). Backs
// FlowIsFocusedElementEditableAsync — the .NET half of the same guard (FlowCanvas.razor's
// HandlePaneKeyDownAsync/HandleNodeKeyDownAsync); KeyboardEventArgs carries no target of its own,
// so this is how the .NET side answers "was the real target editable" for the exact same keydown.
function isFocusedElementEditable(pane) {
    const el = document.activeElement;
    if (!el || el === document.body) return false;
    if (pane && pane.contains && !pane.contains(el)) return false;
    return isKeyboardExempt(el);
}

function localPoint(reg, clientX, clientY) {
    const r = reg.pane.getBoundingClientRect();
    return { x: clientX - r.left, y: clientY - r.top };
}

function swallowNextClick() {
    const handler = (ev) => {
        ev.stopPropagation();
        ev.preventDefault();
        cleanup();
    };
    const cleanup = () => {
        window.removeEventListener('click', handler, true);
        clearTimeout(timer);
    };
    window.addEventListener('click', handler, true);
    const timer = setTimeout(cleanup, 0);
}

function attachGestureListeners(reg) {
    window.addEventListener('pointermove', reg.onPointerMove, true);
    window.addEventListener('pointerup', reg.onPointerUp, true);
    window.addEventListener('pointercancel', reg.onPointerCancel, true);
}

function detachGestureListeners(reg) {
    window.removeEventListener('pointermove', reg.onPointerMove, true);
    window.removeEventListener('pointerup', reg.onPointerUp, true);
    window.removeEventListener('pointercancel', reg.onPointerCancel, true);
}

function capture(reg, pointerId) {
    try { reg.pane.setPointerCapture(pointerId); } catch { /* pointer already gone */ }
}

function release(reg, pointerId) {
    try {
        if (reg.pane.hasPointerCapture && reg.pane.hasPointerCapture(pointerId)) reg.pane.releasePointerCapture(pointerId);
    } catch { /* ignore */ }
}

// Phase 5 sub-flows: the mounted node elements indexed by id, plus each one's parent id
// (data-parent-id — already resolved by .NET: missing parents and cycles never appear here).
function nodeTree(reg) {
    const byId = new Map();
    const parentOf = new Map();
    for (const el of nodeElements(reg)) {
        const id = el.getAttribute('data-flow-node');
        byId.set(id, el);
        const pid = el.getAttribute('data-parent-id');
        if (pid) parentOf.set(id, pid);
    }
    return { byId, parentOf };
}

function hasAncestorIn(tree, id, set) {
    let cursor = tree.parentOf.get(id);
    let guard = 0;
    while (cursor != null && guard++ < 10000) {
        if (set.has(cursor)) return true;
        cursor = tree.parentOf.get(cursor);
    }
    return false;
}

function beginNodeDrag(reg, g) {
    const pane = reg.pane;
    let els;
    if (g.nodeEl.hasAttribute('data-selected')) {
        els = Array.from(pane.querySelectorAll('[data-flow-node][data-selected]'))
            .filter(el => el.getAttribute('data-draggable') !== 'false');
        if (!els.includes(g.nodeEl)) els.push(g.nodeEl);
    } else {
        els = [g.nodeEl];
    }
    // Phase 5: LEADERS are the dragged nodes that are not inside another dragged node; every
    // mounted descendant of a leader is a FOLLOWER that keeps its offset to that leader exactly
    // (never snapped, clamped or helper-lined on its own — its X/Y are relative to its parent, so
    // .NET sees an unchanged relative position on commit).
    const tree = nodeTree(reg);
    const selectedIds = new Set(els.map(el => el.getAttribute('data-flow-node')));
    const leaders = els.filter(el => !hasAncestorIn(tree, el.getAttribute('data-flow-node'), selectedIds));
    const ids = new Set();
    g.items = [];
    const leaderItems = new Map();
    for (const el of leaders) {
        const id = el.getAttribute('data-flow-node');
        ids.add(id);
        const p = nodePos(reg, el);
        const it = { id, el, rawX: p.x, rawY: p.y, x: p.x, y: p.y, x0: p.x, y0: p.y, z0: el.style.zIndex, leader: null };
        g.items.push(it);
        leaderItems.set(id, it);
    }
    if (tree.parentOf.size > 0) {
        for (const [id, el] of tree.byId) {
            if (ids.has(id)) continue;
            // The nearest leader above this node, if any.
            let cursor = tree.parentOf.get(id);
            let leader = null;
            let guard = 0;
            while (cursor != null && guard++ < 10000) {
                if (leaderItems.has(cursor)) { leader = leaderItems.get(cursor); break; }
                cursor = tree.parentOf.get(cursor);
            }
            if (!leader) continue;
            ids.add(id);
            const p = nodePos(reg, el);
            g.items.push({ id, el, rawX: p.x, rawY: p.y, x: p.x, y: p.y, x0: p.x, y0: p.y, z0: el.style.zIndex, leader });
        }
    }
    g.leaders = g.items.filter(it => !it.leader);
    g.tree = tree;
    g.ids = ids;
    g.edges = Array.from(edgeElements(reg)).filter(e => ids.has(e.getAttribute('data-source')) || ids.has(e.getAttribute('data-target')));
    for (const it of g.items) {
        it.el.setAttribute('data-dragging', '');
        it.el.style.zIndex = '1000';
    }
    pane.setAttribute('data-flow-dragging', '');
    capture(reg, g.pointerId);
    g.started = true;
    diag({ ev: 'drag-start', ids: Array.from(ids), generation: g.generation });
    call(reg, 'NodeDragStart', Array.from(ids)).catch(() => { });
}

function livePos(g, el) {
    const id = el.getAttribute('data-flow-node');
    if (g.ids && g.ids.has(id)) {
        const it = g.items.find(i => i.id === id);
        if (it) return { x: it.x, y: it.y };
    }
    return null;
}

function redrawEdges(reg, edges, posOf) {
    for (const e of edges) {
        const p = computeEdge(reg, e, posOf);
        if (!p) continue;
        if (e.getAttribute('d') !== p.d) e.setAttribute('d', p.d);
        const id = e.getAttribute('data-edge-id');
        if (!id) continue;
        const label = reg.pane.querySelector('[data-flow-edge-label][data-edge-id="' + escAttr(id) + '"]');
        if (label) {
            label.style.left = p.labelX + 'px';
            label.style.top = p.labelY + 'px';
        }
    }
}

function moveNodes(reg, g, clientX, clientY) {
    const zoom = reg.vp.zoom > 0 ? reg.vp.zoom : 1;
    const dx = (clientX - g.lastX) / zoom;
    const dy = (clientY - g.lastY) / zoom;
    const snapGrid = g.opts.snap;
    for (const it of g.leaders) {
        it.rawX += dx;
        it.rawY += dy;
        it.x = snapGrid ? snap(it.rawX, snapGrid[0]) : it.rawX;
        it.y = snapGrid ? snap(it.rawY, snapGrid[1]) : it.rawY;
    }
    // Helper lines (phase 4): only for a single dragged node, against every OTHER (non-dragged)
    // node's rect — which node is "the" moving one is ambiguous for a multi-selection drag. Phase 5:
    // "single" means a single LEADER (a dragged group's children are followers, not candidates), and
    // with OnlyRenderVisibleNodes the candidates are the mounted nodes — everything within one
    // viewport of the visible area; a guide to a node further away would be drawn off-screen anyway.
    if (reg.options.helperLines && g.leaders.length === 1) {
        const it = g.leaders[0];
        const size = nodeSize(it.el);
        const others = [];
        for (const el of nodeElements(reg)) {
            if (g.ids.has(el.getAttribute('data-flow-node'))) continue;
            const p = nodePos(reg, el);
            const s = nodeSize(el);
            others.push({ x: p.x, y: p.y, width: s.width, height: s.height });
        }
        const result = computeHelperLines({ x: it.x, y: it.y, width: size.width, height: size.height }, others, reg.options.helperLineThreshold);
        if (result.snapX != null) it.x = result.snapX;
        if (result.snapY != null) it.y = result.snapY;
        drawHelperLines(reg, result);
    }
    // Phase 5: Extent=Parent keeps a leader inside its (non-dragged) parent's rect.
    for (const it of g.leaders) {
        if (it.el.getAttribute('data-extent') !== 'parent') continue;
        const parentEl = g.tree.byId.get(it.el.getAttribute('data-parent-id'));
        if (!parentEl) continue;
        const pp = nodePos(reg, parentEl);
        const ps = nodeSize(parentEl);
        const s = nodeSize(it.el);
        it.x = Math.min(Math.max(it.x, pp.x), pp.x + Math.max(0, ps.width - s.width));
        it.y = Math.min(Math.max(it.y, pp.y), pp.y + Math.max(0, ps.height - s.height));
    }
    for (const it of g.items) {
        if (it.leader) {
            it.x = it.leader.x + (it.x0 - it.leader.x0);
            it.y = it.leader.y + (it.y0 - it.leader.y0);
        }
        it.el.style.transform = nodeTransform(it.x, it.y);
    }
    redrawEdges(reg, g.edges, el => livePos(g, el) || nodePos(reg, el));
    redrawToolbars(reg, g);
}

function endNodeVisuals(reg, g) {
    for (const it of g.items) {
        it.el.removeAttribute('data-dragging');
        it.el.style.zIndex = it.z0 || '';
    }
    reg.pane.removeAttribute('data-flow-dragging');
    clearHelperLines(reg);
}

// Puts nodes and their edges back where the .NET truth (data-x/data-y) says they are.
function restoreFromTruth(reg, items, edges) {
    for (const it of items) {
        reg.pending.delete(it.id);
        if (!it.el.isConnected) continue;
        const p = truthPos(it.el);
        it.el.style.transform = nodeTransform(p.x, p.y);
    }
    redrawEdges(reg, edges.filter(e => e.isConnected), el => nodePos(reg, el));
}

function commitNodeDrag(reg, g) {
    endNodeVisuals(reg, g);
    const changes = g.items.map(it => ({ id: it.id, x: it.x, y: it.y }));
    for (const c of changes) reg.pending.set(c.id, { x: c.x, y: c.y });
    diag({ ev: 'commit', changes, generation: g.generation });
    const items = g.items;
    const edges = g.edges;
    call(reg, 'CommitNodeDrag', changes, g.generation).then(accepted => {
        diag({ ev: 'commit-result', accepted: accepted === true });
        if (accepted !== true) restoreFromTruth(reg, items, edges);
    }, () => {
        restoreFromTruth(reg, items, edges);
    });
}

function cancelGesture(reg, reason) {
    const g = reg.gesture;
    if (!g) return;
    reg.gesture = null;
    detachGestureListeners(reg);
    release(reg, g.pointerId);
    if (g.kind === 'node' && g.started) {
        endNodeVisuals(reg, g);
        restoreFromTruth(reg, g.items, g.edges);
        diag({ ev: 'drag-cancel', reason });
        call(reg, 'NodeDragCancelled').catch(() => { });
    } else if (g.kind === 'resize') {
        restoreResize(reg, g);
        diag({ ev: 'resize-cancel', reason });
    } else if (g.kind === 'pan' && g.moved) {
        reg.pane.removeAttribute('data-flow-panning');
        reportFinal(reg);
    } else if (g.kind === 'connect') {
        removeConnLine(reg);
        clearValidTargets(reg);
        diag({ ev: 'connect-cancel', reason });
    } else if (g.kind === 'reconnect') {
        removeConnLine(reg);
        clearValidTargets(reg);
        diag({ ev: 'reconnect-cancel', reason });
    } else if (g.kind === 'marquee') {
        if (g.rectEl) g.rectEl.remove();
        diag({ ev: 'marquee-cancel', reason });
    } else if (g.kind === 'pinch') {
        reg.touchPoints.clear();
        diag({ ev: 'pinch-cancel', reason });
    }
}

// ── Touch: two-finger pinch-zoom, anchored on the midpoint each frame ───────────────────────────
// One-finger pan/drag needs nothing extra — Pointer Events unify touch with mouse/pen, so the
// existing 'pan'/'node' gesture paths already handle a single touch. Node drag starts immediately
// (no long-press), matching React Flow's own default. touch-action: none on the pane (FlowCanvas.razor)
// keeps the browser from scrolling/zooming the page underneath either gesture.
function pinchMidpoint(reg) {
    const pts = Array.from(reg.touchPoints.values());
    if (pts.length < 2) return null;
    return { x: (pts[0].x + pts[1].x) / 2, y: (pts[0].y + pts[1].y) / 2 };
}

function pinchDistance(reg) {
    const pts = Array.from(reg.touchPoints.values());
    if (pts.length < 2) return 0;
    return Math.hypot(pts[1].x - pts[0].x, pts[1].y - pts[0].y);
}

function beginPinchGesture(reg) {
    const dist = pinchDistance(reg);
    reg.gesture = { kind: 'pinch', startDist: dist || 1, startZoom: reg.vp.zoom };
    attachGestureListeners(reg);
    diag({ ev: 'pinch-start', dist });
}

function makeHandlers(reg) {
    reg.onPointerDown = (e) => {
        if (e.pointerType === 'touch') {
            reg.touchPoints.set(e.pointerId, { x: e.clientX, y: e.clientY });
            if (reg.touchPoints.size === 2 && (!reg.gesture || reg.gesture.kind === 'pan')) {
                // A second finger lands: promote (or start fresh) into a pinch, dropping whatever
                // single-finger pan was in flight for the first one.
                if (reg.gesture) {
                    if (reg.gesture.kind === 'pan' && reg.gesture.moved) reg.pane.removeAttribute('data-flow-panning');
                    detachGestureListeners(reg);
                    release(reg, reg.gesture.pointerId);
                    reg.gesture = null;
                }
                beginPinchGesture(reg);
                return;
            }
            if (reg.touchPoints.size > 2) return; // a third finger: ignore, the pinch keeps running
        }
        if (reg.gesture) return;
        if (e.pointerType === 'mouse' && e.button !== 0) return;
        const target = e.target;
        if (!(target instanceof Element)) return;
        if (target.closest('[data-flow-overlay]')) return;
        const o = reg.options;
        const base = { pointerId: e.pointerId, startX: e.clientX, startY: e.clientY, lastX: e.clientX, lastY: e.clientY, opts: o };

        const endEl = !reg.spaceDown ? target.closest('[data-flow-edge-end]') : null;
        if (endEl) {
            if (o.readonly || !o.reconnectable) return;
            e.preventDefault();
            beginReconnectGesture(reg, endEl, base);
            return;
        }

        const resizeEl = !reg.spaceDown ? target.closest('[data-flow-resize-handle]') : null;
        if (resizeEl) {
            if (o.readonly) return;
            e.preventDefault();
            beginResizeGesture(reg, resizeEl, base);
            return;
        }

        const handleEl = !reg.spaceDown ? target.closest('[data-flow-handle]') : null;
        if (handleEl) {
            if (o.readonly || !o.connectable) return;
            if (o.connectionMode !== 'loose' && handleEl.getAttribute('data-handle-type') !== 'source') return;
            const hNode = handleNode(handleEl);
            if (!hNode || hNode.getAttribute('data-connectable') === 'false') return;
            e.preventDefault();
            beginConnectGesture(reg, handleEl, base);
            return;
        }

        // An edge path (visible or its wide invisible hit twin) or an edge label: don't start a
        // pan/marquee/pane-click gesture here — leave the pointerdown/click alone so the DOM click
        // reaches Blazor's own @onclick on the path (HandleEdgeClickAsync). Without this a plain
        // click on an edge fell through to the generic "background" branch below, which both
        // cancels the click via preventDefault() and synthesizes its own PaneClicked instead.
        const edgeEl = !reg.spaceDown ? target.closest('[data-flow-edge], [data-flow-edge-hit], [data-flow-edge-label]') : null;
        if (edgeEl) return;

        const nodeEl = target.closest('[data-flow-node]');
        if (nodeEl && !reg.spaceDown) {
            if (target.closest('[data-flow-nodrag]') || isEditable(target)) return;
            if (o.readonly || !o.nodesDraggable || nodeEl.getAttribute('data-draggable') === 'false') return;
            reg.gesture = Object.assign(base, {
                kind: 'node', nodeEl, started: false,
                generation: Number(reg.pane.getAttribute('data-flow-generation')) || 0,
            });
            attachGestureListeners(reg);
            return;
        }

        if (isEditable(target)) return;

        // Shift-drag on the empty background: a marquee selection when enabled, otherwise falls
        // through to a pan/pane-click like a plain drag would.
        if (e.shiftKey && o.selectionOnShiftDrag && o.elementsSelectable && !reg.spaceDown) {
            e.preventDefault();
            capture(reg, e.pointerId);
            reg.gesture = Object.assign(base, { kind: 'marquee', rectEl: null });
            attachGestureListeners(reg);
            return;
        }

        // Background (or anywhere with Space held): a pan when enabled, else just a pane click.
        const canPan = o.panOnDrag || reg.spaceDown;
        reg.gesture = Object.assign(base, { kind: 'pan', canPan, moved: false, vp0: { x: reg.vp.x, y: reg.vp.y, zoom: reg.vp.zoom } });
        if (canPan) {
            e.preventDefault();
            capture(reg, e.pointerId);
        }
        attachGestureListeners(reg);
    };

    reg.onPointerMove = (e) => {
        if (e.pointerType === 'touch' && reg.touchPoints.has(e.pointerId)) {
            reg.touchPoints.set(e.pointerId, { x: e.clientX, y: e.clientY });
        }
        const g = reg.gesture;
        if (g && g.kind === 'pinch') {
            if (!reg.touchPoints.has(e.pointerId)) return; // a pointer that isn't part of this pinch
            const dist = pinchDistance(reg);
            const mid = pinchMidpoint(reg);
            if (!mid || dist <= 0) return;
            const o = reg.options;
            const newZoom = clampZoom(g.startZoom * (dist / g.startDist), o.minZoom, o.maxZoom);
            const local = localPoint(reg, mid.x, mid.y);
            applyViewport(reg, zoomAt(reg.vp, newZoom, local.x, local.y), 'pinch');
            scheduleReport(reg);
            return;
        }
        if (!g || e.pointerId !== g.pointerId) return;
        const dist0 = Math.hypot(e.clientX - g.startX, e.clientY - g.startY);
        if (g.kind === 'node') {
            if (!g.started) {
                if (dist0 < DRAG_THRESHOLD) return;
                beginNodeDrag(reg, g);
            }
            moveNodes(reg, g, e.clientX, e.clientY);
            g.lastX = e.clientX;
            g.lastY = e.clientY;
            return;
        }
        if (g.kind === 'resize') {
            updateResize(reg, g, e.clientX, e.clientY, e.shiftKey);
            g.lastX = e.clientX;
            g.lastY = e.clientY;
            return;
        }
        if (g.kind === 'connect') {
            updateConnectionLine(reg, g.sourceNodeEl, g.sourceHandleId, e.clientX, e.clientY);
            updateHoverValidation(reg, g, e.clientX, e.clientY);
            g.lastX = e.clientX;
            g.lastY = e.clientY;
            return;
        }
        if (g.kind === 'reconnect') {
            updateReconnectLine(reg, g.fixedPoint, e.clientX, e.clientY);
            g.lastX = e.clientX;
            g.lastY = e.clientY;
            return;
        }
        if (g.kind === 'marquee') {
            if (!g.rectEl) g.rectEl = createMarqueeRect(reg);
            updateMarqueeRect(reg, g, e.clientX, e.clientY);
            g.lastX = e.clientX;
            g.lastY = e.clientY;
            return;
        }
        g.lastX = e.clientX;
        g.lastY = e.clientY;
        if (!g.canPan) return;
        if (!g.moved) {
            if (dist0 < DRAG_THRESHOLD) return;
            g.moved = true;
            reg.pane.setAttribute('data-flow-panning', '');
            diag({ ev: 'pan-start' });
        }
        applyViewport(reg, { x: g.vp0.x + (e.clientX - g.startX), y: g.vp0.y + (e.clientY - g.startY), zoom: g.vp0.zoom }, 'pan');
        scheduleReport(reg);
    };

    reg.onPointerUp = (e) => {
        if (e.pointerType === 'touch') reg.touchPoints.delete(e.pointerId);
        const g = reg.gesture;
        if (g && g.kind === 'pinch') {
            if (reg.touchPoints.size < 2) {
                // Pinch ends when fewer than two fingers remain. No fallback to a single-finger pan
                // for whichever finger is left — the next touchstart begins a fresh gesture, the
                // simplest of the transition behaviours and the one React Flow itself uses.
                reg.gesture = null;
                detachGestureListeners(reg);
                diag({ ev: 'pinch-end' });
                reportFinal(reg);
            }
            return;
        }
        if (!g || e.pointerId !== g.pointerId) return;
        reg.gesture = null;
        detachGestureListeners(reg);
        release(reg, g.pointerId);
        if (g.kind === 'node') {
            if (g.started) {
                swallowNextClick();
                commitNodeDrag(reg, g);
            }
            return;
        }
        if (g.kind === 'resize') {
            swallowNextClick();
            commitResize(reg, g);
            return;
        }
        if (g.kind === 'connect') {
            swallowNextClick();
            endConnectGesture(reg, g, e.clientX, e.clientY);
            return;
        }
        if (g.kind === 'reconnect') {
            swallowNextClick();
            endReconnectGesture(reg, g, e.clientX, e.clientY);
            return;
        }
        if (g.kind === 'marquee') {
            swallowNextClick();
            endMarquee(reg, g);
            return;
        }
        if (g.moved) {
            reg.pane.removeAttribute('data-flow-panning');
            swallowNextClick();
            diag({ ev: 'pan-end' });
            reportFinal(reg);
            return;
        }
        const local = localPoint(reg, e.clientX, e.clientY);
        const p = screenToFlow(local.x, local.y, reg.vp);
        diag({ ev: 'pane-click', x: p.x, y: p.y });
        call(reg, 'PaneClicked', p.x, p.y).catch(() => { });
    };

    reg.onPointerCancel = (e) => {
        if (e.pointerType === 'touch') reg.touchPoints.delete(e.pointerId);
        const g = reg.gesture;
        if (g && g.kind === 'pinch') {
            if (reg.touchPoints.size < 2) {
                reg.gesture = null;
                detachGestureListeners(reg);
                diag({ ev: 'pinch-cancel' });
            }
            return;
        }
        if (!g || e.pointerId !== g.pointerId) return;
        cancelGesture(reg, 'pointercancel');
    };

    reg.onWheel = (e) => {
        const o = reg.options;
        if (!o.zoomOnScroll) return;
        e.preventDefault();
        const unit = e.deltaMode === 1 ? 0.05 : e.deltaMode ? 1 : 0.002;
        const factor = Math.pow(2, -e.deltaY * unit * (e.ctrlKey ? 10 : 1));
        const from = reg.vp.zoom;
        const to = clampZoom(from * factor, o.minZoom, o.maxZoom);
        if (to === from) return;
        const local = localPoint(reg, e.clientX, e.clientY);
        diag({ ev: 'wheel', deltaY: e.deltaY, ax: local.x, ay: local.y, from, to });
        applyViewport(reg, zoomAt(reg.vp, to, local.x, local.y), 'wheel');
        scheduleReport(reg);
        clearTimeout(reg.wheelTimer);
        reg.wheelTimer = setTimeout(() => {
            reg.wheelTimer = 0;
            diag({ ev: 'wheel-end' });
            reportFinal(reg);
        }, WHEEL_END_MS);
    };

    reg.onKeyDown = (e) => {
        // Editable field / opted-out region: never intercept (arrow-key scroll suppression,
        // Enter/Space connect) — same rule FlowCanvas.razor's own keydown handlers apply via
        // FlowIsFocusedElementEditableAsync. The exact-target-equality checks below already imply
        // this for the current two branches, but a new one added later might not — check explicitly.
        if (isKeyboardExempt(e.target)) return;

        // Phase 5: Ctrl+G / Ctrl+Shift+G group/ungroup in .NET — keep the browser's own "find next"
        // (Ctrl+G) from opening on top of that.
        if ((e.ctrlKey || e.metaKey) && !e.altKey && (e.key === 'g' || e.key === 'G')) {
            e.preventDefault();
            return;
        }

        // Arrow keys on a node move it (FlowCanvas handles the move in .NET); stop the page from
        // scrolling underneath. Only when the node can actually move.
        if (e.key && e.key.startsWith('Arrow')) {
            const nodeEl = e.target instanceof Element ? e.target.closest('[data-flow-node]') : null;
            if (nodeEl && e.target === nodeEl) {
                const o = reg.options;
                if (!o.readonly && o.nodesDraggable && nodeEl.getAttribute('data-draggable') !== 'false') e.preventDefault();
            }
            return;
        }

        // Enter/Space on a handle: keyboard connect (start, or commit against the mode's source).
        if (e.key === 'Enter' || e.key === ' ') {
            const handleEl = e.target instanceof Element ? e.target.closest('[data-flow-handle]') : null;
            if (handleEl && e.target === handleEl) {
                e.preventDefault();
                handleConnectKey(reg, handleEl);
                return;
            }
            // LU-06: Enter/Space on the node host itself raises OnNodeClick (FlowCanvas.razor's own
            // keydown handler does that part); prevent the page from scrolling on Space here, the
            // one thing a plain native listener does that Blazor's own preventDefault:@bind can't
            // do race-free for a key that fires every repeat while held.
            const nodeEl = e.target instanceof Element ? e.target.closest('[data-flow-node]') : null;
            if (nodeEl && e.target === nodeEl) {
                e.preventDefault();
            }
        }
    };

    reg.onWindowKeyDown = (e) => {
        if (e.key === 'Escape' && reg.kbConnect) {
            cancelKbConnect(reg, 'escape-key');
            return;
        }
        if (e.key === 'Escape' && reg.gesture) {
            cancelGesture(reg, 'escape');
            return;
        }
        if ((e.code === 'Space' || e.key === ' ') && reg.hover && !isEditable(e.target) && !(e.target instanceof Element && e.target.closest('button, a'))) {
            if (!reg.spaceDown) {
                reg.spaceDown = true;
                reg.pane.setAttribute('data-flow-space-pan', '');
            }
            e.preventDefault();
        }
    };

    reg.onWindowKeyUp = (e) => {
        if ((e.code === 'Space' || e.key === ' ') && reg.spaceDown) {
            reg.spaceDown = false;
            reg.pane.removeAttribute('data-flow-space-pan');
        }
    };

    reg.onPointerEnter = () => { reg.hover = true; };
    reg.onPointerLeave = () => { reg.hover = false; };

    reg.onMutations = (mutations) => {
        let stamp = false, added = false;
        for (const m of mutations) {
            if (m.type === 'attributes') {
                if (m.attributeName === 'data-flow-viewport') {
                    stamp = true;
                } else if ((m.attributeName === 'data-x' || m.attributeName === 'data-y') && m.target.hasAttribute('data-flow-node')) {
                    // A render landed for this node: its data-x/data-y is the truth again.
                    reg.pending.delete(m.target.getAttribute('data-flow-node'));
                }
            } else if (m.type === 'childList') {
                for (const n of m.addedNodes) {
                    if (n.nodeType !== 1) continue;
                    added = true;
                    for (const el of observeNodes(reg, n)) queueMeasure(reg, el);
                }
                for (const n of m.removedNodes) {
                    if (n.nodeType !== 1) continue;
                    const gone = [];
                    if (n.hasAttribute && n.hasAttribute('data-flow-node')) gone.push(n);
                    if (n.querySelectorAll) for (const el of n.querySelectorAll('[data-flow-node]')) gone.push(el);
                    for (const el of gone) {
                        if (reg.observed.has(el)) {
                            reg.observed.delete(el);
                            reg.ro.unobserve(el);
                        }
                        const id = el.getAttribute('data-flow-node');
                        reg.measured.delete(id);
                        reg.reportedMeasure.delete(id);
                    }
                }
            }
        }
        if (stamp) applyStamp(reg);
        if (added) applyPatterns(reg);
    };

    reg.onResize = (entries) => {
        for (const entry of entries) {
            const el = entry.target;
            if (el === reg.pane) {
                const w = reg.pane.clientWidth, h = reg.pane.clientHeight;
                if (w !== reg.paneW || h !== reg.paneH) {
                    reg.paneW = w;
                    reg.paneH = h;
                    call(reg, 'PaneResized', w, h).catch(() => { });
                }
            } else if (el.isConnected) {
                queueMeasure(reg, el);
            }
        }
        if (reg.initialFitPending) tryInitialFit(reg);
    };
}

function registerCanvas(pane, dotNetRef, options) {
    if (!pane) return;
    const existing = registrations.get(pane);
    if (existing) {
        // Idempotent re-registration: swap the .NET reference and options in place. A gesture in
        // flight keeps the options snapshot it started with.
        existing.dotNetRef = dotNetRef;
        existing.options = normalizeOptions(options);
        applyStamp(existing);
        return;
    }
    ensureCss();
    const opts = normalizeOptions(options);
    const reg = {
        pane,
        root: pane.closest('[data-slot="flow-canvas"]') || pane,
        dotNetRef,
        options: opts,
        vp: { x: 0, y: 0, zoom: 1 },
        stampId: null,
        lastReported: null,
        gesture: null,
        kbConnect: null,
        connLine: null,
        liveEl: null,
        touchPoints: new Map(), // active touch pointerId -> {x, y}, for two-finger pinch
        hoverValidateId: null, // phase 4 ValidateOnHover: the last hovered candidate's dedupe key
        lastHoverInvalidEl: null,
        pending: new Map(),
        measured: new Map(),
        reportedMeasure: new Set(),
        measureQueue: new Set(),
        observed: new Set(),
        measureFrame: 0,
        reportFrame: 0,
        wheelTimer: 0,
        paneW: -1,
        paneH: -1,
        hover: false,
        spaceDown: false,
        ready: false,
        initialFitPending: opts.fitViewOnInit && !opts.virtualized,
    };
    makeHandlers(reg);
    registrations.set(pane, reg);

    applyStamp(reg);
    reg.lastReported = { x: reg.vp.x, y: reg.vp.y, zoom: reg.vp.zoom };

    pane.addEventListener('pointerdown', reg.onPointerDown);
    pane.addEventListener('wheel', reg.onWheel, { passive: false });
    pane.addEventListener('keydown', reg.onKeyDown);
    pane.addEventListener('pointerenter', reg.onPointerEnter);
    pane.addEventListener('pointerleave', reg.onPointerLeave);
    window.addEventListener('keydown', reg.onWindowKeyDown);
    window.addEventListener('keyup', reg.onWindowKeyUp);

    reg.mo = new MutationObserver(reg.onMutations);
    reg.mo.observe(pane, { subtree: true, childList: true, attributes: true, attributeFilter: ['data-flow-viewport', 'data-x', 'data-y'] });

    reg.ro = new ResizeObserver(reg.onResize);
    reg.ro.observe(pane);
    const nodes = observeNodes(reg, pane);

    // First measurement synchronously, so the initial fit below sees real sizes in this frame.
    reg.paneW = pane.clientWidth;
    reg.paneH = pane.clientHeight;
    call(reg, 'PaneResized', reg.paneW, reg.paneH).catch(() => { });
    for (const el of nodes) reg.measureQueue.add(el);
    flushMeasure(reg);
    tryInitialFit(reg);
    if (!reg.initialFitPending) markReady(reg);
    diag({ ev: 'register', nodes: nodes.length, fitViewOnInit: opts.fitViewOnInit });
}

function unregisterCanvas(pane) {
    if (!pane) return;
    const reg = registrations.get(pane);
    if (!reg) return;
    // Never commit on teardown — put the DOM back and stop listening (reuses the Escape-cancel
    // logic for every gesture kind, including a connect line or a marquee rect in progress).
    if (reg.gesture) cancelGesture(reg, 'unregister');
    if (reg.kbConnect) cancelKbConnect(reg, 'unregister');
    if (reg.liveEl) reg.liveEl.remove();
    pane.removeEventListener('pointerdown', reg.onPointerDown);
    pane.removeEventListener('wheel', reg.onWheel);
    pane.removeEventListener('keydown', reg.onKeyDown);
    pane.removeEventListener('pointerenter', reg.onPointerEnter);
    pane.removeEventListener('pointerleave', reg.onPointerLeave);
    window.removeEventListener('keydown', reg.onWindowKeyDown);
    window.removeEventListener('keyup', reg.onWindowKeyUp);
    if (reg.mo) reg.mo.disconnect();
    if (reg.ro) reg.ro.disconnect();
    if (reg.reportFrame) cancelAnimationFrame(reg.reportFrame);
    if (reg.measureFrame) cancelAnimationFrame(reg.measureFrame);
    if (reg.wheelTimer) clearTimeout(reg.wheelTimer);
    reg.dotNetRef = null;
    registrations.delete(pane);
    diag({ ev: 'unregister' });
}

function updateOptions(pane, options) {
    const reg = pane && registrations.get(pane);
    if (!reg) return;
    reg.options = normalizeOptions(options);
}

function setViewport(pane, x, y, zoom) {
    const reg = pane && registrations.get(pane);
    if (!reg || !(zoom > 0)) return;
    applyViewport(reg, { x, y, zoom }, 'set');
    reportFinal(reg);
}

function fitViewExport(pane, padding, minZoom, maxZoom, anchorNodeId) {
    const reg = pane && registrations.get(pane);
    if (!reg) return;
    if (fitFromDom(reg, padding, minZoom, maxZoom, 'fit', anchorNodeId)) reg.initialFitPending = false;
}

function getViewport(pane) {
    const reg = pane && registrations.get(pane);
    return reg ? [reg.vp.x, reg.vp.y, reg.vp.zoom] : null;
}

// LU-07: a FRESH measured size (getBoundingClientRect-backed clientWidth/Height), not whatever the
// ResizeObserver's own debounced PaneResized report happened to have delivered by the time this is
// called — used by FlowCanvas.FitViewAsync right after a container resize, where that report can
// still be in flight. Also refreshes reg.paneW/H and re-reports PaneResized when it changed, so the
// next .NET-computed fit (AllSized nodes) sees the same fresh size without another round trip.
function getPaneSize(pane) {
    const reg = pane && registrations.get(pane);
    if (!reg) return null;
    const w = reg.pane.clientWidth, h = reg.pane.clientHeight;
    if (w !== reg.paneW || h !== reg.paneH) {
        reg.paneW = w;
        reg.paneH = h;
        call(reg, 'PaneResized', w, h).catch(() => { });
    }
    return [w, h];
}

// ── Export (phase 4) ─────────────────────────────────────────────────────

// Copies every COMPUTED style (not just the inline ones) from source onto target, recursively in
// document order — the only way a cloned, detached node keeps looking like the live one once it's
// re-parented into the export SVG's <foreignObject>, since it no longer matches any of the page's
// real CSS selectors there.
function inlineComputedStyles(source, target) {
    const cs = getComputedStyle(source);
    let text = '';
    for (let i = 0; i < cs.length; i++) {
        const prop = cs[i];
        text += prop + ':' + cs.getPropertyValue(prop) + ';';
    }
    target.setAttribute('style', text);
    const sKids = source.querySelectorAll('*');
    const tKids = target.querySelectorAll('*');
    for (let i = 0; i < sKids.length; i++) {
        if (tKids[i]) inlineComputedStyles(sKids[i], tKids[i]);
    }
}

// Best-effort raster export: every node's DOM, computed-style-inlined, inside an SVG
// <foreignObject>, drawn to a <canvas>, read back as a PNG data URL. Never throws — a cross-origin
// image/font anywhere in a node template taints the canvas and browsers refuse readback
// (SecurityError on toDataURL); that, and every other failure mode here, resolves to null and is
// journaled, exactly like every other engine failure path in this file.
async function exportPng(pane, scale) {
    const reg = pane && registrations.get(pane);
    if (!reg) return null;
    try {
        const rects = domRects(reg);
        const bounds = getBounds(rects);
        if (!bounds) return null;
        const s = scale > 0 ? scale : 1;
        const pad = 20;
        const logicalW = bounds.width + pad * 2;
        const logicalH = bounds.height + pad * 2;
        const w = Math.max(1, Math.ceil(logicalW * s));
        const h = Math.max(1, Math.ceil(logicalH * s));

        const svgNS = 'http://www.w3.org/2000/svg';
        const svg = document.createElementNS(svgNS, 'svg');
        svg.setAttribute('xmlns', svgNS);
        svg.setAttribute('width', String(w));
        svg.setAttribute('height', String(h));
        const fo = document.createElementNS(svgNS, 'foreignObject');
        fo.setAttribute('x', '0');
        fo.setAttribute('y', '0');
        fo.setAttribute('width', String(w));
        fo.setAttribute('height', String(h));

        const wrapper = document.createElement('div');
        wrapper.setAttribute('xmlns', 'http://www.w3.org/1999/xhtml');
        wrapper.style.position = 'relative';
        wrapper.style.width = logicalW + 'px';
        wrapper.style.height = logicalH + 'px';
        wrapper.style.transform = 'scale(' + s + ')';
        wrapper.style.transformOrigin = '0 0';
        wrapper.style.background = getComputedStyle(reg.pane).backgroundColor || '#fff';

        for (const el of nodeElements(reg)) {
            const clone = el.cloneNode(true);
            inlineComputedStyles(el, clone);
            const p = nodePos(reg, el);
            clone.style.position = 'absolute';
            clone.style.left = '0';
            clone.style.top = '0';
            clone.style.transform = 'translate(' + (p.x - bounds.x + pad) + 'px, ' + (p.y - bounds.y + pad) + 'px)';
            wrapper.appendChild(clone);
        }
        fo.appendChild(wrapper);
        svg.appendChild(fo);

        const xml = new XMLSerializer().serializeToString(svg);
        const svgUrl = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(xml);
        const img = new Image();
        const loaded = new Promise((resolve, reject) => {
            img.onload = () => resolve();
            img.onerror = () => reject(new Error('export image failed to load'));
        });
        img.src = svgUrl;
        await loaded;

        const canvas = document.createElement('canvas');
        canvas.width = w;
        canvas.height = h;
        const ctx = canvas.getContext('2d');
        ctx.drawImage(img, 0, 0, w, h);
        const dataUrl = canvas.toDataURL('image/png');
        diag({ ev: 'export-png', w, h });
        return dataUrl;
    } catch (err) {
        diag({ ev: 'export-png-error', message: String((err && err.message) || err) });
        return null;
    }
}

export const flow = {
    registerCanvas,
    unregisterCanvas,
    updateOptions,
    setViewport,
    fitView: fitViewExport,
    getViewport,
    getPaneSize,
    isFocusedElementEditable,
    exportPng,
};

// Test-only seam: the pure geometry, importable from Node without a DOM (tests/js/*.mjs).
export const __testing = {
    fmt, clampZoom, snap, screenToFlow, zoomAt, getBounds, fitView, handleAnchor,
    straightPath, bezierPath, smoothStepPath, stepPoints, edgePath,
    isEditable, isKeyboardExempt, isFocusedElementEditable,
    computeHelperLines,
};

export default flow;
