# SpeedDial

**Path:** `src/Lumeo/UI/SpeedDial/`
**Class:** Trigger
**Files:** SpeedDial.razor

## Contract — OK
- `@namespace Lumeo` present.
- `Class` and `AdditionalAttributes` present with `@attributes="AdditionalAttributes"` on root.
- `IAsyncDisposable` implemented; `JSDisconnectedException` caught in DisposeAsync and Close.
- `ComponentInteropService` used via `@inject`; no direct `IJSRuntime`.
- No raw hex/hsl literals. No `dark:` prefixes.
- Uses `<Blazicon Svg="Lucide.Plus" />` for icon.

## API — WARN
- Trigger class requires `Disabled`, `Size`, `Variant`, `OnClick`.
- `Disabled` absent on root component (present on SpeedDialItem only).
- `OnClick` absent (SpeedDial does not expose a root click callback).
- `Size` absent.
- `Variant` present as string param.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/SpeedDialPage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — WARN
- Registry entry: present (`speed-dial`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: missing `Blazicons.Lucide` package dep (registry-gen does not emit packageDependencies)
