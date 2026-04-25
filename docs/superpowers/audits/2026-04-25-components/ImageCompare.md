# ImageCompare

**Path:** `src/Lumeo/UI/ImageCompare/`
**Class:** Display
**Files:** ImageCompare.razor

## Contract — FAIL
- `@namespace Lumeo` present
- Has `Class` and `AdditionalAttributes` params
- Root `<div>` carries `@attributes="AdditionalAttributes"`
- Raw color literals found in inline styles:
  - Line 30: `background: white; ... color: #555; box-shadow: 0 2px 8px rgba(0,0,0,0.3)`
  - Line 40: same pattern (vertical orientation handle)
  - Line 52: `background: rgba(0,0,0,0.55); color: white`
  - Line 58: `background: rgba(0,0,0,0.55); color: white`
  - These are inside inline SVG handle divs and label overlays, not `<svg>` literal paths — violates no-raw-color rule
- No `dark:` prefix

## API — OK
- Display class: Size not applicable (Width/Height params used); no Variant needed
- All relevant params present: BeforeSrc, AfterSrc, BeforeLabel, AfterLabel, InitialPosition, Width, Height, Orientation

## Bugs — OK
- No JS interop, no `async void`, no direct IJSRuntime
- Pure Blazor component with range input

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/ImageComparePage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none)
