// scripts/prerender/og-cards.mjs
//
// Usage: node og-cards.mjs <publish-dir>
//
// For every route in sitemap.xml:
//   1. Open the prerendered HTML and pull its <title>
//   2. Render a 1280×640 OG card via Puppeteer (inline HTML template)
//   3. Save PNG to <publish-dir>/wwwroot/og/<slug>.png
//   4. Rewrite og:image / twitter:image in the route's index.html to point at it
//
// Runs after prerender in CI. Reuses Puppeteer's Chromium (already installed).

import puppeteer from 'puppeteer';
import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { join, resolve } from 'node:path';

const publishDir = process.argv[2];
if (!publishDir) {
    console.error('usage: node og-cards.mjs <publish-dir>');
    process.exit(1);
}
const wwwroot = resolve(publishDir, 'wwwroot');
const ogDir = join(wwwroot, 'og');
mkdirSync(ogDir, { recursive: true });

const SITE = 'https://lumeo.nativ.sh';

const sitemapXml = readFileSync(join(wwwroot, 'sitemap.xml'), 'utf8');
const routes = [...sitemapXml.matchAll(/<loc>([^<]+)<\/loc>/g)]
    .map(m => new URL(m[1]).pathname)
    .filter(Boolean);

console.log(`[og-cards] ${routes.length} routes`);

function escapeHtml(s) {
    return String(s || '').replace(/[&<>"']/g, c => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;',
    }[c]));
}

