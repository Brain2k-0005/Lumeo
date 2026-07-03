#!/usr/bin/env node
// Generates docs/Lumeo.Docs/wwwroot/registry-search.json — the fallback
// search dataset used when Algolia is unavailable or disabled.
// Covers: components (with dead-link filter), patterns, blocks, and docs guides.
import { readFileSync, writeFileSync, existsSync, readdirSync } from 'node:fs';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, '..', '..');
const registryPath = join(repoRoot, 'src', 'Lumeo', 'registry', 'registry.json');
const outPath = join(repoRoot, 'docs', 'Lumeo.Docs', 'wwwroot', 'registry-search.json');
const componentPagesDir = join(repoRoot, 'docs', 'Lumeo.Docs', 'Pages', 'Components');

const registry = JSON.parse(readFileSync(registryPath, 'utf-8'));

// Case-INSENSITIVE page-file lookup, mirroring RegistryGen's Program.cs PageFileExists.
// A component's slug->PascalCase derivation ("cta-section" -> "CtaSection") can differ in
// casing from the actual page file ("CTASectionPage.razor" keeps the CTA acronym upper-case),
// so a plain existsSync(exact name) silently passes on Windows (case-insensitive NTFS) but
// fails on Linux CI (case-sensitive ext4) — dropping that component from the search index and
// failing the "Registry is up to date" gate there, even though nothing about the component
// actually changed. Build a lower-cased filename set per directory once and compare against it.
function fileNamesLowerCased(dir) {
    if (!existsSync(dir)) return new Set();
    return new Set(readdirSync(dir).map(f => f.toLowerCase()));
}
const topPageNames = fileNamesLowerCased(componentPagesDir);
const chartPageNames = fileNamesLowerCased(join(componentPagesDir, 'Charts'));

// --- Components (filtered to slugs with a real docs page) ---
const componentItems = Object.entries(registry.components)
    .filter(([slug]) => {
        // Accept if a top-level page exists or a Charts sub-page exists
        const topPage = `${toPascalCase(slug)}Page.razor`.toLowerCase();
        const chartPage = `${toPascalCase(slug)}ChartPage.razor`.toLowerCase();
        return topPageNames.has(topPage) || chartPageNames.has(chartPage);
    })
    .map(([slug, c]) => ({
        id: `component:${slug}`,
        type: 'component',
        title: c.name,
        summary: c.description,
        category: c.category,
        url: `/components/${slug}`,
    }));

// --- Patterns (routed under /blocks/* — patterns and blocks share the same prefix) ---
const patterns = [
    { label: 'Analytics',     slug: 'analytics' },
    { label: 'Authentication', slug: 'authentication' },
    { label: 'Calendar',      slug: 'calendar' },
    { label: 'Chat',          slug: 'chat' },
    { label: 'Dashboard',     slug: 'dashboard' },
    { label: 'E-Commerce',    slug: 'ecommerce' },
    { label: 'File Manager',  slug: 'file-manager' },
    { label: 'Filters',       slug: 'filters' },
    { label: 'Form Wizard',   slug: 'form-wizard' },
    { label: 'Kanban',        slug: 'kanban' },
    { label: 'Mail',          slug: 'mail' },
    { label: 'Music',         slug: 'music' },
    { label: 'Notifications', slug: 'notifications' },
    { label: 'Settings',      slug: 'settings' },
    { label: 'Social Feed',   slug: 'social-feed' },
    { label: 'Task Tracker',  slug: 'task-tracker' },
];

const patternItems = patterns.map(p => ({
    id: `pattern:${p.slug}`,
    type: 'pattern',
    title: p.label,
    summary: `${p.label} UI pattern`,
    category: 'Patterns',
    url: `/blocks/${p.slug}`,
}));

// --- Blocks ---
const blocks = [
    { label: 'Sign In',        slug: 'sign-in' },
    { label: 'Sign Up',        slug: 'sign-up' },
    { label: 'Reset Password', slug: 'reset-password' },
    { label: 'OTP Verify',     slug: 'otp-verify' },
    { label: 'Pricing Table',  slug: 'pricing' },
    { label: 'Hero Section',   slug: 'hero' },
    { label: 'Dashboard Block', slug: 'dashboard-block' },
    { label: 'Settings Page',  slug: 'settings-page' },
];

