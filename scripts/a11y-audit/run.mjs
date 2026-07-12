#!/usr/bin/env node
// scripts/a11y-audit/run.mjs
//
// axe-core WCAG A/AA sweep of every /components/<slug> docs route.
//
// 1. Rebuilds the Tailwind CSS bundles, builds docs/Lumeo.Docs (unless
//    --no-build), and boots it with `dotnet run --no-build` (Blazor Server
//    hosting — same pattern as .github/workflows/e2e.yml), waiting for the
//    HTTP endpoint to answer.
// 2. Enumerates every component slug from src/Lumeo/registry/registry.json
//    (the single source of truth also used by the docs nav/catalog).
// 3. For each /components/<slug> route: Puppeteer navigates, waits for
//    document.documentElement.dataset.blazorReady === 'true' (WASM/Blazor-
//    Server hydration signal set by js/docs.js), injects axe-core, and runs
//    it scoped to <main> (the page content: hero demo, examples, API table)
//    — excluding the shared app shell (topbar, sidebar nav, footer, cookie
//    consent banner), which is not per-component and would otherwise spam
//    every single report with the same shell-chrome findings.
// 4. Writes reports/<slug>.json per component + reports/summary.json
//    (violation counts by rule/impact, totals).
//
// Usage:
//   node run.mjs                  # build + run full sweep
//   node run.mjs --no-build       # docs site already built, skip `dotnet build`
//   node run.mjs --base-url <url> # crawl an already-running server (skips dotnet entirely)
//   node run.mjs --slug button    # single component, for fast iteration
//
// Exit code 0 always (this script only *collects*; check-baseline.mjs is the
// gate). Prints a one-line summary at the end.

import { spawn } from 'node:child_process';
import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as sleep } from 'node:timers/promises';
import puppeteer from 'puppeteer';

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, '..', '..');
const reportsDir = join(__dirname, 'reports');

const args = process.argv.slice(2);
const noBuild = args.includes('--no-build');
const baseUrlArg = args.includes('--base-url') ? args[args.indexOf('--base-url') + 1] : null;
const slugArg = args.includes('--slug') ? args[args.indexOf('--slug') + 1] : null;
const PORT = process.env.A11Y_AUDIT_PORT || 5291;

// axe-core's own dist bundle, injected as a raw script (no CDN — offline-safe,
// version-pinned via package.json).
const axeCorePath = resolve(__dirname, 'node_modules', 'axe-core', 'axe.min.js');
if (!existsSync(axeCorePath)) {
    console.error(`axe-core not found at ${axeCorePath}. Run 'npm install' in scripts/a11y-audit first.`);
    process.exit(1);
}
const axeSource = readFileSync(axeCorePath, 'utf8');

// ---------------------------------------------------------------------------
// 1. Registry -> routes
// ---------------------------------------------------------------------------
const registryPath = join(repoRoot, 'src', 'Lumeo', 'registry', 'registry.json');
if (!existsSync(registryPath)) {
    console.error(`Registry not found at ${registryPath}. Run 'dotnet run --project tools/Lumeo.RegistryGen' first.`);
    process.exit(1);
}
const registry = JSON.parse(readFileSync(registryPath, 'utf8'));
let slugs = Object.entries(registry.components)
    .filter(([, c]) => c.hasDocsPage)
    .map(([slug]) => slug)
    .sort();
if (slugArg) slugs = slugs.filter(s => s === slugArg);
if (slugs.length === 0) {
    console.error(slugArg ? `No component matches slug '${slugArg}'.` : 'No documented components found in registry.');
    process.exit(1);
}
console.log(`[a11y-audit] ${slugs.length} component route(s) to sweep`);

// ---------------------------------------------------------------------------
// 2. Docs site: build + boot (unless an already-running server was given)
// ---------------------------------------------------------------------------
let dotnetProc = null;
let baseUrl = baseUrlArg;

