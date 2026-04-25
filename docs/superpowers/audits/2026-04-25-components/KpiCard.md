# KpiCard

**Path:** `src/Lumeo/UI/KpiCard/`
**Class:** Display
**Files:** KpiCard.razor

## Contract — OK
- `@namespace Lumeo` present
- Has `Class` and `AdditionalAttributes` params
- Root `<div>` carries `@attributes="AdditionalAttributes"`
- No raw color literals, no `dark:` prefix
- Uses `<Lumeo.Delta>` component internally (no direct SVG or Blazicons)

## API — OK
- Display class: Size not applicable (fixed card layout); Variant not applicable
- Core params present: Label, Value, Delta, DeltaFormat, DeltaPositive, IconContent, SparkContent

## Bugs — OK
- No `async void`, no JS interop, no direct IJSRuntime
- No lifecycle methods — pure render component

## Docs — FAIL
- Page: `docs/Lumeo.Docs/Pages/Components/KpiCardPage.razor` (MISSING)
- No ComponentDemo blocks
- API Reference: MISSING
- Indexed in ComponentsIndex.razor: no

## CLI — WARN
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: missing — uses `<Lumeo.Delta>` internally but "delta" not in dependencies array
