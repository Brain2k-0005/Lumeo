#!/usr/bin/env node
// Visual regression sweep: screenshots every /components/* docs route in
// light + dark, compares against committed PNG baselines with pixelmatch.
//
// Usage:
//   node run.mjs                 compare against committed baselines (CI mode)
//   node run.mjs --update        (re)write the baselines this run produces
//   node run.mjs --route=toast   only sweep routes whose slug contains "toast"
//   node run.mjs --base-url=http://localhost:5287   skip spawning dotnet run
//
// Env:
//   VR_BASE_URL   same as --base-url
//   VR_PORT       port to launch the docs dev-server on (default 5287)
//
// Tolerance: a pixel is "different" when its perceptual delta (pixelmatch's
// YIQ-weighted threshold, see below) exceeds THRESHOLD (0..1, pixelmatch
// default scale — lower is stricter). A route FAILS when more than
// MAX_DIFF_RATIO of its pixels are "different". These mirror the tolerances
// already established for the C# visual test in
// tests/Lumeo.Tests.E2E/Visual/HomePageVisualTests.cs (0.5% pixel budget),
// picked to tolerate cross-platform font-hinting / sub-pixel AA drift while
// still catching real layout/color regressions.
// 1% (not the C# homepage test's 0.5%) — this sweep hits 207 LIVE demo pages
// (interactive widgets, some with today's-date-dependent content like
// Calendar/DatePicker) rather than one static homepage, so it carries more
// inherent entropy even with animations frozen. Still tight enough that a
// real layout regression (typically thousands of px) trips it easily.
const THRESHOLD = 0.15;
const MAX_DIFF_RATIO = 0.01; // 1%

import { chromium } from 'playwright-core';
import pixelmatch from 'pixelmatch';
import { PNG } from 'pngjs';
import { spawn } from 'child_process';
import path from 'path';
import fs from 'fs';
import http from 'http';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.join(__dirname, '..', '..');
const BASELINES_DIR = path.join(__dirname, 'baselines');
const DIFFS_DIR = path.join(__dirname, '.diffs');
const DOCS_PAGES_DIR = path.join(REPO_ROOT, 'docs', 'Lumeo.Docs', 'Pages', 'Components');

const args = process.argv.slice(2);
const UPDATE = args.includes('--update');
const routeFilterArg = args.find((a) => a.startsWith('--route='));
const ROUTE_FILTER = routeFilterArg ? routeFilterArg.slice('--route='.length) : null;
const baseUrlArg = args.find((a) => a.startsWith('--base-url='));
const BASE_URL = baseUrlArg ? baseUrlArg.slice('--base-url='.length) : (process.env.VR_BASE_URL || null);
// NOT 5287 — that's the docs app's conventional dev port (launchSettings.json /
// PlaywrightTestBase's default), which other concurrently-running sessions or
// dev servers on this machine may already be bound to. Discovered the hard way:
// an unrelated worktree's docs server was squatting on 5287 mid-sweep, this
// script's spawn silently failed to bind ("address already in use"), and
// waitForServer's HTTP poll happily connected to that OTHER server instead —
// so a big chunk of "baselines" were screenshots of a different codebase's
// build until that other server was torn down and every subsequent request
// 404/connection-refused'd. A private, unusual port makes that failure mode
// impossible instead of merely unlikely.
const PORT = process.env.VR_PORT || '58217';

const VIEWPORT = { width: 1280, height: 800 };

// Fixed reference instant for the frozen browser/WASM clock below — any fixed
// value works (it just has to be STABLE across runs so baselines stay
// comparable); this one has no special significance beyond being memorable.
const FROZEN_NOW_MS = Date.UTC(2025, 0, 15, 12, 0, 0);

// ---------------------------------------------------------------------
// Route discovery — every literal `@page "/components/..."` directive under
// the docs app's Components pages, so newly added component pages are swept
// automatically with no list to keep in sync.
// ---------------------------------------------------------------------
function discoverRoutes() {
  const routes = new Set();
  const pageRe = /@page\s+"(\/components\/[a-zA-Z0-9/_-]+)"/g;
  const walk = (dir) => {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) walk(full);
      else if (entry.name.endsWith('.razor')) {
        const src = fs.readFileSync(full, 'utf-8');
        for (const m of src.matchAll(pageRe)) routes.add(m[1]);
      }
    }
  };
  walk(DOCS_PAGES_DIR);
  return [...routes].sort();
}