function run(cmd, cmdArgs, opts = {}) {
    return new Promise((resolveP, rejectP) => {
        const p = spawn(cmd, cmdArgs, { stdio: 'inherit', shell: process.platform === 'win32', ...opts });
        p.on('exit', (code) => code === 0 ? resolveP() : rejectP(new Error(`${cmd} ${cmdArgs.join(' ')} exited ${code}`)));
        p.on('error', rejectP);
    });
}

async function waitForServer(url, timeoutMs = 120_000) {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
        try {
            const res = await fetch(url);
            if (res.ok || res.status === 404) return true;
        } catch { /* not up yet */ }
        await sleep(1000);
    }
    return false;
}

if (!baseUrl) {
    const dotnetExe = process.env.DOTNET_EXE || 'dotnet';
    const docsProj = join(repoRoot, 'docs', 'Lumeo.Docs', 'Lumeo.Docs.csproj');

    if (!noBuild) {
        // Same Tailwind steps .github/workflows/a11y-audit.yml runs before its own
        // `dotnet build` — `dotnet build` alone does NOT regenerate the CSS bundles
        // (no MSBuild hook for it), so without this a local full sweep or baseline
        // regen would audit against stale/unstyled CSS, which can mask or mimic
        // real a11y issues (e.g. a contrast fix that only changed Tailwind classes).
        console.log('[a11y-audit] npm run build:css (root utilities)...');
        await run('npm', ['run', 'build:css'], { cwd: repoRoot });
        console.log('[a11y-audit] npm run css:build (docs/Lumeo.Docs)...');
        await run('npm', ['run', 'css:build'], { cwd: join(repoRoot, 'docs', 'Lumeo.Docs') });

        console.log('[a11y-audit] dotnet build docs/Lumeo.Docs (Release)...');
        await run(dotnetExe, ['build', docsProj, '-c', 'Release', '--nologo']);
    }

    baseUrl = `http://localhost:${PORT}`;
    console.log(`[a11y-audit] dotnet run docs/Lumeo.Docs --urls ${baseUrl} ...`);
    dotnetProc = spawn(dotnetExe, [
        'run', '--project', docsProj, '-c', 'Release', '--no-launch-profile', '--no-build',
        '--urls', baseUrl,
    ], {
        cwd: repoRoot,
        shell: process.platform === 'win32',
        stdio: ['ignore', 'pipe', 'pipe'],
    });
    dotnetProc.stdout.on('data', (d) => process.env.A11Y_AUDIT_VERBOSE && process.stdout.write(`[docs-server] ${d}`));
    dotnetProc.stderr.on('data', (d) => process.stderr.write(`[docs-server] ${d}`));

    const up = await waitForServer(baseUrl, 120_000);
    if (!up) {
        console.error(`[a11y-audit] docs server did not come up within 120s at ${baseUrl}`);
        dotnetProc.kill();
        // dotnet run typically spawns a child apphost process on Windows that
        // plain .kill() may not terminate — apply the same tree-kill fallback
        // the finally block below uses, so this early-exit path can't leave
        // an orphaned server bound to PORT behind for the next run.
        if (process.platform === 'win32' && dotnetProc.pid) {
            spawn('taskkill', ['/pid', String(dotnetProc.pid), '/T', '/F'], { stdio: 'ignore', shell: true });
        }
        process.exit(1);
    }
    console.log(`[a11y-audit] docs server ready at ${baseUrl}`);
    // Blazor Server needs a beat after the HTTP listener answers before the
    // SignalR hub + circuit factory are fully wired; harmless if already ready.
    await sleep(2000);
}

// ---------------------------------------------------------------------------
// 3. Crawl + axe
// ---------------------------------------------------------------------------
mkdirSync(reportsDir, { recursive: true });

const browser = await puppeteer.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-dev-shm-usage'],
});

const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

const summary = {
    generated: new Date().toISOString(),
    baseUrl,
    tags: AXE_TAGS,
    scope: 'main (page content — hero demo, examples, API reference table); shared app shell (topbar, sidebar nav, footer, consent banner) excluded',
    components: {},
    totals: { violations: 0, byImpact: { critical: 0, serious: 0, moderate: 0, minor: 0 } },
    errors: [],
};

