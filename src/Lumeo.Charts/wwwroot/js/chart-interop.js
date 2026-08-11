// Lumeo.Charts — native rendering engine JS interop.
//
// This module is the ENTIRE JS surface of the native (non-ECharts) charting
// engine, now scoped to lightweight cases (sparklines/mini charts) rather
// than a full 14-type engine: four narrow calls, each corresponding to a
// deliberate C#/JS boundary decision (see docs/superpowers/specs
// "charts-first-party-engine"). C# owns every layout/scale/tick/path/hit-test
// decision; JS never decides anything here, it only measures, forwards raw
// input, or reads computed CSS. Do not add a fifth call without revisiting
// that boundary decision first.
//
//   measureTextWidths(requests)                    — batched text metrics
//   registerPointerTrack / unregisterPointerTrack   — rAF-throttled pointer index forwarding
//   resolveThemeColors(tokens)                      — export-time theme color resolution
//   observeChartBox / unobserveChartBox             — ResizeObserver-backed real-box-size reporting
//
// The Canvas-fallback draw-command path (canvasDraw) was dropped along with
// the discrete-shape-heavy native chart types (heatmap, dense scatter) that
// were its only consumer — a sparkline is always SVG.
//
// Does NOT touch echarts-interop.js — the legacy ECharts path stays fully
// independent and unmodified.
//
// observeChartBox/unobserveChartBox was added deliberately to close the
// native engine's aspect-ratio distortion bug: charts rendered into a
// hardcoded 600x350 viewBox with preserveAspectRatio="none", so whenever the
// REAL rendered box's aspect differed from 600:350 (essentially always, since
// these charts default to Width="100%") the SVG stretched non-uniformly —
// circles became ellipses, axis-label glyphs stretched. This call is opted
// into ONLY by hosts that need real-pixel parity, and the C# side renders
// nothing (not a wrong-aspect frame) until the first callback lands.

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
    if (rect.width <= 0) return 0;
    // plotOriginX/plotWidth (from CartesianChartHost's PlotX/PlotWidth) are in
    // LOGICAL viewBox units, but rect.left/rect.width from getBoundingClientRect
    // are real screen pixels. The two only coincide when the chart happens to
    // render at exactly its viewBox size (e.g. a literal "600px" width) — any
    // other size (a "100%"-width chart, a resized container) silently resolved
    // the WRONG category index, a real hit-testing bug, not just a cosmetic
    // offset: a fully-passing test suite never caught it because every fixture
    // rendered at the coincidental 1:1 size. Fixed by converting localX back
    // into logical viewBox units via the owning <svg>'s own viewBox.baseVal
    // before comparing it against plotOriginX/plotWidth.
    const svg = element.ownerSVGElement;
    const viewBoxWidth = svg && svg.viewBox && svg.viewBox.baseVal && svg.viewBox.baseVal.width > 0
      ? svg.viewBox.baseVal.width
      : rect.width; // no viewBox found — assume 1:1, matching the previous behavior
    const scaleX = rect.width / viewBoxWidth;
    const localX = (clientX - rect.left) / scaleX;
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

// ---------------------------------------------------------------------------
// observeChartBox / unobserveChartBox — see the module-header remarks. Uses
// ResizeObserver's contentBoxSize/contentRect rather than
// getBoundingClientRect (which CartesianChartHost/XyChartHost already use for
// tooltip positioning, per THEIR OWN documented remarks): contentRect is
// relative to the element's own border box, not the viewport, so — unlike
// getBoundingClientRect — it never goes stale when the page scrolls; it only
// fires when the box's own SIZE actually changes. Per spec, browsers deliver
// the FIRST ResizeObserver callback for a newly-observed element before the
// next paint (not on some later macrotask), so the very first real layout
// already reflects the true box size.
// ---------------------------------------------------------------------------

const boxObservers = new Map(); // elementId -> ResizeObserver

export function observeChartBox(elementId, dotNetRef, callbackName) {
  // Idempotent: tear down any previous observer for this id first, so a
  // caller can simply re-register after a loading-skeleton swap (a new DOM
  // element under the same id) without a separate "update" call.
  unobserveChartBox(elementId);

  const el = document.getElementById(elementId);
  if (!el) return;

  const report = (width, height) => {
    if (width > 0 && height > 0) {
      dotNetRef.invokeMethodAsync(callbackName, width, height).catch(() => {});
    }
  };

  const ro = new ResizeObserver((entries) => {
    for (const entry of entries) {
      // contentBoxSize is the modern, DPI/writing-mode-correct API; entries[0]
      // (not the whole array — Blink can report multiple fragments) covers
      // every UA that ships ResizeObserver at all. contentRect is the
      // universal fallback for the rare implementation missing it.
      const box = entry.contentBoxSize && entry.contentBoxSize.length > 0
        ? { width: entry.contentBoxSize[0].inlineSize, height: entry.contentBoxSize[0].blockSize }
        : entry.contentRect;
      report(box.width, box.height);
    }
  });
  ro.observe(el);
  boxObservers.set(elementId, ro);
}

export function unobserveChartBox(elementId) {
  const ro = boxObservers.get(elementId);
  if (ro) {
    ro.disconnect();
    boxObservers.delete(elementId);
  }
}
