# DataGrid

**Path:** `src/Lumeo/UI/DataGrid/`
**Class:** Other (enterprise data grid)
**Files:** DataGrid.razor, DataGridBody.razor, DataGridCell.razor, DataGridColumn.cs, DataGridColumnDef.razor, DataGridColumnFilter.razor, DataGridColumnVisibility.razor, DataGridContext.cs, DataGridDetailRow.razor, DataGridEnums.cs, DataGridFilterOperator.cs, DataGridFooter.razor, DataGridGroupRow.razor, DataGridHeader.razor, DataGridHeaderCell.razor, DataGridLayoutService.cs, DataGridPagination.razor, DataGridRow.razor, DataGridServerService.cs, DataGridState.cs, DataGridToolbar.razor, DataGridToolbarColumns.razor, DataGridToolbarContext.cs, DataGridToolbarCopySelected.razor, DataGridToolbarExport.razor, DataGridToolbarFullscreen.razor, DataGridToolbarLayouts.razor, ToolbarContent.razor

## Contract — OK
- All checks pass.
- `DataGrid.razor` uses `[Inject] private ComponentInteropService Interop` (property injection, not field, but valid).
- Implements `IAsyncDisposable`; catches `JSDisconnectedException` in `DisposeAsync`.

## API — OK
- Comprehensive: `Items`, `Columns`, `SelectionMode`, `EditMode`, `PageSize`, `ShowPagination`, `ShowToolbar`, `ServerMode`, `OnServerRequest`, `TotalCount`, `IsLoading`, `OnRowClick`, `OnRowDoubleClick`, `SelectedItems` + `SelectedItemsChanged`, `Class` all present.

## Bugs — WARN
- `_ = InvokeAsync(StateHasChanged)` used in `AddColumnDef` and `RemoveColumnDef` (DataGrid.razor:469, 483) — these are called from child component init, not from lifecycle methods. Not a clear leak but discarded task.
- `SelectedItemsChanged.InvokeAsync(...)` called without `await` in several synchronous methods (`ToggleSelection`, `SelectAll`, `ClearSelection`) — fire-and-forget pattern, minor.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/DataGridPage.razor` (exists)
- 31 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present (key `data-grid`)
- Files declared: 28 of 28
- Missing from registry: none
- Component deps declared: `button`, `checkbox`, `filter`, `heading`, `list`, `pagination`, `select`, `skeleton` declared; OK
