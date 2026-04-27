#!/usr/bin/env node
// Reads src/Lumeo/registry/registry.json + walks the published docs site for
// patterns/blocks/guides, builds Algolia records, pushes to index 'lumeo_docs'.
//
// Required env: ALGOLIA_APP_ID, ALGOLIA_ADMIN_KEY
// Optional env: ALGOLIA_INDEX_NAME (defaults to 'lumeo_docs')
import { algoliasearch } from 'algoliasearch';
import { readFileSync } from 'node:fs';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, '..', '..');
const registryPath = join(repoRoot, 'src', 'Lumeo', 'registry', 'registry.json');

const appId = process.env.ALGOLIA_APP_ID;
const adminKey = process.env.ALGOLIA_ADMIN_KEY;
const indexName = process.env.ALGOLIA_INDEX_NAME ?? 'lumeo_docs';

if (!appId || !adminKey) {
    console.error('ALGOLIA_APP_ID and ALGOLIA_ADMIN_KEY env vars are required.');
    process.exit(1);
}

const registry = JSON.parse(readFileSync(registryPath, 'utf-8'));
const records = [];

for (const [slug, comp] of Object.entries(registry.components)) {
    records.push({
        objectID: `component:${slug}`,
        type: 'component',
        title: comp.name,
        summary: comp.description,
        category: comp.category,
        subcategory: comp.subcategory ?? null,
        url: `/components/${slug}`,
        thumbnail: comp.thumbnail ?? null,
        package: comp.nugetPackage,
    });
}