const blockItems = blocks.map(b => ({
    id: `block:${b.slug}`,
    type: 'block',
    title: b.label,
    summary: `${b.label} block`,
    category: 'Blocks',
    url: `/blocks/${b.slug}`,
}));

// --- Docs guides ---
const guides = [
    { label: 'Introduction',       href: 'docs/introduction' },
    { label: 'CLI',                href: 'docs/cli' },
    { label: 'Form Validation',    href: 'docs/form-validation' },
    { label: 'LumeoForm Generator', href: 'docs/lumeo-form' },
    { label: 'Templates',          href: 'docs/templates' },
    { label: 'RTL Support',        href: 'docs/rtl' },
    { label: 'Culture & Formatting', href: 'docs/culture' },
    { label: 'MCP Server',         href: 'docs/mcp' },
    { label: 'Registry',           href: 'docs/registry' },
    { label: 'Accessibility',      href: 'docs/accessibility' },
    { label: 'Density',            href: 'docs/density' },
    { label: 'Long Forms in Sheets', href: 'docs/long-forms-in-sheets' },
    { label: 'Service vs Markup Overlays', href: 'docs/service-vs-markup-overlays' },
    { label: 'Mobile Sheet Patterns', href: 'docs/mobile-sheet-patterns' },
    { label: 'Blazor-WASM Lifecycle', href: 'docs/blazor-wasm-lifecycle' },
    { label: 'Theme Overrides',    href: 'docs/theme-overrides' },
    { label: 'Changelog',          href: 'docs/changelog' },
    { label: 'Contributing',       href: 'docs/contributing' },
    { label: 'Toast Service',      href: 'docs/services/toast' },
    { label: 'Overlay Service',    href: 'docs/services/overlay' },
    { label: 'Theme Service',      href: 'docs/services/theme' },
    { label: 'Keyboard Shortcuts', href: 'docs/services/keyboard-shortcuts' },
    { label: 'Component Interop',  href: 'docs/services/component-interop' },
    { label: 'DataGrid Export',    href: 'docs/services/datagrid-export' },
];

const guideItems = guides.map(g => ({
    id: `guide:${g.href}`,
    type: 'guide',
    title: g.label,
    summary: `${g.label} documentation`,
    category: 'Getting Started',
    url: `/${g.href}`,
}));

// --- Icon gallery (the /icons pack browser) ---
const iconGalleryItems = [
    {
        id: 'icons:gallery',
        type: 'icons',
        title: 'Icon Packs',
        summary: 'Browse and copy Lucide, Tabler and Phosphor icons',
        category: 'Icons',
        url: '/icons',
    },
];

const allItems = [...componentItems, ...patternItems, ...blockItems, ...guideItems, ...iconGalleryItems];

// --- Dead-link sanity check: every emitted URL must match a real @page route ---
const realRoutes = collectAllRoutes(join(repoRoot, 'docs', 'Lumeo.Docs', 'Pages'));
const dead = allItems.filter(i => !realRoutes.has(i.url.replace(/^\//, '').replace(/\/$/, '').toLowerCase()));
if (dead.length > 0) {
    console.error(`ERROR: ${dead.length} dead-link entries in registry-search.json:`);
    for (const d of dead) console.error(`  ${d.type.padEnd(9)} ${d.url}  (${d.title})`);
    process.exit(1);
}

writeFileSync(outPath, JSON.stringify(allItems));
console.log(`Wrote ${allItems.length} items to ${outPath} (${componentItems.length} components, ${patternItems.length} patterns, ${blockItems.length} blocks, ${guideItems.length} guides, ${iconGalleryItems.length} icons) — all routes verified live.`);

// --- Helpers ---
function toPascalCase(slug) {
    return slug.split(/[-\/]/).map(w => w.charAt(0).toUpperCase() + w.slice(1)).join('');
}

function collectAllRoutes(pagesDir) {
    const routes = new Set();
    function walk(dir) {
        for (const entry of readdirSync(dir, { withFileTypes: true })) {
            const full = join(dir, entry.name);
            if (entry.isDirectory()) { walk(full); continue; }
            if (!entry.name.endsWith('.razor')) continue;
            const content = readFileSync(full, 'utf-8');
            for (const m of content.matchAll(/@page\s+"\/?([^"]*)"/g)) {
                routes.add(m[1].trim().replace(/\/$/, '').toLowerCase());
            }
        }
    }
    walk(pagesDir);
    return routes;
}