// Route -> filesystem-safe baseline name, e.g. /components/charts/area -> charts-area
function slugFor(route) {
  return route.replace(/^\/components\//, '').replace(/\//g, '-');
}

// serverProc is OPTIONAL — passed only when this script spawned its own docs
// dev-server (not when --base-url points at an externally-running instance).
// Guards against the "port-squatting" corruption this port was chosen to
// avoid in the first place: if our spawn hit address-in-use and exited, an
// HTTP response at `url` came from a DIFFERENT, pre-existing process — not
// the checkout we're about to sweep. We refuse to treat that as "ready".
function waitForServer(url, timeoutMs, serverProc) {
  const deadline = Date.now() + timeoutMs;
  return new Promise((resolve, reject) => {
    let settled = false;
    const cleanup = () => { if (serverProc) serverProc.removeListener('exit', onExit); };
    const onExit = (code) => {
      if (settled) return;
      settled = true;
      reject(new Error(
        `docs dev-server process exited (code ${code}) before it became reachable at ${url} — ` +
        `likely a port collision (another process already bound that port) or a startup crash.`));
    };
    if (serverProc) serverProc.once('exit', onExit);
    const tryOnce = () => {
      if (settled) return;
      const req = http.get(url, (res) => {
        res.resume();
        if (settled) return;
        // Our own spawn may have ALREADY exited (e.g. lost an address-in-use
        // race) even though something answered at this URL — that response
        // is from an unrelated process squatting on the port, not our build.
        if (serverProc && serverProc.exitCode !== null) {
          settled = true;
          cleanup();
          reject(new Error(
            `docs dev-server process already exited (code ${serverProc.exitCode}) — the response at ` +
            `${url} came from a different, pre-existing process (port collision).`));
          return;
        }
        settled = true;
        cleanup();
        resolve();
      });
      req.on('error', () => {
        if (settled) return;
        if (Date.now() > deadline) {
          settled = true;
          cleanup();
          reject(new Error(`docs dev-server did not come up at ${url} within ${timeoutMs}ms`));
        } else {
          setTimeout(tryOnce, 500);
        }
      });
      req.setTimeout(2000, () => req.destroy());
    };
    tryOnce();
  });
}

async function main() {
  fs.mkdirSync(BASELINES_DIR, { recursive: true });
  if (fs.existsSync(DIFFS_DIR)) fs.rmSync(DIFFS_DIR, { recursive: true, force: true });
  fs.mkdirSync(DIFFS_DIR, { recursive: true });

  let routes = discoverRoutes();
  if (ROUTE_FILTER) routes = routes.filter((r) => r.includes(ROUTE_FILTER));
  if (routes.length === 0) {
    console.error('No routes matched — nothing to do.');
    process.exit(1);
  }
  console.log(`Discovered ${routes.length} /components/* route(s)${ROUTE_FILTER ? ` (filtered by "${ROUTE_FILTER}")` : ''}.`);

  let serverProc = null;
  let baseUrl = BASE_URL;
  if (!baseUrl) {
    baseUrl = `http://localhost:${PORT}`;
    console.log(`Spawning docs dev-server on ${baseUrl} ...`);
    const dotnet = process.env.DOTNET_EXE || 'dotnet';
    // CI pre-builds Release (see visual-regression.yml) and sets VR_NO_BUILD=1
    // to reuse it instead of rebuilding Debug here. Local runs default to a
    // plain `dotnet run` (Debug, builds on demand) for a no-setup experience.
    const runArgs = process.env.VR_NO_BUILD === '1'
      ? ['run', '--no-launch-profile', '-c', 'Release', '--no-build', '--urls', baseUrl]
      : ['run', '--no-launch-profile', '--urls', baseUrl];
    serverProc = spawn(dotnet, runArgs, {
      cwd: path.join(REPO_ROOT, 'docs', 'Lumeo.Docs'),
      env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development' },
      stdio: ['ignore', 'pipe', 'pipe'],
    });
    let serverLog = '';
    serverProc.stdout.on('data', (d) => { serverLog += d; });
    serverProc.stderr.on('data', (d) => { serverLog += d; });
    serverProc.on('exit', (code) => {
      if (code !== null && code !== 0) {
        console.error(`docs dev-server exited early (code ${code}). Last output:\n${serverLog.slice(-4000)}`);
      }
    });
    try {
      await waitForServer(baseUrl, 120_000, serverProc);
    } catch (e) {
      console.error(serverLog.slice(-4000));
      throw e;
    }
    console.log('docs dev-server is up.');
  } else {
    console.log(`Using externally-running docs server at ${baseUrl}.`);
  }

  const browser = await chromium.launch();
  const results = [];

  try {
    for (const theme of ['light', 'dark']) {
      const context = await browser.newContext({
        viewport: VIEWPORT,
        reducedMotion: 'reduce', // mask known-dynamic regions: CSS honoring
                                  // prefers-reduced-motion collapses to ~1ms
                                  // (same mechanism WCAG 2.3.3 support uses —
                                  // see lumeo.css's reduced-motion blocks).
      });
      const page = await context.newPage();
      // Freeze the browser/WASM clock BEFORE the app boots. Several swept docs
      // routes render DateTime.Today/DateTime.Now client-side (Calendar,
      // DatePicker, Scheduler, AgentMessageList, ...), so without this the
      // committed baselines are tied to the day/minute they were captured and
      // later scheduled runs diff moving "today" highlights and timestamps
      // instead of real visual regressions. Runs as an init script (before any
      // page — including the Blazor WASM runtime bootstrap — executes) so
      // dotnet's own wall-clock derivation (which reads Date.now() at startup)
      // observes the frozen value too, not just app code that calls `new
      // Date()` directly.
      await page.addInitScript((fixedNowMs) => {
        const OriginalDate = Date;
        class FrozenDate extends OriginalDate {
          constructor(...args) {
            if (args.length === 0) super(fixedNowMs);
            else super(...args);
          }
          static now() { return fixedNowMs; }
        }
        // eslint-disable-next-line no-global-assign
        Date = FrozenDate;
      }, FROZEN_NOW_MS);
      // Deterministic theme + no first-run banners stealing focus/paint.
      await page.addInitScript((mode) => {
        try {
          localStorage.setItem('theme-mode', mode);
          localStorage.setItem('theme', mode);
          localStorage.setItem('lumeo:consent:v1', JSON.stringify({ analytics: false, marketing: false }));
        } catch (_) { /* ignore */ }
      }, theme);

      for (const route of routes) {
        const slug = `${slugFor(route)}.${theme}`;
        try {
          const res = await page.goto(`${baseUrl}${route}`, { waitUntil: 'networkidle', timeout: 30_000 });
          if (!res || res.status() >= 400) {
            results.push({ route, theme, status: 'ERROR', detail: `HTTP ${res ? res.status() : 'no response'}` });
            continue;
          }
          // Belt-and-suspenders on top of reducedMotion: freeze anything that
          // doesn't gate on prefers-reduced-motion (infinite marquees,
          // shimmer/sparkle decorations, live counters) and let two rAFs
          // flush layout before capture — same idiom as HomePageVisualTests.
          // NOTE: animation-play-state:paused was tried first and rejected —
          // it freezes whatever progress each element happened to reach at
          // the moment the style landed, which is itself non-deterministic
          // (wall-clock dependent) and produced a *worse* flake than no
          // override at all. Collapsing duration/delay to ~0 instead forces
          // every animation to its "to" keyframe deterministically — same
          // idiom as HomePageVisualTests.cs.
          await page.addStyleTag({
            content: '*, *::before, *::after { animation-duration: 0.001s !important; animation-delay: 0s !important; transition-duration: 0s !important; transition-delay: 0s !important; caret-color: transparent !important; scroll-behavior: auto !important; }',
          });
          // The docs sidebar smooth-scrolls its active-route item into view on
          // navigation, and that scroll can already be mid-flight by the time
          // our style tag above lands (scroll-behavior:auto only affects
          // scrolls triggered AFTER it's injected) — the single biggest
          // remaining source of flake after the font-swap fix (visible as the
          // WHOLE sidebar lighting up in a diff, not just below-the-fold
          // content). Force-settle by re-triggering an instant scroll on
          // whatever the app marked as the current route link.
          await page.evaluate(() => {
            const active = document.querySelector('[aria-current="page"]');
            active?.scrollIntoView({ behavior: 'instant', block: 'nearest' });
          });
          await page.waitForTimeout(150);
          // Web-font swap (Google Fonts loaded async) reflows text after
          // networkidle fires, shifting everything below the fold by a few
          // px — the single biggest source of screenshot flake seen while
          // building this harness. Wait for fonts to actually finish before
          // the two-rAF layout settle.
          await page.evaluate(() => (document.fonts && document.fonts.ready) ? document.fonts.ready : null);
          await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
          // A second networkidle wait catches deferred/second-order fetches
          // (e.g. a "N tests" badge sourced from a manifest fetched after
          // the initial render) that can still land after the first one and
          // shift layout below the fold.
          try { await page.waitForLoadState('networkidle', { timeout: 5_000 }); } catch (_) { /* best effort */ }
          // Final settle buffer — some demo pages (e.g. Chart) lazy-load a
          // heavy JS chunk (Apache ECharts) client-side on demand; the chunk
          // fetch itself can complete inside the networkidle window but the
          // widget's own mount/first-paint lands a bit after, still shifting
          // layout below it. A flat buffer catches this class of "async
          // resource resolved, but its DOM/layout effect landed late"
          // without special-casing individual routes.
          await page.waitForTimeout(400);

          const buffer = await page.screenshot({ fullPage: false });
          const result = compareOrUpdate(slug, buffer, route, theme);
          results.push(result);
        } catch (e) {
          results.push({ route, theme, status: 'ERROR', detail: e.message });
        }
      }
      await context.close();
    }
  } finally {
    await browser.close();
    if (serverProc) serverProc.kill();
  }

  report(results);
}

function compareOrUpdate(slug, buffer, route, theme) {
  const baselinePath = path.join(BASELINES_DIR, `${slug}.png`);
  if (UPDATE) {
    fs.writeFileSync(baselinePath, buffer);
    return { route, theme, status: 'UPDATED', detail: baselinePath };
  }
  if (!fs.existsSync(baselinePath)) {
    // A missing baseline in compare mode must FAIL, not silently pass as
    // "NEW" — that used to auto-write the file to the working tree, which
    // let an added route (or an accidentally deleted baseline) sail through
    // CI without ever being screenshotted-and-compared. Only `--update` is
    // allowed to create baselines.
    return {
      route, theme, status: 'FAIL',
      detail: `missing baseline: ${baselinePath} — run with --update to create it`,
    };
  }

  const baseline = PNG.sync.read(fs.readFileSync(baselinePath));
  const current = PNG.sync.read(buffer);
  if (baseline.width !== current.width || baseline.height !== current.height) {
    return {
      route, theme, status: 'FAIL',
      detail: `dimension mismatch: baseline ${baseline.width}x${baseline.height} vs current ${current.width}x${current.height}`,
    };
  }

  const { width, height } = baseline;
  const diff = new PNG({ width, height });
  const diffPixels = pixelmatch(baseline.data, current.data, diff.data, width, height, { threshold: THRESHOLD });
  const ratio = diffPixels / (width * height);

  if (ratio >= MAX_DIFF_RATIO) {
    const diffPath = path.join(DIFFS_DIR, `${slug}.diff.png`);
    const currentPath = path.join(DIFFS_DIR, `${slug}.current.png`);
    fs.writeFileSync(diffPath, PNG.sync.write(diff));
    fs.writeFileSync(currentPath, buffer);
    return {
      route, theme, status: 'FAIL',
      detail: `${diffPixels}/${width * height} px (${(ratio * 100).toFixed(2)}%) exceed ${(MAX_DIFF_RATIO * 100).toFixed(2)}% tolerance — diff: ${diffPath}`,
    };
  }
  return { route, theme, status: 'PASS', detail: `${(ratio * 100).toFixed(3)}% diff` };
}

function report(results) {
  const byStatus = { PASS: 0, FAIL: 0, NEW: 0, UPDATED: 0, ERROR: 0 };
  for (const r of results) byStatus[r.status] = (byStatus[r.status] || 0) + 1;

  for (const r of results) {
    if (r.status === 'FAIL' || r.status === 'ERROR') {
      console.log(`${r.status}  ${r.route} [${r.theme}] — ${r.detail}`);
    }
  }
  console.log('\n=== visual-regression summary ===');
  for (const [status, count] of Object.entries(byStatus)) {
    if (count > 0) console.log(`  ${status}: ${count}`);
  }
  console.log(`  total: ${results.length}`);

  if (byStatus.FAIL > 0 || byStatus.ERROR > 0) {
    process.exitCode = 1;
  }
}

main().catch((e) => {
  console.error('visual-regression run FAILED:', e);
  process.exit(1);
});
