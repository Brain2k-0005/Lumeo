# Lumeo Audit Worker — Shared Checklist

You are auditing a slice of Lumeo components. **Read-only — do not edit any source code.**

Your job for each assigned component:
1. Read the component's directory (`src/Lumeo/UI/{Name}/`).
2. Run the 5-dimension checklist below.
3. Write **one** detail file `docs/superpowers/audits/2026-04-25-components/{Name}.md` per component.
4. Append **one** row to your matrix-fragment file `docs/superpowers/audits/_workspace/matrix-{bucket}.md`.

Output format is fixed — see the templates at the bottom. **Do not free-form.**

---

## Dimension 1 — Contract compliance

For each `.razor` file in the component dir:

| Check | How |
|-------|-----|
| First non-blank line is `@namespace Lumeo` | Read first 5 lines |
| Has `[Parameter] public string? Class { get; set; }` | Grep `public string\?\s*Class\s*{\s*get` in `.razor` and `.razor.cs` |
| Has `[Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes` | Grep `CaptureUnmatchedValues` |
| Root element carries `@attributes="AdditionalAttributes"` | Grep `@attributes=\"AdditionalAttributes\"` |
| No raw color literals | Grep `#[0-9a-fA-F]{3,8}\b`, `rgb\(`, `rgba\(`, `hsl\(`, `hsla\(` in `.razor` and `.razor.css`. Allow inside `<svg>` literal icon paths only. |
| No `dark:` Tailwind prefix | Grep `\bdark:` |
| Icons via Blazicons.Lucide | Grep `<Blazicon\b` should appear if any `<svg>` blocks > 3 lines exist |

**Overlay components only** (Dialog, Sheet, Drawer, Popover, Tooltip, ContextMenu, DropdownMenu, HoverCard, AlertDialog, Toast, Combobox, Select, Cascader, MegaMenu, Mention, Command, ColorPicker, DatePicker, DateTimePicker, TimePicker, Tour, PopConfirm):

| Check | How |
|-------|-----|
| Implements `IAsyncDisposable` | Grep `IAsyncDisposable` |
| Catches `JSDisconnectedException` | Grep `JSDisconnectedException` |
| Uses `ComponentInteropService` (no direct `[Inject] IJSRuntime`) | Grep `\[Inject\][^]]*IJSRuntime` should be ABSENT; grep `ComponentInteropService` should be PRESENT |

Cell value: `OK` if all pass, `WARN` if 1-2 minor fail, `FAIL` if multiple or any blocker fails.

---

## Dimension 2 — API completeness

Determine the component's class:

- **Form input**: Input, NumberInput, OtpInput, PasswordInput, Textarea, Select, Combobox, Checkbox, RadioGroup, Switch, Slider, DatePicker, DateTimePicker, TimePicker, ColorPicker, FileUpload, TagInput, Mention, Cascader, TreeSelect, RichTextEditor, InplaceEditor, InputMask, Rating
- **Overlay**: see overlay list above
- **Trigger**: Button, ShimmerButton, Toggle, ToggleGroup, BackToTop, SpeedDial
- **Container**: Card, Bento, Sheet, Dialog, Drawer, Popover, HoverCard, Sidebar, Container, ScrollArea, Resizable, Splitter
- **Display**: Avatar, Badge, Chip, KpiCard, Statistic, Delta, SparkCard, Sparkline, Progress, Skeleton, Spinner, QRCode, Code, Kbd, Image, Watermark
- **Other**: rest

Check parameters present (read `.razor` and `.razor.cs` for `[Parameter]` declarations):

| Class | Required parameters |
|-------|---------------------|
| Form input | `Disabled`, `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name`, `Value` + `ValueChanged`, plus `Placeholder`/`ReadOnly`/`MaxLength` for text-bearing inputs |
| Overlay | `Open` + `OpenChanged`, `OnOpen`, `OnClose`, `Disabled` |
| Trigger | `Disabled`, `Size`, `Variant`, `OnClick` |
| Container | `ChildContent`, `Size` (where applicable) |
| Display | `Size`, `Variant` (where applicable) |
| Other | judgement — flag obvious gaps relative to similar components in shadcn/ui or MudBlazor |

Cell value: `OK` if class-required parameters present, `WARN` if 1-2 missing, `FAIL` if 3+ missing or any "Value/Open" missing its `Changed` callback.

---

## Dimension 3 — Bug surface

Static signs only — no runtime testing:

