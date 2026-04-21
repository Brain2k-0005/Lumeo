# DataGrid Filtering Audit — 2026-04

Branch: `v2.0-dev`
Scope: `src/Lumeo/UI/DataGrid/*`, `src/Lumeo/UI/Filter/*` (standalone — not used by DataGrid)

## TL;DR

The DataGrid ships a minimal but functional column filter: one operator + one value per
column, AND-combined across columns, with a hard-coded operator catalog per `FilterType`.
The evaluation engine (`DataGridFilterOperator.Evaluate`) is static and not extensible.
We added two small extension points (`Operators` whitelist, `FilterTemplate` render
fragment) that cover the most common customization needs without touching the engine.
Full custom-operator registration and OR-group logic remain 2.1 work.

---

## Section 1 — Operator matrix (current)

Legend: ✓ implemented • ✗ missing • ~ partial

| Data type  | equals | not-equals | contains | not-contains | starts-with | ends-with | gt | gte | lt | lte | between | is-empty | is-not-empty | matches-regex | in-set | not-in-set | is-today | is-this-week | is-this-month | is-this-year |
| ---------- | :----: | :--------: | :------: | :----------: | :---------: | :-------: | :-: | :-: | :-: | :-: | :-----: | :------: | :----------: | :-----------: | :----: | :--------: | :------: | :-----------: | :-----------: | :----------: |
| String     | ✓      | ✓          | ✓        | ✓            | ✓           | ✓         | —   | —   | —   | —   | —       | ✓        | ✓            | ✗             | —      | —          | —        | —             | —             | —            |
| Number     | ✓      | ✓          | —        | —            | —           | —         | ✓   | ✓   | ✓   | ✓   | ✓       | ~¹       | ~¹           | —             | —      | —          | —        | —             | —             | —            |
| Date       | ✓      | ✓          | —        | —            | —           | —         | ✓   | ✓   | ✓   | ✓   | ✓       | ~¹       | ~¹           | —             | —      | —          | ✗        | ✗             | ✗             | ✗            |
| Boolean    | ✓²     | —          | —        | —            | —           | —         | —   | —   | —   | —   | —       | ~¹       | ~¹           | —             | —      | —          | —        | —             | —             | —            |
| Enum (Select) | ✓    | —          | ✓³       | —            | —           | —         | —   | —   | —   | —   | —       | —        | —            | —             | ~³     | ✗          | —        | —             | —             | —            |
| Any        | —      | —          | —        | —            | —           | —         | —   | —   | —   | —   | —       | ✓        | ✓            | —             | —      | —          | —        | —             | —             | —            |

Notes:
1. `IsEmpty` / `IsNotEmpty` work through `value is null || value.ToString() == ""`.
   That means they also match missing dates / nullable numbers, so functionally they
   double as "is-null" / "is-not-null" — but the UI label says "empty", which is
   confusing on non-text columns.
2. Boolean columns render a single checkbox; the evaluator compares via
   `value.ToString() == true/false`. Effectively only "equals true / equals false".
3. `Select` filter encodes multi-selected values as a comma-separated string in
   `FilterDescriptor.Value`. It's evaluated inside `DataGridFilterOperator.Evaluate`
   as a custom branch (look for `FilterType == DataGridFilterType.Select`). So
   "in-set" is implemented, just not exposed as a first-class operator.

---

## Section 2 — Extension points

### Can a consumer pass a custom filter render fragment for a column?
**Yes — new in this pass.** `DataGridColumn<TItem>.FilterTemplate` is a
`RenderFragment<DataGridFilterTemplateContext>`. `DataGridColumnFilter.razor`
renders the custom fragment in place of the default body when set:

```razor
<DataGridColumnDef TItem="Employee" Title="Salary" Field="Salary" Filterable="true">
    <FilterTemplate Context="ctx">
        <Slider @bind-Value="_min" Min="0" Max="200000" />
        <Button OnClick="@(async () => await ctx.Apply(
            new FilterDescriptor(ctx.Field!, FilterOperator.GreaterThanOrEqual, _min,
                FilterType: DataGridFilterType.Number)))">Apply</Button>
        <Button Variant="Button.ButtonVariant.Ghost"
                OnClick="@(async () => await ctx.Apply(null))">Clear</Button>
    </FilterTemplate>
</DataGridColumnDef>
```

