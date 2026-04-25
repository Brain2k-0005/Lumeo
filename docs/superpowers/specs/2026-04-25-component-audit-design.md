# Lumeo Component Audit — Design

**Status:** Approved 2026-04-25
**Author:** Claude (Opus 4.7) for @Brain2k-005
**Scope:** Read-only audit of all 128 components in `src/Lumeo/UI/`. No code changes.

## Goal

Produce a machine-driven, evidence-based audit of every Lumeo component across five quality dimensions. Output a triage matrix plus 128 per-component detail files that can be dropped into future fix-work prompts verbatim.

The audit answers a single question for the maintainer: **"Where do I spend the next sprint?"**

## Non-goals

- Editing component, doc, or CLI code.
- Deep manual a11y review (rc.13/14 already swept; assume clean unless contract grep flags it).
- Running `lumeo add` against a fresh consumer project (deferred — cf. Q3 option B).
- Performance profiling or runtime benchmarks.
- Subjective design critique (color, layout, motion).

## Dimensions

Five columns, each derived from a deterministic check. Cell values are `OK` / `WARN` / `FAIL` plus a one-line note. No subjective scoring.

### 1. Contract compliance

Verifies the rules in `CLAUDE.md`. Per-component checks:

- First non-blank line of `.razor` is `@namespace Lumeo`.
- Component declares `[Parameter] public string? Class { get; set; }`.
- Component declares `[Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }`.
- Root element carries `@attributes="AdditionalAttributes"`.
- No raw color literals: `#[0-9a-f]{3,8}`, `rgb(`, `rgba(`, `hsl(`, `hsla(` in `.razor` / `.razor.css`.
- No Tailwind `dark:` prefix.
- Icons use `<Blazicon Svg="Lucide.X" />` from `Blazicons.Lucide` (flag any inline `<svg>` blocks > 3 lines or imports of other icon libs).
- Overlay components (Dialog, Sheet, Drawer, Popover, Tooltip, ContextMenu, DropdownMenu, HoverCard, AlertDialog, Toast, Combobox, Select, Cascader, MegaMenu, Mention, Command, ColorPicker, DatePicker, DateTimePicker, TimePicker):
  - Implement `IAsyncDisposable`.
  - Catch `JSDisconnectedException` in dispose paths.
  - Use `ComponentInteropService` (no `[Inject] IJSRuntime` direct).

### 2. API completeness

Bias: if a similar primitive in shadcn/ui, ReUI, or MudBlazor exposes a parameter, flag its absence as `WARN`. Per-component checks:

- `Disabled`, `Size`, `Variant` declared where applicable (component-class lookup table — see Appendix A).
- Two-way binding pairs: every `Value` / `Open` / `Selected` / `Checked` parameter has a matching `*Changed` `EventCallback`.
- Form-input components (Input, NumberInput, OtpInput, PasswordInput, Textarea, Select, Combobox, Checkbox, RadioGroup, Switch, Slider, DatePicker, DateTimePicker, TimePicker, ColorPicker, FileUpload, TagInput, Mention, Cascader, TreeSelect, RichTextEditor, InplaceEditor, InputMask):
  - `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name` parameters present.
  - `ReadOnly`, `Placeholder`, `MaxLength` where the input accepts free text.
- Container components (Card, Sheet, Dialog, etc.) accept `RenderFragment ChildContent`.
- Lifecycle: where there is an `Open` parameter, both `OpenChanged` and `OnOpen` / `OnClose` callbacks exist.

### 3. Bug surface

Static signs only — runtime behaviour is out of scope. Per-component checks:

- `async void` methods that are not Blazor lifecycle / event handler signatures.
- Calls to `JSRuntime.InvokeAsync` / `Interop.*` outside an `if (firstRender)` guard inside `OnAfterRenderAsync`.
- Event subscriptions (`+=`) without a matching `-=` in `Dispose` / `DisposeAsync`.
- Missing null guards on JS interop returns assigned to non-nullable locals.
- Public mutable fields (should be `[Parameter]` properties).
- `[Inject] IJSRuntime` declared directly in a component (should route through `ComponentInteropService`).
- `Task` returned but discarded with `_ =` in lifecycle methods.

### 4. Doc parity

Per-component checks against `docs/Lumeo.Docs/`:

- `Pages/Components/{ComponentName}Page.razor` exists.
- The page contains an "API Reference" section (heading or `id="api-reference"`).
- The page contains at least one `<ComponentDemo …>` block.
- The component is referenced in `Pages/ComponentsIndex.razor`.

### 5. CLI / Registry parity

Requires the registry JSON produced by `tools/Lumeo.RegistryGen` to be regenerated once at audit start. Per-component checks:

- A registry entry exists keyed by component name.
- All `.razor`, `.razor.cs`, `.css`, and `.js` files belonging to the component directory are listed in the entry's `files` array.
- Component-level dependencies (other Lumeo components referenced via tag) are declared.
- Package-level dependencies (e.g. `Blazicons.Lucide` when the component uses Lucide icons) are declared.

