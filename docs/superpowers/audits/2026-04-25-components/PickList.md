# PickList

**Path:** `src/Lumeo/UI/PickList/`
**Class:** Form input (shuttle picker)
**Files:** PickList.razor

## Contract — OK
- `@namespace Lumeo` present. Has `Class`, `AdditionalAttributes`, `@attributes` on root element.
- No raw color literals. No `dark:` prefix.
- Uses `<Blazicon>` for all four directional arrows.

## API — WARN
- Has `Items`, `SelectedItems` + `SelectedItemsChanged`, `ItemTemplate`, `ShowSearch`, `SourceTitle`, `TargetTitle`, `Height`. Good feature set.
- Missing standard form-input params: `Disabled`, `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name`.

## Bugs — OK
- No async void. No discarded Tasks. No event subscriptions without dispose.
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/PickListPage.razor` (exists)
- 2 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no (not listed)

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: none (none referenced; Blazicons used — packageDependencies gap is global)