async function auditOne(page, slug) {
    const route = `/components/${slug}`;
    const t0 = Date.now();
    await page.goto(baseUrl + route, { waitUntil: 'load', timeout: 60_000 });
    try {
        await page.waitForFunction(
            () => document.documentElement.dataset.blazorReady === 'true',
            { timeout: 30_000 },
        );
    } catch {
        console.warn(`[a11y-audit] ${slug}: blazorReady never set within 30s — auditing degraded DOM anyway`);
    }
    // Extra settle for late client-side renders (e.g. CodeMirror/chart canvases
    // that mount just after blazorReady).
    await sleep(300);

    await page.evaluate(axeSource);
    const result = await page.evaluate(async (tags) => {
        const context = document.querySelector('main') ? { include: [['main']] } : undefined;
        return await window.axe.run(context, {
            runOnly: { type: 'tag', values: tags },
            resultTypes: ['violations'],
        });
    }, AXE_TAGS);

    const violations = result.violations.map(v => ({
        id: v.id,
        impact: v.impact,
        description: v.description,
        help: v.help,
        helpUrl: v.helpUrl,
        tags: v.tags,
        nodes: v.nodes.map(n => ({
            target: n.target,
            html: n.html.length > 400 ? n.html.slice(0, 400) + '…' : n.html,
            failureSummary: n.failureSummary,
        })),
    }));

    const report = { slug, route, durationMs: Date.now() - t0, violationCount: violations.length, violations };
    writeFileSync(join(reportsDir, `${slug}.json`), JSON.stringify(report, null, 2), 'utf8');

    summary.components[slug] = {
        violationCount: violations.length,
        rules: violations.map(v => ({ id: v.id, impact: v.impact, nodeCount: v.nodes.length })),
    };
    summary.totals.violations += violations.length;
    for (const v of violations) {
        if (v.impact && summary.totals.byImpact[v.impact] !== undefined) summary.totals.byImpact[v.impact]++;
    }

    const tag = violations.length === 0 ? '✓' : `✗ (${violations.length})`;
    console.log(`[a11y-audit] ${tag} ${route} (${Date.now() - t0}ms)`);
}

try {
    const concurrency = Number(process.env.A11Y_AUDIT_CONCURRENCY) || (process.platform === 'win32' ? 3 : 6);
    const queue = [...slugs];
    async function worker() {
        const page = await browser.newPage();
        await page.setViewport({ width: 1280, height: 900 });
        while (queue.length) {
            const slug = queue.shift();
            try {
                await auditOne(page, slug);
            } catch (err) {
                console.error(`[a11y-audit] ✗ ${slug} — ${err.message}`);
                summary.errors.push({ slug, error: err.message });
            }
        }
        await page.close();
    }
    await Promise.all(Array.from({ length: Math.min(concurrency, slugs.length) }, worker));
} finally {
    await browser.close();
    if (dotnetProc) {
        dotnetProc.kill();
        // Give the process tree a moment; on Windows `dotnet run` spawns a
        // child apphost that survives a plain kill() otherwise.
        if (process.platform === 'win32' && dotnetProc.pid) {
            spawn('taskkill', ['/pid', String(dotnetProc.pid), '/T', '/F'], { stdio: 'ignore', shell: true });
        }
    }
}

writeFileSync(join(reportsDir, 'summary.json'), JSON.stringify(summary, null, 2), 'utf8');

console.log(`[a11y-audit] done: ${slugs.length} routes, ${summary.totals.violations} violations ` +
    `(critical ${summary.totals.byImpact.critical}, serious ${summary.totals.byImpact.serious}, ` +
    `moderate ${summary.totals.byImpact.moderate}, minor ${summary.totals.byImpact.minor}), ` +
    `${summary.errors.length} error(s). Reports in ${reportsDir}`);

if (summary.errors.length > 0) process.exitCode = 1;
