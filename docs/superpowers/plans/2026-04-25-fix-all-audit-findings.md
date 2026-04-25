# Fix-All Plan — 2026-04-25 Audit Findings

**Driver:** Resolve every actionable finding from `docs/superpowers/audits/2026-04-25-component-audit.md`.
**Mode:** Parallel agent execution; lead (this session) consolidates and verifies.
**Constraint:** No new architectural work. Use existing patterns (`Form/FormField.razor`, `ComponentInteropService`, theme tokens).

## Architectural decisions made up-front

1. **Form-input validation pattern.** `Form/FormField.razor` already exists and provides `Label`, `HelperText`, `ErrorText`, `Required`, `Invalid` via `<FormField>` wrapper + `<EditForm>` validation. **We document this as the official pattern.** We do NOT add Required/Invalid/ErrorText/HelperText/Label/Name to each of the 26 form inputs. This collapses ~50 of the audit WARNs and the 3 API FAILs (Switch, InplaceEditor, TagInput) into "OK by design".
2. **Per-component assets.** Lumeo's distribution model ships `lumeo.css` + `theme.js` + `components.js` from `_content/Lumeo/`. Per-component `.js`/`.css` files are not a thing. RegistryGen does NOT need to emit them.
3. **Overlay naming drift (`IsOpen` vs `Open`).** Out of scope — would be a breaking change. Logged for v3.

## Workstreams (dispatched in parallel)

### Sprint A — Registry / CLI parity (1 agent)
- Add `packageDependencies` field to RegistryGen output.
- Scan each component for `<Blazicon` → emit `Blazicons.Lucide` package.
- Scan each component for known wwwroot JS module references (echarts-interop, gantt, rich-text-editor, scheduler) → emit any companion package deps if applicable.
- Run RegistryGen; verify `registry.json` has packageDependencies for affected components.

### Sprint B — Document FormField pattern (1 agent)
- Update `docs/Lumeo.Docs/Pages/Components/FormPage.razor` with a "Validation pattern" section showing `<FormField>` wrapping common inputs (Input, Select, Checkbox, Switch).
- Add a top-of-page note to ~5 representative form-input pages (InputPage, SelectPage, CheckboxPage, SwitchPage, TextareaPage) pointing to the FormField pattern.
- Do NOT modify any form input source.

### Sprint C — Doc index + missing pages (2 agents)
- **Agent C1 — `ComponentsIndex.razor`:** add the ~40 missing entries (list in audit's structural-findings §5). Categorize correctly per RegistryGen's `categoryMap`. Single file edit.
- **Agent C2 — Missing doc pages (15):** create `Pages/Components/{X}Page.razor` for: Code, Delta, KpiCard, Marquee, NumberTicker, Overlay, PromptInput, QRCode, ReasoningDisplay, ShimmerButton, SparkCard, Sparkles, StreamingText, TextReveal, ToolCallCard. Use `AccordionPage.razor` as a structural template — at minimum: header, 1-3 ComponentDemo blocks, API Reference table.

### Sprint D — Source-code fixes (4 agents in parallel)

Top fixes only, per the audit's Top 20 minus the FormField-collapsed entries:

**Agent D1 — Contract FAILs (3 components):**
- `Chart.razor`: replace direct `[Inject] IJSRuntime` with `[Inject] ComponentInteropService Interop`. Update all `JSRuntime.InvokeAsync(...)` call-sites to route through `Interop`.
- `ConsentBanner.razor`: add `[Parameter] string? Class`, `[Parameter(CaptureUnmatchedValues = true)] Dictionary<string, object>? AdditionalAttributes`, and `@attributes="AdditionalAttributes"` on the root element. Combine `Class` into the existing class string.
- `ImageCompare.razor`: replace raw color literals (`rgba(...)`, `#555`, `white`) in handle/label inline styles with theme tokens. Use `bg-background`, `text-foreground`, `border-border` etc. — adjust as appropriate for visual fidelity.

**Agent D2 — Theme-token cleanup (5 components):**
- `Delta.razor`: replace hardcoded `emerald`/`rose` Tailwind classes with theme tokens (`text-positive`, `text-destructive`, or define new positive token if missing — check `lumeo.css`).
- `ToolCallCard.razor`: replace `bg-emerald-500` / `text-emerald-500` with theme tokens.
- `StreamingText.razor`: remove `dark:prose-invert` (no `dark:` prefix per CLAUDE.md). Use `prose-invert` conditionally or via CSS variable.
- `Progress.razor`: replace `rgba()` literal in stripe keyframe with a CSS variable.
- `Tour.razor`: replace `rgba(0,0,0,0.5)` SVG overlay fill with a CSS variable (`--color-overlay` or similar — define if absent).

**Agent D3 — Lifecycle/disposal bugs (5 components):**
- `ShimmerButton.razor`: implement `IAsyncDisposable`. Track the ripple JS event listener handle and remove it in `DisposeAsync`. Catch `JSDisconnectedException`.
- `Tour.razor`: gate `Interop.LockScroll()` behind `firstRender` (or add a `_locked` flag).
- `MegaMenu.razor`: replace discarded `Task` in `HandleMouseEnter` with proper `await` (or explicit fire-and-forget with a logged exception handler).
- `Toast.razor` / `ToastProvider.razor`: replace 6× `_ = Task.Delay(...)` exit-animation pattern with a `CancellationTokenSource` per toast; cancel on dispose.
- `Overlay.razor`: replace 2× `_ = InvokeAsync(...)` with `await InvokeAsync(...)` where possible; otherwise route through a logged fire-and-forget helper.

**Agent D4 — Inline-SVG → Blazicons + small fixes (4 components + 2 minor):**
- `InplaceEditor.razor`: replace inline SVG icons (edit, check, x) with `<Blazicon Svg="Lucide.Pencil" />` etc.
- `Kanban/KanbanColumn.razor`: replace inline SVG plus icon with Blazicon.
- `Sortable/Sortable.razor`: replace inline SVG grip icon with Blazicon.
- `RichTextEditor.razor`: add `JSDisconnectedException` catch in `DisposeAsync`.
- `AgentMessageList.razor`: same — add `JSDisconnectedException` catch in `DisposeAsync`.
- `Mention.razor`: add null guard for `caretInfo` before property access in `HandleBlur`.

## Verification (lead, after all 8 agents finish)

1. `dotnet run --project tools/Lumeo.RegistryGen` — regen registry.
2. `dotnet build src/Lumeo/Lumeo.csproj` — must succeed clean.
3. `dotnet build docs/Lumeo.Docs/Lumeo.Docs.csproj` — docs site must build.
4. Spot-grep: no `[Inject].*IJSRuntime` outside `Services/`. No new raw color literals.
5. Re-read 3 Contract FAILs to confirm fixes landed.
6. Single commit per workstream (4 commits total) for clean history; or one squashed commit if simpler.

## Out of scope (deferred to v3 / separate decision)

- Overlay `IsOpen` → `Open` rename (breaking change).
- Adding `Disabled`/`Size`/`Variant` to display components where the audit flagged WARN. Display components have a different parameter idiom; mass-changing would create churn without clear benefit.
- Form-input page navigation (e.g., is "FileUpload" a `<FormField>` candidate? Probably not — it's structurally different). Document consumers' choice rather than enforce.
