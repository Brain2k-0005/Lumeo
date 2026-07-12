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
import { nodeShapeHash } from './node-identity.mjs';

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

// Builds a shape -> accepted-count multiset from a baseline entry.
// nodeShapeCounts (added alongside the nodeShapes SET so old entries and
// diff-review stay readable) is the real per-shape ceiling; entries written
// before it existed fall back to treating every listed shape as accepted
// exactly once — see the module doc comment below for why a plain Set can't
// replace this.
function shapeCountsOf(nodeShapes, nodeShapeCounts) {
    if (nodeShapeCounts) return new Map(Object.entries(nodeShapeCounts));
    return new Map((nodeShapes ?? []).map(s => [s, 1]));
}

// Keyed by (component, rule). The value carries the accepted node-count
// ceiling AND a MULTISET (shape -> accepted count) of accepted node SHAPES
// (see node-identity.mjs) for that pair. Neither the aggregate count nor a
// plain shape SET is enough on its own: a run that fixes one baselined shape-A
// node while a different, unreviewed shape-A node starts failing elsewhere can
// leave the aggregate nodeCount AND the shape set both unchanged (shape-A was
// already accepted, at some count) while still being a genuinely new
// violation instance — a per-shape count is what catches that same-count,
// same-shape-set swap; a Set alone only catches a shape that never appeared
// in the baseline at all.
const baselineMap = new Map(baseline.entries.map(e => [
    baselineKey(e.component, e.rule),
    { nodeCount: e.nodeCount ?? 0, shapeCounts: shapeCountsOf(e.nodeShapes, e.nodeShapeCounts) },
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
// node, carrying the surviving node count AND a per-shape multiset of node
// SHAPES (see node-identity.mjs) — the count alone can't tell "known node
// fixed, new node introduced" apart from "nothing changed", but the shapes can.
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
            // Per-shape counts (multiset), not just a Set of which shapes are
            // present — see the module doc comment above baselineMap for why
            // membership alone misses a same-count shape swap.
            const shapeCounts = new Map();
            for (const n of survivingNodes) {
                const shape = nodeShapeHash(n.target, n.html);
                shapeCounts.set(shape, (shapeCounts.get(shape) ?? 0) + 1);
            }
            current.push({ component: report.slug, rule: v.id, impact: v.impact, nodeCount: survivingNodes.length, shapeCounts });
        }
    }
}

const gated = current.filter(v => GATED_IMPACTS.has(v.impact));

function baselineFor(v) {
    return baselineMap.get(baselineKey(v.component, v.rule));
}

// Shapes this run sees MORE of than the baseline ever accepted for this
// (component, rule) pair — a shape entirely new to the baseline (accepted
// count 0) is the obvious case, but a shape the baseline DOES know about,
// just at a lower count, is the actual "different node" signal a plain shape
// SET can't see: one baselined shape-A node got fixed while a different
// shape-A node started failing elsewhere, so shape-A's count grew back up to
// (or past) the ceiling via a swap the aggregate nodeCount alone can't
// distinguish from "nothing changed".
function unacceptedShapes(v, entry) {
    const unaccepted = [];
    for (const [shape, count] of v.shapeCounts) {
        if (count > (entry.shapeCounts.get(shape) ?? 0)) unaccepted.push(shape);
    }
    return unaccepted;
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
// surviving node count is now below the accepted ceiling, or some baselined
// shape's count has dropped (partially or fully cleared). Left alone, the
// ceiling/shape counts stay at the old, wider acceptance forever, so a later
// regression can regrow all the way back up to it (or a different node slip
// into the now-slack shape count) without ever being classified as NEW — e.g.
// data-grid :: button-name shrinking from 50 nodes to 1 must lower
// baseline.json's nodeCount (and nodeShapeCounts) to match in the same PR.
const shrunk = known.filter(v => {
    const entry = baselineFor(v);
    if (v.nodeCount < entry.nodeCount) return true;
    for (const [shape, acceptedCount] of entry.shapeCounts) {
        if ((v.shapeCounts.get(shape) ?? 0) < acceptedCount) return true;
    }
    return false;
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
        let clearedShapes = 0;
        for (const [shape, acceptedCount] of entry.shapeCounts) {
            if ((v.shapeCounts.get(shape) ?? 0) < acceptedCount) clearedShapes++;
        }
        console.error(`  - ${v.component} :: ${v.rule} — baselined at ${entry.nodeCount} node(s)/${entry.shapeCounts.size} shape(s), ` +
            `now ${v.nodeCount} node(s)/${v.shapeCounts.size} shape(s) (${clearedShapes} baselined shape(s) reproducing less than accepted)`);
    }
    console.error(`\nRegenerate ${shrunk.length === 1 ? 'this entry' : 'these entries'} with gen-baseline.mjs (nodeCount AND nodeShapeCounts) ` +
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
            note = ` — count unchanged but ${newShapeCount} shape(s) now reproduce more often than baselined: ` +
                `a different node is now failing this rule, not (only) the ones baseline.json accepted`;
        }
        console.error(`  ✗ ${v.component} :: ${v.rule} (${v.impact}, ${v.nodeCount} node(s))${note}`);
    }
    console.error(`\nEither fix these, or — if genuinely a false positive from docs-page chrome — add them to ` +
        `exclusions.json with a reason + a targetPattern precise enough to not also swallow real component ` +
        `findings under the same rule. Otherwise regenerate baseline.json with gen-baseline.mjs to accept as tracked ` +
        `debt (baseline should only grow when a violation is real and deliberately deferred, not as a rubber stamp).`);
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

if (brandNew.length > 0 || stale.length > 0 || shrunk.length > 0 || summaryErrors.length > 0 || missingSlugs.length > 0) {
    process.exit(1);
}

console.log('[check-baseline] OK — no new critical/serious violations beyond baseline, no stale entries, ' +
    'no sweep errors, no missing routes.');
