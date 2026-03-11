using Microsoft.AspNetCore.Components;

namespace Lumeo;

public record DataGridContext<TItem>(
    IReadOnlyList<DataGridColumn<TItem>> Columns,
    IReadOnlyList<TItem> DisplayedItems,
    IReadOnlyList<TItem> SelectedItems,
    List<SortDescriptor> Sorts,
    List<FilterDescriptor> Filters,
    string? GlobalSearch,
    int CurrentPage,
    int PageSize,
    int TotalCount,
    DataGridSelectionMode SelectionMode,
    DataGridEditMode EditMode,
    bool IsLoading,
    EventCallback<TItem> OnRowClick,
    EventCallback<TItem> OnRowDoubleClick,
    EventCallback<SortDescriptor> OnSort,
    EventCallback<FilterDescriptor> OnFilter,
    EventCallback<TItem> OnSelectionChanged,
    EventCallback<CellEditEventArgs<TItem>> OnCellEdit,
    Func<TItem, bool> IsRowSelected,
    Func<TItem, bool> IsRowExpanded,
    Action<TItem> ToggleSelection,
    Action SelectAll,
    Action ClearSelection,
    Action<TItem> ToggleRowExpand
);
