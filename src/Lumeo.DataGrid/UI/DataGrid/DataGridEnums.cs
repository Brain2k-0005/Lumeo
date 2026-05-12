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
