// Phase 5 perf fact #2 — DataGrid interaction hot paths: column resize and
// column reorder, ms-per-move.
//
// Methodology mirrors the PR-353 (#353, "datagrid-reorder-resize") design
// claim documented in CHANGELOG.md 4.1.0: "All per-move work stays in
// JavaScript (zero .NET interop calls during drag/resize movement)". To
// measure that JS-only hot path in isolation — without Playwright/CDP's own
// per-event IPC round-trip drowning out the actual cost — every event in the
// burst is a synthetic PointerEvent dispatched from INSIDE a single
// page.evaluate() call, directly at the same element the real
// registerColumnResize()/registerColumnReorder() pointer listeners
// (src/Lumeo/wwwroot/js/components.js) are bound to.
//
// IMPORTANT asymmetry between the two numbers (Codex finding, 2026-07-12):
// reorder's onPointerMove writes `cell.style.transform` SYNCHRONOUSLY on
// every move, so its ms/move genuinely includes that DOM write. resize's
// onPointerMove only records `pendingWidth` and schedules
// `requestAnimationFrame(flushPendingWidth)` for the actual width/guideline
// DOM write — and because this loop dispatches all N pointermoves back to
// back inside one synchronous page.evaluate() call, it never yields to the
// event loop, so flushPendingWidth's rAF callback CANNOT run until after t1
// is already captured. The resize number below is therefore enqueue-only
// handler cost (updating pendingWidth), not the full per-move cost a real
// drag pays — see resizeRuns' note field and PerformanceFacts.razor's
// "Column resize / reorder" accordion for the disclosure. Forcing a real
// rAF flush per synthetic move (i.e. yielding after every dispatch) was
// deliberately rejected: it would make the "ms/move" figure dominated by
// requestAnimationFrame's ~16 ms scheduling latency instead of actual JS
// handler cost, which is a different and more misleading number than the
// one being disclosed here.
import { BASE_URL, launchBrowser, machineInfo, median, nowIso, withFreshPage, writeResult } from './lib/util.mjs';

const RUNS = 5;
const MOVES_PER_RUN = 3000;

async function loadGrid(page) {
  await page.goto(`${BASE_URL}/e2e/perf-bench`, { waitUntil: 'networkidle' });
  await page.waitForFunction(
    () => document.querySelector('[data-testid="perfbench-ready"]')?.textContent === 'ready',
    undefined,
    { timeout: 30_000 },
  );
  await page.evaluate(() => {
    window.__perfDone = false;
    // See datagrid-100k.mjs for why this observes the DataGrid root rather
    // than the <tbody> element directly (Blazor replaces the whole <tbody>
    // when swapping from the empty-state to the loaded-rows @if branch).
    const root = document.querySelector('[data-slot="datagrid"]');
    const obs = new MutationObserver(() => {
      if (root.querySelector('tbody[data-slot="datagrid-body"] tr[data-slot="datagrid-row"]')) {
        obs.disconnect();
        window.__perfDone = true;
      }
    });
    obs.observe(root, { childList: true, subtree: true });
  });
  await page.click('[data-testid="perfbench-load"]', { timeout: 5_000, noWaitAfter: true }).catch(() => {});
  await page.waitForFunction(() => window.__perfDone === true, undefined, { timeout: 30_000 });
  await page.waitForTimeout(300);
}

async function measureResize(page, moves) {
  return page.evaluate((N) => {
    const th = document.querySelector('thead th[aria-colindex="5"]'); // Salary
    const handle = th.querySelector('[data-slot="datagrid-resize-handle"]');
    const rect = handle.getBoundingClientRect();
    const startX = rect.x + rect.width / 2;
    const y = rect.y + rect.height / 2;
    const pointerId = 9101;
    const base = { bubbles: true, cancelable: true, pointerId, pointerType: 'mouse', button: 0, isPrimary: true, clientY: y };

    handle.dispatchEvent(new PointerEvent('pointerdown', { ...base, clientX: startX }));
    const t0 = performance.now();
    for (let i = 0; i < N; i++) {
      handle.dispatchEvent(new PointerEvent('pointermove', { ...base, clientX: startX + i * 0.05 }));
    }
    const t1 = performance.now();
    handle.dispatchEvent(new PointerEvent('pointerup', { ...base, clientX: startX + N * 0.05 }));
    return (t1 - t0) / N;
  }, moves);
}

