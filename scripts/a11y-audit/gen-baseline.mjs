#!/usr/bin/env node
// One-off: build baseline.json from the current reports/ + exclusions.json,
// the same node-level filtering logic as check-baseline.mjs. Never run this
// blindly to "make CI green" — it's meant for the initial baseline commit
// and for deliberate reviewed updates, not routine use.
import { readFileSync, readdirSync, existsSync, writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
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

// axe-findings.json is this script's OWN full-detail dump, written below.
// Shape is { findings: [...] }, not a per-component report with `.violations`,
// so a second run in the same reports dir must skip it or hit a TypeError at
// `for (const v of report.violations)` (same exclusion check-baseline.mjs does).
const reportFiles = readdirSync(reportsDir)
    .filter(f => f.endsWith('.json') && f !== 'summary.json' && f !== 'axe-findings.json');
const entries = [];
const findings = []; // full detail for axe-findings.json

for (const file of reportFiles) {
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
            entries.push({ component: report.slug, rule: v.id, impact: v.impact });
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