The context exposes `Field`, the `CurrentFilter` (or null), and an `Apply`
callback. Passing `null` to `Apply` clears the filter; the grid handles filter
removal and pagination reset.

### Can a consumer register a new operator globally?
**No.** `FilterOperator` is a `public enum` and `DataGridFilterOperator.Evaluate`
is a static `switch` on it. Adding "fuzzy match" or "contains any of" as a
first-class operator requires:
- a new enum value (ABI break for external serialized layouts),
- a new branch in the evaluator,
- localization keys in `DataGridColumnFilter.razor`'s label map.

Consumers can work around this today by using `FilterTemplate` (custom UI that
pre-processes the value and picks an existing operator), or by filtering data
themselves upstream before feeding it to the grid.

### Can a consumer swap the entire filter engine?
**No.** `DataGrid.ProcessClientData` calls `DataGridFilterOperator.Evaluate`
directly. There is no `IFilterEvaluator` / `Func<TItem, FilterDescriptor, bool>`
injection point. For server-side mode consumers already control evaluation (they
receive `DataGridServerRequest.Filters` and handle filtering themselves), so this
gap only bites client-side use.

### Can a consumer restrict which operators a column exposes?
**Yes — new in this pass.** `DataGridColumn<TItem>.Operators` accepts an optional
`List<FilterOperator>`. The filter popover intersects this with the
FilterType defaults (so silly combos like `StartsWith` on a Number column are
dropped). Empty intersection falls back to the whitelist verbatim so a consumer
who knows what they're doing can force "weird" combinations.

### How are filters combined (AND/OR)?
**AND only.** `DataGrid.ProcessClientData` iterates `_filters` and applies each
column's filter as an additional `.Where(...)` clause. There is no way to
express "Department = Sales OR Department = Marketing" from the UI — for that
users pick the `Select` filter type, which evaluates as "value in set".
Cross-column OR is not expressible at all.

---

## Section 3 — Gaps (prioritized)

| #  | Gap | Pain | Notes |
|----|-----|:----:|-------|
| 1  | No OR / grouping between column filters | H | Blocks advanced queries. Usually shipped as a separate "QueryBuilder" component — out of scope per ticket. |
| 2  | No custom-operator registration (`FilterOperator` is a closed enum) | H | Covered partially by `FilterTemplate`, but consumers can't surface their own operator in the default popover dropdown. |
| 3  | No regex operator for String columns | M | One extra branch + one extra enum value. Candidate for 2.1. |
| 4  | No explicit `is-null` / `is-not-null` distinct from `is-empty` | M | `IsEmpty` today conflates null and "". Rename or split. |
| 5  | Date operators missing relative matchers (`is-today`, `is-this-week`, `is-this-month`, `is-this-year`) | M | Users routinely ask for "recent" filters; currently achievable only via `Between` with manual dates. |
| 6  | Boolean column has no `is-null` state | L | Relevant only for `bool?` columns. |
| 7  | `FilterOperator.Between` doesn't reset `ValueTo` when switching operators | L | Stale value silently ignored; works but cluttered UI state. |
| 8  | No engine swap point (`IDataGridFilterEvaluator`) | L | Low demand today; server-side users bypass the engine anyway. |
| 9  | Standalone `Filter.razor` / `FilterBar.razor` components don't share code with the DataGrid filter — two separate implementations drifting | L | Refactor candidate; neither exposes operators today. |
| 10 | `IsEmpty` / `IsNotEmpty` label ("Filter.IsEmpty") reads as text-only even on Number/Date columns | L | Localization polish. |

---

## Section 4 — Small fixes shipped in this pass

All changes are additive and non-breaking.

1. **`Operators` parameter** on `DataGridColumn<TItem>` and `DataGridColumnDef`
   (`src/Lumeo/UI/DataGrid/DataGridColumn.cs`, `DataGridColumnDef.razor`). The
   filter popover's `GetAvailableOperators()` intersects the FilterType default
   set with the whitelist; falls back to the whitelist verbatim when the
   intersection is empty (`src/Lumeo/UI/DataGrid/DataGridColumnFilter.razor`).

