// scripts/prerender/check-showcases-trimmed.mjs
//
// Usage: node check-showcases-trimmed.mjs <publish-dir>
//
// Verifies that `dotnet publish -c Release` (PublishTrimmed=true, see
// docs/Lumeo.Docs/Lumeo.Docs.csproj) hasn't stripped the catalog showcases
// (docs/Lumeo.Docs/Shared/Showcases/*Showcase.razor) from the trimmed build.
//
// ShowcaseResolver (docs/Lumeo.Docs/Services/ShowcaseResolver.cs) discovers these
// types PURELY by reflection — Type.GetType on a name built at runtime from the
// registry component name — with no static reference to any of them anywhere in
// the compiled call graph. That means a normal (never-trimmed) `dotnet run` or
// `dotnet test` can render every showcase correctly while the actual `dotnet
// publish` output — what the Cloudflare deploy in
// .github/workflows/deploy-cloudflare.yml ships — silently strips them, and every
// Navigation card in production falls back to the static thumbnail while looking
// completely fine in local dev. ILLink.Descriptors.xml (wired via
// <TrimmerRootDescriptor> in the csproj) is what's supposed to prevent that; this
// script is the real gate that proves it still works, by serving the REAL
// published wwwroot and loading /components in a real (headless) browser — the
// published assemblies are WebCil-wrapped .wasm modules under
// wwwroot/_framework/, not plain .dll files, so inspecting their metadata
// directly isn't portable; asserting on rendered DOM output is both simpler and
// stronger (it also catches a trimmed-away METHOD BODY, not just a missing type).
//
// Lives alongside prerender.mjs so it can reuse its static-server helper, its
// Blazor-hydration marker (document.documentElement.dataset.blazorReady ===
// 'true'), and its already-installed puppeteer dependency — CI runs `npm ci`
// here before "Publish Blazor WASM", so no extra install step is needed.

import { startServer } from './server.mjs';
import puppeteer from 'puppeteer';
import { resolve } from 'node:path';

// Every wave-0 Navigation-category showcase's catalog-card title, keyed by the
// registry component Name shown as the card's <h3>. Kept in sync manually with
// docs/Lumeo.Docs/Shared/Showcases/*Showcase.razor — if a later wave adds
// showcases for a different category, extend this list rather than replace it.
const EXPECTED_SHOWCASE_TITLES = [
    'Accordion', 'Affix', 'AppBar', 'BackToTop', 'BottomNav', 'Breadcrumb',
    'Carousel', 'Collapsible', 'MegaMenu', 'Menubar', 'NavigationMenu',
    'Pagination', 'Scrollspy', 'Sidebar', 'SpeedDial', 'Splitter', 'Stepper',
    'Tabs', 'Toolbar',
];

async function main() {
    const publishDir = process.argv[2];
    if (!publishDir) {
        console.error('usage: node check-showcases-trimmed.mjs <publish-dir>');
        process.exit(1);
    }
    const wwwroot = resolve(publishDir, 'wwwroot');
    console.log(`[check-showcases-trimmed] wwwroot = ${wwwroot}`);

    const server = await startServer(wwwroot, 4301);
    const browser = await puppeteer.launch({ headless: 'new', args: ['--no-sandbox'] });
    try {
        const page = await browser.newPage();
        // Tall viewport + a full scroll pass below: showcases mount lazily via
        // LazyRender's IntersectionObserver (only the first row of the first
        // category is Eager), so anything still off-screen renders its
        // thumbnail/icon PLACEHOLDER — indistinguishable from a genuinely
        // trimmed-away showcase unless every card has actually scrolled into view
        // at least once first.
        await page.setViewport({ width: 1440, height: 2000 });
        const consoleErrors = [];
        page.on('pageerror', (e) => consoleErrors.push(String(e)));

        await page.goto(`${server.url}/components`, { waitUntil: 'networkidle0' });
        await page.waitForFunction(
            () => document.documentElement.dataset.blazorReady === 'true',
            { timeout: 30000 },
        );

        // Scroll the full page height in steps so every row crosses the
        // IntersectionObserver's rootMargin at least once, then return to the top.
        await page.evaluate(async () => {
            const step = window.innerHeight;
            const total = document.documentElement.scrollHeight;
            for (let y = 0; y < total; y += step) {
                window.scrollTo(0, y);
                await new Promise((r) => setTimeout(r, 150));
            }
            window.scrollTo(0, 0);
        });
        await new Promise((r) => setTimeout(r, 500));

        const results = await page.evaluate((expectedTitles) => {
            return expectedTitles.map((title) => {
                const heading = Array.from(document.querySelectorAll('h3'))
                    .find((h) => h.textContent.trim() === title);
                if (!heading) return { title, found: false, reason: 'card not found on /components' };

                const card = heading.closest('.rounded-xl');
                if (!card) return { title, found: false, reason: 'card wrapper not found' };

                const preview = card.querySelector('.aspect-\\[16\\/9\\]');
                if (!preview) return { title, found: false, reason: 'preview box not found' };

                // A trimmed-away showcase type makes ShowcaseResolver.Resolve return
                // null, so CatalogCard falls back to the thumbnail/icon — recognisable
                // by either an <img> or the icon-fallback class inside the preview.
                const fellBackToThumbnail = !!preview.querySelector('img, .catalog-card-icon-fallback');
                return { title, found: !fellBackToThumbnail, reason: fellBackToThumbnail ? 'fell back to thumbnail/icon — showcase did not render' : null };
            });
        }, EXPECTED_SHOWCASE_TITLES);

        const failures = results.filter((r) => !r.found);

        console.log(`Checked ${results.length} showcases; ${failures.length} did not render as a live showcase.`);
        for (const r of results) {
            console.log(`  ${r.found ? 'OK  ' : 'FAIL'} ${r.title}${r.reason ? ' — ' + r.reason : ''}`);
        }
        if (consoleErrors.length > 0) {
            console.log(`Page errors (${consoleErrors.length}):`);
            consoleErrors.slice(0, 10).forEach((e) => console.log('  ' + e));
        }

        if (failures.length > 0) {
            console.error(`\n${failures.length}/${results.length} showcases were trimmed away from the published build.`);
            console.error('Check ILLink.Descriptors.xml is still wired via <TrimmerRootDescriptor> in docs/Lumeo.Docs/Lumeo.Docs.csproj.');
            process.exitCode = 1;
        } else {
            console.log('\nAll showcases survived PublishTrimmed.');
        }
    } finally {
        await browser.close();
        await server.close();
    }
}

main().catch((e) => {
    console.error(e);
    process.exit(1);
});
