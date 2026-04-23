# Lumeo Docs SSG Prerendering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a build-time static HTML render for every public route of the Lumeo docs site, so Cloudflare Pages serves prerendered content for first paint and SEO while the Blazor WASM app hydrates on top.

**Architecture:** Keep the existing standalone Blazor WASM project untouched. After `dotnet publish`, run a Node.js prerender step that (a) serves the published `wwwroot/` via a local static server, (b) visits every route in `sitemap.xml` with Puppeteer, (c) waits for Blazor to signal "ready," (d) captures the rendered HTML, and (e) writes `<route>/index.html` files into the release directory. Cloudflare Pages' `_redirects` already handles SPA fallback, so when a visitor lands on `/components/button`, the edge serves the prerendered HTML immediately, then the WASM bundle boots and takes over the `<main>` element — identical markup, no flash.

**Tech Stack:** Node 20 + Puppeteer for the crawler, `sirv` for the local static server, existing .NET 10 Blazor WASM build, existing GitHub Actions → Cloudflare Pages Direct Upload pipeline.

**Trade-offs / why this approach:**
- No project restructuring. The alternative — converting to a Blazor Web App with `InteractiveWebAssembly` render mode — would require a full ASP.NET Core server project, splitting components into Client/Server/Shared, and still wouldn't solve Cloudflare Pages (static-only) hosting.
- Route-crawler prerender is framework-agnostic and battle-tested for SPAs. If the Blazor build ever changes internals, the crawler doesn't care — it reads the DOM.
- Puppeteer in CI adds ~90s to deploys (acceptable; current deploy is ~2m40s).

**Non-goals:**
- Partial hydration / island architecture (Blazor WASM still boots full app).
- Per-page bundles / route-level code-splitting (different optimization).
- Server-side rendering at request time (static only).

---

## File Structure

**New files:**
- `scripts/prerender/package.json` — Node deps (puppeteer, sirv)
- `scripts/prerender/package-lock.json` — lockfile
- `scripts/prerender/server.mjs` — tiny static file server (localhost:4300) serving the publish output
- `scripts/prerender/prerender.mjs` — Puppeteer crawler: reads `sitemap.xml`, crawls each route, writes `<route>/index.html`
- `scripts/prerender/verify.mjs` — post-deploy smoke test: curl a sample of live routes, assert prerendered content
- `scripts/prerender/README.md` — run locally: `npm install && node prerender.mjs ../../release/wwwroot`
- `scripts/prerender/.gitignore` — node_modules

**Modified files:**
- `docs/Lumeo.Docs/Layout/MainLayout.razor` — in `OnAfterRenderAsync(firstRender: true)`, set `document.documentElement.dataset.blazorReady = 'true'` and store `document.title` / description before hydration replaces content. Signal used by Puppeteer to know when to snapshot.
- `docs/Lumeo.Docs/wwwroot/index.html` — no structural change, but add a `<!-- prerender:head -->` marker comment where per-route `<title>` and `<meta description>` will be injected by the crawler.
- `.github/workflows/deploy-cloudflare.yml` — insert prerender step between Tailwind rebuild and CF Pages deploy.

**Unchanged but critical:**
- `docs/Lumeo.Docs/wwwroot/_redirects` — existing `/* /index.html 200` still handles SPA fallback for routes not prerendered (or 404s).
- `docs/Lumeo.Docs/wwwroot/_headers` — existing cache-control; prerendered HTML inherits the `/` no-cache rule (good — we want fresh HTML, cached JS/CSS).

---

## Task 1: Scaffold the prerender tool directory

**Files:**
- Create: `scripts/prerender/package.json`
- Create: `scripts/prerender/.gitignore`
- Create: `scripts/prerender/README.md`

- [ ] **Step 1: Create `scripts/prerender/package.json`**

