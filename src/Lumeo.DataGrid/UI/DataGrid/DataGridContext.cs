using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Lumeo;

public record DataGridContext<TItem>(
    IReadOnlyList<DataGridColumn<TItem>> Columns,
    /// <summary>
    /// Pre-filtered view of <see cref="Columns"/> containing only columns with
    /// <c>Visible == true</c>. Cached at the grid level (one list per render),
    /// so descendants (Row, Cell, Header, Footer, Body) can iterate without
    /// running <c>Columns.Where(c =&gt; c.Visible)</c> per render. At 1k rows ×
    /// 10 cols, this saves the per-render allocation/iteration of the filter.
    /// </summary>
    IReadOnlyList<DataGridColumn<TItem>> VisibleColumns,
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
    /// <summary>True when the unified pointer-based (mouse/touch/pen) row-reorder
    /// engine is actually wired up for this render — <see cref="RowReorderable"/>
    /// AND the grid is in a flat, non-virtualized layout (not grouped, not a tree
    /// grid, not <c>Virtualize</c>-backed). Grouped/tree-grid indices restart per
    /// section and virtualized rows aren't all present in the DOM to live-shift,
    /// so those modes keep the drag handle visible but inert rather than wiring
    /// the JS pointer listener — see DataGrid.RowReorderPointerActive.</summary>
    bool RowReorderPointerActive,
    /// <summary>Stable per-slot token for the row currently at the given index in
    /// <see cref="DisplayedItems"/> — the value-type half of the row-reorder DOM
    /// key contract (see <c>DataGridRowKeys.DomKeyFor</c> / <c>DataGrid.MoveRow</c>).
    /// Only meaningful (and only ever read) when <see cref="RowReorderPointerActive"/>
    /// is true.</summary>
    Func<int, long> RowTokenAt,
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

    // --- Multi-level grouping + drag-to-group panel (rc.35) ---
    /// <summary>Ordered list of fields the grid is currently grouped by (1 entry =
    /// classic single-level grouping, &gt;1 = nested tree). Empty when not grouped.</summary>
    IReadOnlyList<string> GroupFields,
    /// <summary>Multi-level group tree. Non-null only when <see cref="GroupFields"/>
    /// has more than one entry; single-level grouping still uses <see cref="GroupedSections"/>.</summary>
    IReadOnlyList<DataGridGroupNode<TItem>>? GroupTree,
    /// <summary>Tests whether a group node at the given "/"-joined path is expanded.</summary>
    Func<string, bool> IsGroupPathExpanded,
    /// <summary>Toggles the expand state of the group node at the given path.</summary>
    Action<string> ToggleGroupPath,
    /// <summary>Adds a field as a new (deepest) grouping level — called from the group panel.</summary>
    Action<string> AddGroupField,
    /// <summary>Removes a field from the grouping levels.</summary>
    Action<string> RemoveGroupField,
    /// <summary>When true, the grid renders the group-panel strip and Groupable
    /// column headers are drag-source-eligible (drag-to-group target is the panel
    /// itself). Header cells read this to decide whether to set draggable=true
    /// even for columns that aren't Reorderable. (rc.41)</summary>
    bool ShowGroupPanel,

    // --- Tree-grid mode (rc.35) ---
    /// <summary>When non-null, the grid is in tree-grid mode: this returns a row's
    /// child rows (or null/empty for leaves).</summary>
    Func<TItem, IEnumerable<TItem>?>? ChildItemsSelector,
    /// <summary>True when the grid is rendering hierarchical (tree-grid) rows.</summary>
    bool IsTreeGrid,
    /// <summary>Field of the column that carries the tree chevron + indentation
    /// (null = first visible column).</summary>
    string? TreeColumnField,
    /// <summary>Tests whether a tree node is expanded.</summary>
    Func<TItem, bool> IsTreeNodeExpanded,
    /// <summary>Toggles a tree node's expand state.</summary>
    Action<TItem> ToggleTreeNode,
    /// <summary>Per-row tree depth (0 = root). Only meaningful in tree-grid mode.</summary>
    Func<TItem, int> TreeLevelOf,
    /// <summary>True when a row has child rows (used to show the chevron).</summary>
    Func<TItem, bool> TreeHasChildren,

    // --- Batch / buffered edit mode (rc.35) ---
    /// <summary>True when <see cref="EditMode"/> is <see cref="DataGridEditMode.Batch"/>.</summary>
    bool IsBatchEdit,
    /// <summary>Tests whether a (row, field) pair currently has a buffered edit.</summary>
    Func<TItem, string, bool> IsCellDirty,
    /// <summary>Returns the buffered value for (row, field) if dirty, else the row's current value.</summary>
    Func<TItem, DataGridColumn<TItem>, object?> GetBatchValue,
    /// <summary>Writes a buffered value for (row, column) into the pending-changes buffer.</summary>
    Action<TItem, DataGridColumn<TItem>, object?> SetBatchValue,

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
    Func<int, KeyboardEventArgs, Task> OnHeaderKeyDown,

    // --- Appearance flags (needed by sub-components for conditional styling) ---
    /// <summary>True when the grid has <c>Striped="true"</c>. Used by pinned cells to
    /// apply the correct alternating background instead of a flat <c>bg-card</c>.</summary>
    bool Striped,
    /// <summary>True when at least one column is pinned to the left. Used by leading
    /// non-data cells (selection checkbox, drag handle) to make them sticky too so
    /// they don't scroll behind pinned data columns.</summary>
    bool HasPinnedLeft,
    /// <summary>True when the grid has <c>Hoverable="true"</c>. Used by pinned cells
    /// to apply a group-hover tint that matches the row hover highlight.</summary>
    bool Hoverable,

    // --- Column groups (parent header that spans multiple data columns) ---
    /// <summary>The registered <c>DataGridColumnGroup</c>s. <see cref="DataGridHeader{TItem}"/>
    /// reads this to render a second <c>tr</c> above the data-column row with one <c>th</c>
    /// per group spanning its members via colspan. Empty when no groups are declared.</summary>
    IReadOnlyList<DataGridColumnGroupInfo> ColumnGroups,

    // --- Density (rc.42) ---
    /// <summary>True when the grid has <c>Compact="true"</c>. Descendant header/data/structural
    /// cells read this (via <see cref="CellPaddingClass"/> / <see cref="HeaderPaddingClass"/>) to
    /// render tighter padding so the effective row height genuinely shrinks — not just the font.
    /// Rebuilt into the cascading context on every grid render, so toggling <c>Compact</c> after
    /// first render flows straight through to the cells.</summary>
    bool Compact
)
{
    /// <summary>Padding utilities for a body data cell, tightened under <see cref="Compact"/>.
    /// Kept here (rather than duplicated per cell) so the compact/normal values can't drift
    /// between <see cref="DataGridCell{TItem}"/> and the row's structural cells.</summary>
    internal string CellPaddingClass => Compact ? "px-2 py-1" : "px-3 py-2";

    /// <summary>Padding utilities for a header cell, tightened under <see cref="Compact"/>.</summary>
    internal string HeaderPaddingClass => Compact ? "px-2 py-1" : "px-3 py-2";

    /// <summary>Vertical padding utility only — used by the fixed-width structural cells
    /// (drag handle, selection checkbox, detail chevron) whose horizontal padding differs
    /// from a data cell but whose height must still collapse under <see cref="Compact"/>.</summary>
    internal string CellPaddingY => Compact ? "py-1" : "py-2";

    /// <summary>Number of header <c>&lt;tr&gt;</c> rows above the body, i.e. the 1-based
    /// aria-rowindex the FIRST body row must report. Normally 1 header row (the data-column
    /// row), so body rows start at 2; with <see cref="ColumnGroups"/> declared the header is a
    /// 2-row block (parent labels + data-column row), so body rows must start at 3. Kept here
    /// (rather than duplicated per row type) so <see cref="DataGridRow{TItem}"/> and
    /// <see cref="DataGridGroupRow{TItem}"/> can't drift on this number and report a body
    /// row's aria-rowindex that collides with the header's.</summary>
    internal int HeaderRowOffset => ColumnGroups.Count > 0 ? 3 : 2;

    /// <summary>Total number of <c>role="row"</c> <c>&lt;tr&gt;</c> elements
    /// <see cref="DataGridBody{TItem}"/> will render in its tbody for the CURRENT state —
    /// group header rows, item rows, and item rows whose detail row is currently expanded,
    /// alike. Mirrors DataGridBody's own branch selection (multi-level grouped / single-level
    /// grouped / flat-or-tree-grid) so <c>DataGrid</c>'s <c>aria-rowcount</c> (header rows +
    /// this) can never silently drift from what <see cref="DataGridRowIndexer"/> actually
    /// hands out as <c>aria-rowindex</c> — the exact gap the PR 365 review caught for group
    /// headers (aria-rowcount stayed at <c>DisplayedItems.Count + 1</c>, ignoring every
    /// rendered group row and the second header row <see cref="ColumnGroups"/> adds).
    /// <paramref name="hasDetailTemplate"/> is passed in rather than read off Context because
    /// "a DetailTemplate is configured" is a <c>DataGridBody</c> parameter, not part of this
    /// record. <paramref name="countDetailRowsInFlatMode"/> should be false when the caller
    /// is rendering through <c>UseVirtualization</c> / the server <c>ItemsProvider</c> — see
    /// <see cref="DataGridRowIndexer"/>'s remarks for why a virtualized row's own
    /// <c>aria-rowindex</c> doesn't yet account for detail-row precession either, so counting
    /// it here would report a ceiling individual rows can never actually reach.</summary>
    internal int CountBodyRows(bool hasDetailTemplate, bool countDetailRowsInFlatMode)
    {
        if (GroupTree is { Count: > 0 } tree)
            return tree.Sum(node => CountGroupNodeRows(node, hasDetailTemplate));

        if (IsGrouped && GroupedSections is { Count: > 0 } sections)
        {
            var total = 0;
            foreach (var grp in sections)
            {
                total++; // the group header row itself
                if (!IsGroupExpanded(grp.Key)) continue;
                total += grp.Items.Count;
                if (hasDetailTemplate) total += grp.Items.Count(IsRowExpanded);
            }
            return total;
        }

        // Flat / tree-grid / virtualized: DisplayedItems is already the exact row list
        // DataGridBody iterates (tree-grid rows included, already flattened).
        var count = DisplayedItems.Count;
        if (hasDetailTemplate && countDetailRowsInFlatMode)
            count += DisplayedItems.Count(IsRowExpanded);
        return count;
    }

    private int CountGroupNodeRows(DataGridGroupNode<TItem> node, bool hasDetailTemplate)
    {
        var total = 1; // this node's own group row
        if (!IsGroupPathExpanded(node.Path)) return total;

        if (node.Children.Count > 0)
        {
            foreach (var child in node.Children)
                total += CountGroupNodeRows(child, hasDetailTemplate);
        }
        else
        {
            total += node.Items.Count;
            if (hasDetailTemplate) total += node.Items.Count(IsRowExpanded);
        }
        return total;
    }
}
