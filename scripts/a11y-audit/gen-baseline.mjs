#!/usr/bin/env node
// One-off: build baseline.json from the current reports/ + exclusions.json,
// the same node-level filtering logic as check-baseline.mjs. Never run this
// blindly to "make CI green" — it's meant for the initial baseline commit
// and for deliberate reviewed updates, not routine use.
import { readFileSync, readdirSync, existsSync, writeFileSync } from 'node:fs';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { nodeShapeHash } from './node-identity.mjs';

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, '..', '..');
const reportsDir = join(__dirname, 'reports');
const exclusions = JSON.parse(readFileSync(join(__dirname, 'exclusions.json'), 'utf8'));
const GATED_IMPACTS = new Set(['critical', 'serious']);

const rulesExclusions = exclusions.exclusions.map(e => ({
    rule: e.rule,
    component: e.component ?? null,
    targetPattern: new RegExp(e.targetPattern),
}));
function isNodeExcluded(component, rule, matchable) {
    return rulesExclusions.some(ex =>
        ex.rule === rule && (ex.component === null || ex.component === component) && ex.targetPattern.test(matchable));
}

// summary.json (run.mjs) and axe-findings.json (this script's own output,
// written into reportsDir below) are not per-component reports — skip both,
// or a second run without clearing reports/ crashes on axe-findings.json's
// {findings: [...]} shape (no `.violations`), same exclusion check-baseline.mjs
// already applies.
if (!existsSync(reportsDir)) {
    console.error(`[gen-baseline] reports dir not found: ${reportsDir}. Run 'node run.mjs' first.`);
    process.exit(1);
}

const reportFiles = readdirSync(reportsDir).filter(f =>
    f.endsWith('.json') && f !== 'summary.json' && f !== 'axe-findings.json');

// Same source-of-truth check-baseline.mjs applies: prefer summary.json's
// components (written atomically by run.mjs at the end of THIS sweep) over
// "which reports/<slug>.json files happen to exist on disk". run.mjs does
// not clear reportsDir before writing, so a component rename/removal or an
// aborted `--slug` run can leave stale per-component files behind; without
// this filter, baseline.json would silently re-absorb findings for routes
// the latest sweep never actually audited. Falls back to file presence when
// summary.json is absent (e.g. a hand-built reports/ fixture).
const summaryPath = join(reportsDir, 'summary.json');
const summary = existsSync(summaryPath) ? JSON.parse(readFileSync(summaryPath, 'utf8')) : null;
const reportedSlugs = summary?.components
    ? new Set(Object.keys(summary.components))
    : new Set(reportFiles.map(f => f.replace(/\.json$/, '')));

// Refuse to overwrite the committed baseline from an incomplete sweep — this
// is the only tool that WRITES baseline.json (check-baseline.mjs only reads
// it), and entries is rebuilt from scratch below, so a partial run silently
// drops every skipped/errored component's accepted debt as if it had been
// fixed. A `node run.mjs --slug button` sweep followed by this script would
// otherwise truncate baseline.json down to just Button's findings. Apply the
// same two completeness checks check-baseline.mjs already gates CI on:
// per-route errors recorded in summary.json, and full coverage against the
// registry (the same source run.mjs enumerates routes from).
if (summary?.errors?.length > 0) {
    console.error(`[gen-baseline] refusing to write baseline.json: ${summary.errors.length} route(s) errored ` +
        `during the sweep (see summary.json) and were never actually audited:`);
    for (const e of summary.errors) console.error(`  ✗ ${e.slug} — ${e.error}`);
    console.error('\nRe-run a full sweep (node run.mjs) with zero errors before regenerating the baseline.');
    process.exit(1);
}
const registryPath = join(repoRoot, 'src', 'Lumeo', 'registry', 'registry.json');
if (summary && existsSync(registryPath)) {
    const registry = JSON.parse(readFileSync(registryPath, 'utf8'));
    const expectedSlugs = Object.entries(registry.components)
        .filter(([, c]) => c.hasDocsPage)
        .map(([slug]) => slug);
    const missingSlugs = expectedSlugs.filter(s => !reportedSlugs.has(s));
    if (missingSlugs.length > 0) {
        console.error(`[gen-baseline] refusing to write baseline.json: ${missingSlugs.length} documented ` +
            `component(s) from registry.json are missing from summary.json (partial sweep, e.g. --slug):`);
        for (const s of missingSlugs) console.error(`  ✗ ${s}`);
        console.error('\nRe-run a full sweep (node run.mjs, no --slug/--base-url subset) before regenerating the baseline.');
        process.exit(1);
    }
}

