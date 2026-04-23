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