// Patterns and blocks: derived from docs/Lumeo.Docs/Pages/Patterns/ and /Blocks/.
// Each entry corresponds to a routed page in the docs site.
// Slugs use kebab-case derived from the filename (strip trailing "Pattern"/"Block").
const extras = [
    // ── Patterns ─────────────────────────────────────────────────────────────
    {
        id: 'pattern:analytics',
        type: 'pattern',
        title: 'Analytics',
        url: '/patterns/analytics',
        category: 'Patterns',
        summary: 'Analytics dashboard with metrics, charts, and data breakdowns.',
    },
    {
        id: 'pattern:authentication',
        type: 'pattern',
        title: 'Authentication',
        url: '/patterns/authentication',
        category: 'Patterns',
        summary: 'Sign-in and sign-up authentication flow pattern.',
    },
    {
        id: 'pattern:calendar',
        type: 'pattern',
        title: 'Calendar',
        url: '/patterns/calendar',
        category: 'Patterns',
        summary: 'Full-page calendar with event scheduling.',
    },
    {
        id: 'pattern:chat',
        type: 'pattern',
        title: 'Chat',
        url: '/patterns/chat',
        category: 'Patterns',
        summary: 'Messaging interface with conversation threads.',
    },
    {
        id: 'pattern:dashboard',
        type: 'pattern',
        title: 'Dashboard',
        url: '/patterns/dashboard',
        category: 'Patterns',
        summary: 'Stats, charts, and a data table together.',
    },
    {
        id: 'pattern:e-commerce',
        type: 'pattern',
        title: 'E-Commerce',
        url: '/patterns/e-commerce',
        category: 'Patterns',
        summary: 'Product listing, cart, and checkout pattern.',
    },
    {
        id: 'pattern:file-manager',
        type: 'pattern',
        title: 'File Manager',
        url: '/patterns/file-manager',
        category: 'Patterns',
        summary: 'File browser with grid/list toggle and upload.',
    },
    {
        id: 'pattern:filters',
        type: 'pattern',
        title: 'Filters',
        url: '/patterns/filters',
        category: 'Patterns',
        summary: 'Search and filter panel with faceted results.',
    },
    {
        id: 'pattern:form-wizard',
        type: 'pattern',
        title: 'Form Wizard',
        url: '/patterns/form-wizard',
        category: 'Patterns',
        summary: 'Multi-step form with validation.',
    },
    {
        id: 'pattern:kanban',
        type: 'pattern',
        title: 'Kanban',
        url: '/patterns/kanban',
        category: 'Patterns',
        summary: 'Drag-and-drop kanban board with swimlanes.',
    },
    {
        id: 'pattern:mail',
        type: 'pattern',
        title: 'Mail',
        url: '/patterns/mail',
        category: 'Patterns',
        summary: 'Email client layout with inbox and compose.',
    },
    {
        id: 'pattern:music',
        type: 'pattern',
        title: 'Music',
        url: '/patterns/music',
        category: 'Patterns',
        summary: 'Music player UI with library and playback controls.',
    },
    {
        id: 'pattern:notifications',
        type: 'pattern',
        title: 'Notifications',
        url: '/patterns/notifications',
        category: 'Patterns',
        summary: 'Notification center with read/unread state.',
    },
    {
        id: 'pattern:settings',
        type: 'pattern',
        title: 'Settings',
        url: '/patterns/settings',
        category: 'Patterns',
        summary: 'App settings page with sections and form controls.',
    },
    {
        id: 'pattern:social-feed',
        type: 'pattern',
        title: 'Social Feed',
        url: '/patterns/social-feed',
        category: 'Patterns',
        summary: 'Social media feed with posts, likes, and comments.',
    },
    {
        id: 'pattern:task-tracker',
        type: 'pattern',
        title: 'Task Tracker',
        url: '/patterns/task-tracker',
        category: 'Patterns',
        summary: 'Task list with status, priority, and filtering.',
    },
    // ── Blocks ────────────────────────────────────────────────────────────────
    {
        id: 'block:dashboard',
        type: 'block',
        title: 'Dashboard Block',
        url: '/blocks/dashboard',
        category: 'Blocks',
        summary: 'Drop-in dashboard layout block with KPI cards and charts.',
    },
    {
        id: 'block:hero',
        type: 'block',
        title: 'Hero Section',
        url: '/blocks/hero',
        category: 'Blocks',
        summary: 'Marketing hero with CTA and social proof.',
    },
    {
        id: 'block:otp-verify',
        type: 'block',
        title: 'OTP Verify',
        url: '/blocks/otp-verify',
        category: 'Blocks',
        summary: 'One-time password verification block.',
    },
    {
        id: 'block:pricing-table',
        type: 'block',
        title: 'Pricing Table',
        url: '/blocks/pricing-table',
        category: 'Blocks',
        summary: 'Tiered pricing table with feature comparison.',
    },
    {
        id: 'block:reset-password',
        type: 'block',
        title: 'Reset Password',
        url: '/blocks/reset-password',
        category: 'Blocks',
        summary: 'Password reset flow block with email input.',
    },
    {
        id: 'block:settings-page',
        type: 'block',
        title: 'Settings Page',
        url: '/blocks/settings-page',
        category: 'Blocks',
        summary: 'Full settings page block with form sections.',
    },
    {
        id: 'block:sign-in',
        type: 'block',
        title: 'Sign In',
        url: '/blocks/sign-in',
        category: 'Blocks',
        summary: 'Sign-in block with email, password, and OAuth options.',
    },
    {
        id: 'block:sign-up',
        type: 'block',
        title: 'Sign Up',
        url: '/blocks/sign-up',
        category: 'Blocks',
        summary: 'Sign-up block with registration form and validation.',
    },
];

for (const extra of extras) {
    records.push({ ...extra, objectID: extra.id });
}

console.log(`Pushing ${records.length} records to ${indexName} on ${appId}…`);
const client = algoliasearch(appId, adminKey);
await client.replaceAllObjects({ indexName, objects: records, batchSize: 500 });

// Configure searchable attributes + ranking on every push (idempotent).
await client.setSettings({
    indexName,
    indexSettings: {
        searchableAttributes: ['title', 'summary', 'category', 'subcategory'],
        attributesForFaceting: ['type', 'category', 'package'],
        customRanking: ['asc(title)'],
    },
});

console.log('Indexing complete.');
