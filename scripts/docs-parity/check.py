#!/usr/bin/env python3
"""Docs/source parameter parity checker.

For every component in the registry (src/Lumeo/registry/registry.json),
diffs the public [Parameter] properties declared in its source files against
the parameter names documented in its docs page's API table
(docs/Lumeo.Docs/Pages/Components/<Name>Page.razor).

Pages that render their props table from the registry (via <ComponentDocPage>
or a direct <PropsTable> usage) are structurally guaranteed to be in sync and
are skipped.

Checks performed, in both directions:
  - MISSING_DOCS_PAGE / undocumented [Parameter]: a source parameter with no
    matching API-table row (or no docs page at all).
  - STALE_DOC_ROW: an API-table row whose parameter no longer exists on the
    component's source (renamed/removed but the docs row was left behind).

Docs-page filename resolution is case-insensitive (matches how a Linux CI
checkout behaves): the actual files on disk are indexed once, and the
expected "<Name>Page.razor" name is resolved through that index. An exact
case match is required for a clean pass; anything resolved via a different
case is reported as a non-fatal WARNING line so the drift gets fixed without
blocking CI on a platform difference.

Deliberate, reviewed omissions can be recorded in allowlist.json (next to
this script), scoped by declaring type (component slug -> "missingParams" ->
declaring type name -> parameter name -> reason), so an allowlist entry only
suppresses the exact declaring type it was reviewed for. Stale doc rows use
a separate, flat "staleDocRows" namespace (component slug -> row name ->
reason) so the two reason spaces never collide on the same name.

Everything else must match exactly or the check exits non-zero with a
readable per-page report.

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
# ChartPage.razor itself. The expected set of those subpages is derived from
# the chart source files themselves (see chart_source_types()) rather than
# from whatever happens to exist on disk, so a deleted/mis-cased chart page
# is caught as a real MISSING_DOCS_PAGE instead of silently disappearing.
CHART_SLUG = "chart"
CHARTS_SRC_SUBDIR = os.path.join("Charts")
CHARTS_DOCS_SUBDIR = "Charts"
CHART_FILE_RE = re.compile(r"[\\/]Charts[\\/](\w+)\.razor$")

# Only table headers in this set mark a table as a parameter/prop table. Any
# other header (e.g. "Component"/"Renders", "Value"/"Description" for enum
# tables, or a colspan'd enum-member sub-heading) is a different kind of
# table entirely and its rows never participate in either direction of the
# parity check. Tables documenting @ref-only methods/members can adopt this
# same opt-out by heading their table "Method" or "Member" instead of
# "Property"/"Prop"/"Parameter".
PARAM_TABLE_HEADERS = ("Property", "Prop", "Parameter")

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

# A documented row only counts as a *parameter* row (for the reverse
# STALE_DOC_ROW check) when its name is a plain identifier. This naturally
# excludes method-call rows like "ExportLayout()" or "MoveRow(oldIndex,
# newIndex)" (parens aren't valid in an identifier) and HTML-entity-encoded
# tag rows like "&lt;ToolbarContent&gt;" (& and ; aren't valid either).
IDENT_RE = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")

# Convention (established in DataGridPage.razor's DataGrid<TItem> table,
# which documents @ref-only getters alongside real [Parameter]s in one
# table): a Type cell ending in "(get)" marks a read-only member exposed for
# @ref access, not a settable [Parameter] — skip it for the reverse check.
GET_ONLY_MARKER = "(get)"

# Many docs pages document more than one type in their API Reference section
# (a component plus its @ref-only nested/companion model types — e.g.
# DataGridPage's "DataGrid<TItem>" table, MapLegendPage's separate
# "MapLegend"/"MapLegendItem" tables, BentoPage embedding KpiCard/SparkCard/
# Delta showcase tables, TreeViewPage's "TreeViewItem<T>" plain-record shape
# table). Each such table is preceded by a "<Heading ...>TypeName</Heading>"
# naming exactly which type it documents — that's an existing, repo-wide
# convention, not something invented for this check. The reverse check
# reads that heading and resolves rows against *that* type's real members
# (searched anywhere under src/, not just the current component's own
# registry files) instead of assuming every row belongs to the current
# component, which is what caused false STALE_DOC_ROW hits on those
# companion-type tables.
HEADING_RE = re.compile(r"<Heading\b[^>]*>(.*?)</Heading>", re.S)

# [CascadingParameter] members (e.g. Input's private `FormField` context) are
# legitimately documented in some API tables for clarity even though the
# forward check never requires them to be (they're not part of the public
# tag-attribute surface). Recognized the same way as [Parameter] but with a
# different attribute name and without requiring `public`.
CASCADING_DECL_RE = re.compile(
    r"\[\s*CascadingParameter\b[^\]]*\]"
    r"(?:\s*\[[^\]]*\])*"
    r"\s*(?:private\s+|public\s+|protected\s+|internal\s+)?"
    r"(?:virtual\s+|override\s+|required\s+|static\s+|new\s+)*"
    r"(.+?)\s+(\w+)\s*(?:\{|=>|;)",
    re.S,
)

# Blazor generic-component type parameters (e.g. "@typeparam TItem"). Some
# docs pages document these alongside real [Parameter]s in the same API
# table (e.g. TagInput's "TItem" row) — recognized, not required.
TYPEPARAM_RE = re.compile(r"^\s*@typeparam\s+(\w+)", re.M)

# General "any public settable/gettable member" matcher, used only to read
# the shape of a plain data/model type (not a Blazor component) named by a
# table's heading — e.g. ToastOptions, FileSystemNode, TreeViewItem<T>,
# SpeedDialItem. Unlike PARAM_DECL_RE this doesn't require a [Parameter]
# attribute (plain models don't have one), but it keeps the same
# "name immediately followed by {, => or ;" shape filter that already
# excludes methods and constructors.
PUBLIC_MEMBER_RE = re.compile(
    r"public\s+"
    r"(?:virtual\s+|override\s+|required\s+|static\s+|readonly\s+|new\s+)*"
    r"[A-Za-z_][\w.]*(?:<[^;{}]*>)?\??(?:\[\])?"
    r"\s+(\w+)\s*(?:\{|=>|;)",
    re.S,
)

# Declares a class/record/struct anywhere under src/ — used to locate the
# body of a type named by a docs heading, wherever it actually lives
# (top-level file, or nested inside another component's @code block).
TYPE_DECL_RE = re.compile(
    r"\b(?:public|internal)\s+(?:(?:static|sealed|abstract|partial|readonly)\s+)*"
    r"(?:class|record|struct)\s+([A-Za-z_][A-Za-z0-9_]*)\b"
)

HEAD_SNIFF_BYTES = 4000
SRC_DIR = os.path.join(ROOT, "src")


def build_case_index(dir_path):
    """Case-insensitive filename index for one directory: lower(name) -> actual on-disk name.

    Built once per directory. On a case-sensitive filesystem (Linux CI) this
    exactly reflects the checked-out file; on a case-insensitive filesystem
    it still reports the OS-visible casing, so the same resolution logic
    behaves correctly either way.
    """
    if not os.path.isdir(dir_path):
        return {}
    return {fn.lower(): fn for fn in os.listdir(dir_path) if fn.endswith(".razor")}


def resolve_page(index, expected_name):
    """Resolve expected_name against a case-insensitive index.

    Returns (actual_name_or_None, warning_or_None). actual_name is None when
    no file matches even case-insensitively (a real MISSING_DOCS_PAGE).
    warning is set (non-fatal) when the file exists but under different
    casing than the derived convention expects.
    """
    actual = index.get(expected_name.lower())
    if actual is None:
        return None, None
    if actual != expected_name:
        return actual, f"casing mismatch: expected '{expected_name}', found '{actual}' on disk"
    return actual, None


def chart_source_types(files):
    """Component type names for each dedicated chart file (Charts/<Name>.razor)."""
    types = set()
    for rel in files:
        m = CHART_FILE_RE.search(rel)
        if m:
            types.add(m.group(1))
    return types


def _declaring_type_name(rel):
    """Declaring type name for one source file's relative path.

    A Razor code-behind file (X.razor.cs) declares the SAME partial type as
    its sibling X.razor — strip the compound ".razor.cs" suffix as a unit so
    it indexes under "X" (matching X.razor's own entry, allowlist.json's
    scoping key, and docs-heading convention), instead of the naive
    os.path.splitext() result "X.razor", which never matches anything and
    silently falls back to the flat page-wide set for that file's params.
    """
    base = os.path.basename(rel)
    if base.endswith(".razor.cs"):
        return base[: -len(".razor.cs")]
    return os.path.splitext(base)[0]


def extract_source_params_by_type(files, src_root):
    """Returns (type_params, cascading_params, file_errors).

    type_params: dict[declaring type name -> set of [Parameter] names]. The
    declaring type name is the file's basename without extension, which is
    also the convention used by allowlist.json's scoping key and by the
    existing allowlist reason text (e.g. "DataGridHeaderCell parameter").
    cascading_params: same shape, but for [CascadingParameter] members and
    @typeparam generic type parameters (e.g. TagInput's "TItem") — tracked
    separately since the forward check never requires either to be
    documented, but the reverse check should still recognize them as real
    when they *are* documented (see CASCADING_DECL_RE / TYPEPARAM_RE).
    file_errors: list of (rel_path, error_code) for files that don't exist.
    """
    type_params = {}
    cascading_params = {}
    file_errors = []
    for rel in files:
        if not (rel.endswith(".razor") or rel.endswith(".cs")):
            continue
        fp = os.path.join(src_root, rel)
        type_name = _declaring_type_name(rel)
        if not os.path.exists(fp):
            file_errors.append((rel, "MISSING_FILE"))
            continue
        with open(fp, encoding="utf-8") as f:
            text = f.read()
        names = {m.group(2) for m in PARAM_DECL_RE.finditer(text)}
        type_params.setdefault(type_name, set()).update(names)
        cnames = {m.group(2) for m in CASCADING_DECL_RE.finditer(text)}
        cnames.update(m.group(1) for m in TYPEPARAM_RE.finditer(text))
        cascading_params.setdefault(type_name, set()).update(cnames)
    return type_params, cascading_params, file_errors


def _skip_balanced(text, i, open_ch, close_ch):
    """i points at open_ch; returns the index just past the matching close_ch."""
    depth = 0
    n = len(text)
    while i < n:
        if text[i] == open_ch:
            depth += 1
        elif text[i] == close_ch:
            depth -= 1
            if depth == 0:
                return i + 1
        i += 1
    return n


def _parse_positional_params(params_text):
    """Extracts parameter names from a primary-constructor param list, e.g.
    'string? TargetSelector, string Title, string? Description = null'."""
    parts = []
    depth = 0
    current = []
    for ch in params_text:
        if ch in "<([":
            depth += 1
        elif ch in ">)]":
            depth -= 1
        if ch == "," and depth == 0:
            parts.append("".join(current))
            current = []
        else:
            current.append(ch)
    if current:
        parts.append("".join(current))
    names = []
    for part in parts:
        decl = part.split("=", 1)[0].strip()
        tokens = decl.split()
        if tokens:
            names.append(tokens[-1].lstrip("*"))
    return names


def _extract_type_members(text, start):
    """Given text and the offset right after a 'class/record/struct Name' match,
    returns the set of public member names: brace-body properties/fields via
    PUBLIC_MEMBER_RE, plus primary-constructor parameter names for positional
    records (`record Foo(string A, int B)`), or both if a record has both."""
    i, n = start, len(text)
    while i < n and text[i] in " \t\r\n":
        i += 1
    if i < n and text[i] == "<":  # generic type parameters
        i = _skip_balanced(text, i, "<", ">")
        while i < n and text[i] in " \t\r\n":
            i += 1

    members = set()
    if i < n and text[i] == "(":  # positional record primary constructor
        close = _skip_balanced(text, i, "(", ")")
        members.update(_parse_positional_params(text[i + 1:close - 1]))
        i = close
        while i < n and text[i] in " \t\r\n":
            i += 1

    if i < n and text[i] == ":":  # base list: Base(...), IInterface
        depth = 0
        i += 1
        while i < n:
            c = text[i]
            if c in "([{":
                depth += 1
            elif c in ")]}":
                depth -= 1
            elif c in ";{" and depth == 0:
                break
            i += 1

    while i < n and text[i] in " \t\r\n":
        i += 1
    if i < n and text[i] == "{":
        close = _skip_balanced(text, i, "{", "}")
        body = text[i + 1:close - 1]
        members.update(m.group(1) for m in PUBLIC_MEMBER_RE.finditer(body))
    return members


def build_type_decl_index():
    """One-time scan of the whole src/ tree: type name -> list of (file text,
    offset right after the declaration). Used to resolve a docs heading like
    "ToastOptions" or "TreeViewItem<T>" to its real members, wherever that
    type is actually declared (top-level file, or nested in another
    component's @code block) — see the module docstring note on headings."""
    index = {}
    for dirpath, dirnames, filenames in os.walk(SRC_DIR):
        dirnames[:] = [d for d in dirnames if d not in ("obj", "bin", "node_modules")]
        for fn in filenames:
            if not (fn.endswith(".cs") or fn.endswith(".razor")):
                continue
            fp = os.path.join(dirpath, fn)
            with open(fp, encoding="utf-8") as f:
                text = f.read()
            for m in TYPE_DECL_RE.finditer(text):
                index.setdefault(m.group(1), []).append((text, m.end()))
    return index


def get_type_members(type_name, decl_index, cache):
    """Members of a type declared anywhere under src/, memoized by name."""
    if type_name not in cache:
        members = set()
        for text, end in decl_index.get(type_name, []):
            members |= _extract_type_members(text, end)
        cache[type_name] = members
    return cache[type_name]


def clean_heading_type_name(raw_heading):
    """'ToastOptions' / 'TreeViewItem&lt;T&gt;' / 'DataGrid&lt;TItem&gt;' -> bare type name.
    Returns None for headings that clearly aren't naming a single type (contain
    whitespace or punctuation beyond a trailing generic-arity suffix), so callers
    fall back to the current component's own aggregate instead of guessing."""
    text = _strip_tags(raw_heading).strip()
    text = re.sub(r"(&lt;.*?&gt;|<.*?>)\s*$", "", text).strip()
    if IDENT_RE.match(text):
        return text
    return None


def _strip_tags(html):
    return re.sub(r"<[^>]+>", "", html).strip()


# Grouped tables that document several sub-components in one place
# disambiguate rows with a "Component.Prop" prefix instead of a bare
# parameter name (e.g. "Column.Groupable" in DataGridPage's group-panel
# table). Both the literal cell text AND the tail after the last dot are
# checked against source, so either convention counts as documented/real.
# The prefix is captured too: it names the row's declaring type directly
# (stronger than a preceding <Heading>, which — if present at all — merely
# names whichever type happens to head the whole table) and takes priority
# as the row's owner_type so e.g. a stale "OldComponent.Value" row isn't
# hidden by some sibling type on the same page still declaring "Value".
_DOTTED_RE = re.compile(r"^([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)$")


def _iter_param_table_rows(text):
    """Yields (owner_heading, cell-text list) for every row of every
    Property/Prop/Parameter table, in document order.

    owner_heading is the raw text of the nearest preceding
    "<Heading ...>...</Heading>" (or None if the page has none before this
    table) — see the module note above HEADING_RE for why that matters.

    Handles both raw HTML <table> markup and the Blazor <Table>/<TableRow>/
    <TableCell> component markup used elsewhere in the docs.
    """
    events = []
    for m in HEADING_RE.finditer(text):
        events.append((m.start(), "heading", m.group(1)))
    for m in re.finditer(r"<table\b[^>]*>(.*?)</table>", text, re.S | re.I):
        events.append((m.start(), "table_a", m.group(1)))
    for m in re.finditer(r"<Table\b[^>]*>(.*?)</Table>", text, re.S):
        events.append((m.start(), "table_b", m.group(1)))
    events.sort(key=lambda e: e[0])

    current_heading = None
    for _, kind, data in events:
        if kind == "heading":
            current_heading = data
            continue

        if kind == "table_a":
            table_html = data
            header_m = re.search(r"<thead\b[^>]*>(.*?)</thead>", table_html, re.S | re.I)
            if not header_m:
                continue
            header_cells = re.findall(r"<th\b[^>]*>(.*?)</th>", header_m.group(1), re.S | re.I)
            if not header_cells or _strip_tags(header_cells[0]) not in PARAM_TABLE_HEADERS:
                continue
            body_m = re.search(r"<tbody\b[^>]*>(.*?)</tbody>", table_html, re.S | re.I)
            body_html = body_m.group(1) if body_m else table_html
            for row_m in re.finditer(r"<tr\b[^>]*>(.*?)</tr>", body_html, re.S | re.I):
                cells = re.findall(r"<td\b[^>]*>(.*?)</td>", row_m.group(1), re.S | re.I)
                if cells:
                    yield current_heading, [_strip_tags(c) for c in cells]
        else:  # table_b
            table_html = data
            header_m = re.search(r"<TableHeader\b[^>]*>(.*?)</TableHeader>", table_html, re.S)
            if not header_m:
                continue
            header_cells = re.findall(r"<TableHead\b[^>]*>(.*?)</TableHead>", header_m.group(1), re.S)
            if not header_cells or _strip_tags(header_cells[0]) not in PARAM_TABLE_HEADERS:
                continue
            body_m = re.search(r"<TableBody\b[^>]*>(.*?)</TableBody>", table_html, re.S)
            body_html = body_m.group(1) if body_m else table_html
            for row_m in re.finditer(r"<TableRow\b[^>]*>(.*?)</TableRow>", body_html, re.S):
                cells = re.findall(r"<TableCell\b[^>]*>(.*?)</TableCell>", row_m.group(1), re.S)
                if cells:
                    yield current_heading, [_strip_tags(c) for c in cells]


def extract_documented_rows(docs_path):
    """Returns (rows, error) for one docs page's API table(s).

    Each row is {"name": <raw first-cell text>, "check_name": <name, or the
    tail after the last dot for "Component.Prop" rows>, "type_cell": <second
    cell text, or "">, "owner_type": <clean_heading_type_name() of the
    nearest preceding heading, or None>}.
    """
    if not os.path.exists(docs_path):
        return [], "MISSING_DOCS_PAGE"
    with open(docs_path, encoding="utf-8") as f:
        text = f.read()

    rows = []
    for heading, cells in _iter_param_table_rows(text):
        name = cells[0]
        if not name:
            continue
        type_cell = cells[1] if len(cells) > 1 else ""
        m = _DOTTED_RE.match(name)
        dotted_owner, check_name = (m.group(1), m.group(2)) if m else (None, name)
        owner_type = dotted_owner or (clean_heading_type_name(heading) if heading else None)
        rows.append(
            {"name": name, "check_name": check_name, "type_cell": type_cell, "owner_type": owner_type}
        )
    return rows, None


def extract_documented_params(docs_path):
    """Returns (documented_names, error) — the flat set used by the forward check."""
    rows, err = extract_documented_rows(docs_path)
    documented = set()
    for r in rows:
        documented.add(r["name"])
        documented.add(r["check_name"])
    return documented, err


def is_parameter_row(row):
    """True when a documented row's check_name looks like a real [Parameter], not
    an @ref-only method (has parens, e.g. "ExportLayout()"), an @ref-only
    getter (Type cell marked "(get)"), or an HTML-entity-encoded tag name
    (e.g. "&lt;ToolbarContent&gt;", which fails the identifier shape)."""
    if not IDENT_RE.match(row["check_name"]):
        return False
    if GET_ONLY_MARKER in row["type_cell"]:
        return False
    return True


def is_registry_driven(docs_path):
    if not os.path.exists(docs_path):
        return False
    with open(docs_path, encoding="utf-8") as f:
        head = f.read(HEAD_SNIFF_BYTES)
    return any(marker in head for marker in REGISTRY_DRIVEN_MARKERS)


ALLOWLIST_NAMESPACES = ("missingParams", "staleDocRows")


def load_allowlist():
    """Loads allowlist.json.

    Shape: { slug: { "missingParams": { declaringType: { param: reason } },
                      "staleDocRows": { rowName: reason } } }

    "missingParams" is scoped by declaring type so an entry reviewed for one
    sub-component (e.g. data-grid's internal DataGridRow.Item) can never
    suppress the same parameter name on an unrelated declaring type.
    "staleDocRows" is a separate, flat reason namespace (a doc row and a
    missing-param entry with the same literal name never collide).
    """
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

    for slug, namespaces in data.items():
        if not isinstance(namespaces, dict):
            errors.append(
                f"allowlist['{slug}'] must be an object with 'missingParams'/'staleDocRows' keys."
            )
            continue
        for ns_key in namespaces:
            if ns_key not in ALLOWLIST_NAMESPACES:
                errors.append(
                    f"allowlist['{slug}']['{ns_key}'] is not a recognized namespace "
                    f"(expected one of {ALLOWLIST_NAMESPACES})."
                )

        missing_params = namespaces.get("missingParams", {})
        if not isinstance(missing_params, dict):
            errors.append(f"allowlist['{slug}']['missingParams'] must be an object keyed by declaring type.")
        else:
            for type_name, entries in missing_params.items():
                if not isinstance(entries, dict):
                    errors.append(
                        f"allowlist['{slug}']['missingParams']['{type_name}'] must be an object "
                        f"mapping param name -> reason."
                    )
                    continue
                for param, reason in entries.items():
                    if not isinstance(reason, str) or not reason.strip():
                        errors.append(
                            f"allowlist['{slug}']['missingParams']['{type_name}']['{param}'] "
                            f"must have a non-empty string reason."
                        )

        stale_rows = namespaces.get("staleDocRows", {})
        if not isinstance(stale_rows, dict):
            errors.append(f"allowlist['{slug}']['staleDocRows'] must be an object mapping row name -> reason.")
        else:
            for row_name, reason in stale_rows.items():
                if not isinstance(reason, str) or not reason.strip():
                    errors.append(
                        f"allowlist['{slug}']['staleDocRows']['{row_name}'] must have a non-empty string reason."
                    )

    return data, errors


def main():
    with open(REGISTRY_PATH, encoding="utf-8") as f:
        registry = json.load(f)
    components = registry["components"]

    allowlist, allowlist_errors = load_allowlist()

    docs_index = build_case_index(DOCS_COMPONENTS_ROOT)
    charts_index = build_case_index(os.path.join(DOCS_COMPONENTS_ROOT, CHARTS_DOCS_SUBDIR))
    type_decl_index = build_type_decl_index()
    type_member_cache = {}

    # Parsed once for every registered component (not just the one currently
    # being checked) so a docs heading naming a *different* registered
    # component — e.g. BentoPage.razor showcasing KpiCard/SparkCard/Delta's
    # own tables, or FeatureGridPage.razor showcasing FeatureItem's — can
    # still be resolved to that component's real [Parameter]s in the reverse
    # check below. Blazor components have no textual "class Foo" declaration
    # (the class is synthesized from the .razor filename), so this is the
    # only way to find their members; build_type_decl_index() (textual
    # class/record/struct search) only covers plain model types.
    per_slug_source = {}
    global_component_params = {}
    global_component_cascading = {}
    for gslug, gv in components.items():
        gsrc_root = os.path.join(ROOT, "src", gv.get("nugetPackage", "Lumeo"))
        gtp, gcp, gfe = extract_source_params_by_type(gv.get("files", []), gsrc_root)
        per_slug_source[gslug] = (gtp, gcp, gfe)
        for t, names in gtp.items():
            global_component_params.setdefault(t, set()).update(names)
        for t, names in gcp.items():
            global_component_cascading.setdefault(t, set()).update(names)

    results = []
    skipped_registry_driven = []
    allowlist_used = set()  # (slug, type_name, param) for missingParams
    stale_allowlist_used = set()  # (slug, row_name) for staleDocRows
    casing_warnings = []

    for slug, v in sorted(components.items()):
        name = v["name"]
        files = v.get("files", [])
        expected_page = f"{name}Page.razor"
        actual_page, casing_warning = resolve_page(docs_index, expected_page)
        docs_path = os.path.join(DOCS_COMPONENTS_ROOT, actual_page or expected_page)
        if casing_warning:
            casing_warnings.append(f"{slug}: {casing_warning}")

        if is_registry_driven(docs_path):
            skipped_registry_driven.append(slug)
            continue

        type_params, cascading_params, file_errors = per_slug_source[slug]
        file_errors = list(file_errors)

        all_source_params = set()
        for names in type_params.values():
            all_source_params.update(names)
        all_source_params -= EXCLUDE_PARAMS

        # Fallback source set for the reverse (stale-row) check only: real
        # [Parameter]s plus documented [CascadingParameter]s plus companion
        # type members, spanning the whole page (used when a row's table has
        # no resolvable owner heading, i.e. the common single-table case).
        all_stale_check_params = set(all_source_params)
        for names in cascading_params.values():
            all_stale_check_params.update(names)

        param_declaring_types = {}
        for type_name, names in type_params.items():
            for p in names:
                if p in EXCLUDE_PARAMS:
                    continue
                param_declaring_types.setdefault(p, set()).add(type_name)

        doc_rows, doc_err = extract_documented_rows(docs_path)
        doc_pages_checked = [(docs_path, doc_rows)]

        if slug == CHART_SLUG:
            expected_chart_types = sorted(chart_source_types(files))
            for chart_type in expected_chart_types:
                expected_chart_page = f"{chart_type}Page.razor"
                actual_chart_page, chart_casing_warning = resolve_page(charts_index, expected_chart_page)
                if chart_casing_warning:
                    casing_warnings.append(f"{slug} ({chart_type}): {chart_casing_warning}")
                if actual_chart_page is None:
                    file_errors.append(
                        (f"{CHARTS_DOCS_SUBDIR}/{expected_chart_page}", "MISSING_DOCS_PAGE")
                    )
                    continue
                chart_page_path = os.path.join(DOCS_COMPONENTS_ROOT, CHARTS_DOCS_SUBDIR, actual_chart_page)
                if is_registry_driven(chart_page_path):
                    continue
                sub_rows, _ = extract_documented_rows(chart_page_path)
                doc_pages_checked.append((chart_page_path, sub_rows))

        # Documented set scoped by declaring type: a source parameter counts as
        # documented only when it appears under a table whose owner heading
        # resolves to ITS OWN declaring type — not merely somewhere on the
        # page. Without this scoping, a table for one sibling type "absorbs"
        # an identically named parameter that actually lives on a different
        # type (e.g. ContextMenuPage.razor documents ContextMenuTrigger's
        # LongPressMs/MoveTolerancePx under the ContextMenu heading; a flat
        # by-name set would wrongly treat ContextMenuTrigger as documented
        # too, since both types declare unrelated params under the same
        # names elsewhere).
        #
        # A row only competes against a *sibling* declaring type when its
        # heading resolves to one of this component's OTHER own types (the
        # ContextMenu/ContextMenuTrigger case above). A row whose heading
        # doesn't resolve to any of this component's own types at all —
        # either because there is no heading (a descriptive prose H2 like
        # DataGrid's "Tree-grid mode" / "Group panel" sections, or
        # Combobox's inline multi-select table), or because it names
        # something else entirely (an external model type, an enum, a
        # different registered component) — makes no ownership claim among
        # this component's siblings, so it still counts for all of them, as
        # before scoping existed.
        #
        # Likewise, scoping only bites for a declaring type the page itself
        # singles out with its own heading SOMEWHERE (a deliberate, named
        # section — like ContextMenuTrigger's). A type that never gets a
        # heading of its own on this page at all (the common case for
        # internal composition/helper types among a component's files, e.g.
        # DataGrid's DataGridColumnVisibility, which piggybacks conventional
        # names like Columns/Class that are already documented under the
        # main table) keeps the old flat, page-wide match — the docs
        # authors never carved out a section to scope it against, so there
        # is nothing to mis-file it under. Pragmatic fallback: a component
        # with only one declaring type is never "headed" either, and so
        # always takes this flat path.
        flat_documented = set()
        headed_types = set()
        scoped_documented = {}
        unscoped_documented = set()
        for _, rows in doc_pages_checked:
            for r in rows:
                owner = r["owner_type"]
                if owner is not None and owner not in type_params:
                    # The row's owner (heading or dotted prefix) resolves to
                    # a real type name, but not one of THIS component's own
                    # declaring types — e.g. a companion model's own
                    # showcase table (SpeedDialPage's "SpeedDialItem"
                    # table), or another registered component's table
                    # embedded for illustration (BentoPage showcasing
                    # KpiCard/SparkCard/Delta). It documents a DIFFERENT
                    # type entirely, so it must not count toward any of
                    # this component's own undocumented parameters —
                    # skip it instead of falling into the page-wide sets.
                    continue
                flat_documented.add(r["name"])
                flat_documented.add(r["check_name"])
                if owner in type_params:
                    headed_types.add(owner)
                    bucket = scoped_documented.setdefault(owner, set())
                    bucket.add(r["name"])
                    bucket.add(r["check_name"])
                else:
                    unscoped_documented.add(r["name"])
                    unscoped_documented.add(r["check_name"])

        def is_documented(type_name, param_name):
            if type_name not in headed_types:
                return param_name in flat_documented
            if param_name in scoped_documented.get(type_name, set()):
                return True
            return param_name in unscoped_documented

        # --- Forward check: source [Parameter]s missing from docs ---
        missing_params_allowlist = allowlist.get(slug, {}).get("missingParams", {})
        missing = []
        for p in sorted(all_source_params):
            declaring_types = param_declaring_types.get(p, set())
            undocumented_types = {t for t in declaring_types if not is_documented(t, p)}
            if not undocumented_types:
                continue
            covered_types = {t for t in undocumented_types if p in missing_params_allowlist.get(t, {})}
            for t in covered_types:
                allowlist_used.add((slug, t, p))
            uncovered_types = undocumented_types - covered_types
            if not uncovered_types:
                continue
            if covered_types:
                missing.append(
                    f"{p} (undocumented on {', '.join(sorted(uncovered_types))}; "
                    f"already allowlisted for {', '.join(sorted(covered_types))})"
                )
            else:
                missing.append(p)

        # Allowlist entries for params that are no longer missing (docs caught
        # up, or the param was renamed/removed) go stale silently otherwise —
        # flag them so the allowlist stays honest and shrinks over time.
        for type_name, entries in missing_params_allowlist.items():
            if type_name not in type_params:
                allowlist_errors.append(
                    f"allowlist['{slug}']['missingParams']['{type_name}'] refers to an unknown "
                    f"declaring type (no source file named '{type_name}.razor'/'.cs' among {name}'s files)."
                )
                continue
            for p in entries:
                if p not in type_params[type_name]:
                    allowlist_errors.append(
                        f"allowlist['{slug}']['missingParams']['{type_name}']['{p}'] claims "
                        f"{type_name} declares parameter '{p}', but it does not (fix forward: "
                        f"correct or remove this entry)."
                    )
                elif (slug, type_name, p) not in allowlist_used:
                    allowlist_errors.append(
                        f"allowlist['{slug}']['missingParams']['{type_name}']['{p}'] is stale: "
                        f"'{p}' on {type_name} is no longer an undocumented parameter on {name} "
                        f"(fix forward: remove this entry)."
                    )

        # --- Reverse check: documented rows whose parameter no longer exists in source ---
        stale_doc_allowlist = allowlist.get(slug, {}).get("staleDocRows", {})
        stale_rows = []
        seen_stale_names = set()
        for _, rows in doc_pages_checked:
            for r in rows:
                if not is_parameter_row(r):
                    continue
                check_name = r["check_name"]
                if check_name in EXCLUDE_PARAMS:
                    continue

                # A row is accepted if its name is a real member ANYWHERE
                # relevant: this component's own [Parameter]s/@ref-getters
                # (the common case — this alone covers a table whose heading
                # names one of the *current* slug's own files, since two
                # sibling files of the same component, e.g. ContextMenu's
                # own table documenting ContextMenuTrigger's LongPressMs,
                # are both already in all_stale_check_params), PLUS whatever
                # the table's specific owner heading additionally resolves
                # to: a plain model type found anywhere under src/ (e.g.
                # ToastOptions, TreeViewItem<T>), or another REGISTERED
                # component's own [Parameter]s (e.g. Bento's page showcasing
                # KpiCard/SparkCard/Delta's tables). Widening rather than
                # replacing means a heading that fails to resolve to
                # anything never turns a real row into a false positive.
                check_source = set(all_stale_check_params)
                owner_type = r["owner_type"]
                if owner_type is not None:
                    check_source |= type_params.get(owner_type, set())
                    check_source |= cascading_params.get(owner_type, set())
                    check_source |= get_type_members(owner_type, type_decl_index, type_member_cache)
                    check_source |= global_component_params.get(owner_type, set())
                    check_source |= global_component_cascading.get(owner_type, set())

                if check_name in check_source:
                    continue
                if check_name in stale_doc_allowlist:
                    stale_allowlist_used.add((slug, check_name))
                    continue
                if check_name in seen_stale_names:
                    continue
                seen_stale_names.add(check_name)
                stale_rows.append(r["name"])
        stale_rows.sort()

        for row_name in stale_doc_allowlist:
            if (slug, row_name) not in stale_allowlist_used:
                allowlist_errors.append(
                    f"allowlist['{slug}']['staleDocRows']['{row_name}'] is stale: "
                    f"'{row_name}' is documented and matches a real source parameter on {name} now "
                    f"(fix forward: remove this entry)."
                )

        results.append(
            {
                "slug": slug,
                "component": name,
                "page": actual_page or expected_page,
                "doc_err": doc_err,
                "file_errors": file_errors,
                "missing": missing,
                "stale": stale_rows,
                "allowlisted": sorted(
                    f"{t}.{p}" for t, entries in missing_params_allowlist.items() for p in entries
                    if (slug, t, p) in allowlist_used
                ),
            }
        )

    # Unknown slugs in the allowlist (never reached the loop above at all,
    # e.g. typo'd or removed component) are just as much drift as a stale param.
    for slug in allowlist:
        if slug not in components:
            allowlist_errors.append(
                f"allowlist['{slug}'] refers to an unknown component slug (not in registry.json)."
            )

    gap_results = [
        r for r in results if r["missing"] or r["stale"] or r["doc_err"] or r["file_errors"]
    ]

    total_checked = len(results)
    total_skipped = len(skipped_registry_driven)
    total_missing = sum(len(r["missing"]) for r in results)
    total_stale = sum(len(r["stale"]) for r in results)
    total_allowlisted = sum(len(r["allowlisted"]) for r in results)

    print(f"Docs parity check: {total_checked} page(s) checked, "
          f"{total_skipped} registry-driven page(s) skipped, "
          f"{total_allowlisted} param(s) allowlisted.")

    if casing_warnings:
        print()
        print(f"WARNING: {len(casing_warnings)} docs page casing mismatch(es) "
              f"(resolved case-insensitively, not blocking; fix by renaming the file "
              f"to match the derived <Name>Page.razor convention):")
        for w in casing_warnings:
            print(f"  - {w}")

    if gap_results:
        print()
        print(f"FAIL: {len(gap_results)} page(s) with parity gaps "
              f"({total_missing} undocumented parameter(s), {total_stale} stale doc row(s)):")
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
            for p in r["stale"]:
                print(f"    STALE_DOC_ROW: {p}")
            print()
        print("Fix: add the missing row(s) to the page's API table (verify the "
              "correct type/description against the source), remove/correct stale "
              "row(s) that no longer match a source parameter, or — only for a "
              "deliberate, reviewed omission — add a scoped entry with a reason to "
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