2. **`FilterTemplate` render fragment** on `DataGridColumn<TItem>` /
   `DataGridColumnDef`, typed as
   `RenderFragment<DataGridFilterTemplateContext>`. The filter popover renders
   the custom fragment in place of the default operator + value inputs when set.

3. **`DataGridFilterTemplateContext` record** added to
   `src/Lumeo/UI/DataGrid/DataGridState.cs`: `(string? Field, FilterDescriptor?
   CurrentFilter, Func<FilterDescriptor?, Task> Apply)`.

4. **`CurrentFilter` wired to the popover** via `DataGridHeaderCell`, so custom
   templates can hydrate their initial state from the currently-applied filter
   without poking at the grid directly.

5. **Tests** — `tests/Lumeo.Tests/Components/DataGrid/DataGridFilterExtensibilityTests.cs`
   (11 tests, all pass): evaluator parity tests for the existing operators
   (Contains / NotContains / StartsWith / EndsWith / IsEmpty / IsNotEmpty /
   numeric comparisons / Between), whitelist plumbing, and `FilterTemplate`
   context behavior.

6. **Docs** — `docs/Lumeo.Docs/Pages/Components/DataGridPage.razor` gains:
   - Two new rows in the `DataGridColumnDef` parameter table (`Operators`,
     `FilterTemplate`).
   - A "Filtering Extensibility" subsection right after the Toolbar demo with
     code snippets for both extension points and an explicit list of what is
     *not* pluggable today.

---

## Section 5 — Larger work recommended for 2.1

Out of scope for this 2.0 polish pass:

- **Custom-operator registration.** The evaluator is a `static switch` on a
  closed enum. Doing this properly means introducing something like
  `IFilterOperatorHandler` registered via a service + allowing `FilterDescriptor`
  to carry a string operator key alongside the enum (for unknown ops). Non-trivial
  because layout persistence (`DataGridLayout.Filters`) serializes the
  `FilterOperator` enum, so a custom-op registry needs to round-trip too.

- **Per-grid evaluator override.** `[Parameter] Func<TItem, FilterDescriptor, bool>?
  FilterEvaluator` on `DataGrid`. Small surface, easy win, left out because no
  consumer has asked yet.

- **OR / grouped filter logic.** This is the QueryBuilder territory and is
  already on the 2026-04 gap analysis — do not build it inside `DataGridColumnFilter`.

- **LINQ-expression compilation for server-side translation.** Currently the
  server handler receives `List<FilterDescriptor>` and translates itself. A
  helper that converts a filter list into `Expression<Func<TItem, bool>>` would
  save each consumer 30 lines of boilerplate. Medium effort.

- **Relative-date operators** (`IsToday`, `IsThisWeek`, `IsThisMonth`,
  `IsThisYear`). These *can* be added without breaking serialized layouts if
  appended at the end of the `FilterOperator` enum. Engine change is small, but
  the UI needs thought (do we hide the value input? what about time zones?).
  Defer to 2.1 to get it right.

- **Regex operator.** Small code change; defer only because it interacts with
  the relative-date decision (both add to the same enum + label map).

- **Unifying `Filter.razor` / `FilterBar.razor` with the DataGrid popover.**
  The standalone filter components are completely separate implementations —
  there's an opportunity to share an operator/label vocabulary between them.
  Refactor, not 2.0 material.

---

## Build verification

- `dotnet build src/Lumeo/Lumeo.csproj -c Release` — passes (0 warn / 0 err).
- `dotnet build docs/Lumeo.Docs/Lumeo.Docs.csproj -c Release` — passes (0 warn / 0 err).
- `dotnet test --filter DataGridFilterExtensibility` — 11 / 11 pass.
- `dotnet test --filter DataGrid` — 98 pass, 5 fail. All 5 failures are
  `DataGridExpandableTests.*`, rooted in the known in-flight baseline issue:
  `Button.razor` now has `[Inject] IComponentInteropService` and the expandable
  toolbar instantiates a Button. Not caused by this audit's changes — verified by
  stack trace (`Cannot provide a value for property 'Interop' on type 'Lumeo.Button'`).