async function measureReorder(page, moves) {
  return page.evaluate((N) => {
    const grid = document.querySelector('[data-slot="datagrid"]');
    const th = document.querySelector('thead th[aria-colindex="2"]'); // Name
    const grip = th.querySelector('[data-reorder-grip]');
    const rect = grip.getBoundingClientRect();
    const startX = rect.x + rect.width / 2;
    const y = rect.y + rect.height / 2;
    const pointerId = 9202;
    const base = { bubbles: true, cancelable: true, pointerId, pointerType: 'mouse', button: 0, isPrimary: true, clientY: y };

    // Grip pointerdown arms the drag synchronously (no movement threshold),
    // matching the touch/pen initiation path — see registerColumnReorder's
    // onPointerDown in components.js.
    grip.dispatchEvent(new PointerEvent('pointerdown', { ...base, clientX: startX }));
    const t0 = performance.now();
    for (let i = 0; i < N; i++) {
      // Sweep back and forth across sibling columns so computeTargetIdx's
      // projected-index branch is actually exercised, not just the clamp.
      const x = startX + Math.sin(i / 40) * 80;
      grid.dispatchEvent(new PointerEvent('pointermove', { ...base, clientX: x }));
    }
    const t1 = performance.now();
    grid.dispatchEvent(new PointerEvent('pointerup', { ...base, clientX: startX }));
    return (t1 - t0) / N;
  }, moves);
}

async function main() {
  const browser = await launchBrowser();

  const resizeRuns = [];
  for (let i = 0; i < RUNS; i++) {
    const msPerMove = await withFreshPage(browser, async (page) => {
      await loadGrid(page);
      return measureResize(page, MOVES_PER_RUN);
    });
    console.log(`resize run ${i + 1}/${RUNS}: ${msPerMove.toFixed(4)} ms/move`);
    resizeRuns.push(msPerMove);
  }

  const reorderRuns = [];
  for (let i = 0; i < RUNS; i++) {
    const msPerMove = await withFreshPage(browser, async (page) => {
      await loadGrid(page);
      return measureReorder(page, MOVES_PER_RUN);
    });
    console.log(`reorder run ${i + 1}/${RUNS}: ${msPerMove.toFixed(4)} ms/move`);
    reorderRuns.push(msPerMove);
  }

  const machine = await machineInfo(browser);
  await browser.close();

  const summary = {
    metric: 'datagrid-hotpaths',
    measuredAt: nowIso(),
    version: '4.2.0+main',
    movesPerRun: MOVES_PER_RUN,
    baselineNote:
      'CHANGELOG.md 4.1.0 (PR #353) claims ~0.004-0.007 ms per move event for ' +
      'this same zero-.NET-interop-per-move design. This script re-measures it ' +
      'independently against a loaded (10k-row) grid using synthetic ' +
      'PointerEvents dispatched in-page (no Playwright/CDP round trip per event).',
    resizeNote:
      'registerColumnResize defers its DOM width/guideline write to a ' +
      'requestAnimationFrame callback (once per frame). This script dispatches ' +
      'every synthetic pointermove synchronously in one loop, which never yields ' +
      'to the event loop, so that rAF callback cannot run before this figure is ' +
      'captured. This number is therefore ENQUEUE-ONLY handler cost, not the ' +
      'full per-move cost (including the deferred DOM write) a real drag pays. ' +
      'Contrast with reorder below, whose DOM write happens synchronously on ' +
      'every move and so is fully included.',
    resize: { runsMsPerMove: resizeRuns, medianMsPerMove: median(resizeRuns) },
    reorder: { runsMsPerMove: reorderRuns, medianMsPerMove: median(reorderRuns) },
    machine,
  };
  console.log('medians:', {
    resizeMsPerMove: summary.resize.medianMsPerMove,
    reorderMsPerMove: summary.reorder.medianMsPerMove,
  });
  writeResult('datagrid-hotpaths.json', summary);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
