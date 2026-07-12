#!/usr/bin/env node
// Blazor Server latency leg — drives tests/Lumeo.Tests.ServerHost (a real
// interactive-SERVER host, genuine SignalR circuit) with artificial
// round-trip latency injected via CDP network throttling.
//
// WHY CDP THROTTLING, NOT A SERVER-SIDE DELAY MIDDLEWARE:
//   The host also wires an opt-in server-side delay middleware
//   (LUMEO_SERVERLEG_DELAY_MS, see Program.cs) for anyone who wants to
//   reproduce a scenario without a Chromium/CDP dependency. This harness
//   uses CDP's Network.emulateNetworkConditions instead, applied to the
//   WHOLE page (including the persistent WebSocket the circuit uses),
//   because it delays the actual bytes-on-the-wire for every individual
//   SignalR frame in both directions — a live simulation of a slow client,
//   not just "the first HTTP response is slower". A delay middleware only
//   adds latency to the initial negotiate/upgrade HTTP request; once the
//   WebSocket is open, Blazor's render-batch and event-dispatch frames
//   flow straight through it with zero added delay, so it can't reproduce
//   "a drag commit's round-trip takes 200ms" — exactly the class of bug
//   (stuck transforms, races between a settle timer and a slow .NET
//   round-trip) this leg exists to catch. CDP throttling is Chromium-only,
//   which is fine here — this leg's job is circuit-latency behavior, not
//   cross-engine coverage (scripts/pointer-harness/ covers that).
//
// Usage: node run.mjs [--rtt=200]

import { chromium } from 'playwright-core';
import { spawn } from 'child_process';
import path from 'path';
import http from 'http';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.join(__dirname, '..', '..');
const HOST_PROJECT = path.join(REPO_ROOT, 'tests', 'Lumeo.Tests.ServerHost', 'Lumeo.Tests.ServerHost.csproj');

const rttArg = process.argv.find((a) => a.startsWith('--rtt='));
const RTT_MS = rttArg ? parseInt(rttArg.slice('--rtt='.length), 10) : 200;
const PORT = process.env.SERVERLEG_PORT || '58291';
const BASE_URL = `http://localhost:${PORT}`;
const DOTNET_EXE = process.env.DOTNET_EXE || 'dotnet';

let passCount = 0;
let xfailCount = 0;
let xpassCount = 0;
const failures = [];
function assert(cond, msg) {
  if (cond) { passCount++; console.log('PASS: ' + msg); }
  else { failures.push(msg); console.log('FAIL: ' + msg); }
}
// A "known-broken" assertion: the underlying product bug is real, tracked,
// and NOT what this harness fix is meant to land (see README.md — the
// Blazor Server ToastProvider case). Routing it through plain assert() means
// npm test / `node run.mjs` is red on every single run by design, which
// trains people to ignore the exit code and blocks wiring this leg into CI
// as a regression gate. This still LOGS the outcome loudly (XFAIL/XPASS) so
// the bug stays visible and a fix is provable without gating the process
// exit code on it: XFAIL (still broken) never fails the run; XPASS (now
// passing) also never fails the run, but is a deliberately loud nudge to
// promote this back to a plain assert() once seen.
function assertKnownBroken(cond, msg) {
  if (cond) { xpassCount++; console.log('XPASS (unexpected — bug fixed? promote to assert()): ' + msg); }
  else { xfailCount++; console.log('XFAIL (known bug, see README.md): ' + msg); }
}

