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
import { mkdirSync, writeFileSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';

const publishDir = process.argv[2];
if (!publishDir) {
    console.error('usage: node prerender.mjs <publish-dir>');
    process.exit(1);
}
const wwwroot = resolve(publishDir, 'wwwroot');

console.log(`[prerender] wwwroot = ${wwwroot}`);

// Registry JSON inlined into the catalog's prerendered HTML so RegistryService
// can read it synchronously on hydration (no async fetch -> no skeleton flash ->
// no layout shift). `<` is escaped to < so the payload can't break out of
// the <script> tag. Best-effort: if the file is missing, the catalog falls back
// to fetching /registry.json as before.
let registryInlineScript = '';
try {
    const registryJson = readFileSync(join(wwwroot, 'registry.json'), 'utf8').replace(/</g, '\\u003c');
    registryInlineScript = `<script id="lumeo-registry-data" type="application/json">${registryJson}</script>`;
} catch {
    console.warn('[prerender] registry.json not found — catalog will fetch it client-side.');
}

const server = await startServer(wwwroot);
console.log(`[prerender] local server at ${server.url}`);

const browser = await puppeteer.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox'],
});

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
        // Flag the prerender context BEFORE any app script runs. Components that
        // lazy-mount below-the-fold content via IntersectionObserver (which never
        // fires in this non-scrolling headless crawl) check this flag and render
        // eagerly instead, so their output is baked into the static HTML. Real
        // users (no flag) keep the lazy behaviour.
        await page.evaluateOnNewDocument(() => {
            window.__LUMEO_PRERENDER__ = true;
        });
        while (queue.length) {
            const route = queue.shift();
            const t0 = Date.now();
            let degraded = false;
            try {
                // `load` instead of `networkidle2`: component doc pages now lazy-load
                // CDN deps (pdf.js, Leaflet, CodeMirror) on first render. networkidle2
                // would never fire while those background imports stream in, blowing
                // the 45s budget. `load` waits for DOM + primary resources, then we
                // rely on the blazorReady signal below to know the app is mounted.
                await page.goto(server.url + route, { waitUntil: 'load', timeout: 60000 });
                try {
                    await page.waitForFunction(
                        () => document.documentElement.dataset.blazorReady === 'true',
                        { timeout: 30000 },
                    );
                } catch (waitErr) {
                    // Some pages (e.g. heavy JS-interop demos like Scrollspy) keep their
                    // OnAfterRenderAsync chain busy past 30s and never set blazorReady.
                    // Capture the page anyway — networkidle2 already fired so the static
                    // HTML is renderable, just possibly with skeleton states. A warning,
                    // not a failure, so SSG keeps shipping.
                    degraded = true;
                }

                // The catalog grid hydrates from an async registry.json fetch that
                // can resolve after blazorReady, leaving a skeleton in the captured
                // HTML (huge LCP + CLS for real users, who then re-render it client
                // side). Wait (bounded) for the actual cards so they're baked in.
                if (route === '/components') {
                    await page
                        .waitForFunction(
                            () => document.querySelector('main img[src*="/preview-cards/"]') !== null,
                            { timeout: 10000 },
                        )
                        .catch(() => {});
                }

                // Strip Blazor render-boundary markers (`<!--!-->`). HTML parsers
                // treat <title>...</title> as raw text, so embedded comments are
                // displayed literally in the browser tab. Safe to remove — the
                // WASM app re-renders from scratch on hydration and doesn't rely
                // on these markers.
                let html = await page.evaluate(() => {
                    const raw = '<!DOCTYPE html>\n' + document.documentElement.outerHTML;
                    return raw.replace(/<!--!-->/g, '');
                });

                // Inline the registry into the catalog so hydration is skeleton-free.
                if (route === '/components' && registryInlineScript) {
                    html = html.replace('</head>', `${registryInlineScript}</head>`);
                }

                // Root "/" writes to wwwroot/index.html (overwrite the stock
                // shell); everything else writes to wwwroot/<path>/index.html.
                const outPath = route === '/'
                    ? join(wwwroot, 'index.html')
                    : join(wwwroot, route, 'index.html');
                mkdirSync(join(outPath, '..'), { recursive: true });
                writeFileSync(outPath, html, 'utf8');

                results.ok++;
                if (degraded) results.degraded = (results.degraded || 0) + 1;
                const tag = degraded ? '⚠ (degraded)' : '✓';
                console.log(`[worker ${id}] ${tag} ${route} (${Date.now() - t0}ms)`);
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
