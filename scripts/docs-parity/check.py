#!/usr/bin/env python3
"""Docs/source parameter parity checker.

For every component in the registry (src/Lumeo/registry/registry.json),
diffs the public [Parameter] properties declared in its source files against
the parameter names documented in its docs page's API table
(docs/Lumeo.Docs/Pages/Components/<Name>Page.razor).

Pages that render their props table from the registry (via <ComponentDocPage>
or a direct <PropsTable> usage) are structurally guaranteed to be in sync and
are skipped.

Deliberate, reviewed omissions can be recorded in allowlist.json (next to
this script) with a required "reason" per entry. Everything else must match
exactly or the check exits non-zero with a readable per-page report.

Stdlib-only, no dependencies, Python 3.8+. Run from anywhere:
    python scripts/docs-parity/check.py
"""

from __future__ import annotations

import json
import os
import re
import sys

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))

REGISTRY_PATH = os.path.join(ROOT, "src", "Lumeo", "registry", "registry.json")
DOCS_COMPONENTS_ROOT = os.path.join(ROOT, "docs", "Lumeo.Docs", "Pages", "Components")
ALLOWLIST_PATH = os.path.join(SCRIPT_DIR, "allowlist.json")

# Parameters that are structural/inherited and never belong in a hand-written
# API table (Class/AdditionalAttributes are cross-cutting HTML passthrough,
# ChildContent is implicit render-fragment plumbing, CascadingParameter is not
# even a [Parameter] in practice but excluded defensively by name too).
EXCLUDE_PARAMS = {"Class", "ChildContent", "AdditionalAttributes", "CascadingParameter"}

# Markers that indicate a docs page renders its API table from the registry
# (Lumeo.Docs/Shared/ComponentDocPage.razor -> PropsTable.razor), so source/docs
# parity is structurally guaranteed and a textual diff would be a false positive.
REGISTRY_DRIVEN_MARKERS = ("<ComponentDocPage", "<PropsTable")

# The "chart" registry entry bundles all ~30 individual chart-type .razor files
# (AreaChart, BarChart, ...) as its source "files", but each chart type has its
# OWN docs page under Components/Charts/<Name>Page.razor rather than on
# ChartPage.razor itself. Union those in for this one slug.
CHART_SLUG = "chart"
CHARTS_DOCS_SUBDIR = "Charts"

# Matches: [Parameter] (optionally with args, optionally followed by more
# stacked attributes like [EditorRequired] on the same or subsequent lines),
# then a public property/field declaration (possibly spanning newlines),
# capturing the identifier name.
PARAM_DECL_RE = re.compile(
    r"\[\s*Parameter\b[^\]]*\]"  # [Parameter] or [Parameter(CaptureUnmatchedValues = true)]
    r"(?:\s*\[[^\]]*\])*"  # optional stacked attributes, e.g. [EditorRequired]
    r"\s*public\s+"
    r"(?:virtual\s+|override\s+|required\s+|static\s+|new\s+)*"
    r"(.+?)\s+(\w+)\s*(?:\{|=>|;)",  # non-greedy type, then identifier
    re.S,
)

HEAD_SNIFF_BYTES = 4000


def extract_source_params(file_path):
    """Returns (param_names, error) for one source file."""
    if not os.path.exists(file_path):
        return [], "MISSING_FILE"
    with open(file_path, encoding="utf-8") as f:
        text = f.read()
    return [m.group(2) for m in PARAM_DECL_RE.finditer(text)], None


def _strip_tags(html):
    return re.sub(r"<[^>]+>", "", html).strip()


# Grouped tables that document several sub-components in one place (e.g. a
# DataGrid group-panel table, or MapLegend/MapLegendItem sharing a table)
# disambiguate rows with a "Component.Prop" prefix instead of a bare
# parameter name (e.g. "MapLegend.Position", "Column.Groupable"). Record
# both the literal cell text AND the tail after the last dot, so either
# convention counts as documented.
_DOTTED_RE = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*\.([A-Za-z_][A-Za-z0-9_]*)$")


def _add_documented(documented, name):
    documented.add(name)
    m = _DOTTED_RE.match(name)
    if m:
        documented.add(m.group(1))


