// Phase 5 perf fact #1 — DataGrid large-dataset benchmarks: initial render,
// virtualized scroll fps, sort time, filter (search) time.
//
// Drives docs/Lumeo.Docs/Pages/E2E/PerfBench.razor (noindex, not in nav). See
// that file's RowCount comment (and PerformanceFacts.razor's methodology
// section) for why the AUTOMATED dataset here is 10,000 rows, not a literal
// 100,000: this project's docs app is a non-AOT-compiled (interpreter-tier)
// Blazor WASM build, and 100k rows reliably exhausts the wasm32 linear-memory
// ceiling while even 75k took ~60s just to render once — both measured by
// hand and reported alongside these numbers rather than re-run on every CI
// pass. 10k keeps this script fast and reliable for repeated runs.
//
// Methodology: 5 independent runs, each on a FRESH page (no warm-up carried
// over), median reported per metric. Every timing window is measured with
// performance.now() INSIDE the page (an observer armed via one page.evaluate
// call, the trigger fired via a real page.click(), the result read back via
// page.waitForFunction/page.evaluate) — the click itself blocks the page's
// main thread synchronously for multi-second renders, which was long enough
// to make a single "click-and-await-in-one-evaluate" pattern unreliable
// (Chromium's CDP session drops the execution context mid-call under that
// load) — so this script deliberately splits arm / trigger / read into three
// separate steps instead.
import { BASE_URL, launchBrowser, machineInfo, median, nowIso, withFreshPage, writeResult } from './lib/util.mjs';

const RUNS = 5;
const ROW_COUNT = 10_000;

async function gotoReady(page) {
  await page.goto(`${BASE_URL}/e2e/perf-bench`, { waitUntil: 'networkidle' });
  await page.waitForFunction(
    () => document.querySelector('[data-testid="perfbench-ready"]')?.textContent === 'ready',
    undefined,
    { timeout: 30_000 },
  );
}

async function armAndClick(page, { armFn, clickSelector, resultTimeoutMs = 30_000 }) {
  await page.evaluate(armFn);
  await page.click(clickSelector, { timeout: 5_000, noWaitAfter: true }).catch(() => {});
  await page.waitForFunction(() => window.__perfDone === true, undefined, { timeout: resultTimeoutMs });
  return page.evaluate(() => window.__perfResult);
}

