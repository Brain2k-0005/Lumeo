# a11y-audit

axe-core WCAG A/AA sweep of every `/components/<slug>` docs route, scoped to
`<main>` (page content: hero demo, examples, API reference table) so shared
app-shell chrome (topbar, sidebar nav, footer, cookie-consent banner) doesn't
spam every single report with the same non-component findings.

## Run locally

```bash
cd scripts/a11y-audit
npm install

# Full sweep: builds docs/Lumeo.Docs (Release), boots it with `dotnet run`,
# crawls all 164 documented components, writes reports/, tears the server down.
node run.mjs

# Fast iteration on one component (skips nothing — still builds+boots unless
# you also pass --no-build / --base-url):
node run.mjs --slug button

# Against an already-running docs server (skips dotnet entirely):
node run.mjs --base-url http://localhost:5290

# Docs site already built this session:
node run.mjs --no-build

# Check current reports/ against the committed baseline (the actual CI gate):
node check-baseline.mjs
```

Output: `reports/<slug>.json` per component (full violation detail — rule id,
impact, help URL, offending nodes) + `reports/summary.json` (counts by
component/rule/impact). `reports/` is gitignored — it's a CI artifact, not
committed state.

## Files

- `run.mjs` — the sweep. Registry-driven route list, Puppeteer + axe-core.
- `check-baseline.mjs` — the gate. Diffs `reports/*.json` against
  `baseline.json`, ignoring anything in `exclusions.json`; fails on any new
  critical/serious violation.
- `gen-baseline.mjs` — internal helper, not part of CI. Regenerates
  `baseline.json` from the current `reports/` + `exclusions.json` using the
  exact same node-level filtering as `check-baseline.mjs`. Only run this
  deliberately after a real full-sweep re-triage (e.g. after fixing a batch of
  violations, to shrink the baseline) — never as a reflex to make a red gate
  green, since that would silently accept new debt instead of catching it.
- `baseline.json` — committed. The accepted-but-not-yet-fixed critical/serious
  violations as of the last triage, keyed by `(component, rule)`. A fix makes
  its entry stale (`check-baseline.mjs` will say so); prune it in the same PR
  as the fix so the baseline actually shrinks over time. Never add an entry
  here as a rubber stamp — only for a real finding you're deliberately
  deferring, with a reason.
- `exclusions.json` — committed. Confirmed **false positives** — findings
  caused by shared docs-app chrome or the audit harness itself, not by the
  audited component. Excluded from both the report totals and the baseline
  entirely. Each entry documents why.

## Why weekly, not per-PR

A full sweep is a `dotnet build` + Blazor Server boot + 164-page Puppeteer
crawl (roughly the cost of the E2E suite, done separately for a11y). That's
too slow for the PR critical path. `.github/workflows/a11y-audit.yml` runs it
weekly + on `workflow_dispatch`. A scoped variant that only crawls
components touched by a PR's diff is a reasonable follow-up if the manual
weekly cadence proves too slow to catch regressions.

## Scope note: what "the demo region" means here

Component doc pages don't tag a single dedicated "demo" element — a page is
title + hero demo + Examples + API reference table, all inside one `<main>`.
Scoping to `<main>` (excluding the topbar/sidebar/footer/consent-banner shell
rendered by `MainLayout.razor`) is the practical equivalent: everything
`<main>` contains is either the component itself or docs prose directly about
it, and everything excluded is identical across all 164 pages and therefore
not a per-component finding.
