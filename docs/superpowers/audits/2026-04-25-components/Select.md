# Select

**Path:** `src/Lumeo/UI/Select/`
**Class:** Form input (Overlay)
**Files:** Select.razor, SelectContent.razor, SelectGroup.razor, SelectItem.razor, SelectLabel.razor, SelectTrigger.razor

## Contract — WARN
- All files have `@namespace Lumeo` as first line.
- Select.razor root element is `<CascadingValue>` — `@attributes` on inner `<div>` only; `Class` parameter absent from Select.razor (root context component).
- SelectContent, SelectGroup, SelectItem, SelectLabel, SelectTrigger all have `Class` + `AdditionalAttributes`.
- `IAsyncDisposable` in SelectContent.razor — present.
- `JSDisconnectedException` caught in SelectContent.Cleanup — present.
- Uses `ComponentInteropService` in SelectContent — correct, no direct `IJSRuntime`.
- No raw color literals. No `dark:` prefix.
- Icons via `<Blazicon Svg="Lucide.*" />` in SelectTrigger and SelectItem.

## API — WARN
- Overlay class: `IsOpen`+`IsOpenChanged` present (naming differs from `Open`/`OpenChanged` convention).
- Missing: `OnOpen`, `OnClose` callbacks.
- Form input class: `Value`+`ValueChanged` present; `Disabled` present.
- Missing: `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name`.

## Bugs — OK
- JS interop in SelectContent.`OnAfterRenderAsync` not strictly guarded by `firstRender` — uses `_registered` flag instead. Pattern is safe.
- No async void, no event leaks.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/SelectPage.razor` (exists)
- 8 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — WARN
- Registry entry: present (`select`)
- Files declared: 6 of 6
- Missing from registry: none
- Component deps declared: `list`, `spinner` — uses `<Spinner>` in SelectContent; `list` dep is ambiguous (no `<List>` used directly). Missing: no `Blazicons.Lucide` packageDependency (structural gap).
