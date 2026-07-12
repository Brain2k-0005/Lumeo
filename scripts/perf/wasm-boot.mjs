// Phase 5 perf fact #4 — WASM boot-to-interactive on the docs home ('/').
//
// The docs home uses deferred hydration (see docs/Lumeo.Docs/wwwroot/index.html):
// Blazor.start() does NOT run on '/' until the user's first real interaction
// (pointerdown/keydown/touchstart fire it instantly; scroll/wheel/pointermove
// need ~150ms of sustained movement; a 10s fallback boots regardless). That's
// a deliberate perf trade — it hides WASM boot cost behind idle time a real
// visitor spends reading the page before they touch anything.
//
// This script measures the boot cost itself, not that idle time: it navigates
// to '/', then immediately dispatches a synthetic pointerdown (the same
// "instant" trigger category real interaction uses) and times from that
// dispatch to document.documentElement.dataset.blazorReady flipping to
// "true" — the exact moment MainLayout's first interactive render completes
// (see docs/Lumeo.Docs/Layout/MainLayout.razor calling
// lumeo.signalBlazorReady, and js/docs.js for what that flag does).
import { BASE_URL, launchBrowser, machineInfo, median, nowIso, withFreshPage, writeResult } from './lib/util.mjs';

const RUNS = 5;

async function measureOnce(page) {
  const navStart = Date.now();
  await page.goto(`${BASE_URL}/`, { waitUntil: 'domcontentloaded' });
  const navToDomMs = Date.now() - navStart;

  const bootMs = await page.evaluate(() => new Promise((resolve) => {
    if (document.documentElement.dataset.blazorReady === 'true') {
      resolve(0);
      return;
    }
    const t0 = performance.now();
    const obs = new MutationObserver(() => {
      if (document.documentElement.dataset.blazorReady === 'true') {
        obs.disconnect();
        resolve(performance.now() - t0);
      }
    });
    obs.observe(document.documentElement, { attributes: true, attributeFilter: ['data-blazor-ready'] });
    // Same "instant" trigger category as a real pointerdown/keydown/touchstart
    // (see index.html's deferred-hydration controller) — arms Blazor.start()
    // immediately instead of waiting out the 10s fallback or sustained-scroll path.
    window.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, cancelable: true }));
  }));

  return { navToDomMs, bootMs };
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
    metric: 'wasm-boot',
    measuredAt: nowIso(),
    version: '4.2.0+main',
    route: '/',
    triggerNote:
      'Boot is deliberately deferred on \'/\' behind first user interaction ' +
      '(see index.html). This script dispatches a synthetic pointerdown ' +
      'immediately after navigation to trigger boot on the same "instant" path ' +
      'a real click/keypress uses, then times trigger -> blazorReady — i.e. it ' +
      'measures the runtime boot cost itself, not how long a visitor sat idle ' +
      'before touching the page.',
    runs,
    machine,
    medians: {
      navToDomMs: median(runs.map((r) => r.navToDomMs)),
      bootMs: median(runs.map((r) => r.bootMs)),
    },
  };
  console.log('medians:', summary.medians);
  writeResult('wasm-boot.json', summary);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
