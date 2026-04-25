# TreeView

**Path:** `src/Lumeo/UI/TreeView/`
**Class:** Display
**Files:** TreeView.razor, TreeViewNode.razor

## Contract — WARN
- `TreeViewNode.razor` has no `[Parameter] public string? Class { get; set; }` and no `@attributes="AdditionalAttributes"` — it is an internal node component with no `AdditionalAttributes` parameter at all.
- `TreeView.razor` has `Class` and `AdditionalAttributes` — OK.
- No raw color literals. No `dark:` prefix.
- Uses `<Blazicon>` and `<Checkbox>` — OK.

## API — OK
- `Items`, `ShowCheckboxes`, `Expandable`, `OnItemClick`, `OnItemCheck`, `ShowSearch`, `Class`, `AdditionalAttributes` — appropriate for display component.
- `Size` and `Variant` absent — display component checklist only requires where applicable.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/TreeViewPage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no (missing from ComponentsIndex)

## CLI — OK
- Registry entry: present (`tree-view`)
- Files declared: 2 of 2
- Missing from registry: none
- Component deps declared: `checkbox` listed — correct (`<Checkbox>` used in TreeViewNode)