## Deliverables

```
docs/superpowers/audits/
├── 2026-04-25-component-audit.md          # main matrix + Top 20 Findings
└── 2026-04-25-components/
    ├── Accordion.md
    ├── Affix.md
    ├── …                                  # 128 files total
    └── Watermark.md
```

### Matrix file format

```markdown
# Component Audit — 2026-04-25

## Top 20 Findings
1. **Toast** — registers JS handler in OnInitialized, never disposed (Bugs: FAIL)
2. **Combobox** — no `Invalid`/`ErrorText`/`HelperText` parameters (API: FAIL)
…

## Matrix

| Component | Contract | API | Bugs | Docs | CLI |
|-----------|----------|-----|------|------|-----|
| Accordion | OK | WARN — missing `Disabled` | OK | OK | OK |
| Affix     | OK | OK | WARN — JS init not guarded | OK | FAIL — no registry entry |
| …         | …  | …  | …    | …    | …   |
```

### Per-component file format

```markdown
# Accordion

**Path:** `src/Lumeo/UI/Accordion/`

## Contract — OK
All checks pass.

## API — WARN
- Missing `Disabled` parameter (parity with shadcn/ui Accordion).
- `Type` parameter has no `TypeChanged` callback (one-way only).

## Bugs — OK
No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/AccordionPage.razor`
- 4 ComponentDemo blocks.
- API Reference present.

## CLI — FAIL
- No entry in `registry.json`.
```

## Architecture

```
┌────────────────────────────────────────────────┐
│ Lead (Opus 4.7) — this session                 │
│   1. Generate registry.json snapshot           │
│   2. Slice 128 components into 8 buckets       │
│   3. TeamCreate w/ 8 Sonnet 4.6 workers        │
│   4. Concat worker outputs → matrix            │
│   5. Compute Top 20 by severity                │
│   6. Commit                                    │
└────────────┬───────────────────────────────────┘
             │ dispatches
             ▼
┌────────────────────────────────────────────────┐
│ 8 × Sonnet 4.6 workers (parallel)              │
│   Each receives:                               │
│     • Audit checklist (this doc, §Dimensions)  │
│     • registry.json                            │
│     • List of ~16 component dirs to audit      │
│     • Output template (matrix row + per-cmp md)│
│   Each emits to a unique temp folder.          │
└────────────────────────────────────────────────┘
```

### Why parallel workers, not one big sweep
- Each component is independent — no shared state to coordinate.
- 128 components × ~5 dimensions × grep+read is too many tool calls for one context window without compaction loss.
- Sonnet at 16-comp scope fits comfortably; Opus reserved for synthesis where judgement matters (Top 20 ranking, severity calibration).

### Bucketing
Alphabetical, balanced by directory size (heuristic: file count). Concrete buckets fixed in the implementation plan.

## Severity rules (for Top 20 ranking)

`FAIL` outranks `WARN` outranks `OK`. Within the same level, dimension priority is: **Bugs > Contract > CLI > API > Docs**. Ties broken by the prominence list below (highest first):

1. Button, Input, Dialog, Card, Select, Checkbox, RadioGroup, Switch, Form, Toast
2. Combobox, DatePicker, DropdownMenu, Popover, Tooltip, Sheet, Drawer, Tabs, Table, Sidebar
3. all other components alphabetically

## Risks

- **Subjective API gaps.** "Missing `Variant`" is a judgement call for components like `Marquee` or `BorderBeam`. Mitigated by Appendix A's component-class lookup; residual noise accepted.
- **Registry coverage may be near-zero.** If `Lumeo.RegistryGen` does not yet emit entries for most components, column 5 will be a wall of `FAIL`. That is itself the audit signal — no mitigation needed.
- **Static checks miss runtime bugs.** Promised — see "Bug surface" wording. The audit is a triage tool, not a test suite.
- **rc.14 a11y claims unverified.** Audit treats a11y as out of scope. If contract grep accidentally turns up missing `aria-*` patterns, they get logged under Contract; otherwise no a11y column.

## Appendix A — Component-class lookup (excerpt)

| Class | Members | Required API |
|-------|---------|--------------|
| Form input | Input, Textarea, NumberInput, … | Required, Invalid, ErrorText, HelperText, Label, Name, ReadOnly |
| Overlay | Dialog, Sheet, Drawer, Popover, … | Open + OpenChanged, OnOpen, OnClose, IAsyncDisposable, JSDisconnectedException |
| Container | Card, Bento, Section, … | ChildContent |
| Trigger | Button, ShimmerButton, ToggleGroup, … | Disabled, Size, Variant, OnClick |
| Display | Avatar, Badge, Chip, KpiCard, … | Size, Variant |

Full table to be inlined into the implementation plan.

## Open decisions deferred to implementation plan

- Exact bucket assignments (8 × ~16 components).
- Worker prompt template wording.
- Output temp-folder layout for safe parallel writes.
