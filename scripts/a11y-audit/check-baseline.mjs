#!/usr/bin/env node
// scripts/a11y-audit/check-baseline.mjs
//
// The actual CI gate. Reads reports/*.json (written by run.mjs), drops
// confirmed false-positive NODES listed in exclusions.json (matched by rule +
// a selector regex against each violation node's target — not by whole
// rule/component, so a shared-chrome false positive under e.g.
// "color-contrast" doesn't blind the gate to a genuinely different
// "color-contrast" node the SAME component introduces elsewhere on its own
// page, such as a low-contrast destructive-variant demo), then fails
// (exit 1) if any NEW critical/serious (component, rule) pair remains beyond
// what's accepted in baseline.json.
//
// Moderate/minor violations are reported but never fail the gate (tracked,
// not blocking — matches the "critical/serious only" instruction).
//
// Usage: node check-baseline.mjs [--reports-dir <dir>]

import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const reportsDir = args.includes('--reports-dir')
    ? resolve(args[args.indexOf('--reports-dir') + 1])
    : join(__dirname, 'reports');

const GATED_IMPACTS = new Set(['critical', 'serious']);

function loadJson(path, fallback) {
    if (!existsSync(path)) return fallback;
    return JSON.parse(readFileSync(path, 'utf8'));
}

const baseline = loadJson(join(__dirname, 'baseline.json'), { entries: [] });
const exclusions = loadJson(join(__dirname, 'exclusions.json'), { exclusions: [] });

const baselineKey = (component, rule) => `${component}::${rule}`;
const baselineSet = new Set(baseline.entries.map(e => baselineKey(e.component, e.rule)));

// Each exclusion applies to one rule and matches individual violation NODES
// via a regex against their target selector (joined). `component: null`/
// omitted means "any component" (used for shell chrome that's identical on
// every page, e.g. FactsRail, breadcrumb, docs-prose links).
const rulesExclusions = exclusions.exclusions.map(e => ({
    rule: e.rule,
    component: e.component ?? null,
    targetPattern: new RegExp(e.targetPattern),
}));

function isNodeExcluded(component, rule, target) {
    return rulesExclusions.some(ex =>
        ex.rule === rule &&
        (ex.component === null || ex.component === component) &&
        ex.targetPattern.test(target));
}

if (!existsSync(reportsDir)) {
    console.error(`[check-baseline] reports dir not found: ${reportsDir}. Run 'node run.mjs' first.`);
    process.exit(1);
}

const reportFiles = readdirSync(reportsDir).filter(f => f.endsWith('.json') && f !== 'summary.json');
if (reportFiles.length === 0) {
    console.error(`[check-baseline] no per-component reports found in ${reportsDir}.`);
    process.exit(1);
}

// current: one row per (component, rule) that has at least one NON-excluded
// node, carrying only the surviving node count.
const current = [];
let totalViolationInstances = 0;
let totalExcludedNodes = 0;

for (const file of reportFiles) {
    const report = JSON.parse(readFileSync(join(reportsDir, file), 'utf8'));
    for (const v of report.violations) {
        totalViolationInstances += v.nodes.length;
        const survivingNodes = v.nodes.filter(n => {
            const target = Array.isArray(n.target) ? n.target.join(' ') : String(n.target);
            const excluded = isNodeExcluded(report.slug, v.id, target);
            if (excluded) totalExcludedNodes++;
            return !excluded;
        });
        if (survivingNodes.length > 0) {
            current.push({ component: report.slug, rule: v.id, impact: v.impact, nodeCount: survivingNodes.length });
        }
    }
}

const gated = current.filter(v => GATED_IMPACTS.has(v.impact));
const known = gated.filter(v => baselineSet.has(baselineKey(v.component, v.rule)));
const brandNew = gated.filter(v => !baselineSet.has(baselineKey(v.component, v.rule)));

// Informational: baseline entries that no longer reproduce (fixed) — the
// maintainer should prune these so the baseline keeps shrinking.
const currentKeySet = new Set(gated.map(v => baselineKey(v.component, v.rule)));
const stale = baseline.entries.filter(e => !currentKeySet.has(baselineKey(e.component, e.rule)));

console.log(`[check-baseline] ${totalViolationInstances} total violation node(s) across ${reportFiles.length} components ` +
    `(${totalExcludedNodes} excluded as false positives). ${gated.length} critical/serious (component,rule) pair(s) remain ` +
    `(${known.length} known/baselined, ${brandNew.length} NEW).`);

if (stale.length > 0) {
    console.log(`[check-baseline] ${stale.length} baseline entr${stale.length === 1 ? 'y is' : 'ies are'} no longer reproducing — ` +
        `consider pruning baseline.json to shrink it:`);
    for (const e of stale) console.log(`  - ${e.component} :: ${e.rule}`);
}

if (brandNew.length > 0) {
    console.error(`\n[check-baseline] ${brandNew.length} NEW critical/serious violation(s) not in baseline.json:`);
    for (const v of brandNew) {
        console.error(`  ✗ ${v.component} :: ${v.rule} (${v.impact}, ${v.nodeCount} node(s))`);
    }
    console.error(`\nEither fix these, or — if genuinely a false positive from docs-page chrome — add them to ` +
        `exclusions.json with a reason + a targetPattern precise enough to not also swallow real component ` +
        `findings under the same rule. Otherwise add them to baseline.json to accept as tracked debt ` +
        `(baseline should only grow when a violation is real and deliberately deferred, not as a rubber stamp).`);
    process.exit(1);
}

console.log('[check-baseline] OK — no new critical/serious violations beyond baseline.');
