# Lumeo Component Audit — 2026-04-25

**Scope:** All 128 components in `src/Lumeo/UI/`.
**Method:** Static, machine-checkable scan against the 5-dimension checklist in `_workspace/AUDIT_CHECKLIST.md`. Read-only — no source changed.
**Workers:** 8 parallel Sonnet 4.6 agents, 16 components each.
**Detail files:** `2026-04-25-components/{ComponentName}.md` (one per component).

---

## Headline numbers

| Dimension | OK | WARN | FAIL | Notes |
|-----------|----|------|------|-------|
| Contract  | ~95 | ~30 | 3 | FAILs: Chart, ConsentBanner, ImageCompare |
| API       | ~70 | ~55 | 3 | FAILs: InplaceEditor, Switch, TagInput |
| Bugs      | ~110 | ~15 | 0 | No bug FAILs — but 15 WARN-level lifecycle/disposal smells |
| Docs      | ~70 | ~43 | 15 | 15 components have no doc page; ~40 components have a page that isn't indexed |
| CLI/Reg   | ~110 | ~17 | 0 | Registry covers all components; gap is structural (see below) |

**One-sentence verdict:** the components themselves are in good shape. The two biggest gaps are (1) form-input validation parameters are absent across most inputs, and (2) the documentation index is missing roughly a third of the catalog.

---

## Structural findings (apply to many components — fix once, fix everywhere)

These are not per-component issues; they are systemic gaps worth a single coordinated fix:

1. **Registry generator skips `.js` and `.css` files.** `tools/Lumeo.RegistryGen/Program.cs:294-298` only enumerates `.razor` and `.cs`. Any component shipping its own JS or scoped CSS (Carousel, Dialog, Drawer, FileUpload, Gantt, Mention, Popover, RichTextEditor, Scheduler, ShimmerButton, etc.) will install incomplete via `lumeo add`. This is the **#1 CLI blocker** — until fixed, `lumeo add carousel` produces a broken consumer.
2. **Registry generator does not emit `packageDependencies`.** Components that use `<Blazicon Svg="Lucide.X" />` need the consumer to install `Blazicons.Lucide`, but no entry declares it. Affects ~80% of components.
3. **Overlay naming drift: `IsOpen/IsOpenChanged` vs. `Open/OpenChanged`.** Dialog, Drawer, DropdownMenu, AlertDialog, Collapsible, Tour, Mention, RadioGroupItem (in some places), and others use `IsOpen` / `IsOpenChanged`. The de-facto standard in shadcn/ui and ReUI is `Open`. Pick one for v2 and align.
4. **Form-input validation parameters absent across the board.** `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name` are missing on nearly every form input: Calendar, Cascader, Checkbox, ColorPicker, Combobox, DatePicker, DateTimePicker, FileUpload, InplaceEditor, Input, InputMask, Mention, NumberInput, OtpInput, PasswordInput, PickList, RadioGroup, Rating, RichTextEditor, Select, Slider, Switch, TagInput, Textarea, TimePicker, TreeSelect. A shared `FormFieldBase` mixin or a doc-level decision (e.g. "wrap with `<FormField>`") resolves this in one PR.
5. **~40 components have a doc page but aren't indexed in `ComponentsIndex.razor`.** Affix, Bento, BlurFade, BorderBeam, BottomNav, Cascader, ColorPicker, Container, DateTimePicker, Descriptions, Filter, Flex, Grid, Heading, Icon, Image, ImageCompare, InputMask, Kanban, Kbd, Link, List, MegaMenu, Mention, NumberInput, PasswordInput, PickList, PopConfirm, Rating, RichTextEditor, Scheduler, Scrollspy, Segmented, Sortable, Sparkline, Spacer, Stack, Statistic, Steps, Text, TimePicker, Timeline, Transfer, TreeSelect, TreeView, Watermark — most have docs, just no index entry.
6. **15 components have no doc page at all.** Code, Delta, KpiCard, Marquee, NumberTicker, Overlay, PromptInput, QRCode, ReasoningDisplay, ShimmerButton, SparkCard, Sparkles, StreamingText, TextReveal, ToolCallCard.

---

## Top 20 Findings (ranked by severity → dimension priority → prominence)

Severity order: FAIL > WARN. Dimension order within FAIL: Bugs > Contract > CLI > API > Docs. Tie-breaker: component prominence (form-input/overlay primitives over decorative ones).