def extract_documented_params(docs_path):
    """Returns (documented_names, error) for one docs page's API table(s)."""
    if not os.path.exists(docs_path):
        return set(), "MISSING_DOCS_PAGE"
    with open(docs_path, encoding="utf-8") as f:
        text = f.read()

    documented = set()

    # --- Pattern A: raw HTML tables <table>...</table> ---
    for table_m in re.finditer(r"<table\b[^>]*>(.*?)</table>", text, re.S | re.I):
        table_html = table_m.group(1)
        header_m = re.search(r"<thead\b[^>]*>(.*?)</thead>", table_html, re.S | re.I)
        if not header_m:
            continue
        header_cells = re.findall(r"<th\b[^>]*>(.*?)</th>", header_m.group(1), re.S | re.I)
        if not header_cells:
            continue
        first_header = _strip_tags(header_cells[0])
        if first_header not in ("Property", "Prop", "Parameter"):
            continue
        body_m = re.search(r"<tbody\b[^>]*>(.*?)</tbody>", table_html, re.S | re.I)
        body_html = body_m.group(1) if body_m else table_html
        for row_m in re.finditer(r"<tr\b[^>]*>(.*?)</tr>", body_html, re.S | re.I):
            cells = re.findall(r"<td\b[^>]*>(.*?)</td>", row_m.group(1), re.S | re.I)
            if not cells:
                continue
            name = _strip_tags(cells[0])
            if name:
                _add_documented(documented, name)

    # --- Pattern B: Blazor <Table>/<TableRow>/<TableCell> components ---
    for table_m in re.finditer(r"<Table\b[^>]*>(.*?)</Table>", text, re.S):
        table_html = table_m.group(1)
        header_m = re.search(r"<TableHeader\b[^>]*>(.*?)</TableHeader>", table_html, re.S)
        if not header_m:
            continue
        header_cells = re.findall(r"<TableHead\b[^>]*>(.*?)</TableHead>", header_m.group(1), re.S)
        if not header_cells:
            continue
        first_header = _strip_tags(header_cells[0])
        if first_header not in ("Property", "Prop", "Parameter"):
            continue
        body_m = re.search(r"<TableBody\b[^>]*>(.*?)</TableBody>", table_html, re.S)
        body_html = body_m.group(1) if body_m else table_html
        for row_m in re.finditer(r"<TableRow\b[^>]*>(.*?)</TableRow>", body_html, re.S):
            cells = re.findall(r"<TableCell\b[^>]*>(.*?)</TableCell>", row_m.group(1), re.S)
            if not cells:
                continue
            name = _strip_tags(cells[0])
            if name:
                _add_documented(documented, name)

    return documented, None


def is_registry_driven(docs_path):
    if not os.path.exists(docs_path):
        return False
    with open(docs_path, encoding="utf-8") as f:
        head = f.read(HEAD_SNIFF_BYTES)
    return any(marker in head for marker in REGISTRY_DRIVEN_MARKERS)


def load_allowlist():
    if not os.path.exists(ALLOWLIST_PATH):
        return {}, [f"Allowlist file not found: {ALLOWLIST_PATH}"]
    with open(ALLOWLIST_PATH, encoding="utf-8") as f:
        try:
            data = json.load(f)
        except json.JSONDecodeError as e:
            return {}, [f"Allowlist is not valid JSON: {e}"]

    errors = []
    if not isinstance(data, dict):
        return {}, ["Allowlist root must be an object keyed by component slug."]

    for slug, entries in data.items():
        if not isinstance(entries, dict):
            errors.append(f"allowlist['{slug}'] must be an object mapping param name -> reason.")
            continue
        for param, reason in entries.items():
            if not isinstance(reason, str) or not reason.strip():
                errors.append(
                    f"allowlist['{slug}']['{param}'] must have a non-empty string reason."
                )
    return data, errors