```json
{
  "name": "lumeo-prerender",
  "version": "1.0.0",
  "private": true,
  "description": "Static HTML prerender for the Lumeo docs site. Runs in CI after dotnet publish.",
  "type": "module",
  "engines": { "node": ">=20" },
  "scripts": {
    "prerender": "node prerender.mjs",
    "verify": "node verify.mjs"
  },
  "dependencies": {
    "puppeteer": "^23.11.1",
    "sirv": "^3.0.0",
    "fast-xml-parser": "^4.5.1"
  }
}
```

- [ ] **Step 2: Create `scripts/prerender/.gitignore`**

```
node_modules/
```

- [ ] **Step 3: Create `scripts/prerender/README.md`**

````markdown
# Lumeo docs prerender

Static HTML renderer for the Blazor WASM docs site. Produces `<route>/index.html`
for every route in `sitemap.xml` so first paint and search engine crawlers get
rendered content instead of an empty SPA shell.

## Run locally

```bash
# 1. Publish the docs site
dotnet publish ../../docs/Lumeo.Docs/Lumeo.Docs.csproj -c Release -o ../../release

# 2. Install prerender deps (one-time)
npm install

# 3. Crawl
node prerender.mjs ../../release/wwwroot

# 4. Optional: smoke-test a deployed URL
node verify.mjs https://lumeo.nativ.sh
```

## How it works

1. `server.mjs` serves `<publish-dir>/wwwroot/` on `localhost:4300`.
2. `prerender.mjs` reads `sitemap.xml`, launches headless Chromium, visits
   each route, waits for `document.documentElement.dataset.blazorReady === 'true'`
   (signalled from `MainLayout.razor`), then writes the rendered `<html>` to
   `<route>/index.html` in the publish dir.
3. Cloudflare Pages then serves the prerendered HTML; the Blazor WASM bundle
   hydrates on top. Identical markup = no visible flash.
````

- [ ] **Step 4: Install deps to generate lockfile**

Run: `cd scripts/prerender && npm install`
Expected: `node_modules/` populated, `package-lock.json` created, zero vulnerabilities.

- [ ] **Step 5: Commit**

```bash
git add scripts/prerender/package.json scripts/prerender/package-lock.json scripts/prerender/.gitignore scripts/prerender/README.md
git commit -m "feat(prerender): scaffold Node-based static renderer tool directory"
```

---

## Task 2: Static file server for local crawl

**Files:**
- Create: `scripts/prerender/server.mjs`

- [ ] **Step 1: Write the server**

```javascript
// scripts/prerender/server.mjs
//
// Minimal static server used by prerender.mjs to serve the dotnet publish
// output during crawl. Single export: startServer(rootDir, port = 4300).
// Returns { url, close() }.
//
// SPA fallback: any route that doesn't resolve to a file returns index.html
// (200) — same behavior as Cloudflare Pages' _redirects, so Blazor routing
// boots correctly for every URL we crawl.

import { createServer } from 'node:http';
import sirv from 'sirv';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

export function startServer(rootDir, port = 4300) {
    const indexHtml = readFileSync(join(rootDir, 'index.html'), 'utf8');
    const serve = sirv(rootDir, { dev: true, etag: true, single: false });

    const server = createServer((req, res) => {
        serve(req, res, () => {
            res.statusCode = 200;
            res.setHeader('Content-Type', 'text/html; charset=utf-8');
            res.end(indexHtml);
        });
    });

    return new Promise((resolve, reject) => {
        server.on('error', reject);
        server.listen(port, '127.0.0.1', () => {
            resolve({
                url: `http://127.0.0.1:${port}`,
                close: () => new Promise(r => server.close(r)),
            });
        });
    });
}
```

- [ ] **Step 2: Manual sanity check**

Publish docs first if not already: `dotnet publish docs/Lumeo.Docs/Lumeo.Docs.csproj -c Release -o release`

Then from `scripts/prerender`:

```bash
node --input-type=module -e "import { startServer } from './server.mjs'; const s = await startServer('../../release/wwwroot'); console.log(s.url); setTimeout(() => process.exit(0), 2000);"
```

Expected output: `http://127.0.0.1:4300` followed by process exit. In a second terminal during the 2s window, `curl http://127.0.0.1:4300/components/button` should return HTML containing `<div id="app">` (SPA fallback served index.html, status 200).