| Check | How |
|-------|-----|
| `async void` outside event handlers | Grep `async void` — flag any not matching standard Blazor handler signatures |
| JS interop outside `firstRender` guard | Grep `InvokeAsync\|Interop\.` inside `OnAfterRenderAsync`. If the call is not inside an `if (firstRender)` block, flag. |
| Event subscriptions without dispose | Grep `+=` on events in component code — verify matching `-=` exists in Dispose/DisposeAsync. |
| Direct `[Inject] IJSRuntime` | Grep `\[Inject\][^]]*IJSRuntime` — should not be present |
| Public mutable fields | Grep `public\s+\w+\s+\w+\s*;` outside `[Parameter]` |
| Discarded Task with `_ =` in lifecycle | Grep `_ = .*Async\(` inside `OnInitialized\|OnAfterRender\|OnParametersSet` |
| Missing null guards on JS returns | Look for `var x = await ... InvokeAsync<NonNullable>` followed by `x.Foo` without null check |

Cell value: `OK` if no findings, `WARN` if 1-2 minor, `FAIL` if any clear leak / disposal miss / direct IJSRuntime in an overlay.

---

## Dimension 4 — Doc parity

Check `docs/Lumeo.Docs/Pages/Components/{ComponentName}Page.razor`:

| Check | How |
|-------|-----|
| Page file exists | File existence check |
| Has API Reference section | Grep `API Reference\|api-reference` in the page |
| Has at least one ComponentDemo | Grep `<ComponentDemo` |
| Listed in `docs/Lumeo.Docs/Pages/ComponentsIndex.razor` | Grep component name in that file |

Cell value: `OK` if all pass, `WARN` if missing demo or API Reference, `FAIL` if page absent.

---

## Dimension 5 — CLI / Registry parity

Read `src/Lumeo/registry/registry.json`. Find the entry for this component (key = kebab-case of name).

| Check | How |
|-------|-----|
| Entry exists | JSON key present |
| All `.razor`, `.razor.cs`, `.cs` files in the component dir are listed | Compare directory contents to entry's `files` array |
| `.js` files in the component dir are listed | Same comparison — known gap, the registry generator currently skips `.js` and `.css`. Flag with note "registry-gen does not emit JS/CSS". |
| Component-level dependencies declared | Compare against `<OtherComponent` references found in Dimension-1 grep |
| Package deps (Blazicons.Lucide, etc.) declared | If component uses `<Blazicon`, the entry should mention `Blazicons.Lucide`. Currently the generator does not emit `packageDependencies` — flag that as a structural gap, once, in the matrix preamble; per-component cell only flags missing files/component deps. |

Cell value: `OK` if all checked items pass, `WARN` if a JS or CSS file is missing from the entry, `FAIL` if the component dir contains files that aren't in the registry list at all (other than scoped CSS) or no entry exists.

---

## Output templates

### Per-component file template

Write to `docs/superpowers/audits/2026-04-25-components/{ComponentName}.md`:

```markdown
# {ComponentName}

**Path:** `src/Lumeo/UI/{ComponentName}/`
**Class:** {Form input | Overlay | Trigger | Container | Display | Other}
**Files:** {list .razor / .razor.cs / .cs / .js / .css filenames}

## Contract — {OK | WARN | FAIL}
- {finding 1, with file:line if applicable}
- {finding 2}
... or "All checks pass."

## API — {OK | WARN | FAIL}
- {finding 1}
... or "All class-required parameters present."

## Bugs — {OK | WARN | FAIL}
- {finding 1}
... or "No findings."

## Docs — {OK | WARN | FAIL}
- Page: `docs/Lumeo.Docs/Pages/Components/{Name}Page.razor` ({exists | MISSING})
- {N} ComponentDemo blocks
- API Reference: {present | MISSING}
- Indexed in ComponentsIndex.razor: {yes | no}

## CLI — {OK | WARN | FAIL}
- Registry entry: {present | MISSING}
- Files declared: {N of M}
- Missing from registry: {list filenames}
- Component deps declared: {OK | missing: ...}
```

### Matrix-row fragment template

Append to `docs/superpowers/audits/_workspace/matrix-{bucket}.md`:

```markdown
| {ComponentName} | {OK\|WARN\|FAIL} — {one-line note or "—"} | {OK\|WARN\|FAIL} — {note} | {OK\|WARN\|FAIL} — {note} | {OK\|WARN\|FAIL} — {note} | {OK\|WARN\|FAIL} — {note} |
```

One row per component, in alphabetical order, no header (the lead will add the header).

---

## Working rules

- **Do not edit any source file.** Audit is read-only.
- **Use Grep, Read, Glob.** Don't use Bash for searches.
- **Do not invoke Skill** — work directly through this checklist.
- If a check is genuinely ambiguous for a component, mark it `WARN` with note "ambiguous: {reason}" rather than guessing.
- Keep notes terse — under 80 chars per cell.
- Process your slice in alphabetical order.
- When done, leave a one-line status message: `Bucket {N} done — {16} components audited.`