def main():
    with open(REGISTRY_PATH, encoding="utf-8") as f:
        registry = json.load(f)
    components = registry["components"]

    allowlist, allowlist_errors = load_allowlist()

    results = []
    skipped_registry_driven = []
    allowlist_used = set()

    for slug, v in sorted(components.items()):
        name = v["name"]
        files = v.get("files", [])
        docs_path = os.path.join(DOCS_COMPONENTS_ROOT, f"{name}Page.razor")

        if is_registry_driven(docs_path):
            skipped_registry_driven.append(slug)
            continue

        src_root = os.path.join(ROOT, "src", v.get("nugetPackage", "Lumeo"))

        all_source_params = set()
        file_errors = []
        for rel in files:
            if not (rel.endswith(".razor") or rel.endswith(".cs")):
                continue
            fp = os.path.join(src_root, rel)
            params, err = extract_source_params(fp)
            if err:
                file_errors.append((rel, err))
            all_source_params.update(params)

        all_source_params -= EXCLUDE_PARAMS

        documented, doc_err = extract_documented_params(docs_path)

        if slug == CHART_SLUG:
            charts_dir = os.path.join(DOCS_COMPONENTS_ROOT, CHARTS_DOCS_SUBDIR)
            if os.path.isdir(charts_dir):
                for fn in sorted(os.listdir(charts_dir)):
                    if not fn.endswith("Page.razor"):
                        continue
                    chart_page_path = os.path.join(charts_dir, fn)
                    if is_registry_driven(chart_page_path):
                        continue
                    sub_documented, _ = extract_documented_params(chart_page_path)
                    documented |= sub_documented

        raw_missing = sorted(p for p in all_source_params if p not in documented)

        component_allowlist = allowlist.get(slug, {})
        missing = []
        for p in raw_missing:
            if p in component_allowlist:
                allowlist_used.add((slug, p))
            else:
                missing.append(p)

        # Allowlist entries for params that are no longer missing (docs caught
        # up, or the param was renamed/removed) go stale silently otherwise —
        # flag them so the allowlist stays honest and shrinks over time.
        for p in component_allowlist:
            if (slug, p) not in allowlist_used:
                allowlist_errors.append(
                    f"allowlist['{slug}']['{p}'] is stale: '{p}' is no longer an "
                    f"undocumented parameter on {name} (fix forward: remove this entry)."
                )

        results.append(
            {
                "slug": slug,
                "component": name,
                "page": f"{name}Page.razor",
                "doc_err": doc_err,
                "file_errors": file_errors,
                "missing": missing,
                "allowlisted": sorted(p for p in component_allowlist if (slug, p) in allowlist_used),
            }
        )

    # Unknown slugs in the allowlist (never reached the loop above at all,
    # e.g. typo'd or removed component) are just as much drift as a stale param.
    for slug in allowlist:
        if slug not in components:
            allowlist_errors.append(
                f"allowlist['{slug}'] refers to an unknown component slug (not in registry.json)."
            )

    gap_results = [r for r in results if r["missing"] or r["doc_err"] or r["file_errors"]]

    total_checked = len(results)
    total_skipped = len(skipped_registry_driven)
    total_missing = sum(len(r["missing"]) for r in results)
    total_allowlisted = sum(len(r["allowlisted"]) for r in results)

    print(f"Docs parity check: {total_checked} page(s) checked, "
          f"{total_skipped} registry-driven page(s) skipped, "
          f"{total_allowlisted} param(s) allowlisted.")

    if gap_results:
        print()
        print(f"FAIL: {len(gap_results)} page(s) with parity gaps "
              f"({total_missing} undocumented parameter(s)):")
        print()
        for r in sorted(gap_results, key=lambda x: x["slug"]):
            print(f"  {r['component']} ({r['page']})")
            if r["doc_err"]:
                print(f"    error: {r['doc_err']} — expected at "
                      f"docs/Lumeo.Docs/Pages/Components/{r['page']}")
            for rel, err in r["file_errors"]:
                print(f"    error: {err} — {rel}")
            for p in r["missing"]:
                print(f"    missing: {p}")
            print()
        print("Fix: add the missing row(s) to the page's API table (verify the "
              "correct type/description against the source), or — only for a "
              "deliberate, reviewed omission — add an entry with a reason to "
              "scripts/docs-parity/allowlist.json.")

    if allowlist_errors:
        print()
        print(f"FAIL: {len(allowlist_errors)} allowlist problem(s):")
        for e in allowlist_errors:
            print(f"  - {e}")

    if gap_results or allowlist_errors:
        return 1

    print("OK: source [Parameter]s and documented API tables are in parity.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
