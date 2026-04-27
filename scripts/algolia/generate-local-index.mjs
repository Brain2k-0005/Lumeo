#!/usr/bin/env node
// Generates docs/Lumeo.Docs/wwwroot/registry-search.json — the fallback
// search dataset used when Algolia is unavailable or disabled.
import { readFileSync, writeFileSync } from 'node:fs';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, '..', '..');
const registryPath = join(repoRoot, 'src', 'Lumeo', 'registry', 'registry.json');
const outPath = join(repoRoot, 'docs', 'Lumeo.Docs', 'wwwroot', 'registry-search.json');

const registry = JSON.parse(readFileSync(registryPath, 'utf-8'));
const items = Object.entries(registry.components).map(([slug, c]) => ({
    id: `component:${slug}`,
    type: 'component',
    title: c.name,
    summary: c.description,
    category: c.category,
    url: `/components/${slug}`,
}));

writeFileSync(outPath, JSON.stringify(items));
console.log(`Wrote ${items.length} items to ${outPath}`);
