// Renders a single 480x270 catalog thumbnail for one component.
// Distinct from preview-card.mjs (which renders 1280x640 OG/social cards).
import { writeFileSync } from 'node:fs';

function escapeHtml(s) {
    return String(s || '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

export function cardHtml({ name, category, description }) {
    return `<!DOCTYPE html><html><head><meta charset="utf-8"><style>
* { box-sizing: border-box; margin: 0; padding: 0; }
html, body { width: 480px; height: 270px; font-family: -apple-system, 'Segoe UI', system-ui, 'Helvetica Neue', Arial, sans-serif; color: #fafafa; background: #0a0a0a; overflow: hidden; }
.wrap { position: relative; width: 100%; height: 100%; padding: 28px 32px; display: flex; flex-direction: column; justify-content: space-between; background:
    radial-gradient(circle at 18% 22%, rgba(245,158,11,0.18) 0%, rgba(10,10,10,0) 42%),
    radial-gradient(circle at 85% 80%, rgba(245,158,11,0.10) 0%, rgba(10,10,10,0) 45%),
    #0a0a0a; }
.grid { position: absolute; inset: 0; background-image:
    linear-gradient(rgba(255,255,255,0.04) 1px, transparent 1px),
    linear-gradient(90deg, rgba(255,255,255,0.04) 1px, transparent 1px);
    background-size: 24px 24px; pointer-events: none; }
.top { position: relative; }
.pill { display: inline-block; padding: 4px 10px; border-radius: 999px; background: rgba(255,255,255,0.08); border: 1px solid rgba(255,255,255,0.14); font-size: 10px; font-weight: 500; color: #d4d4d4; text-transform: uppercase; letter-spacing: 0.1em; }
.name { font-size: 32px; font-weight: 700; line-height: 1.05; letter-spacing: -0.02em; position: relative; }
.desc { font-size: 13px; color: #a3a3a3; line-height: 1.4; position: relative; }
</style></head><body><div class="wrap">
<div class="grid"></div>
<div class="top"><span class="pill">${escapeHtml(category)}</span></div>
<div><div class="name">${escapeHtml(name)}</div><div class="desc">${escapeHtml(description)}</div></div>
</div></body></html>`;
}

export async function renderCard(browser, { name, category, description }, outPath) {
    const page = await browser.newPage();
    await page.setViewport({ width: 480, height: 270, deviceScaleFactor: 2 });
    await page.setContent(cardHtml({ name, category, description }), { waitUntil: 'load' });
    const buf = await page.screenshot({ type: 'png', omitBackground: false });
    writeFileSync(outPath, buf);
    await page.close();
}
