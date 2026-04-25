# Label

**Path:** `src/Lumeo/UI/Label/`
**Class:** Other
**Files:** Label.razor

## Contract — OK
- `@namespace Lumeo` present
- Has `Class` and `AdditionalAttributes` params
- Root `<label>` carries `@attributes="AdditionalAttributes"`
- No raw color literals, no `dark:` prefix
- No SVG blocks

## API — OK
- Other class (form label utility): ChildContent, For, Class all present
- Reasonable API for a label primitive; no required params missing

## Bugs — OK
- No `async void`, no JS interop, no lifecycle methods
- Pure render component

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/LabelPage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none)