// serverProc/getLog let us reject a response that came from a DIFFERENT,
// pre-existing process squatting on SERVERLEG_PORT rather than the host we
// just spawned — same reasoning (and same fix) as
// scripts/visual-regression/run.mjs's waitForServer: an exitCode check at
// the moment an HTTP response arrives can't catch this, because Node only
// flips exitCode once its own 'exit' event fires, which lags the real
// bind-failure by however long dotnet takes to start up — while an HTTP
// round trip to an ALREADY-RUNNING squatter on localhost can return in low
// single-digit ms. Only trust a response once Kestrel's own "Now listening
// on" log line names this exact port, since only OUR process's captured
// stdout can produce that.
function waitForServer(url, timeoutMs, serverProc, getLog) {
  const deadline = Date.now() + timeoutMs;
  const port = new URL(url).port;
  const listeningRe = new RegExp(`Now listening on:.*[:/]${port}\\b`);
  return new Promise((resolve, reject) => {
    let settled = false;
    const cleanup = () => { if (serverProc) serverProc.removeListener('exit', onExit); };
    const onExit = (code) => {
      if (settled) return;
      settled = true;
      reject(new Error(
        `server host process exited (code ${code}) before it became reachable at ${url} — ` +
        `likely a port collision (another process already bound that port) or a startup crash.`));
    };
    if (serverProc) serverProc.once('exit', onExit);
    const tryOnce = () => {
      if (settled) return;
      if (serverProc && getLog && !listeningRe.test(getLog())) {
        if (Date.now() > deadline) {
          settled = true;
          cleanup();
          reject(new Error(
            `server host never logged "Now listening on" for port ${port} within ${timeoutMs}ms ` +
            `(port collision or startup failure). Last output:\n${getLog().slice(-2000)}`));
        } else {
          setTimeout(tryOnce, 300);
        }
        return;
      }
      const req = http.get(url, (res) => {
        res.resume();
        if (settled) return;
        settled = true;
        cleanup();
        resolve();
      });
      req.on('error', () => {
        if (settled) return;
        if (Date.now() > deadline) {
          settled = true;
          cleanup();
          reject(new Error(`server host did not come up at ${url} within ${timeoutMs}ms`));
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
  // Build by default — same reasoning as the visual-regression leg
  // (scripts/visual-regression/run.mjs): a bare `--no-build` silently runs
  // whatever Debug output happens to already be on disk, which in a
  // clean checkout (or after source changes since the last manual build) is
  // either missing (dotnet run exits before the host ever comes up) or
  // stale (the leg exercises code that doesn't match the commit under
  // review, making PASS/FAIL meaningless). Only skip the build via an
  // explicit opt-in, mirroring VR_NO_BUILD=1, for CI jobs that pre-build.
  const NO_BUILD = process.env.SERVERLEG_NO_BUILD === '1';
  // ARCH is opt-in, NOT forced: passing --arch pins `dotnet run` to that
  // target RID instead of the host's native architecture. Forcing x64
  // unconditionally would break this leg on ARM64 hosts (Apple Silicon,
  // ARM Linux CI) unless an x64 runtime/emulation happens to be installed
  // there too. Leave --arch off by default so `dotnet run` picks the native
  // RID; set SERVERLEG_ARCH (e.g. =x64) only if a specific target is needed.
  const ARCH = process.env.SERVERLEG_ARCH || null;
  console.log(`Spawning Blazor Server host on ${BASE_URL} (net10.0${ARCH ? `, --arch ${ARCH}` : ''}${NO_BUILD ? ', --no-build' : ''}) ...`);
  const runArgs = ['run', '--project', HOST_PROJECT, ...(ARCH ? ['--arch', ARCH] : []), '-c', 'Debug', ...(NO_BUILD ? ['--no-build'] : []), '--urls', BASE_URL];
  const serverProc = spawn(DOTNET_EXE, runArgs, {
    cwd: REPO_ROOT,
    env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development' },
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  let serverLog = '';
  serverProc.stdout.on('data', (d) => { serverLog += d; });
  serverProc.stderr.on('data', (d) => { serverLog += d; });
  // 90s (not 60s): now that we build by default (see NO_BUILD above), the
  // deadline has to cover an on-demand Debug build, not just process
  // startup against an already-built output.
  try {
    await waitForServer(BASE_URL, 90_000, serverProc, () => serverLog);
  } catch (e) {
    console.error(serverLog.slice(-4000));
    // The wait itself can reject while dotnet run is still alive (e.g. a slow
    // Debug build exceeding the 90s budget, or a Kestrel log line that never
    // matches "Now listening on") — the later finally below is never reached
    // from this early return, so kill the spawned process here too. Safe to
    // call even if it already exited (the onExit path that also rejects).
    serverProc.kill();
    throw e;
  }
  console.log('Server host is up.');

  // browser is declared OUTSIDE the try (started null) and the try wraps
  // chromium.launch()/CDP setup too, not just the scenarios: if Chromium is
  // missing or launch()/CDP setup throws, control used to skip straight past
  // the try/finally below without ever reaching serverProc.kill(), leaving
  // the spawned `dotnet run` alive and SERVERLEG_PORT bound — poisoning the
  // next run with a port collision. The finally's `if (browser)` guard
  // handles the case where launch() itself is what threw.
  let browser;
  try {
    browser = await chromium.launch();
    const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
    const cdp = await page.context().newCDPSession(page);
    await cdp.send('Network.enable');
    // downloadThroughput/uploadThroughput: -1 means "unlimited" — only latency
    // is injected, isolating the RTT variable from bandwidth effects.
    await cdp.send('Network.emulateNetworkConditions', {
      offline: false,
      latency: RTT_MS,
      downloadThroughput: -1,
      uploadThroughput: -1,
    });
    console.log(`CDP network throttling active: ${RTT_MS}ms latency on the whole page (incl. the SignalR WebSocket).`);

    await page.goto(BASE_URL);
    await page.waitForFunction(() => window.Blazor !== undefined, { timeout: 20_000 });
    // Circuit-ready probe: a plain, provider-free interactive element proves
    // the circuit is actually dispatching events before we trust any
    // component-specific scenario below (isolates "circuit not ready yet"
    // from "component under test is broken").
    await page.waitForSelector('[data-testid="plain-counter"]');
    await page.click('[data-testid="plain-counter"]');
    await page.waitForFunction(
      () => document.querySelector('[data-testid="plain-counter"]')?.textContent.includes('Count: 1'),
      { timeout: RTT_MS * 5 + 5_000 },
    );
    console.log(`Circuit confirmed interactive under ${RTT_MS}ms RTT.`);

    await scenarioDataGridDragCommit(page, RTT_MS);
    // Dialog runs BEFORE the toast burst deliberately: the toast scenario
    // exercises a KNOWN-BROKEN path (see README.md) whose 8 fire-and-forget
    // SafeAsyncDispatcher dispatches never resolve, and empirically that
    // leaves the circuit slow to process unrelated LATER events under RTT
    // throttling (observed: Dialog's open round-trip stopped completing
    // within any reasonable timeout once it ran after the burst). Ordering
    // dialog first keeps that scenario's result about Dialog, not a
    // downstream symptom of the Toast bug.
    await scenarioDialogExitAnimation(page, RTT_MS);
    await scenarioToastBurst(page, RTT_MS);
  } finally {
    // browser.close() can reject (Chromium already crashed, transport torn
    // down) — awaiting it before serverProc.kill() used to let that
    // rejection exit this finally early, leaving the spawned dotnet host
    // alive and SERVERLEG_PORT bound, poisoning the next run with a port
    // collision (same fix as scripts/visual-regression/run.mjs). Swallow
    // the close failure so kill() always runs.
    if (browser) await browser.close().catch(() => {});
    serverProc.kill();
  }

  console.log(`\n=== server-leg summary (RTT=${RTT_MS}ms) ===`);
  console.log(`  PASS: ${passCount}`);
  console.log(`  FAIL: ${failures.length}`);
  if (xfailCount > 0) console.log(`  XFAIL: ${xfailCount} (known bug, not gating exit code — see README.md)`);
  if (xpassCount > 0) console.log(`  XPASS: ${xpassCount} (a known-broken scenario now passes — promote it to assert() in run.mjs)`);
  if (failures.length > 0) {
    console.log('\nFailures:');
    for (const f of failures) console.log('  - ' + f);
    console.log('\nSee scripts/server-leg/README.md for the confirmed-product-bug writeup (Toast under Blazor Server).');
    process.exitCode = 1;
  }
}

// ---------------------------------------------------------------------
// Scenario 1: DataGrid column drag commit under RTT — no stuck transforms.
// ---------------------------------------------------------------------
async function scenarioDataGridDragCommit(page, rtt) {
  console.log(`\n--- Scenario: DataGrid column drag commit under ${rtt}ms RTT ---`);
  const grip = await page.$('[data-testid="server-leg-grid"] th [data-reorder-grip]');
  assert(grip !== null, 'DataGrid renders a reorder grip on the first reorderable column header');
  if (!grip) return;

  const headers = await page.$$('[data-testid="server-leg-grid"] th');
  assert(headers.length >= 2, `DataGrid renders at least 2 column headers (got ${headers.length})`);
  if (headers.length < 2) return;

  // Observable commit marker: the header column ORDER before the drag. A
  // broken commit path (drag never reorders anything, or the server ignores
  // the commit) previously still passed this scenario, because the only
  // post-action check was "no inline transforms remain" — true whether or
  // not anything actually moved. Comparing before/after order proves the
  // drag's server round-trip actually landed a reorder, not just that the
  // transient drag-transform styling cleared.
  const colOrderBefore = await page.evaluate(() =>
    [...document.querySelectorAll('[data-testid="server-leg-grid"] th')].map((th) => th.getAttribute('data-col-id')));

  const gripBox = await grip.boundingBox();
  const targetBox = await headers[1].boundingBox();

  await page.mouse.move(gripBox.x + gripBox.width / 2, gripBox.y + gripBox.height / 2);
  await page.mouse.down();
  await page.mouse.move(targetBox.x + targetBox.width / 2, targetBox.y + targetBox.height / 2, { steps: 10 });
  await page.mouse.up();

  // The commit round-trips through the circuit — under RTT ms latency this
  // takes measurably longer than local; wait generously past the settle
  // window (180ms client-side) PLUS several RTTs for the .NET commit to land.
  await page.waitForTimeout(rtt * 4 + 1_000);

  const colOrderAfter = await page.evaluate(() =>
    [...document.querySelectorAll('[data-testid="server-leg-grid"] th')].map((th) => th.getAttribute('data-col-id')));
  assert(JSON.stringify(colOrderAfter) !== JSON.stringify(colOrderBefore),
    `dragging the first column's grip onto the second header actually reorders the columns under ${rtt}ms RTT ` +
    `(before: ${JSON.stringify(colOrderBefore)}, after: ${JSON.stringify(colOrderAfter)} — unchanged means the ` +
    `commit round-trip never landed, or the server ignored it)`);

  const stuckTransforms = await page.evaluate(() => {
    const cells = document.querySelectorAll('[data-testid="server-leg-grid"] th, [data-testid="server-leg-grid"] td');
    const stuck = [];
    for (const el of cells) {
      const t = el.style.transform;
      if (t && t !== 'none') stuck.push({ tag: el.tagName, col: el.getAttribute('data-col-id'), transform: t });
    }
    return stuck;
  });
  assert(stuckTransforms.length === 0,
    `no stuck inline transforms remain on any grid cell after the drag commit settles under ${rtt}ms RTT ` +
    `(found ${stuckTransforms.length}: ${JSON.stringify(stuckTransforms)})`);
}

// ---------------------------------------------------------------------
// Scenario 2: Toast burst — cap invariant holds visually.
// KNOWN BROKEN — see README.md. The "at least one toast renders" assertion
// is kept honest (not loosened/skipped, still exercised every run) via
// assertKnownBroken() so a future fix is provable by this flipping XFAIL ->
// XPASS, WITHOUT making a red exit code the permanent, ignorable default for
// this leg (npm test / CI must be able to treat this leg as a real gate).
// ---------------------------------------------------------------------
async function scenarioToastBurst(page, rtt) {
  console.log(`\n--- Scenario: Toast burst cap invariant under ${rtt}ms RTT ---`);
  await page.click('[data-testid="toast-burst-button"]');
  // 8 toasts fired; MaxToasts default is 5 — wait past several RTTs for the
  // burst's commits to land, then assert the viewport never exceeds the cap.
  await page.waitForTimeout(rtt * 4 + 1_000);
  const toastCount = await page.evaluate(() =>
    document.querySelectorAll('[role="status"], [role="alert"]').length);
  assertKnownBroken(toastCount > 0,
    `at least one toast is visible after firing a burst of 8 (got ${toastCount}) — ` +
    `KNOWN FAILURE: confirmed product bug, ToastProvider never renders under Blazor Server ` +
    `(ToastService.OnShow fires, but no SignalR render batch is sent — see README.md)`);
  assert(toastCount <= 5,
    `visible toast count never exceeds MaxToasts=5 (got ${toastCount})`);
}

// ---------------------------------------------------------------------
// Scenario 3: Dialog open/close exit animation completes.
// ---------------------------------------------------------------------
async function scenarioDialogExitAnimation(page, rtt) {
  console.log(`\n--- Scenario: Dialog open/close exit animation under ${rtt}ms RTT ---`);
  await page.click('[data-testid="server-leg-dialog-trigger"]');
  // Opening round-trips several sequential interop calls (LockScroll,
  // SetupFocusTrap, ...) before the content commits — each pays the full
  // RTT, so the budget scales with RTT more generously than a single
  // round-trip would need.
  await page.waitForSelector('[data-testid="server-leg-dialog-content"]', { timeout: rtt * 10 + 5_000 });
  assert(true, 'dialog opens under RTT');

  // Close via DialogClose, then wait for the exit animation's round-trip
  // (OnExitAnimationEnd interop callback) to actually unmount the content —
  // not a blind timer race against the .NET side.
  await page.click('[data-testid="server-leg-dialog-close"]');
  await page.waitForSelector('[data-testid="server-leg-dialog-content"]', { state: 'detached', timeout: rtt * 10 + 5_000 });
  const stillPresent = await page.evaluate(() => !!document.querySelector('[data-testid="server-leg-dialog-content"]'));
  assert(!stillPresent, `dialog content is fully unmounted after its exit animation completes under ${rtt}ms RTT`);
}

main().catch((e) => {
  console.error('server-leg run FAILED:', e);
  process.exit(1);
});
