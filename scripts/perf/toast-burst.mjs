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
// "Settle" = from the trigger click to 150ms of quiet after BOTH the last DOM
// mutation inside <body> (toast mount + any auto-dismiss timers touching the
// DOM, via a MutationObserver) AND the last toast's CSS enter animation
// (.animate-toast-in, 300ms) actually finishing (via animationend). The
// animation itself does not mutate the DOM, so the mutation observer alone
// would go quiet while toasts are still visibly sliding in — animationend
// tracking closes that gap so the number reflects what a user actually sees
// settle, not just when the last ToastService.Show() call returns.
import { BASE_URL, launchBrowser, machineInfo, median, nowIso, withFreshPage, writeResult } from './lib/util.mjs';

const RUNS = 5;
const QUIET_WINDOW_MS = 150;
const TOAST_COUNT = 5;

async function measureOnce(page) {
  // Pass TOAST_COUNT through as the `toasts` query param PerfBench.razor reads
  // (SupplyParameterFromQuery(Name = "toasts")) — without it, FireToastBurst
  // silently falls back to its own default of 5 regardless of what TOAST_COUNT
  // above says, so the JSON summary's toastCount would no longer match how many
  // toasts were actually fired if TOAST_COUNT is ever changed (e.g. to re-test
  // 6/10/100 once the eviction-crash bug documented below is fixed).
  await page.goto(`${BASE_URL}/e2e/perf-bench?toasts=${TOAST_COUNT}`, { waitUntil: 'networkidle' });
  // Wait past PerfBench's background 10k-row generation (OnInitializedAsync)
  // so it isn't still occupying the single-threaded WASM main thread when the
  // toast burst fires — that would inject unrelated multi-second noise into
  // the settle-time measurement below.
  await page.waitForFunction(
    () => document.querySelector('[data-testid="perfbench-ready"]')?.textContent === 'ready',
    undefined,
    { timeout: 30_000 },
  );

  // Start the clock inside the page's own 'click' handler on the burst
  // button — fired the moment the click actually lands there — NOT when
  // page.click() below sends the CDP command. Previously t0 was captured in
  // the evaluate() below, armed BEFORE page.click()'s CDP round trip +
  // actionability checks, which folded that overhead into the reported
  // settle time even though the docs claim timing starts at the trigger and
  // excludes Playwright/CDP time. See the identical fix/comment in
  // datagrid-100k.mjs's armAndClick.
  const toastBurstSelector = '[data-testid="perfbench-toast-burst"]';
  await page.evaluate((selector) => {
    window.__perfDone = false;
    window.__perfT0 = null;
    document.querySelector(selector).addEventListener(
      'click',
      () => { window.__perfT0 = performance.now(); },
      { capture: true, once: true },
    );
  }, toastBurstSelector);

  await page.evaluate((quietMs) => {
    let lastMutation = 0;
    let seen = false;
    const obs = new MutationObserver(() => {
      seen = true;
      lastMutation = performance.now();
    });
    obs.observe(document.body, { childList: true, subtree: true, attributes: true });
    // The toast enter transition (.animate-toast-in, 300ms — see
    // src/Lumeo/wwwroot/css/lumeo.css) is a pure CSS animation: it does not
    // touch the DOM while running, so the MutationObserver above goes quiet
    // as soon as the toasts mount, well before they finish visibly entering.
    // Track in-flight 'toast-in' animations via animationstart/animationend
    // and require all of them to finish (not just "no DOM mutation lately")
    // before the quiet window is allowed to close — otherwise a single early
    // mutation plus 150ms of DOM silence could resolve the loop while the
    // animation is still mid-flight (it doesn't mutate the DOM, so nothing
    // would ever contradict a premature "settled").
    let pendingAnimations = 0;
    const onAnimStart = (e) => {
      if (e.animationName === 'toast-in') pendingAnimations++;
    };
    const onAnimEnd = (e) => {
      if (e.animationName === 'toast-in') {
        pendingAnimations = Math.max(0, pendingAnimations - 1);
        seen = true;
        lastMutation = performance.now();
      }
    };
    document.addEventListener('animationstart', onAnimStart, true);
    document.addEventListener('animationend', onAnimEnd, true);
    function poll() {
      if (seen && pendingAnimations === 0 && (performance.now() - lastMutation) > quietMs) {
        obs.disconnect();
        document.removeEventListener('animationstart', onAnimStart, true);
        document.removeEventListener('animationend', onAnimEnd, true);
        window.__perfResult = performance.now() - window.__perfT0;
        window.__perfDone = true;
        return;
      }
      requestAnimationFrame(poll);
    }
    requestAnimationFrame(poll);
  }, QUIET_WINDOW_MS);
  await page.click(toastBurstSelector, { timeout: 5_000, noWaitAfter: true }).catch(() => {});
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
