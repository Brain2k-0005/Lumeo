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
    /// existing horizontal scrollbar takes over, instead of columns collapsing. Implemented with
    /// <c>table-layout: auto</c> plus a per-column CSS <c>min-width</c> (never a fixed pixel
    /// <c>width</c>), which — unlike <c>table-layout: fixed</c> — browsers do honour as a floor.
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
