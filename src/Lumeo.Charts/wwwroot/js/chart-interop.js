// Lumeo.Charts — native rendering engine JS interop.
//
// This module is the ENTIRE JS surface of the native (non-ECharts) charting
// engine: four narrow calls, each corresponding to a deliberate C#/JS
// boundary decision (see docs/superpowers/specs "charts-first-party-engine").
// C# owns every layout/scale/tick/path/hit-test decision; JS never decides
// anything here, it only measures, forwards raw input, paints pre-computed
// commands, or reads computed CSS. Do not add a fifth call without revisiting
// that boundary decision first.
//
//   measureTextWidths(requests)                    — batched text metrics
//   registerPointerTrack / unregisterPointerTrack   — rAF-throttled pointer index forwarding
//   canvasDraw(elementId, commandsJson)             — Canvas fallback paint execution
//   resolveThemeColors(tokens)                      — export-time theme color resolution
//
// Does NOT touch echarts-interop.js — the legacy ECharts path stays fully
// independent and unmodified.

// ---------------------------------------------------------------------------
// measureTextWidths — batched, offscreen-canvas text measurement.
// Same underlying technique (canvas 2D context + measureText) the owner's
// reference demos already use for their own ad-hoc text sizing (axis labels,
// word-cloud placement) — validated here as the right primitive, just called
// once per batch instead of once per label/word so the interop round-trip
// cost is amortised.
// ---------------------------------------------------------------------------

let measureCtx = null;

function getMeasureContext() {
  if (!measureCtx) {
    measureCtx = document.createElement('canvas').getContext('2d');
  }
  return measureCtx;
}

export function measureTextWidths(requests) {
  const ctx = getMeasureContext();
  const widths = new Array(requests.length);
  for (let i = 0; i < requests.length; i++) {
    ctx.font = requests[i].font;
    widths[i] = ctx.measureText(requests[i].text).width;
  }
  return widths;
}

// ---------------------------------------------------------------------------
// registerPointerTrack / unregisterPointerTrack — rAF-throttled pointer index
// forwarding. Reuses the exact dedup pattern already proven in the legacy
// Chart.razor tooltip bridge: only invoke back into .NET when the RESOLVED
// index actually changes, never once per animation frame. All index
// arithmetic here mirrors ChartHitTester.IndexForPointerX exactly — this is
// forwarding + O(1) index math, not a decision.
// ---------------------------------------------------------------------------

const trackers = new Map();

export function registerPointerTrack(element, plotOriginX, plotWidth, pointCount, dotNetRef) {
  // Idempotent: tear down any previous registration on this element first so
  // a caller can simply re-register after a resize/re-layout without a
  // separate "update" call.
  unregisterPointerTrack(element);

  const state = { plotOriginX, plotWidth, pointCount };
  let raf = 0;
  let lastClientX = 0;
  let lastIndex = -1;

  const resolveIndex = (clientX) => {
    if (state.pointCount <= 0) return -1;
    if (state.pointCount === 1) return 0;
    const rect = element.getBoundingClientRect();
    const localX = clientX - rect.left;
    if (state.plotWidth <= 0) return 0;
    const t = (localX - state.plotOriginX) / state.plotWidth;
    const raw = Math.round(t * (state.pointCount - 1));
    return Math.max(0, Math.min(state.pointCount - 1, raw));
  };

  const onMove = (e) => {
    lastClientX = e.clientX;
    if (raf) return;
    raf = requestAnimationFrame(() => {
      raf = 0;
      const idx = resolveIndex(lastClientX);
      if (idx !== lastIndex) {
        lastIndex = idx;
        dotNetRef.invokeMethodAsync('OnChartPointerIndex', idx);
      }
    });
  };

  const onLeave = () => {
    if (raf) { cancelAnimationFrame(raf); raf = 0; }
    lastIndex = -1;
    dotNetRef.invokeMethodAsync('OnChartPointerLeave');
  };

  element.addEventListener('pointermove', onMove);
  element.addEventListener('pointerleave', onLeave);
  trackers.set(element, { onMove, onLeave });
}

export function unregisterPointerTrack(element) {
  const entry = trackers.get(element);
  if (!entry) return;
  element.removeEventListener('pointermove', entry.onMove);
  element.removeEventListener('pointerleave', entry.onLeave);
  trackers.delete(element);
}

// ---------------------------------------------------------------------------
// canvasDraw — thin imperative executor for the Canvas fallback path
// (opt-in only: live-high-frequency series, or discrete-shape series above
// the shape-count budget). C# has already computed 100% of the geometry;
// this only executes. `op` maps 1:1 to a CanvasRenderingContext2D method.
// ---------------------------------------------------------------------------

const canvasStates = new Map(); // elementId -> { canvas, ctx }

function getCanvasState(elementId) {
  const canvas = document.getElementById(elementId);
  if (!canvas) return null;

  let state = canvasStates.get(elementId);
  if (!state || state.canvas !== canvas) {
    const ctx = canvas.getContext('2d');
    const dpr = window.devicePixelRatio || 1;
    // Scale ONCE per (re)bound canvas element — canvas width/height attributes
    // are set to CSS-size * dpr by the caller; this makes subsequent draw
    // commands operate in CSS-pixel coordinates like the SVG path does.
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    state = { canvas, ctx };
    canvasStates.set(elementId, state);
  }
  return state;
}

export function canvasDraw(elementId, commandsJson) {
  const state = getCanvasState(elementId);
  if (!state) return;
  const ctx = state.ctx;
  const commands = JSON.parse(commandsJson);

  for (const cmd of commands) {
    const a = cmd.args || [];
    if (cmd.style) {
      if (cmd.style.color) { ctx.strokeStyle = cmd.style.color; ctx.fillStyle = cmd.style.color; }
      if (cmd.style.width != null) ctx.lineWidth = cmd.style.width;
    }
    switch (cmd.op) {
      case 'beginPath': ctx.beginPath(); break;
      case 'closePath': ctx.closePath(); break;
      case 'moveTo': ctx.moveTo(a[0], a[1]); break;
      case 'lineTo': ctx.lineTo(a[0], a[1]); break;
      case 'rect': ctx.rect(a[0], a[1], a[2], a[3]); break;
      case 'clearRect': ctx.clearRect(a[0], a[1], a[2], a[3]); break;
      case 'arc': ctx.arc(a[0], a[1], a[2], a[3], a[4]); break;
      case 'stroke': ctx.stroke(); break;
      case 'fill': ctx.fill(); break;
      default: break; // unknown op — ignore rather than throw; C# side is the source of truth
    }
  }
}

export function disposeCanvas(elementId) {
  canvasStates.delete(elementId);
}

// ---------------------------------------------------------------------------
// resolveThemeColors — export-time-only CSS custom property resolution.
// Never called during normal on-screen rendering: live SVG consumes
// var(--token) natively and repaints on theme swap with zero JS. This exists
// purely so a PNG/standalone-SVG export can bake in concrete color values.
// ---------------------------------------------------------------------------

export function resolveThemeColors(tokens) {
  const style = getComputedStyle(document.documentElement);
  const result = {};
  for (const token of tokens) {
    result[token] = style.getPropertyValue(token).trim();
  }
  return result;
}
