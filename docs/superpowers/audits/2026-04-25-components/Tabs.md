# Tabs

**Path:** `src/Lumeo/UI/Tabs/`
**Class:** Other
**Files:** Tabs.razor, TabsList.razor, TabsTrigger.razor, TabsContent.razor

## Contract — OK
- All 4 files have `@namespace Lumeo`, `Class`, `AdditionalAttributes`, `@attributes="AdditionalAttributes"`.
- No raw hex/hsl. No `dark:` prefix.
- Uses `<Blazicon Svg="Lucide.X" />` in TabsTrigger for close button.
- TabsList implements `IAsyncDisposable`; uses `ComponentInteropService`; no direct `IJSRuntime`.
- `JSDisconnectedException` not caught in TabsList.DisposeAsync (returns ValueTask.CompletedTask — no JS cleanup needed, OK).

## API — OK
- For Other: `ActiveValue`/`ActiveValueChanged` two-way binding, `Orientation`, `Variant`, `AnimatedIndicator`, `ChildContent`, plus sub-components have appropriate params.

## Bugs — OK
- TabsList `OnAfterRenderAsync` calls `Interop.TabsMeasure` without `firstRender` guard — intentional: must re-measure when `ActiveValue` changes. Not a bug.
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/TabsPage.razor` (exists)
- 10 ComponentDemo blocks (via grep)
- API Reference: present
- Indexed in ComponentsIndex.razor: yes (Layout group)

## CLI — WARN
- Registry entry: present (`tabs`)
- Files declared: 4 of 4
- Missing from registry: none
- Component deps declared: missing `Blazicons.Lucide` package dep (registry-gen does not emit packageDependencies)
