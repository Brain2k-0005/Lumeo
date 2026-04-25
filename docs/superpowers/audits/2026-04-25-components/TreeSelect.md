# TreeSelect

**Path:** `src/Lumeo/UI/TreeSelect/`
**Class:** Form input (Overlay)
**Files:** TreeSelect.razor

## Contract — OK
- All checks pass.
- `@namespace Lumeo`, `Class`, `AdditionalAttributes`, `@attributes` on root — present.
- Implements `IAsyncDisposable` — OK.
- `JSDisconnectedException` caught in `CloseDropdown` and `DisposeAsync` — OK.
- Uses `ComponentInteropService` (no direct IJSRuntime) — OK.
- No raw color literals. No `dark:` prefix.
- Uses `<Blazicon>` for icons — OK.

## API — WARN
- `Value` + `ValueChanged`, `Values` + `ValuesChanged`, `Disabled`, `Placeholder`, `Searchable`, `Multiple`, `Items`, `ExpandAll` all present.
- Missing form-input parameters: `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name` — 6 absent.
- Missing `Open`/`OpenChanged` (overlay exposure).

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/TreeSelectPage.razor` (exists)
- 5 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no (missing from ComponentsIndex)

## CLI — WARN
- Registry entry: present (`tree-select`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: `list` listed — no `<List>` component usage found in source. Spurious dep.