const entries = [];
const findings = []; // full detail for axe-findings.json

for (const file of reportFiles) {
    const slug = file.replace(/\.json$/, '');
    if (!reportedSlugs.has(slug)) continue;
    const report = JSON.parse(readFileSync(join(reportsDir, file), 'utf8'));
    for (const v of report.violations) {
        // Match against target selector + raw outerHTML, not target alone — see
        // check-baseline.mjs for why (axe's minimal-selector target unpredictably
        // omits classes; outerHTML always has the full, stable class list).
        const survivingNodes = v.nodes.filter(n => {
            const target = Array.isArray(n.target) ? n.target.join(' ') : String(n.target);
            const matchable = `${target} ${n.html ?? ''}`;
            return !isNodeExcluded(report.slug, v.id, matchable);
        });
        if (survivingNodes.length === 0) continue;
        if (GATED_IMPACTS.has(v.impact)) {
            // nodeCount is the accepted debt ceiling for this (component, rule)
            // pair, not just a presence flag — check-baseline.mjs fails the gate
            // if a later run's surviving node count for the SAME pair exceeds
            // this, so a new nameless control added to a component that already
            // has baselined debt under that rule still fails as NEW instead of
            // being waved through by the (component, rule) key alone.
            //
            // nodeShapes is the accepted set of node IDENTITIES (see
            // node-identity.mjs) for the pair — count alone can't tell "a
            // baselined node got fixed, a different one started failing" apart
            // from "nothing changed" when the totals happen to land the same;
            // the shape set is what lets check-baseline.mjs catch that.
            //
            // nodeShapeCounts carries the same shapes as a MULTISET (shape ->
            // how many surviving nodes have it), not just membership. A plain
            // Set can't tell "one baselined shape-A node got fixed while a
            // different shape-A node appeared elsewhere" apart from "nothing
            // changed" — same shape SET, same total nodeCount, but a genuine
            // swap happened. Per-shape counts catch that: if this run's count
            // for a shape exceeds what was ever accepted for it, that's new,
            // even when the pair's aggregate totals still land the same.
            const shapeHashes = survivingNodes.map(n => nodeShapeHash(n.target, n.html));
            const nodeShapes = [...new Set(shapeHashes)].sort();
            const nodeShapeCounts = Object.fromEntries(
                nodeShapes.map(shape => [shape, shapeHashes.filter(s => s === shape).length]));
            entries.push({ component: report.slug, rule: v.id, impact: v.impact, nodeCount: survivingNodes.length, nodeShapes, nodeShapeCounts });
        }
        findings.push({
            component: report.slug,
            route: report.route,
            rule: v.id,
            impact: v.impact,
            help: v.help,
            helpUrl: v.helpUrl,
            nodeCount: survivingNodes.length,
            sampleTargets: survivingNodes.slice(0, 3).map(n => n.target),
            sampleHtml: survivingNodes.slice(0, 3).map(n => n.html),
        });
    }
}

entries.sort((a, b) => a.component.localeCompare(b.component) || a.rule.localeCompare(b.rule));
const impactRank = { critical: 0, serious: 1, moderate: 2, minor: 3 };
findings.sort((a, b) => (impactRank[a.impact] - impactRank[b.impact]) || (b.nodeCount - a.nodeCount));

const baseline = {
    $schema: './baseline.schema.json',
    generated: '2026-07-12',
    description: 'Accepted-but-not-yet-fixed critical/serious axe violations as of the last full triage. check-baseline.mjs fails CI only on a NEW (component, rule) pair not listed here. A fix makes its entry stale — check-baseline.mjs reports stale entries; prune them in the same PR as the fix so this file actually shrinks over time.',
    entries,
};
writeFileSync(join(__dirname, 'baseline.json'), JSON.stringify(baseline, null, 2) + '\n', 'utf8');
console.log(`Wrote ${entries.length} baseline entries.`);

const outFindings = {
    generated: new Date().toISOString(),
    totalReports: reportFiles.length,
    gatedPairs: findings.filter(f => GATED_IMPACTS.has(f.impact)).length,
    findings,
};
// reports/ is gitignored (see README) — a fine home for this optional,
// full-detail findings dump alongside the per-component report files it's
// derived from. No machine-specific path so this runs for any maintainer.
const findingsPath = join(reportsDir, 'axe-findings.json');
writeFileSync(findingsPath, JSON.stringify(outFindings, null, 2), 'utf8');
console.log(`Wrote ${findings.length} findings (all impacts) to ${findingsPath}`);
