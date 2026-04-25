# Marquee

**Path:** `src/Lumeo/UI/Marquee/`
**Class:** Other
**Files:** Marquee.razor

## Contract — OK
- `@namespace Lumeo` present
- Has `Class` and `AdditionalAttributes` params
- Root `<div>` carries `@attributes="AdditionalAttributes"`
- No raw color literals, no `dark:` prefix
- No SVG blocks

## API — OK
- Other class: Speed, Direction, PauseOnHover, Reverse, Vertical, ChildContent all present
- Reasonable API for a marquee/ticker component

## Bugs — OK
- No `async void`, no JS interop, no lifecycle methods
- Pure CSS-animation component (uses CSS custom property `--lumeo-marquee-duration`)

## Docs — FAIL
- Page: `docs/Lumeo.Docs/Pages/Components/MarqueePage.razor` (MISSING)
- No ComponentDemo blocks
- API Reference: MISSING
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none)
