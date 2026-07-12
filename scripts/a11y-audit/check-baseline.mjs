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
// This gate does NOT rely solely on run.mjs's own exit code to catch a
// degraded sweep (a route that errored/timed out and so never got a
// reports/<slug>.json written at all). run.mjs DOES set process.exitCode = 1
// when summary.errors is non-empty, and the scheduled workflow's steps run
// in sequence without `if: always()` before this one, so that already stops
// a broken sweep from silently reaching this gate in CI today. But this
// script is also run standalone/manually (README, local iteration) where
// nothing enforces that ordering — so it independently cross-checks
// summary.json's errors AND the full expected-slug list from the registry
// (the same source run.mjs enumerates routes from), and fails loudly if
// either shows the sweep was incomplete, instead of quietly grading whatever
// subset of reports happened to get written.
//
// Usage: node check-baseline.mjs [--reports-dir <dir>]

import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, '..', '..');
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

// summary.json (run.mjs) and axe-findings.json (gen-baseline.mjs) are
// aggregate/derived files that live alongside the per-component reports in
// this same directory but don't have a `violations` array — skip both so a
// later `node check-baseline.mjs` run doesn't crash trying to iterate them.
const reportFiles = readdirSync(reportsDir).filter(f =>
    f.endsWith('.json') && f !== 'summary.json' && f !== 'axe-findings.json');
if (reportFiles.length === 0) {
    console.error(`[check-baseline] no per-component reports found in ${reportsDir}.`);
    process.exit(1);
}

// Cross-check against the same registry.json run.mjs enumerates routes from
// (not just "trust whatever files happen to be in reportsDir"), and against
// summary.json's collected per-route errors — a route that errored/timed out
// never gets a reports/<slug>.json written, and would otherwise be silently
// skipped from grading (see run.mjs's summary.errors comment). Both files
// are optional here (a `--reports-dir` pointed at a hand-built fixture won't
// have either) so their absence is not itself an error.
const registryPath = join(repoRoot, 'src', 'Lumeo', 'registry', 'registry.json');
const registry = loadJson(registryPath, null);
const summary = loadJson(join(reportsDir, 'summary.json'), null);
const summaryErrors = summary?.errors ?? [];

// Prefer summary.components — written atomically by run.mjs at the end of
// THIS sweep — over "which reports/<slug>.json files happen to exist on
// disk". File presence alone can't distinguish a slug this run actually
// covered from a stale file left by an older sweep (different registry
// state, a renamed/removed slug), an aborted `--slug` run, or a hand-copied
// reports directory; any of those would let a component that was never
// audited just now slip past the coverage check below AND (see the grading
// loop further down) let a stale report keep a fixed baseline entry alive
// or invent an old NEW finding for a route that wasn't part of this sweep.
// Fall back to file presence only when summary.json is absent (e.g. a
// `--reports-dir` pointed at a hand-built fixture with no summary.json).
const reportedSlugs = summary?.components
    ? new Set(Object.keys(summary.components))
    : new Set(reportFiles.map(f => f.replace(/\.json$/, '')));

let missingSlugs = [];
if (registry) {
    const expectedSlugs = Object.entries(registry.components)
        .filter(([, c]) => c.hasDocsPage)
        .map(([slug]) => slug);
    missingSlugs = expectedSlugs.filter(s => !reportedSlugs.has(s));
}

// current: one row per (component, rule) that has at least one NON-excluded
// node, carrying only the surviving node count.
const current = [];
let totalViolationInstances = 0;
let totalExcludedNodes = 0;

for (const file of reportFiles) {
    const slug = file.replace(/\.json$/, '');
    // Grade only slugs the current sweep actually reported (reportedSlugs,
    // above). When summary.json exists this skips stale reports/<slug>.json
    // files left on disk by an older sweep or a renamed/removed component —
    // otherwise such a file would still be graded here even though it's not
    // part of summary.components, the current sweep's source of truth.
    if (!reportedSlugs.has(slug)) continue;
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
    console.error(`\n[check-baseline] ${stale.length} baseline entr${stale.length === 1 ? 'y is' : 'ies are'} no longer reproducing:`);
    for (const e of stale) console.error(`  - ${e.component} :: ${e.rule}`);
    console.error(`\nPrune ${stale.length === 1 ? 'this entry' : 'these entries'} from baseline.json in the same PR as the fix. ` +
        `This is enforced, not advisory: a stale (component, rule) pair stays in baselineSet even after the ` +
        `violation it was accepted for is gone, so a LATER, genuinely different violation under that same rule ` +
        `would silently match the stale key and be waved through as "known debt" instead of failing as NEW.`);
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
}

if (summaryErrors.length > 0) {
    console.error(`\n[check-baseline] ${summaryErrors.length} route(s) errored during the sweep (see summary.json) — ` +
        `the gate cannot vouch for these, they were never actually audited:`);
    for (const e of summaryErrors) console.error(`  ✗ ${e.slug} — ${e.error}`);
}

if (missingSlugs.length > 0) {
    console.error(`\n[check-baseline] ${missingSlugs.length} documented component(s) from registry.json have no ` +
        `reports/<slug>.json at all — silently excluded from this gate run instead of audited:`);
    for (const s of missingSlugs) console.error(`  ✗ ${s}`);
    console.error(`\nRe-run the sweep (node run.mjs) and make sure it completes for every route before trusting this gate.`);
}

if (brandNew.length > 0 || stale.length > 0 || summaryErrors.length > 0 || missingSlugs.length > 0) {
    process.exit(1);
}

console.log('[check-baseline] OK — no new critical/serious violations beyond baseline, no stale entries, ' +
    'no sweep errors, no missing routes.');