| # | Component | Dim | Sev | Finding |
|---|-----------|-----|-----|---------|
| 1 | **Chart** | Contract | FAIL | `Chart.razor:26` injects `IJSRuntime` directly. Should route through `ComponentInteropService` per CLAUDE.md. JS interop in update path also runs outside `firstRender` guard (intentional for re-render, but worth a `_initialized` flag). |
| 2 | **ConsentBanner** | Contract | FAIL | Missing `[Parameter] Class`, `[Parameter] AdditionalAttributes`, and `@attributes="AdditionalAttributes"` on root. Otherwise high-quality (proper `IDisposable`, event unsubscription). |
| 3 | **ImageCompare** | Contract | FAIL | Raw color literals (`rgba(...)`, `#555`, `white`) baked into handle/label inline styles. Replace with theme tokens. |
| 4 | **Switch** | API | FAIL | Missing 5 of 8 form-input params: `Required`, `Invalid`, `ErrorText`, `HelperText`, `Name`. Tier-1 form primitive — fix first. |
| 5 | **InplaceEditor** | API | FAIL | Missing 6+ form-input params; also uses inline SVG icons instead of Blazicons (Contract WARN). |
| 6 | **TagInput** | API | FAIL | Missing 6 of 8 form-input params. Also: `OnAfterRenderAsync` calls JS interop outside `firstRender` whenever suggestions are visible (Bugs WARN). |
| 7 | **ShimmerButton** | Docs+Bugs | FAIL+WARN | No doc page. Also: ripple JS listener registered without cleanup; component has no `IAsyncDisposable` → leak on unmount. |
| 8 | **PromptInput** | Docs | FAIL | No doc page — but it's the AI surface primitive. Should be one of the most-visited pages. |
| 9 | **KpiCard** | Docs | FAIL | No doc page. Also: `Delta` component dependency missing from registry deps. |
| 10 | **Overlay** | Docs | FAIL | No doc page. Also: 2× `_ = InvokeAsync(...)` discarded Tasks; exceptions swallowed silently (Bugs WARN). |
| 11 | **QRCode** | Docs | FAIL | No doc page. Also: bare `catch` swallows encoder errors. |
| 12 | **Code** | Docs | FAIL | No doc page. Variant param is stringly-typed — could be enum. |
| 13 | **Marquee** | Docs | FAIL | No doc page. Component itself clean. |
| 14 | **NumberTicker** | Docs | FAIL | No doc page. Component itself clean. |
| 15 | **SparkCard** | Docs | FAIL | No doc page. |
| 16 | **ReasoningDisplay** | Docs+Contract | FAIL+WARN | No doc page. Also uses `Icon` component instead of Blazicons.Lucide directly. |
| 17 | **ToolCallCard** | Docs+Contract | FAIL+WARN | No doc page. Also: hardcoded `bg-emerald-500` / `text-emerald-500` (not theme tokens). |
| 18 | **StreamingText** | Docs+Contract | FAIL+WARN | No doc page. Also: `dark:prose-invert` Tailwind prefix at `StreamingText.razor:30` — violates "no `dark:` prefix" rule. |
| 19 | **Delta** | Docs+Contract | FAIL+WARN | No doc page. Also: hardcoded emerald/rose Tailwind colors instead of CSS vars. |
| 20 | **TextReveal** | Docs | FAIL | No doc page. Component itself clean. |

(`Sparkles` also has Docs FAIL but is a decorative one-off — it ranks 21.)

---

## Notable Bugs (WARN-level — no FAILs, but worth fixing)

| Component | Finding |
|-----------|---------|
| AgentMessageList | `DisposeAsync` missing `JSDisconnectedException` guard |
| Cascader | `OnAfterRenderAsync` registers interop outside `firstRender` (mitigated by `_registered` flag) |
| Chart | Update-path interop runs outside `firstRender` |
| DataGrid | `_ = InvokeAsync(StateHasChanged)` in column-def add/remove; `SelectedItemsChanged` called sync without `await` |
| MegaMenu | Discarded `Task` in `HandleMouseEnter` (unobserved exception risk) |
| Mention | `Task.Delay(150)` timing hack in `HandleBlur`; `caretInfo` accessed without null check |
| NavigationMenu | `_ = InvokeAsync(...)` discarded in timer callback |
| Overlay | 2× discarded Task; exceptions swallowed |
| RichTextEditor | `DisposeAsync` lacks `JSDisconnectedException` guard |
| Scheduler | Null `InstanceId` risk after init (mitigated by try/catch) |
| ShimmerButton | Ripple listener leak; no `IAsyncDisposable` |
| TagInput | Interop outside `firstRender` when suggestions shown |
| ThemeSwitcher | `InvokeAsync` discarded-task pattern |
| Toast | Discarded-task pattern in 6 places; `Task.Delay` without cancel token in exit animation |
| Tour | `LockScroll` called every render — no `firstRender` guard |

---

## Full matrix

Cells: `STATUS — short note`. Components alphabetical. **For per-component detail, see `2026-04-25-components/{Name}.md`.**

