#!/usr/bin/env node
// Generates docs/Lumeo.Docs/wwwroot/registry-search.json — the fallback
// search dataset used when Algolia is unavailable or disabled.
// Covers: components (with dead-link filter), patterns, blocks, and docs guides.
import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, '..', '..');
const registryPath = join(repoRoot, 'src', 'Lumeo', 'registry', 'registry.json');
const outPath = join(repoRoot, 'docs', 'Lumeo.Docs', 'wwwroot', 'registry-search.json');
const componentPagesDir = join(repoRoot, 'docs', 'Lumeo.Docs', 'Pages', 'Components');

const registry = JSON.parse(readFileSync(registryPath, 'utf-8'));

// --- Components (filtered to slugs with a real docs page) ---
const componentItems = Object.entries(registry.components)
    .filter(([slug]) => {
        // Accept if a top-level page exists or a Charts sub-page exists
        const topPage = join(componentPagesDir, `${toPascalCase(slug)}Page.razor`);
        const chartPage = join(componentPagesDir, 'Charts', `${toPascalCase(slug)}ChartPage.razor`);
        return existsSync(topPage) || existsSync(chartPage);
    })
    .map(([slug, c]) => ({
        id: `component:${slug}`,
        type: 'component',
        title: c.name,
        summary: c.description,
        category: c.category,
        url: `/components/${slug}`,
    }));

// --- Patterns ---
const patterns = [
    { label: 'Analytics',     slug: 'analytics' },
    { label: 'Authentication', slug: 'authentication' },
    { label: 'Calendar',      slug: 'calendar' },
    { label: 'Chat',          slug: 'chat' },
    { label: 'Dashboard',     slug: 'dashboard' },
    { label: 'E-Commerce',    slug: 'e-commerce' },
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
    url: `/patterns/${p.slug}`,
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

const allItems = [...componentItems, ...patternItems, ...blockItems, ...guideItems];

writeFileSync(outPath, JSON.stringify(allItems));
console.log(`Wrote ${allItems.length} items to ${outPath} (${componentItems.length} components, ${patternItems.length} patterns, ${blockItems.length} blocks, ${guideItems.length} guides)`);

// --- Helpers ---
function toPascalCase(slug) {
    return slug.split(/[-\/]/).map(w => w.charAt(0).toUpperCase() + w.slice(1)).join('');
}
