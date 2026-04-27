#!/usr/bin/env node
// Reads src/Lumeo/registry/registry.json, renders one PNG per component into
// docs/Lumeo.Docs/wwwroot/preview-cards/. Parallelizes 8 at a time.
import puppeteer from 'puppeteer';
import { readFileSync, mkdirSync, existsSync } from 'node:fs';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { renderCard } from './component-card.mjs';

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, '..', '..');
const registryPath = join(repoRoot, 'src', 'Lumeo', 'registry', 'registry.json');
const outDir = join(repoRoot, 'docs', 'Lumeo.Docs', 'wwwroot', 'preview-cards');

if (!existsSync(registryPath)) {
    console.error(`Registry not found at ${registryPath}. Run 'dotnet run --project tools/Lumeo.RegistryGen' first.`);
    process.exit(1);
}

mkdirSync(outDir, { recursive: true });

const registry = JSON.parse(readFileSync(registryPath, 'utf-8'));
const entries = Object.entries(registry.components);
console.log(`Rendering ${entries.length} thumbnails to ${outDir}`);

const browser = await puppeteer.launch({
    headless: true,
    protocolTimeout: 60000,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-dev-shm-usage'],
});
const concurrency = 4;
let done = 0;

async function worker(slice) {
    for (const [slug, comp] of slice) {
        const outPath = join(outDir, `${slug}.png`);
        await renderCard(browser, {
            name: comp.name,
            category: comp.category,
            description: comp.description,
        }, outPath);
        done++;
        if (done % 16 === 0) console.log(`  ${done}/${entries.length}`);
    }
}

const slices = Array.from({ length: concurrency }, (_, i) =>
    entries.filter((_, idx) => idx % concurrency === i)
);
await Promise.all(slices.map(worker));

await browser.close();
console.log(`Done: ${done} thumbnails written.`);
