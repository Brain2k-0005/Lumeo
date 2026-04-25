# Kbd

**Path:** `src/Lumeo/UI/Kbd/`
**Class:** Display
**Files:** Kbd.razor

## Contract — OK
- `@namespace Lumeo` present
- Has `Class` and `AdditionalAttributes` params
- Root `<kbd>` carries `@attributes="AdditionalAttributes"`
- No raw color literals, no `dark:` prefix
- No SVG blocks

## API — OK
- Display class: Size present (KbdSize enum with Sm/Default/Lg)
- Variant not applicable for Kbd; ChildContent present

## Bugs — OK
- No `async void`, no JS interop, no direct IJSRuntime
- No lifecycle methods at all — pure render component

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/KbdPage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none)
