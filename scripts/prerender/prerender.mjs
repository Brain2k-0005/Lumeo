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
        while (queue.length) {
            const route = queue.shift();
            const t0 = Date.now();
            let degraded = false;
            try {
                await page.goto(server.url + route, { waitUntil: 'networkidle2', timeout: 45000 });
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
                // Strip Blazor render-boundary markers (`<!--!-->`). HTML parsers
                // treat <title>...</title> as raw text, so embedded comments are
                // displayed literally in the browser tab. Safe to remove — the
                // WASM app re-renders from scratch on hydration and doesn't rely
                // on these markers.
                const html = await page.evaluate(() => {
                    const raw = '<!DOCTYPE html>\n' + document.documentElement.outerHTML;
                    return raw.replace(/<!--!-->/g, '');
                });

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