| Component | Contract | API | Bugs | Docs | CLI |
|-----------|----------|-----|------|------|-----|
| Accordion | OK — all contract checks pass | OK — all params present | OK — no findings | OK — page exists, 3 demos, API ref, indexed | OK — 4/4 files |
| Affix | OK — all contract checks pass | OK — all params present | OK — no findings | WARN — not listed in ComponentsIndex | OK — 1/1 files |
| AgentMessageList | WARN — DisposeAsync missing JSDisconnectedException guard | OK — all params present | WARN — DisposeAsync unguarded, may throw on disconnect | WARN — page is AiPage.razor (shared), not indexed | OK — 2/2 files |
| Alert | OK — all contract checks pass | WARN — Size param absent (Display class) | OK — no findings | OK — page exists, 7 demos, API ref, indexed | OK — 1/1 files |
| AlertDialog | WARN — root AlertDialog.razor has no Class/AdditionalAttributes (CascadingValue root, by design) | WARN — uses IsOpen/IsOpenChanged not Open/OpenChanged; OnOpen, OnClose, Disabled absent | OK — no findings | OK — page exists, 4 demos, API ref, indexed | OK — 9/9 files, spinner dep declared |
| AspectRatio | WARN — Class applied to inner div only; AdditionalAttributes on outer div | OK — ChildContent + Ratio present, Size N/A | OK — no findings | OK — page exists, 3 demos, API ref, indexed | OK — 1/1 files |
| Avatar | OK — all contract checks pass | OK — Size present; Variant replaced by Shape+Status | OK — no findings | OK — page exists, 4 demos, API ref, indexed | OK — 4/4 files |
| BackToTop | OK — all contract checks pass | WARN — Disabled, Variant, Size, OnClick absent (Trigger class) | OK — no findings | WARN — not listed in ComponentsIndex | OK — 1/1 files |
| Badge | OK — all contract checks pass | OK — Variant present; Size implicitly fixed | OK — no findings | OK — page exists, 7 demos, API ref, indexed | OK — 1/1 files |
| Bento | OK — all contract checks pass | OK — ChildContent, Gap, Columns, spans present | OK — no findings | WARN — not listed in ComponentsIndex | OK — 2/2 files |
| BlurFade | OK — all contract checks pass | OK — all params present | OK — no findings | WARN — no dedicated page; lives in MotionPage.razor; not indexed | OK — 1/1 files |
| BorderBeam | OK — all contract checks pass | OK — all params present | OK — no findings | WARN — no dedicated page; lives in MotionPage.razor; not indexed | OK — 1/1 files |
| BottomNav | OK — all contract checks pass | OK — all params present for all three files | OK — no findings | WARN — not listed in ComponentsIndex | OK — 3/3 files |
| Breadcrumb | OK — all contract checks pass | OK — all params present | OK — no findings | OK — page exists, 3 demos, API ref, indexed | OK — 7/7 files |
| Button | OK — all contract checks pass | OK — Disabled, Size, Variant, OnClick all present | OK — no findings | OK — page exists, 8 demos, API ref, indexed | OK — 1/1 files, spinner dep declared |
| Calendar | OK — all contract checks pass | WARN — Disabled/Required/Invalid/ErrorText/HelperText/Label/Name absent; uses IsDateDisabled Func | OK — no findings | OK — page exists, 5 demos, API ref, indexed | OK — 1/1 files |
| Card | OK — all contract checks pass | OK — ChildContent/Class present; no Size needed | OK — no findings | OK — 4 demos, API ref present, indexed | OK — 4/4 files, no deps needed |
| Carousel | OK — IAsyncDisposable, JSDisconnectedException, ComponentInteropService all present | WARN — missing Disabled param | OK — no findings | OK — 3 demos, API ref, indexed | OK — 5/5 files declared |
| Cascader | OK — IAsyncDisposable, JSDisconnectedException, ComponentInteropService present | WARN — missing Required/Invalid/ErrorText/HelperText/Label/Name | WARN — OnAfterRenderAsync registers interop outside firstRender, guarded by _registered flag | OK — 3 demos, API ref; not in index | WARN — deps: `list` declared; not indexed |
| Center | OK — all checks pass | OK — Height, Inline, ChildContent, Class present | OK — no findings | OK — 3 demos, API ref; not in index | OK — 1/1 file declared |
| Chart | **FAIL** — Chart.razor injects IJSRuntime directly, not ComponentInteropService (Chart.razor:26) | OK — rich param set on Chart and all wrappers | WARN — direct IJSRuntime; interop outside firstRender guard in else branch (intentional update path) | WARN — 0 ComponentDemo blocks (uses link-card gallery instead), API ref present, indexed | OK — 37/37 files declared |
| Checkbox | OK — all checks pass; inline SVGs are functional check marks | WARN — missing Required/Invalid/ErrorText/HelperText/Name/Value | OK — no findings | OK — 6 demos, API ref, indexed | OK — 1/1 file declared |
| Chip | OK — uses IComponentInteropService interface | OK — Size, Variant, Closable, OnClose present; missing Disabled | OK — no findings | WARN — 5 demos, API ref; not in ComponentsIndex | OK — 2/2 files declared |
| Code | OK — all checks pass | OK — Variant, Size, ChildContent, Class present; Variant is stringly-typed | OK — no findings | **FAIL** — CodePage.razor MISSING; not indexed | OK — 1/1 file declared |
| Collapsible | OK — all checks pass | OK — IsOpen+IsOpenChanged, ChildContent, Class present | OK — no findings | OK — 3 demos, API ref, indexed | OK — 3/3 files declared |
| ColorPicker | WARN — rgba()/hsl() in inline styles for HSV canvas gradient (functional, not theme colors) | WARN — missing Required/Invalid/ErrorText/HelperText/Label/Name | OK — no findings | OK — 4 demos, API ref; not in index | OK — 1/1 file declared |
| Combobox | WARN — Combobox.razor root missing Class param (Combobox.razor:18 hardcodes class="relative") | WARN — missing Disabled/Required/Invalid/ErrorText/HelperText/Label/Name | OK — no findings | OK — 5 demos, API ref, indexed | OK — 6/6 files, spinner dep declared |
| Command | WARN — Command root has no IAsyncDisposable; no JS interop (by design); CommandItem correctly disposes | WARN — no Open/OpenChanged/Disabled on root (designed as embedded palette) | OK — no findings | OK — 4 demos, API ref, indexed | OK — 7/7 files, kbd dep declared |
| ConsentBanner | **FAIL** — missing Class param, AdditionalAttributes param, and @attributes on root element | OK — comprehensive copy params, IDisposable with event unsubscription | OK — no findings | WARN — 6 demos, API ref; not in ComponentsIndex | WARN — ConsentService companion not in registry deps |
| Container | OK — all checks pass | OK — MaxWidth, Center, Padding, ChildContent, Class present | OK — no findings | OK — 2 demos, API ref; not in index | OK — 1/1 file declared |
| ContextMenu | WARN — ContextMenu.razor root missing Class param | WARN — missing OnOpen, OnClose, Disabled | OK — no findings | OK — 3 demos, API ref, indexed | OK — 10/10 files declared |
| DataGrid | OK — ComponentInteropService via [Inject] property; IAsyncDisposable; JSDisconnectedException caught | OK — comprehensive API: Items, SelectionMode, EditMode, ServerMode, SelectedItems+Changed, Class | WARN — _ = InvokeAsync(StateHasChanged) in AddColumnDef/RemoveColumnDef; SelectedItemsChanged called without await in sync methods | OK — 31 demos, API ref; not in ComponentsIndex | OK — 28/28 files, 8 deps declared |
| DataTable | OK — all contract checks pass | WARN — missing pagination params | OK — no findings | OK — page exists, 5 demos, API ref, indexed | OK — 2/2 files, checkbox dep declared |
| DatePicker | OK — all contract checks pass | WARN — missing Required/Invalid/ErrorText/HelperText/Label/Name | OK — no findings | OK — page exists, 11 demos, API ref, indexed | OK — 2/2 files, calendar+popover+time-picker deps |
| DateTimePicker | OK — all contract checks pass | WARN — missing Required/Invalid/ErrorText/HelperText/Label/Name | OK — no findings | WARN — page exists, 3 demos, API ref, not in index | OK — 1/1 files, calendar+popover deps |
| Delta | WARN — hardcoded emerald/rose Tailwind colors (not CSS vars) | OK — Display class; Format+Positive enums cover variant need | OK — no findings | **FAIL** — DeltaPage.razor MISSING, not indexed | OK — 1/1 files, no deps needed |
| Descriptions | OK — all contract checks pass | OK — all class-required params present | OK — no findings | WARN — page exists, 3 demos, API ref, not in index | OK — 2/2 files, no deps needed |
| Dialog | OK — IAsyncDisposable, JSDisconnectedException, ComponentInteropService all present | WARN — missing OnOpen, OnClose, Disabled; IsOpen naming drift | OK — no findings | OK — page exists, 4 demos, API ref, indexed | OK — 8/8 files, no deps listed (Blazicons gap is global) |
| Drawer | OK — IAsyncDisposable, JSDisconnectedException, ComponentInteropService all present | WARN — missing OnOpen, OnClose, Disabled; IsOpen naming drift | OK — no findings | OK — page exists, 4 demos, API ref, indexed | OK — 8/8 files, no deps listed |
| DropdownMenu | OK — IAsyncDisposable, JSDisconnectedException, ComponentInteropService all present | WARN — missing OnOpen, OnClose, Disabled; IsOpen naming drift | OK — no findings | OK — page exists, 6 demos, API ref, indexed | OK — 10/10 files, no deps listed |
| EmptyState | OK — all contract checks pass | OK — Display class; slots cover icon/title/description/action | OK — no findings | OK — page exists, 3 demos, API ref, indexed | OK — 1/1 files, no deps needed |
| FileUpload | OK — all contract checks pass | WARN — missing Disabled/Required/Invalid/ErrorText/HelperText/Name/Value+ValueChanged | OK — no findings | OK — page exists, 4 demos, API ref, indexed | OK — 1/1 files, input dep declared |
| Filter | OK — all contract checks pass | OK — Other class; FilterBar slots + FilterPill label/value/dismiss cover needs | OK — no findings | WARN — page exists, 5 demos, API ref, not in index | OK — 2/2 files, badge dep declared |
| Flex | OK — all contract checks pass | OK — layout utility; direction/gap/align/justify/wrap all present | OK — no findings | WARN — page exists, 4 demos, API ref, not in index | OK — 1/1 files, no deps needed |
| Form | OK — all contract checks pass | OK — Other class; Model/OnValidSubmit/Validator + FormField/Label/Error present | OK — no findings | OK — page exists, 3 demos, API ref, indexed | OK — 9/9 files, label dep declared |
| Gantt | OK — IAsyncDisposable, JSDisconnectedException, IComponentInteropService present | OK — Tasks+TasksChanged/ViewMode/callbacks/Readonly all present | OK — firstRender guard in place; no async void | WARN — page exists, 4 demos, API ref, not in index | OK — 2/2 files, toggle-group dep declared |
| Grid | OK — all contract checks pass | OK — layout utility; Columns/Gap/RowGap/ColGap present | OK — no findings | WARN — page exists, 3 demos, API ref, not in index | OK — 1/1 files, no deps needed |
| Heading | OK — all contract checks pass | OK — Display class; Level/Size/Weight/Tracking present | OK — no findings | WARN — page exists, 3 demos, API ref, not in index | OK — 1/1 files, no deps needed |
| HoverCard | WARN — root HoverCard.razor missing Class param; interop not in firstRender guard | WARN — missing OnOpen, OnClose, Disabled overlay callbacks | OK — no findings | OK — page exists, 4 demos, API ref present, indexed | OK — all 3 files declared |
| Icon | OK — all checks pass | OK — all class-required params present | OK — no findings | WARN — page exists, API ref present, not indexed in ComponentsIndex | OK — 1 of 1 files; Blazicons dep not declared (structural gap) |
| Image | OK — all checks pass | OK — all class-required params present | OK — no findings | WARN — page exists, 3 demos, API ref present, not indexed | OK — 2 of 2 files declared |
| ImageCompare | **FAIL** — raw color literals (rgba, #555, white) in handle/label inline styles | OK — relevant params present for display component | OK — no findings | WARN — page exists, 4 demos, API ref present, not indexed | OK — 1 of 1 files declared |
| InplaceEditor | WARN — inline SVG icons not using Blazicons | **FAIL** — missing Required, Invalid, ErrorText, HelperText, Label, Name (6+ absent) | OK — no findings | WARN — page exists, 5 demos, API ref present, not indexed | OK — 1 of 1 files declared |
| Input | WARN — AdditionalAttributes not on wrapper div in prefix/suffix branch | WARN — missing Required, Invalid, ErrorText, HelperText, Label, Name | OK — no findings | OK — page exists, 8 demos, API ref present, indexed | OK — 1 of 1 files declared |
| InputMask | OK — all checks pass | WARN — missing Required, Invalid, ErrorText, HelperText, Label, Name | OK — no findings | WARN — page exists, 5 demos, API ref present, not indexed | OK — 1 of 1 files declared |
| Kanban | WARN — KanbanColumn inline SVG (plus icon) should use Blazicons | OK — judgement: reasonable API for kanban | OK — no findings | WARN — page exists, 3 demos, API ref present, not indexed | OK — 3 of 3 files declared |
| Kbd | OK — all checks pass | OK — Size present, ChildContent present | OK — no findings | WARN — page exists, 4 demos, API ref present, not indexed | OK — 1 of 1 files declared |
| KpiCard | OK — all checks pass | OK — Label, Value, Delta, IconContent, SparkContent present | OK — no findings | **FAIL** — KpiCardPage.razor MISSING | WARN — Delta component dep missing from registry dependencies |
| Label | OK — all checks pass | OK — ChildContent, For, Class present | OK — no findings | OK — page exists, 3 demos, API ref present, indexed | OK — 1 of 1 files declared |
| Link | OK — all checks pass | OK — Href, Variant, External, Size, ChildContent present | OK — no findings | WARN — page exists, 4 demos, API ref present, not indexed | OK — 1 of 1 files declared |
| List | OK — all checks pass | OK — ChildContent, Size present; ListItem fully featured | OK — no findings | WARN — page exists, 5 demos, API ref present, not indexed | OK — 2 of 2 files declared |
| Marquee | OK — all checks pass | OK — Speed, Direction, PauseOnHover, Reverse, Vertical present | OK — no findings | **FAIL** — MarqueePage.razor MISSING | OK — 1 of 1 files declared |
| MegaMenu | WARN — MegaMenuItem discarded Task in HandleMouseEnter (no await on InvokeAsync) | WARN — missing OnOpen, OnClose overlay callbacks; Open/OpenChanged managed via context | WARN — discarded Task in HandleMouseEnter (unobserved exception risk) | WARN — page exists, 2 demos, API ref present, not indexed | OK — 5 of 5 files declared |
| Mention | OK — all checks pass | WARN — missing Required, Invalid, ErrorText, HelperText, Label, Name | WARN — Task.Delay(150) timing hack in HandleBlur; caretInfo properties accessed without null check | WARN — page exists, 2 demos, API ref present, not indexed | OK — 1 of 1 files declared |
| Menubar | OK — all contract checks pass | OK — nav composite; Disabled on items, OnClick on items | OK — no findings | OK — 4 demos, API ref present, indexed | OK — 8/8 files, no missing deps |
| NavigationMenu | OK — all files pass; Content uses IAsyncDisposable+JSDisconnect | OK — full nav composite; sheet dep correct | WARN — `_ = InvokeAsync(...)` discarded Task in timer callback (low risk) | OK — 3 demos, API ref present, indexed | OK — 10/10 files, sheet dep declared |
| NumberInput | OK — all contract checks pass | WARN — missing Required, Invalid, ErrorText, HelperText, Label, Name | OK — no findings | OK — 4 demos, API ref present; not in ComponentsIndex | OK — 1/1 files |
| NumberTicker | OK — all contract checks pass; IAsyncDisposable, JSDisconnect caught | WARN — Size/Variant N/A for purpose; no gap for this component class | OK — interop in firstRender guard | **FAIL** — page MISSING | OK — 1/1 files |
| OtpInput | OK — all contract checks pass; IAsyncDisposable, JSDisconnect caught | WARN — missing Required, Invalid, ErrorText, HelperText, Label, Name | OK — interop in firstRender guard | OK — 4 demos, API ref present, indexed | OK — 1/1 files; `list` dep questionable |
| Overlay | WARN — no Class/AdditionalAttributes (fragment root, N/A); IAsyncDisposable present; events unsubscribed | OK — programmatic host; API via OverlayService | WARN — `_ = InvokeAsync(...)` × 2, Tasks discarded; exceptions silently swallowed | **FAIL** — page MISSING | OK — 1/1 files, all component deps declared |
| Pagination | OK — all contract checks pass | OK — nav utility; IsActive, Disabled, OnClick all present | OK — no findings | OK — 3 demos, API ref present, indexed | OK — 6/6 files |
| PasswordInput | OK — @attributes on inner input (correct for pass-through); Blazicon icons | WARN — missing Required, Invalid, ErrorText, HelperText, Label, Name, ReadOnly | OK — no findings | OK — 5 demos, API ref present; not in ComponentsIndex | OK — 1/1 files |
| PickList | OK — all contract checks pass | WARN — missing Disabled, Required, Invalid, ErrorText, HelperText, Label, Name | OK — no findings | OK — 2 demos, API ref present; not in ComponentsIndex | OK — 1/1 files |
| PopConfirm | WARN — root is `<Popover>` lacking AdditionalAttributes; missing Open/OpenChanged/OnOpen/OnClose/Disabled | WARN — missing Open+OpenChanged, OnOpen, OnClose, Disabled | OK — no findings | OK — 4 demos, API ref present; not in ComponentsIndex | OK — 1/1 files, button+popover deps correct |
| Popover | WARN — Popover.razor missing Class parameter | WARN — missing OnOpen, OnClose, Disabled; Class absent on root | OK — no findings | OK — 5 demos, API ref present, indexed | OK — 3/3 files |
| Progress | WARN — rgba() color literals in inline <style> stripe keyframe | OK — Value, Max, Variant, Size, Shape all present; rich parameter set | OK — no findings | OK — 9 demos, API ref present, indexed | OK — 3/3 files |
| PromptInput | OK — all contract checks pass; IAsyncDisposable present | OK — Value+ValueChanged, Placeholder, IsLoading, OnSend, content slots | OK — interop in firstRender guard; DisposeAsync is no-op stub | **FAIL** — page MISSING | OK — 1/1 files, icon+spinner deps correct |
| QRCode | OK — currentColor/transparent are SVG attrs not CSS class literals; acceptable | OK — Value, Size, ErrorCorrectionLevel, image overlay params | OK — bare catch swallows QR errors; no lifecycle issues | **FAIL** — page MISSING | OK — 2/2 files (razor+encoder) |
| RadioGroup | OK — all contract checks pass | WARN — missing root Disabled, Required, Invalid, ErrorText, HelperText, Label, Name; RadioGroupCard lacks Disabled | OK — no findings | OK — 3 demos, API ref present, indexed | OK — 3/3 files |
| Rating | OK — all contract checks pass | WARN — missing Disabled (uses ReadOnly instead), Required, Invalid, ErrorText, HelperText, Label, Name | OK — no findings | OK — 5 demos, API ref present; not in ComponentsIndex | OK — 1/1 files |
| ReasoningDisplay | WARN — uses Icon component (not Blazicons.Lucide); no doc page | WARN — Display: Size/Variant absent | OK — no findings | **FAIL** — page missing, not in index | WARN — no Blazicons.Lucide pkg dep |
| Resizable | OK — all contracts pass, JSDisconnect caught | OK — Container params complete | OK — no findings | OK — 3 demos, API ref, indexed | WARN — no pkg dep (no Blazicons needed; structural gap noted) |
| Result | OK — all contracts pass | WARN — Display: Size/Variant absent | OK — no findings | WARN — page exists, 3 demos, not indexed in ComponentsIndex | WARN — no Blazicons.Lucide pkg dep |
| RichTextEditor | WARN — JSDisconnectedException absent in DisposeAsync | WARN — missing Required/Invalid/ErrorText/HelperText/Label/Name | WARN — DisposeAsync lacks JSDisconnect guard | OK — 4 demos, API ref, not indexed | WARN — file listed; no Blazicons pkg dep |
| Scheduler | OK — IAsyncDisposable, JSDisconnect caught, correct interop | OK — comprehensive event API | WARN — null InstanceId risk post-init mitigated by try/catch | OK — 4 demos, API ref, not indexed | WARN — no Blazicons.Lucide pkg dep |
| ScrollArea | OK — all contracts pass | WARN — no Size param (minor; scroll wrapper) | OK — no findings | OK — 3 demos, API ref, indexed | OK — 1 of 1 file, no deps needed |
| Scrollspy | OK — all contracts pass, JSDisconnect caught | OK — ActiveId/Changed, Offset, Smooth present | OK — no findings | OK — 2 demos, API ref, not indexed | OK — 3 of 3 files, no deps needed |
| Segmented | OK — all contracts pass | OK — Value/Changed, Options, Block present | OK — no findings | OK — 3 demos, API ref, not indexed | OK — 1 of 1 file, no deps needed |
| Select | WARN — Select.razor root lacks Class param; OnOpen/OnClose missing | WARN — missing Required/Invalid/ErrorText/HelperText/Label/Name; OnOpen/OnClose absent | OK — _registered flag pattern safe | OK — 8 demos, API ref, indexed | WARN — `list` dep questionable; no Blazicons pkg dep |
| Separator | OK — all contracts pass | OK — Orientation, ChildContent sufficient for primitive | OK — no findings | OK — 4 demos, API ref, indexed | OK — 1 of 1 file, no deps needed |
| Sheet | OK — all contracts pass, JSDisconnect caught, focus trap managed | WARN — missing OnOpen/OnClose/Disabled on root Sheet | OK — no findings | OK — 5 demos, API ref, indexed | OK — 8 of 8 files; no Blazicons pkg dep (structural gap) |
| ShimmerButton | WARN — no IAsyncDisposable; ripple listener not cleaned up | OK — Disabled/Size/Variant/OnClick present | WARN — ripple JS listener leaks on unmount; no DisposeAsync | **FAIL** — page missing, not in index | WARN — missing `button` dep (uses Button enum types) |
| Sidebar | OK — all 12 files compliant, Blazicons used correctly | OK — IsCollapsed/Changed, Variant, IsActive, Href present | OK — no findings | OK — 3 demos, API ref, indexed | OK — 12 of 12 files; no Blazicons pkg dep (structural gap) |
| Skeleton | WARN — inline <style> keyframes block (acceptable but unconventional) | OK — Animation enum, composites cover use cases | OK — no findings | OK — 4 demos, API ref, indexed | OK — 4 of 4 files, no deps needed |
| Slider | OK — all contracts pass | WARN — missing Required/Invalid/ErrorText/HelperText/Label/Name/ReadOnly | OK — no findings | OK — 10 demos, API ref, indexed | OK — 1 of 1 file, no deps needed |
| Sortable | WARN — inline <svg> grip icon instead of Blazicons | OK — Items/Changed, ItemTemplate, Handle, Disabled, Group | OK — no findings | WARN — page as SortableListPage (name mismatch); not in ComponentsIndex | WARN — `list` dep declared but unused |
| Spacer | OK — all contract checks pass | OK — utility component, Size param appropriate | OK — no findings | OK — page exists, 2 demos, API ref present; not in index | OK — 1/1 files |
| SparkCard | OK — all contract checks pass | WARN — Size/Variant absent (display class) | OK — no findings | **FAIL** — page MISSING | OK — 1/1 files |
| Sparkles | WARN — Color param accepts raw CSS from consumer but no literals baked in source | OK — Other class, params appropriate | OK — no findings | **FAIL** — page MISSING | OK — 1/1 files |
| Sparkline | OK — all contract checks pass | OK — Height/Type serve as Size/Variant | OK — no findings | OK — page exists, 6 demos, API ref present; not in index | OK — 1/1 files |
| SpeedDial | OK — IAsyncDisposable + JSDisconnectedException + Interop service all present | WARN — Disabled/Size/OnClick absent on root (Trigger class) | OK — no findings | OK — page exists, 3 demos, API ref present | WARN — Blazicons.Lucide pkg dep not emitted (structural gap) |
| Spinner | OK — all contract checks pass | OK — Size and Variant present | OK — no findings | OK — page exists, 3 demos, API ref present, indexed | OK — 1/1 files |
| Splitter | OK — all 3 files pass; uses ComponentInteropService; IDisposable on sub-components | OK — ChildContent and orientation params present | OK — no findings | OK — page exists, 4 demos, API ref present | OK — 3/3 files |
| Stack | OK — all contract checks pass | OK — Direction/Gap/Align/Justify/Wrap present | OK — no findings | OK — page exists, 4 demos, API ref present; not in index | OK — 1/1 files |
| Statistic | OK — all contract checks pass; uses Blazicon for trend icons | WARN — Size/Variant absent (display class) | OK — no findings | OK — page exists, 2 demos, API ref present | WARN — Blazicons.Lucide pkg dep not emitted |
| Steps | OK — both files pass; uses Blazicon for check/x icons | OK — CurrentStep/CurrentStepChanged + Clickable + Orientation present | OK — no findings | OK — page exists, 8 demos, API ref present | WARN — Blazicons.Lucide pkg dep not emitted |
| StreamingText | WARN — dark:prose-invert in Prose mode (StreamingText.razor:30) | OK — Text/IsStreaming/Prose present | OK — no findings | **FAIL** — page MISSING | OK — 1/1 files |
| Switch | OK — all contract checks pass; uses Spinner component | **FAIL** — Missing Required/Invalid/ErrorText/HelperText/Name (5 of 8 form-input params absent) | OK — no findings | OK — page exists, 12 demos, API ref present, indexed | OK — 1/1 files, spinner dep declared |
| Table | OK — all 7 sub-component files pass | OK — ChildContent on all sub-components; matches shadcn Table | OK — no findings | OK — page exists, 8 demos, API ref present, indexed | OK — 7/7 files |
| Tabs | OK — TabsList IAsyncDisposable + Interop; uses Blazicon X for closable | OK — ActiveValue/ActiveValueChanged + Orientation + Variant present | OK — OnAfterRenderAsync re-measure pattern intentional | OK — page exists, 10 demos, API ref present, indexed | WARN — Blazicons.Lucide pkg dep not emitted |
| TagInput | OK — IAsyncDisposable + JSDisconnectedException + Interop service | **FAIL** — Missing Required/Invalid/ErrorText/HelperText/Label/Name (6 of 8 form-input params) | WARN — OnAfterRenderAsync runs interop outside firstRender on every cycle when suggestions shown | OK — page exists, 6 demos, API ref present | WARN — Blazicons.Lucide pkg dep not emitted |
| Text | OK — all contract checks pass | OK — Size/Weight/Color/As/Leading/Truncate/Align present | OK — no findings | OK — page exists, 10 demos, API ref present | OK — 1/1 files |
| TextReveal | OK — all contract checks pass | OK — animation utility, params sufficient | OK — no findings | **FAIL** — page missing, not in ComponentsIndex | OK — registry entry present, 1/1 files |
| Textarea | OK — all contract checks pass | WARN — missing Required/Invalid/ErrorText/HelperText/Label/Name (6) | OK — no findings | OK — 6 demos, API ref present, indexed | OK — registry entry present, 1/1 files |
| ThemeSwitcher | WARN — PreviewColor inline style may emit raw color at runtime | OK — utility component, minimal params sufficient | WARN — subscription/disposal OK; InvokeAsync pattern used | OK — 3 demos, API ref present, indexed | OK — registry entry present, 1/1 files |
| ThemeToggle | WARN — missing Class parameter | OK — utility toggle, functional | OK — no findings | OK — 3 demos, API ref present, indexed | OK — registry entry present, 1/1 files |
| TimePicker | WARN — no IAsyncDisposable/JSDisconnectedException (delegates to Popover) | WARN — missing Required/Invalid/ErrorText/HelperText/Label/Name; Open not bindable | OK — no findings | WARN — page exists, 4 demos, API ref, but not in ComponentsIndex | OK — registry entry present, 1/1 files, popover dep listed |
| Timeline | OK — all checks pass on both files | OK — all relevant params present | OK — no findings | WARN — page exists, 4 demos, API ref, but not in ComponentsIndex | OK — registry entry present, 2/2 files |
| Toast | WARN — discarded-task pattern in 6 places (event dispatch); no JSDisconnectedException needed | WARN — ToastProvider lacks Open/OpenChanged/Disabled (service-driven by design) | WARN — Task.Delay without cancel token in exit animation path | OK — 8 demos, API ref present, indexed | OK — registry entry present, 7/7 files |
| Toggle | OK — all checks pass | OK — Disabled/Size/Variant/Pressed+PressedChanged present | OK — no findings | OK — 5 demos, API ref present, indexed | OK — registry entry present, 1/1 files |
| ToggleGroup | OK — all checks pass; JSDisconnectedException caught in Item | OK — all trigger params present | OK — no findings | OK — 6 demos, API ref present, indexed | OK — registry entry present, 2/2 files |
| ToolCallCard | WARN — uses bg-emerald-500/text-emerald-500 (hardcoded Tailwind color, not CSS var) | OK — display params appropriate | OK — no findings | **FAIL** — page missing, not in ComponentsIndex | OK — registry entry present, 1/1 files, icon+spinner deps listed |
| Tooltip | WARN — IDisposable (not IAsyncDisposable); no JS interop so JSDisconnectedException N/A; TooltipTrigger missing Class | WARN — missing Open/OpenChanged/Disabled/OnOpen/OnClose | OK — DelayedDispatch properly disposed | OK — 4 demos, API ref present, indexed | OK — registry entry present, 3/3 files |
| Tour | WARN — rgba(0,0,0,0.5) raw color in SVG overlay fill | WARN — uses IsOpen/IsOpenChanged naming; missing Disabled/OnOpen | WARN — LockScroll called every render (no firstRender guard) | WARN — page exists, 2 demos, API ref, but not in ComponentsIndex | OK — registry entry present, 1/1 files |
| Transfer | OK — all checks pass | OK — SourceItems/TargetItems pairs + OnChange present | OK — no findings | WARN — page exists, 2 demos, API ref, but not in ComponentsIndex | WARN — list dep listed but no List component used in source |
| TreeSelect | OK — IAsyncDisposable, JSDisconnectedException, ComponentInteropService all OK | WARN — missing Required/Invalid/ErrorText/HelperText/Label/Name; no Open param | OK — no findings | WARN — page exists, 5 demos, API ref, but not in ComponentsIndex | WARN — list dep listed but no List component used in source |
| TreeView | WARN — TreeViewNode missing Class and AdditionalAttributes | OK — display params appropriate | OK — no findings | WARN — page exists, 3 demos, API ref, but not in ComponentsIndex | OK — registry entry present, 2/2 files, checkbox dep correct |
| Watermark | OK — all checks pass; fill="currentColor" correct | OK — display params appropriate | OK — no findings | WARN — page exists, 2 demos, API ref, but not in ComponentsIndex | OK — registry entry present, 1/1 files |

---

## Suggested next sprints (decision input — not a recommendation to execute)

1. **Sprint A — CLI/Registry repair (1 PR, ~1 day).** Make `RegistryGen` emit `.js` and `.css` files; add `packageDependencies` field with `Blazicons.Lucide` when component uses Blazicons. Unblocks every consumer using `lumeo add`.
2. **Sprint B — Form-input parity (1 PR, ~2 days).** Add `Required`/`Invalid`/`ErrorText`/`HelperText`/`Label`/`Name` to all 26 form-input components — or formalize a `<FormField>` wrapper as the official path and document it. Pick one and apply consistently.
3. **Sprint C — Doc index gap (1 PR, ~half day).** Add the ~40 missing `ComponentsIndex.razor` entries; create the 15 missing doc pages from a shared template.
4. **Sprint D — Top 20 individual fixes.** Address Chart, ConsentBanner, ImageCompare contract FAILs and the 15 WARN-level bug findings (ShimmerButton leak, MegaMenu/Mention/Tour discarded tasks, etc.).

Each sprint maps to a future `superpowers:writing-plans` prompt with the relevant per-component files attached.
