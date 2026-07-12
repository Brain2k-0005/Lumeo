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
import { nodeShapeHash } from './node-identity.mjs';

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
// Keyed by (component, rule). The value carries BOTH the accepted node-count
// ceiling AND the set of accepted node SHAPES (see node-identity.mjs) for
// that pair. Count alone isn't enough: a run that fixes one baselined node
// but introduces a different node under the same (component, rule) can have
// an unchanged (or even lower) count while still shipping a genuinely new
// violation — the shape set catches that; the count ceiling still catches a
// known shape simply growing in volume.
const baselineMap = new Map(baseline.entries.map(e => [
    baselineKey(e.component, e.rule),
    { nodeCount: e.nodeCount ?? 0, shapes: new Set(e.nodeShapes ?? []) },
]));

// Each exclusion applies to one rule and matches individual violation NODES
// via a regex against their target selector (joined) PLUS their raw outerHTML,
// space-joined — axe's target selector is the SHORTEST selector unique enough
// to identify the node on that specific page, so which of an element's classes
// it includes is unpredictable and varies page-to-page (confirmed 2026-07-12:
// the same FactsRail label span's target sometimes includes `tracking-wide`,
// sometimes not, depending on what else needs disambiguating on that route).
// The outerHTML always carries the full, stable class list, so class-based
// exclusions should match against it; selector-structure exclusions (e.g. "is
// this node a nav-wrapped breadcrumb child") still need the target, since an
// ancestor's class isn't present in a leaf node's own outerHTML.
// `component: null`/omitted means "any component" (used for shell chrome
// that's identical on every page, e.g. FactsRail, breadcrumb, docs-prose links).
const rulesExclusions = exclusions.exclusions.map(e => ({
    rule: e.rule,
    component: e.component ?? null,
    targetPattern: new RegExp(e.targetPattern),
}));

function isNodeExcluded(component, rule, matchable) {
    return rulesExclusions.some(ex =>
        ex.rule === rule &&
        (ex.component === null || ex.component === component) &&
        ex.targetPattern.test(matchable));
}

if (!existsSync(reportsDir)) {
    console.error(`[check-baseline] reports dir not found: ${reportsDir}. Run 'node run.mjs' first.`);
    process.exit(1);
}

// axe-findings.json is gen-baseline.mjs's own full-detail dump (shape:
// { findings: [...] }, not a per-component report with `.violations`) — it
// lives in the same reportsDir as the per-component reports it's derived
// from (see gen-baseline.mjs), so it must be excluded here the same way
// summary.json is, or a maintainer re-running gen-baseline then
// check-baseline hits a TypeError instead of a gate result.
const reportFiles = readdirSync(reportsDir)
    .filter(f => f.endsWith('.json') && f !== 'summary.json' && f !== 'axe-findings.json');
if (reportFiles.length === 0) {
    console.error(`[check-baseline] no per-component reports found in ${reportsDir}.`);
    process.exit(1);
}

// current: one row per (component, rule) that has at least one NON-excluded
// node, carrying the surviving node count AND the set of node SHAPES (see
// node-identity.mjs) — the count alone can't tell "known node fixed, new
// node introduced" apart from "nothing changed", but the shape set can.
const current = [];
let totalViolationInstances = 0;
let totalExcludedNodes = 0;

for (const file of reportFiles) {
    const report = JSON.parse(readFileSync(join(reportsDir, file), 'utf8'));
    for (const v of report.violations) {
        totalViolationInstances += v.nodes.length;
        const survivingNodes = v.nodes.filter(n => {
            const target = Array.isArray(n.target) ? n.target.join(' ') : String(n.target);
            const matchable = `${target} ${n.html ?? ''}`;
            const excluded = isNodeExcluded(report.slug, v.id, matchable);
            if (excluded) totalExcludedNodes++;
            return !excluded;
        });
        if (survivingNodes.length > 0) {
            const shapes = new Set(survivingNodes.map(n => nodeShapeHash(n.target, n.html)));
            current.push({ component: report.slug, rule: v.id, impact: v.impact, nodeCount: survivingNodes.length, shapes });
        }
    }
}

const gated = current.filter(v => GATED_IMPACTS.has(v.impact));

function baselineFor(v) {
    return baselineMap.get(baselineKey(v.component, v.rule));
}

// A shape this run sees that the baseline never accepted for this
// (component, rule) pair — the actual "different node" signal, independent
// of whether the aggregate count happens to look unchanged (e.g. one
// baselined node got fixed while a different one started failing).
function unacceptedShapes(v, entry) {
    return [...v.shapes].filter(s => !entry.shapes.has(s));
}

// "Known" requires the (component, rule) pair to be baselined, this run's
// surviving node count to be within the baselined ceiling (catches a known
// shape simply growing in volume), AND every surviving node's shape to be
// one the baseline already accepted (catches a known shape being swapped
// for a different, unreviewed one without moving the count).
const known = gated.filter(v => {
    const entry = baselineFor(v);
    return entry !== undefined && v.nodeCount <= entry.nodeCount && unacceptedShapes(v, entry).length === 0;
});
const brandNew = gated.filter(v => !known.includes(v));

