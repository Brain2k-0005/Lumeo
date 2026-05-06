// One-off: render a few preview OG cards with clean titles to show the design.
import puppeteer from 'puppeteer';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));

// Import the cardHtml function from og-cards.mjs without running its top-level code.
// Inline it here to avoid coupling.
function escapeHtml(s) {
    return String(s || '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
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

const samples = [
    { slug: 'preview-button',   title: 'Button',          subtitle: 'Blazor component documentation', category: 'Component' },
    { slug: 'preview-datagrid', title: 'DataGrid',        subtitle: 'Blazor component documentation', category: 'Component' },
    { slug: 'preview-dashboard',title: 'Dashboard',       subtitle: 'Copy-paste UI block',            category: 'Block' },
    { slug: 'preview-home',     title: 'Lumeo',           subtitle: 'Modern Blazor component library',category: '' },
];

const outDir = join(__dirname, 'preview-samples');
mkdirSync(outDir, { recursive: true });

const browser = await puppeteer.launch({
    headless: true,
    args: ['--no-sandbox'],
    protocolTimeout: 120000,
});

try {
    const page = await browser.newPage();
    await page.setViewport({ width: 1280, height: 640 });
    for (const s of samples) {
        await page.setContent(cardHtml(s), { waitUntil: 'domcontentloaded' });
        await page.screenshot({ path: join(outDir, `${s.slug}.png`), type: 'png' });
        console.log(`✓ ${s.slug}`);
    }
} finally {
    await browser.close();
}
