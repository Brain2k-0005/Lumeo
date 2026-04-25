# DataTable

**Path:** `src/Lumeo/UI/DataTable/`
**Class:** Other (data display table, not strictly any defined class)
**Files:** DataTable.razor, DataTableSortableHeader.razor

## Contract — OK
- All checks pass.

## API — WARN
- Missing `OnSort` callback directly on `DataTable` (exists, but paired via `SortColumnChanged`+`SortDirChanged`)
- No `PageSize` / `OnPageChanged` pagination parameters (considered optional for this component class)
- All core params present: Items, RowTemplate, HeaderTemplate, IsLoading, Selectable, SelectedItems+SelectedItemsChanged, SortColumn+SortColumnChanged, SortDir+SortDirChanged, Class, AdditionalAttributes

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/DataTablePage.razor` (exists)
- 5 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present (`data-table`)
- Files declared: 2 of 2
- Missing from registry: none
- Component deps declared: OK (checkbox listed)