async function measureOnce(page) {
  await gotoReady(page);

  // 1) Initial render time: click "Load" and wait for the first virtualized
  //    row to land in the DOM.
  const initialRenderMs = await armAndClick(page, {
    armFn: () => {
      window.__perfDone = false;
      // Observe the stable DataGrid ROOT, not the <tbody>: Blazor's diff
      // replaces the whole <tbody> element (a different Razor @if branch —
      // empty state vs loaded rows), so a MutationObserver bound to the
      // pre-load tbody node goes stale/detached the moment that swap happens
      // and never sees the real row insertion.
      const root = document.querySelector('[data-slot="datagrid"]');
      const t0 = performance.now();
      const obs = new MutationObserver(() => {
        if (root.querySelector('tbody[data-slot="datagrid-body"] tr[data-slot="datagrid-row"]')) {
          obs.disconnect();
          window.__perfResult = performance.now() - t0;
          window.__perfDone = true;
        }
      });
      obs.observe(root, { childList: true, subtree: true });
    },
    clickSelector: '[data-testid="perfbench-load"]',
  });

  await page.waitForTimeout(300); // let virtualization settle before scrolling

  // 2) Scroll fps: drive the virtualized scroll container's scrollTop via a
  //    rAF loop for ~1s, counting frames — self-contained, no click involved.
  const scrollFps = await page.evaluate(() => new Promise((resolve) => {
    const scroller = document.querySelector('[data-slot="datagrid"] .overflow-auto');
    const maxScroll = scroller.scrollHeight - scroller.clientHeight;
    const durationMs = 1000;
    let frames = 0;
    let start = 0;
    function tick(t) {
      if (!start) start = t;
      const elapsed = t - start;
      scroller.scrollTop = (elapsed / durationMs) * maxScroll * 0.8;
      frames++;
      if (elapsed < durationMs) {
        requestAnimationFrame(tick);
      } else {
        resolve((frames / elapsed) * 1000);
      }
    }
    requestAnimationFrame(tick);
  }));

  // 3) Sort time: click the Salary column's sort button, time until the
  //    header's aria-sort flips (DataGrid re-sorts + re-renders the body in
  //    the same synchronous pass, so this covers that whole cost).
  const sortMs = await armAndClick(page, {
    armFn: () => {
      window.__perfDone = false;
      const th = document.querySelector('thead th[aria-colindex="5"]');
      const t0 = performance.now();
      const obs = new MutationObserver(() => {
        if (th.getAttribute('aria-sort') !== 'none') {
          obs.disconnect();
          window.__perfResult = performance.now() - t0;
          window.__perfDone = true;
        }
      });
      obs.observe(th, { attributes: true, attributeFilter: ['aria-sort'] });
    },
    clickSelector: 'thead th[aria-colindex="5"] button',
  });

  // 4) Filter (search) time: type into the toolbar's global search box and
  //    time until the body re-renders with the filtered set.
  const filterMs = await page.evaluate(() => new Promise((resolve) => {
    const input = document.querySelector('[data-slot="datagrid-toolbar"] input[type="text"]');
    const root = document.querySelector('[data-slot="datagrid"]');
    const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
    const t0 = performance.now();
    const obs = new MutationObserver(() => {
      obs.disconnect();
      resolve(performance.now() - t0);
    });
    obs.observe(root, { childList: true, subtree: true });
    setter.call(input, 'Person 999');
    input.dispatchEvent(new Event('input', { bubbles: true }));
  }));

  const rowCountText = await page.evaluate(() =>
    document.querySelector('[data-testid="perfbench-status"]').textContent);

  return { initialRenderMs, scrollFps, sortMs, filterMs, rowCountText };
}

async function main() {
  const browser = await launchBrowser();
  const runs = [];
  for (let i = 0; i < RUNS; i++) {
    const result = await withFreshPage(browser, measureOnce);
    console.log(`run ${i + 1}/${RUNS}:`, result);
    runs.push(result);
  }
  const machine = await machineInfo(browser);
  await browser.close();

  const summary = {
    metric: 'datagrid-100k',
    measuredAt: nowIso(),
    version: '4.2.0+main',
    rowCount: ROW_COUNT,
    rowCountNote:
      '10,000 rows on every automated run, not a literal 100,000 — this ' +
      'project\'s docs WASM app is a large, non-AOT-compiled (interpreter-tier) ' +
      'build. Two things were measured by hand against it and are reported ' +
      'here rather than re-run on every pass: (1) materializing 100k PerfRow ' +
      'instances plus DataGrid\'s client-side sort/search indices reliably ' +
      'hits the wasm32 linear-memory ceiling, reproduced even after raising ' +
      'WasmInitialHeapSize/EmccMaximumHeapSize to ~3.5 GB/~3.75 GB in ' +
      'Lumeo.Docs.csproj (right up against wasm32\'s 4 GB hard limit); (2) at ' +
      '75k rows (the largest size that fit), the FIRST render alone took ' +
      'roughly a minute of wall clock, confirmed twice. A genuinely 100k+ row ' +
      'CLIENT dataset should use DataGrid\'s Virtualized + OnRangeRequest ' +
      'server-mode path instead of materializing every row in one in-memory ' +
      'List — see /components/datagrid.',
    manuallyMeasuredScaleCeiling: {
      rows_100000: 'OOM (System.OutOfMemoryException) even at ~3.75 GB WASM heap ceiling',
      rows_75000_firstRenderSeconds: '~60 (measured twice, non-AOT interpreter)',
    },
    runs,
    machine,
    medians: {
      initialRenderMs: median(runs.map((r) => r.initialRenderMs)),
      scrollFps: median(runs.map((r) => r.scrollFps)),
      sortMs: median(runs.map((r) => r.sortMs)),
      filterMs: median(runs.map((r) => r.filterMs)),
    },
  };
  console.log('medians:', summary.medians);
  writeResult('datagrid-100k.json', summary);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