// Informational: baseline entries that no longer reproduce (fixed) — the
// maintainer should prune these so the baseline keeps shrinking.
const currentKeySet = new Set(gated.map(v => baselineKey(v.component, v.rule)));
const stale = baseline.entries.filter(e => !currentKeySet.has(baselineKey(e.component, e.rule)));

// Enforced like `stale`, but for a PARTIAL fix: the (component, rule) pair
// still reproduces (so it's not stale — removed entirely), but this run's
// surviving node count is now below the accepted ceiling, or some of the
// baselined shapes no longer reproduce. Left alone, the ceiling/shape set
// stays at the old, wider acceptance forever, so a later regression can
// regrow all the way back up to it (or a different node slip into the
// now-unused shape slot) without ever being classified as NEW — e.g.
// data-grid :: button-name shrinking from 50 nodes to 1 must lower
// baseline.json's nodeCount (and nodeShapes) to match in the same PR.
const shrunk = known.filter(v => {
    const entry = baselineFor(v);
    return v.nodeCount < entry.nodeCount || [...entry.shapes].some(s => !v.shapes.has(s));
});

console.log(`[check-baseline] ${totalViolationInstances} total violation node(s) across ${reportFiles.length} components ` +
    `(${totalExcludedNodes} excluded as false positives). ${gated.length} critical/serious (component,rule) pair(s) remain ` +
    `(${known.length} known/baselined, ${brandNew.length} NEW).`);

if (stale.length > 0) {
    console.error(`\n[check-baseline] ${stale.length} baseline entr${stale.length === 1 ? 'y is' : 'ies are'} no longer reproducing:`);
    for (const e of stale) console.error(`  - ${e.component} :: ${e.rule}`);
    console.error(`\nPrune ${stale.length === 1 ? 'this entry' : 'these entries'} from baseline.json in the same PR as the fix. ` +
        `This is enforced, not advisory: a stale (component, rule) pair stays in the baseline map even after the ` +
        `violation it was accepted for is gone, so a LATER, genuinely different violation under that same rule ` +
        `would silently match the stale key and be waved through as "known debt" instead of failing as NEW.`);
}

if (shrunk.length > 0) {
    console.error(`\n[check-baseline] ${shrunk.length} baseline entr${shrunk.length === 1 ? 'y has' : 'ies have'} shrunk but not fully cleared:`);
    for (const v of shrunk) {
        const entry = baselineFor(v);
        const clearedShapes = [...entry.shapes].filter(s => !v.shapes.has(s)).length;
        console.error(`  - ${v.component} :: ${v.rule} — baselined at ${entry.nodeCount} node(s)/${entry.shapes.size} shape(s), ` +
            `now ${v.nodeCount} node(s)/${v.shapes.size} shape(s) (${clearedShapes} baselined shape(s) no longer reproduce)`);
    }
    console.error(`\nRegenerate ${shrunk.length === 1 ? 'this entry' : 'these entries'} with gen-baseline.mjs (nodeCount AND nodeShapes) ` +
        `in the same PR as the partial fix. This is enforced, not advisory: leaving the old, wider ceiling/shape set in ` +
        `place lets a later regression regrow back up to it, or a different node quietly take over a now-unused shape ` +
        `slot, without ever being classified as NEW.`);
}

if (brandNew.length > 0) {
    console.error(`\n[check-baseline] ${brandNew.length} NEW critical/serious violation(s) not in baseline.json:`);
    for (const v of brandNew) {
        const entry = baselineFor(v);
        let note = '';
        if (entry === undefined) {
            note = '';
        } else if (v.nodeCount > entry.nodeCount) {
            note = ` — baselined at ${entry.nodeCount}, grew to ${v.nodeCount}: check what's new before just raising the ceiling`;
        } else {
            const newShapeCount = unacceptedShapes(v, entry).length;
            note = ` — count unchanged but ${newShapeCount} node(s) don't match any previously-baselined shape: ` +
                `a different node is now failing this rule, not the ones baseline.json accepted`;
        }
        console.error(`  ✗ ${v.component} :: ${v.rule} (${v.impact}, ${v.nodeCount} node(s))${note}`);
    }
    console.error(`\nEither fix these, or — if genuinely a false positive from docs-page chrome — add them to ` +
        `exclusions.json with a reason + a targetPattern precise enough to not also swallow real component ` +
        `findings under the same rule. Otherwise regenerate baseline.json with gen-baseline.mjs to accept as tracked ` +
        `debt (baseline should only grow when a violation is real and deliberately deferred, not as a rubber stamp).`);
}

if (brandNew.length > 0 || stale.length > 0 || shrunk.length > 0) {
    process.exit(1);
}

console.log('[check-baseline] OK — no new critical/serious violations beyond baseline, no stale entries.');