- [ ] **Step 3: Commit**

```bash
git add scripts/prerender/server.mjs
git commit -m "feat(prerender): static file server with SPA fallback"
```

---

## Task 3: Blazor readiness signal

**Files:**
- Modify: `docs/Lumeo.Docs/Layout/MainLayout.razor:508-544` (the existing `OnAfterRenderAsync(bool firstRender)` method)
- Modify: `docs/Lumeo.Docs/wwwroot/index.html` (add one marker)

- [ ] **Step 1: Add JS interop call in `OnAfterRenderAsync` after first render completes**

In `docs/Lumeo.Docs/Layout/MainLayout.razor`, find the block:

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        try
        {
            Nav.LocationChanged += OnLocationChanged;
            await ThemeService.InitializeAsync();
            _darkMode = ThemeService.IsDark;
            ThemeService.OnThemeChanged += OnThemeServiceChanged;
            StateHasChanged();

            await JS.InvokeVoidAsync("lumeo.setupSearch");
```

After `await JS.InvokeVoidAsync("lumeo.setupSearch");` add:

```csharp
            // Signal to the prerender crawler (Puppeteer) that the app has
            // rendered the first route. No-op at runtime for real users.
            await JS.InvokeVoidAsync("eval",
                "document.documentElement.dataset.blazorReady = 'true'");
```

- [ ] **Step 2: Build and verify the signal fires locally**

```bash
dotnet run --project docs/Lumeo.Docs/Lumeo.Docs.csproj
```

Open `http://localhost:5287` in a browser, open DevTools console, run:

```js
document.documentElement.dataset.blazorReady
```

Expected: `"true"` (after the page has finished its first render; wait ~2s).

- [ ] **Step 3: Commit**

```bash
git add docs/Lumeo.Docs/Layout/MainLayout.razor
git commit -m "feat(prerender): set data-blazor-ready after first render completes"
```

---

## Task 4: Single-route prerender (the test case)

**Files:**
- Create: `scripts/prerender/prerender.mjs`

- [ ] **Step 1: Write the first version — hard-coded single route**

```javascript
// scripts/prerender/prerender.mjs
//
// Usage: node prerender.mjs <publish-dir>
//
// Reads publish-dir/wwwroot/sitemap.xml, serves that directory via a local
// static server, then for each <loc> in the sitemap:
//   1. Navigate Puppeteer to the route
//   2. Wait for document.documentElement.dataset.blazorReady === 'true'
//   3. Snapshot document.documentElement.outerHTML
//   4. Write to <publish-dir>/wwwroot/<route-path>/index.html
//
// Root route "/" overwrites index.html in place.
//
// This first version renders ONE hard-coded route for smoke testing. Task 5
// adds batch crawl from sitemap.

import { startServer } from './server.mjs';
import puppeteer from 'puppeteer';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';

const publishDir = process.argv[2];
if (!publishDir) {
    console.error('usage: node prerender.mjs <publish-dir>');
    process.exit(1);
}
const wwwroot = resolve(publishDir, 'wwwroot');

console.log(`[prerender] wwwroot = ${wwwroot}`);

const server = await startServer(wwwroot);
console.log(`[prerender] local server at ${server.url}`);

const browser = await puppeteer.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox'],
});

try {
    const route = '/privacy';
    const page = await browser.newPage();
    await page.setViewport({ width: 1280, height: 800 });

    const start = Date.now();
    await page.goto(server.url + route, { waitUntil: 'networkidle2', timeout: 30000 });
    await page.waitForFunction(
        () => document.documentElement.dataset.blazorReady === 'true',
        { timeout: 30000 },
    );

    const html = await page.evaluate(() => '<!DOCTYPE html>\n' + document.documentElement.outerHTML);

    const outDir = join(wwwroot, route);
    mkdirSync(outDir, { recursive: true });
    writeFileSync(join(outDir, 'index.html'), html, 'utf8');

    console.log(`[prerender] ✓ ${route} (${Date.now() - start}ms, ${(html.length / 1024).toFixed(1)}kB)`);

    await page.close();
} finally {
    await browser.close();
    await server.close();
}
```

- [ ] **Step 2: Run against a fresh publish**

```bash
dotnet publish docs/Lumeo.Docs/Lumeo.Docs.csproj -c Release -o release
cd scripts/prerender
node prerender.mjs ../../release
```

Expected output:

```
[prerender] wwwroot = .../release/wwwroot
[prerender] local server at http://127.0.0.1:4300
[prerender] ✓ /privacy (2500ms, 48.3kB)
```

- [ ] **Step 3: Verify the file contents**

```bash
grep -c "Privacy Policy" release/wwwroot/privacy/index.html
```

Expected: at least `1` (the page title or heading is present in prerendered HTML).

```bash
grep -c "<!DOCTYPE html>" release/wwwroot/privacy/index.html
```

Expected: `1`.

- [ ] **Step 4: Commit**

```bash
git add scripts/prerender/prerender.mjs
git commit -m "feat(prerender): single-route Puppeteer renderer — smoke test"
```

---

## Task 5: Batch crawl from sitemap

**Files:**
- Modify: `scripts/prerender/prerender.mjs`

- [ ] **Step 1: Replace the hard-coded route with a sitemap-driven crawl**

Replace the entire `try { ... } finally { ... }` block in `prerender.mjs` with:

```javascript
try {
    // Parse sitemap.xml for the list of routes to render.
    const sitemapPath = join(wwwroot, 'sitemap.xml');
    const sitemapXml = readFileSync(sitemapPath, 'utf8');
    const { XMLParser } = await import('fast-xml-parser');
    const parsed = new XMLParser().parse(sitemapXml);
    const urlEntries = Array.isArray(parsed.urlset?.url) ? parsed.urlset.url : [parsed.urlset.url];
    const routes = urlEntries
        .map(u => new URL(u.loc).pathname)
        .filter(p => !!p);

    console.log(`[prerender] ${routes.length} routes from sitemap`);

    // Crawl with concurrency = 4. Each worker pulls from the queue.
    const queue = [...routes];
    const results = { ok: 0, fail: 0, durationMs: 0 };
    const startAll = Date.now();

    async function worker(id) {
        const page = await browser.newPage();
        await page.setViewport({ width: 1280, height: 800 });
        while (queue.length) {
            const route = queue.shift();
            const t0 = Date.now();
            try {
                await page.goto(server.url + route, { waitUntil: 'networkidle2', timeout: 45000 });
                await page.waitForFunction(
                    () => document.documentElement.dataset.blazorReady === 'true',
                    { timeout: 30000 },
                );
                const html = await page.evaluate(() => '<!DOCTYPE html>\n' + document.documentElement.outerHTML);

                // Root "/" writes to wwwroot/index.html (overwrite the stock
                // shell); everything else writes to wwwroot/<path>/index.html.
                const outPath = route === '/'
                    ? join(wwwroot, 'index.html')
                    : join(wwwroot, route, 'index.html');
                mkdirSync(join(outPath, '..'), { recursive: true });
                writeFileSync(outPath, html, 'utf8');

                results.ok++;
                console.log(`[worker ${id}] ✓ ${route} (${Date.now() - t0}ms)`);
            } catch (err) {
                results.fail++;
                console.error(`[worker ${id}] ✗ ${route} — ${err.message}`);
            }
        }
        await page.close();
    }

    await Promise.all([worker(1), worker(2), worker(3), worker(4)]);

    results.durationMs = Date.now() - startAll;
    console.log(`[prerender] done: ${results.ok} ok, ${results.fail} failed, ${(results.durationMs / 1000).toFixed(1)}s total`);

    if (results.fail > 0) process.exitCode = 1;
} finally {
    await browser.close();
    await server.close();
}
```

Also add `readFileSync` to the imports at the top:

```javascript
import { mkdirSync, writeFileSync, readFileSync } from 'node:fs';
```

- [ ] **Step 2: Run the full crawl**

```bash
cd scripts/prerender
node prerender.mjs ../../release
```

Expected output (exact numbers vary with route count):

```
[prerender] wwwroot = .../release/wwwroot
[prerender] local server at http://127.0.0.1:4300
[prerender] 190 routes from sitemap
[worker 1] ✓ / (2100ms)
[worker 2] ✓ /components/button (2300ms)
... (188 more lines) ...
[prerender] done: 190 ok, 0 failed, 78.4s total
```

- [ ] **Step 3: Verify a handful of routes**

```bash
grep -c "Privacy Policy" release/wwwroot/privacy/index.html
grep -c "Button" release/wwwroot/components/button/index.html
grep -c "Consent Banner" release/wwwroot/components/consent-banner/index.html
```

Each expected: `>= 1`.

Also verify there's no "404" in any prerendered file:

```bash
grep -rl "Page not found" release/wwwroot/components/ | wc -l
```

Expected: `0`.

- [ ] **Step 4: Commit**

```bash
git add scripts/prerender/prerender.mjs
git commit -m "feat(prerender): batch crawl all sitemap routes with concurrency 4"
```

---

## Task 6: Wire prerender into the deploy workflow

**Files:**
- Modify: `.github/workflows/deploy-cloudflare.yml`

- [ ] **Step 1: Read current workflow**

Run: `cat .github/workflows/deploy-cloudflare.yml`

Note the step order — specifically the step that runs after Tailwind rebuild and `.br`/`.gz` cleanup, before the `cloudflare/pages-action@v1` deploy step.

- [ ] **Step 2: Insert prerender step**

Insert the following step **after** the Tailwind rebuild + compressed-asset cleanup step, and **before** the `cloudflare/pages-action@v1` deploy step:

```yaml
      - name: Prerender routes
        working-directory: scripts/prerender
        run: |
          npm ci
          node prerender.mjs ../../release

      - name: Drop compressed variants of prerendered HTML
        # dotnet publish pre-compressed the shell index.html; our new per-route
        # index.html files aren't compressed, but we must also remove the stale
        # .br/.gz so CF doesn't content-negotiate to the old shell.
        run: |
          find release/wwwroot -name 'index.html.br' -delete
          find release/wwwroot -name 'index.html.gz' -delete
```

- [ ] **Step 3: Push to a throwaway branch and verify**

```bash
git checkout -b prerender-ci-test
git add .github/workflows/deploy-cloudflare.yml
git commit -m "ci(prerender): run prerender step after publish, before CF deploy"
git push -u origin prerender-ci-test
```

Wait for the workflow run: `gh run watch $(gh run list --workflow=deploy-cloudflare.yml --limit=1 --json databaseId -q '.[0].databaseId')`

Expected: workflow succeeds, prerender step logs show "190 ok, 0 failed" (or similar).

If the job fails due to Puppeteer missing Chromium deps in the runner, add before the prerender step:

```yaml
      - name: Install Chromium deps for Puppeteer
        run: |
          sudo apt-get update
          sudo apt-get install -y libatk-bridge2.0-0 libatk1.0-0 libcups2 \
            libdrm2 libxkbcommon0 libxcomposite1 libxdamage1 libxfixes3 \
            libxrandr2 libgbm1 libasound2t64
```

- [ ] **Step 4: Merge to master once green**

```bash
git checkout master
git merge prerender-ci-test --no-ff
git push origin master
git push origin --delete prerender-ci-test
git branch -d prerender-ci-test
```

- [ ] **Step 5: Verify live**

```bash
curl -s https://lumeo.nativ.sh/privacy | grep -c "Privacy Policy"
curl -s https://lumeo.nativ.sh/components/button | grep -c "Button"
curl -s https://lumeo.nativ.sh/components/consent-banner | grep -c "Consent Banner"
```

Each expected: `>= 1`. This confirms the edge is serving prerendered HTML.

**Important:** open https://lumeo.nativ.sh/components/button in a **private browser window** and watch the Network tab: the initial HTML response size should be ~40-80kB (prerendered) instead of ~3kB (stock shell).

---

## Task 7: Post-deploy smoke test

**Files:**
- Create: `scripts/prerender/verify.mjs`
- Modify: `.github/workflows/deploy-cloudflare.yml`

- [ ] **Step 1: Write the verifier**

```javascript
// scripts/prerender/verify.mjs
//
// Usage: node verify.mjs <base-url>
// Exit 0 = all assertions pass, exit 1 = at least one failed.
//
// Fetches a sample of live routes and asserts each response body contains
// expected content that only shows up in rendered HTML (not the stock shell).
// This catches regressions where prerender silently produces 3kB shells.

const baseUrl = (process.argv[2] || '').replace(/\/$/, '');
if (!baseUrl) {
    console.error('usage: node verify.mjs <base-url>');
    process.exit(1);
}

const checks = [
    { path: '/privacy',                 expect: 'Privacy Policy' },
    { path: '/components/button',       expect: 'Button' },
    { path: '/components/consent-banner', expect: 'Consent Banner' },
    { path: '/components/input',        expect: 'Input' },
    { path: '/blocks/dashboard',        expect: 'Dashboard' },
];

let failed = 0;
for (const check of checks) {
    const res = await fetch(baseUrl + check.path);
    const body = await res.text();
    const ok = res.ok && body.includes(check.expect);
    console.log(`${ok ? '✓' : '✗'} ${check.path} — ${res.status}, ${(body.length / 1024).toFixed(1)}kB${ok ? '' : ` (missing "${check.expect}")`}`);
    if (!ok) failed++;
}

if (failed > 0) {
    console.error(`\n${failed} check(s) failed.`);
    process.exit(1);
}
console.log('\nAll checks passed.');
```

- [ ] **Step 2: Run locally against production**

```bash
cd scripts/prerender
node verify.mjs https://lumeo.nativ.sh
```

Expected output:

```
✓ /privacy — 200, 48.2kB
✓ /components/button — 200, 52.1kB
✓ /components/consent-banner — 200, 51.3kB
✓ /components/input — 200, 50.8kB
✓ /blocks/dashboard — 200, 61.4kB

All checks passed.
```

- [ ] **Step 3: Add a post-deploy verify step to the workflow**

In `.github/workflows/deploy-cloudflare.yml`, after the `cloudflare/pages-action@v1` step, add:

```yaml
      - name: Verify prerender on live site
        working-directory: scripts/prerender
        run: |
          # Give CF edge a moment to propagate.
          sleep 20
          node verify.mjs https://lumeo.nativ.sh
```

- [ ] **Step 4: Commit**

```bash
git add scripts/prerender/verify.mjs .github/workflows/deploy-cloudflare.yml
git commit -m "ci(prerender): post-deploy smoke test against live site"
git push origin master
```

- [ ] **Step 5: Observe the workflow**

```bash
gh run watch $(gh run list --workflow=deploy-cloudflare.yml --limit=1 --json databaseId -q '.[0].databaseId')
```

Expected: "Verify prerender on live site" step passes, "All checks passed." in the log.

---

## Self-Review

**Spec coverage:**
- Goal ("static HTML render for every public route") → Tasks 4–5 cover per-route rendering.
- Cloudflare Pages static-only constraint → Task 6 deploys static files only, no server.
- "No visible flash" → Task 3 + Task 4 ensure prerendered markup matches hydrated markup (same Blazor render pipeline, same inputs).
- SEO → Task 7 verifies the edge actually serves rendered content (not stock shell).
- CI integration → Task 6.

**Placeholder scan:** No TBDs, no "similar to Task N" without full code, no "handle errors appropriately." Every code block is complete.

**Type consistency:** `startServer` returns `{ url, close }` — same shape consumed by `prerender.mjs`. The readiness signal `document.documentElement.dataset.blazorReady === 'true'` is set in Task 3 and awaited in Task 4.

**Risk I see but left unaddressed (acceptable):** If a component throws during first render on a specific route, the prerender crawler will hang until timeout (45s). This fails gracefully per-route (logged, process.exitCode = 1) and the rest of the crawl continues. Not worth building a smarter circuit breaker until it actually bites — YAGNI.

---
