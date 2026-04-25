# Link

**Path:** `src/Lumeo/UI/Link/`
**Class:** Other
**Files:** Link.razor

## Contract — OK
- `@namespace Lumeo` present
- Has `Class` and `AdditionalAttributes` params
- Root `<a>` carries `@attributes="AllAttributes"` (merged dict that includes AdditionalAttributes + external attrs) — functionally correct
- No raw color literals, no `dark:` prefix
- No SVG blocks

## API — OK
- Other class: Href, Variant (string), External, Size, ChildContent present
- Reasonable API for a link primitive; no obvious gaps

## Bugs — OK
- No `async void`, no JS interop, no lifecycle methods
- Pure render component

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/LinkPage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none)
