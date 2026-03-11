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
    /// <summary>
    /// Row-level editing. Not yet implemented; reserved for future use.
    /// </summary>
    Row
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