function slugFor(route) {
    if (route === '/') return 'home';
    return route.replace(/^\/+|\/+$/g, '').replace(/\//g, '-');
}

function categoryFor(route) {
    if (route.startsWith('/components/')) return { label: 'Component', tagline: 'Blazor component documentation' };
    if (route.startsWith('/blocks/'))     return { label: 'Block',     tagline: 'Copy-paste UI block' };
    if (route.startsWith('/docs/'))       return { label: 'Docs',      tagline: 'Lumeo documentation' };
    if (route.startsWith('/patterns'))    return { label: 'Pattern',   tagline: 'Composition pattern' };
    return { label: '', tagline: 'Modern Blazor component library' };
}

function extractTitle(html) {
    const m = html.match(/<title>([^<]+)<\/title>/);
    if (!m) return '';
    // Strip Blazor render-boundary markers defensively (should already be gone
    // after prerender.mjs's pass, but avoid producing ugly cards if they leak).
    return m[1].replace(/<!--!-->/g, '').replace(/\s*[—–-]\s*Lumeo\s*$/i, '').trim();
}

function cardHtml({ title, subtitle, category }) {
    const pill = category ? `<div class="pill">${escapeHtml(category)}</div>` : '';
    return `<!DOCTYPE html>
<html><head><meta charset="utf-8"><style>
* { box-sizing: border-box; margin: 0; padding: 0; }
html, body { width: 1280px; height: 640px; font-family: -apple-system, 'Segoe UI', system-ui, 'Helvetica Neue', Arial, sans-serif; color: #fafafa; background: #0a0a0a; overflow: hidden; }
.wrap { position: relative; width: 100%; height: 100%; padding: 72px 80px; display: flex; flex-direction: column; justify-content: space-between; background:
    radial-gradient(circle at 18% 22%, rgba(59,130,246,0.22) 0%, rgba(10,10,10,0) 42%),
    radial-gradient(circle at 85% 80%, rgba(139,92,246,0.18) 0%, rgba(10,10,10,0) 45%),
    #0a0a0a;
}
.grid { position: absolute; inset: 0; background-image:
    linear-gradient(rgba(255,255,255,0.04) 1px, transparent 1px),
    linear-gradient(90deg, rgba(255,255,255,0.04) 1px, transparent 1px);
    background-size: 48px 48px; pointer-events: none; }
.top { display: flex; justify-content: space-between; align-items: center; position: relative; }
.brand { display: flex; align-items: center; gap: 14px; font-size: 28px; font-weight: 700; letter-spacing: -0.02em; }
.brand svg { width: 36px; height: 36px; }
.pill { padding: 8px 18px; border-radius: 999px; background: rgba(255,255,255,0.08); border: 1px solid rgba(255,255,255,0.14); font-size: 15px; font-weight: 500; color: #d4d4d4; text-transform: uppercase; letter-spacing: 0.1em; }
.content { display: flex; flex-direction: column; gap: 20px; max-width: 1050px; position: relative; }
.title { font-size: 76px; font-weight: 800; line-height: 1.02; letter-spacing: -0.035em; }
.subtitle { font-size: 26px; color: #a3a3a3; line-height: 1.4; font-weight: 400; }
.footer { display: flex; justify-content: space-between; align-items: center; font-size: 17px; color: #737373; letter-spacing: 0.02em; position: relative; }
.footer .url { font-weight: 500; color: #a3a3a3; }
.footer .tag { color: #525252; }
</style></head><body><div class="wrap">
<div class="grid"></div>
<div class="top">
  <div class="brand">
    <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="9" stroke="#fafafa" stroke-width="2" opacity="0.55"/>
      <circle cx="16" cy="16" r="3" fill="#fafafa"/>
    </svg>
    Lumeo
  </div>
  ${pill}
</div>
<div class="content">
  <div class="title">${escapeHtml(title)}</div>
  <div class="subtitle">${escapeHtml(subtitle)}</div>
</div>
<div class="footer">
  <span class="url">lumeo.nativ.sh</span>
  <span class="tag">130+ components · MIT · .NET 10</span>
</div>
</div></body></html>`;
}

function rewriteMeta(html, slug) {
    const imgUrl = `${SITE}/og/${slug}.png`;
    html = html.replace(/(<meta property="og:image" content=")[^"]+(")/i, `$1${imgUrl}$2`);
    html = html.replace(/(<meta name="twitter:image" content=")[^"]+(")/i, `$1${imgUrl}$2`);
    return html;
}

const browser = await puppeteer.launch({
    headless: true,
    args: [
        '--no-sandbox',
        '--disable-setuid-sandbox',
        // Reduce GPU/compositor pressure on shared CI runners.
        '--disable-gpu',
        '--disable-dev-shm-usage',
        '--font-render-hinting=none',
    ],
    protocolTimeout: 300000,
});

try {
    const queue = [...routes];
    const results = { ok: 0, fail: 0 };
    const startAll = Date.now();

    async function makePage() {
        const p = await browser.newPage();
        // No JS in our template; disabling avoids Chromium waiting for any
        // implicit script pipeline and cuts screenshot latency substantially.
        await p.setJavaScriptEnabled(false);
        await p.setViewport({ width: 1280, height: 640, deviceScaleFactor: 1 });
        return p;
    }

    async function renderRoute(page, route) {
        const htmlPath = route === '/'
            ? join(wwwroot, 'index.html')
            : join(wwwroot, route, 'index.html');
        if (!existsSync(htmlPath)) {
            throw new Error('no prerendered HTML');
        }
        const routeHtml = readFileSync(htmlPath, 'utf8');
        const title = extractTitle(routeHtml) || 'Lumeo';
        const cat = categoryFor(route);
        await page.setContent(
            cardHtml({ title, subtitle: cat.tagline, category: cat.label }),
            { waitUntil: 'domcontentloaded' },
        );
        const slug = slugFor(route);
        await page.screenshot({ path: join(ogDir, `${slug}.png`), type: 'png', timeout: 60000 });
        writeFileSync(htmlPath, rewriteMeta(routeHtml, slug), 'utf8');
    }

    async function worker(id) {
        let page = await makePage();
        while (queue.length) {
            const route = queue.shift();
            try {
                await renderRoute(page, route);
                results.ok++;
                if (results.ok % 25 === 0) {
                    console.log(`[og-cards] ${results.ok}/${routes.length}`);
                }
            } catch (err) {
                // Recreate the page — a screenshot timeout can leave Chromium's
                // render pipeline wedged for subsequent calls. Retry once.
                console.error(`[worker ${id}] ✗ ${route} — ${err.message}; recreating page`);
                try { await page.close({ runBeforeUnload: false }); } catch {}
                page = await makePage();
                try {
                    await renderRoute(page, route);
                    results.ok++;
                } catch (retryErr) {
                    results.fail++;
                    console.error(`[worker ${id}] ✗✗ ${route} — retry also failed: ${retryErr.message}`);
                }
            }
        }
        try { await page.close({ runBeforeUnload: false }); } catch {}
    }

    // Concurrency 2 — screenshot throughput is GPU/compositor-bound on CI and
    // 4 workers was causing Page.captureScreenshot timeouts.
    await Promise.all([worker(1), worker(2)]);

    const secs = ((Date.now() - startAll) / 1000).toFixed(1);
    const failRate = results.fail / (results.ok + results.fail);
    console.log(`[og-cards] done: ${results.ok} ok, ${results.fail} failed, ${secs}s`);

    // Non-fatal: routes whose cards failed still serve the site-wide fallback
    // og:image (/social-preview.png). Only fail the whole step if a large
    // fraction (>15%) of routes couldn't be rendered — at that point something
    // is systemically broken and we should know.
    if (failRate > 0.15) {
        console.error(`[og-cards] FAIL — ${(failRate * 100).toFixed(1)}% of routes failed, over 15% threshold`);
        process.exitCode = 1;
    }
} finally {
    await browser.close();
}
