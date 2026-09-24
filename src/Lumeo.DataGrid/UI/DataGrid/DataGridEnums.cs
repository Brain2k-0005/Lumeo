namespace Lumeo;

public enum SortDirection
{
    None,
    Ascending,
    Descending
}

public enum DataGridSelectionMode
{
    None,
    Single,
    Multiple
}

public enum PinDirection
{
    None,
    Left,
    Right
}

/// <summary>
/// How the grid lays out column widths. See <c>DataGrid.ColumnSizing</c>.
/// </summary>
public enum DataGridColumnSizing
{
    /// <summary>
    /// The historic behaviour: once every visible column has a <see cref="DataGridColumn{TItem}.Width"/>,
    /// the table lays out <c>table-layout: fixed</c> at the sum of those widths, and a
    /// <see cref="DataGridColumn{TItem}.FillWidth"/> column absorbs the remainder. Because fixed
    /// table layout does not honour CSS <c>min-width</c> on cells, a <c>FillWidth</c> column can be
    /// squeezed to (visually) 0px once enough other columns are visible — there is no floor.
    /// </summary>
    Auto,

    /// <summary>
    /// Columns fill the available container width when there is room, and never shrink below
    /// their own <see cref="DataGridColumn{TItem}.MinWidth"/> (falling back to
    /// <see cref="DataGridColumn{TItem}.Width"/> when <c>MinWidth</c> isn't set) — once the sum of
    /// every visible column's floor exceeds the container, the table grows past it and the grid's
    /// existing horizontal scrollbar takes over, instead of columns collapsing. A user-resized or
    /// <c>LayoutStorageKey</c>-restored width always wins over the declared width, clamped to
    /// <c>MinWidth</c>/<c>MaxWidth</c>.
    ///
    /// Implemented by measuring the grid's horizontal scroll wrapper (a <c>ResizeObserver</c>
    /// reporting back to .NET — see <c>DataGrid.OnFitContainerWidthChanged</c>) and computing an
    /// explicit pixel width per column from it (<see cref="DataGridColumnFit.Compute"/>), then
    /// rendering with <c>table-layout: fixed</c> at that computed width — NOT <c>table-layout:
    /// auto</c> with only a CSS <c>min-width</c>, which an earlier version of this mode used and
    /// which never actually capped growth: under auto layout a <c>white-space: nowrap</c> cell's
    /// minimum content width is its full rendered text width regardless of any width/min-width
    /// declared on the cell, so a 12-column grid of short nowrap values could render wider than
    /// its container even with room to spare (DocFlow field report against 5.11.0). Fixed layout
    /// uses only the widths it's given, never content, so it's a real ceiling; cell content then
    /// truncates with an ellipsis (<see cref="DataGridCell{TItem}"/>) instead of forcing the
    /// column wider. Before the first measurement (SSR, prerender, a host with no ResizeObserver)
    /// the header falls back to a width/min-width hint from the column's own declared/resized
    /// value under <c>table-layout: auto</c>, upgraded to the exact computed width on first paint.
    /// </summary>
    FitWithMinimum
}

public enum DataGridEditMode
{
    None,
    Cell,
    Row,
    /// <summary>
    /// Buffered editing: cell edits are collected into a per-cell pending-changes
    /// buffer instead of committing immediately. Dirty cells are visually marked,
    /// and "Save all" / "Discard" controls appear above the grid. Saving fires
    /// <see cref="DataGrid{TItem}.OnBatchSave"/> with the modified/added rows.
    /// </summary>
    Batch
}

public enum DataGridFilterType
{
    Text,
    Number,
    Date,
    Select,
    Boolean
}

public enum AggregateType
{
    None,
    Sum,
    Average,
    Count,
    Min,
    Max
}

public enum FilterOperator
{
    Contains,
    NotContains,
    Equals,
    NotEquals,
    StartsWith,
    EndsWith,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    IsEmpty,
    IsNotEmpty,
    Between
}
