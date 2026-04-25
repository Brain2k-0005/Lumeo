# RichTextEditor

**Path:** `src/Lumeo/UI/RichTextEditor/`
**Class:** Form input
**Files:** RichTextEditor.razor

## Contract — WARN
- `@namespace Lumeo` as first line. `Class` + `AdditionalAttributes` present.
- `@attributes="AdditionalAttributes"` on root div. No `dark:` prefix. No raw color literals.
- Overlay-adjacent (uses interop): `IAsyncDisposable` implemented.
- `JSDisconnectedException` NOT caught in `DisposeAsync` — RichTextDestroyAsync called without catch.
- Uses `Services.ComponentInteropService` (injected via `@inject`), no direct `IJSRuntime`.
- Icons via `<Icon Name="..." />` component (not Blazicons.Lucide) in toolbar builder — inconsistency.

## API — WARN
- Form input class: `Disabled`, `ReadOnly`, `Value`+`ValueChanged`, `Placeholder` present.
- Missing: `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name`, `MaxLength`.

## Bugs — WARN
- `DisposeAsync`: `await Interop.RichTextDestroyAsync(_instanceId)` has no JSDisconnectedException guard.
- `OnParametersSetAsync` calls interop outside firstRender guard — intentional (update pattern), but no disconnect guard.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/RichTextEditorPage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — WARN
- Registry entry: present (`rich-text-editor`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: `icon` — OK; no Blazicons dep (uses Icon component)
