# List

**Path:** `src/Lumeo/UI/List/`
**Class:** Container
**Files:** List.razor, ListItem.razor

## Contract — OK
- Both files: `@namespace Lumeo` present
- Both have `Class` and `AdditionalAttributes` params
- List.razor: root `<ul>` carries `@attributes="AdditionalAttributes"`
- ListItem.razor: `<li>` carries `@attributes="AdditionalAttributes"` on both branches
- No raw color literals, no `dark:` prefix
- No SVG blocks; icons expected as ChildContent slots

## API — OK
- Container class: ChildContent present; Size present (ListSize enum)
- ListItem has full feature set: Title, Description, Leading, Trailing, Href, OnClick, Disabled

## Bugs — OK
- No `async void`, no JS interop, no direct IJSRuntime
- No lifecycle methods

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/ListPage.razor` (exists)
- 5 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present
- Files declared: 2 of 2
- Missing from registry: none
- Component deps declared: OK (none)
