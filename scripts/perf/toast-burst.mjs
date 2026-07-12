// Phase 5 perf fact #3 — Toast burst: time for toasts to settle.
//
// TOAST_COUNT is 5, not the originally planned 100 — see the comment on
// PerfBench.razor's FireToastBurst for why: the docs site's global
// ToastProvider (MainLayout.razor) uses the default MaxToasts=5, and firing
// more than ~6-7 ToastService.Show() calls in one synchronous burst
// reproducibly CRASHES the WASM renderer via MaxToasts' oldest-eviction path
// (confirmed: 5 stable, 6 stable, 10 crashes the tab — "Target crashed", not
// a catchable JS exception). That is disclosed here as a real finding, not
// silently worked around: a genuine 100-toast burst cannot currently be
// benchmarked against this docs build without hitting that bug first.
//
// "Settle" = from the trigger click to the last DOM mutation inside <body>
// (toast mount + enter animation classes + any auto-dismiss timers touching
// the DOM), observed via a MutationObserver with a 150ms quiet window. This
// is a deliberately generous definition — it also covers the CSS enter
// transition duration, not just the synchronous Show() loop — so the number
// reflects what a user actually sees settle, not just when the last
// ToastService.Show() call returns.
import { BASE_URL, launchBrowser, machineInfo, median, nowIso, withFreshPage, writeResult } from './lib/util.mjs';

const RUNS = 5;
const QUIET_WINDOW_MS = 150;
const TOAST_COUNT = 5;

async function measureOnce(page) {
  await page.goto(`${BASE_URL}/e2e/perf-bench`, { waitUntil: 'networkidle' });
  // Wait past PerfBench's background 10k-row generation (OnInitializedAsync)
  // so it isn't still occupying the single-threaded WASM main thread when the
  // toast burst fires — that would inject unrelated multi-second noise into
  // the settle-time measurement below.
  await page.waitForFunction(
    () => document.querySelector('[data-testid="perfbench-ready"]')?.textContent === 'ready',
    undefined,
    { timeout: 30_000 },
  );

  await page.evaluate((quietMs) => {
    window.__perfDone = false;
    let lastMutation = 0;
    let seen = false;
    const t0 = performance.now();
    const obs = new MutationObserver(() => {
      seen = true;
      lastMutation = performance.now();
    });
    obs.observe(document.body, { childList: true, subtree: true, attributes: true });
    function poll() {
      if (seen && (performance.now() - lastMutation) > quietMs) {
        obs.disconnect();
        window.__perfResult = performance.now() - t0;
        window.__perfDone = true;
        return;
      }
      requestAnimationFrame(poll);
    }
    requestAnimationFrame(poll);
  }, QUIET_WINDOW_MS);
  await page.click('[data-testid="perfbench-toast-burst"]', { timeout: 5_000, noWaitAfter: true }).catch(() => {});
  await page.waitForFunction(() => window.__perfDone === true, undefined, { timeout: 30_000 });
  const settleMs = await page.evaluate(() => window.__perfResult);

  const toastCount = await page.evaluate(() => document.querySelectorAll('[role="status"]').length);
  return { settleMs, toastCount };
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
    metric: 'toast-burst',
    measuredAt: nowIso(),
    version: '4.2.0+main',
    toastCount: TOAST_COUNT,
    toastCountNote:
      '5, not the originally planned 100. The docs site\'s global ToastProvider ' +
      '(MainLayout.razor) uses the default MaxToasts=5; firing more than ~6-7 ' +
      'ToastService.Show() calls in one synchronous burst reproducibly crashes ' +
      'the WASM renderer via MaxToasts\' oldest-eviction path (Playwright ' +
      'reports "Target crashed", not a catchable JS exception). Confirmed by ' +
      'hand: 5 stable, 6 (one eviction) stable, 10 (five evictions) crashes the ' +
      'tab. This is a real bug in ToastProvider\'s eviction path, disclosed here ' +
      'rather than silently worked around — a genuine 100-toast burst cannot ' +
      'currently be benchmarked against this build until it is fixed.',
    quietWindowMs: QUIET_WINDOW_MS,
    runs,
    machine,
    medianSettleMs: median(runs.map((r) => r.settleMs)),
  };
  console.log('median settle:', summary.medianSettleMs, 'ms');
  writeResult('toast-burst.json', summary);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
