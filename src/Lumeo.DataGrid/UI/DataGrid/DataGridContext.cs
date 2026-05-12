using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

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
    bool RowReorderable,
    bool RowContextMenuEnabled,
    EventCallback<TItem> OnRowClick,
    EventCallback<TItem> OnRowDoubleClick,
    EventCallback<SortDescriptor> OnSort,
    EventCallback<FilterDescriptor> OnFilter,
    EventCallback<TItem> OnSelectionChanged,
    EventCallback<CellEditEventArgs<TItem>> OnCellEdit,
    Func<TItem, bool> IsRowSelected,
    Func<TItem, bool> IsRowExpanded,
    Func<TItem, bool> IsRowEditing,
    Action<TItem> ToggleSelection,
    Action SelectAll,
    Action ClearSelection,
    Action<TItem> ToggleRowExpand,
    Action<TItem> StartRowEdit,
    Action<TItem> CancelRowEdit,
    Func<TItem, Task> CommitRowEdit,
    Func<TItem, DataGridColumn<TItem>, object?> GetEditValue,
    Action<TItem, DataGridColumn<TItem>, object?> SetEditValue,
    Func<TItem, string>? RowClass,
    Func<TItem, string>? RowStyle,
    IReadOnlyDictionary<string, double> PinnedLeftOffsets,
    IReadOnlyDictionary<string, double> PinnedRightOffsets,
    IReadOnlyList<DataGridGroupSection<TItem>>? GroupedSections,
    string? GroupBy,
    bool IsGrouped,
    Func<string, bool> IsGroupExpanded,
    Action<string> ToggleGroupExpand,
    CultureInfo Culture,

    // --- Keyboard navigation / ARIA (rc.32) ---
    /// <summary>Stable element-id prefix for the grid. Cells get id
    /// <c>{GridId}-cell-{row}-{col}</c> (row = "h" for the header row).</summary>
    string GridId,
    /// <summary>Currently roving-tabindex row. -1 = header row, 0..RowCount-1 = body rows.</summary>
    int FocusedRow,
    /// <summary>Currently roving-tabindex column index (into the visible-columns list).</summary>
    int FocusedCol,
    /// <summary>Number of navigable body rows (= DisplayedItems.Count, flattened groups).</summary>
    int RowCount,
    /// <summary>Number of navigable columns (= visible data columns).</summary>
    int ColCount,
    /// <summary>Invoked by a body cell on keydown. Args: (rowIndex, colIndex, KeyboardEventArgs).</summary>
    Func<int, int, KeyboardEventArgs, Task> OnCellKeyDown,
    /// <summary>Invoked by a header cell on keydown. Args: (colIndex, KeyboardEventArgs).</summary>
    Func<int, KeyboardEventArgs, Task> OnHeaderKeyDown
);
