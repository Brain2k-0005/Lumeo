namespace Lumeo;

public record SortDescriptor(string Field, SortDirection Direction);

public record FilterDescriptor(
    string Field,
    FilterOperator Operator,
    object? Value,
    object? ValueTo = null,
    DataGridFilterType FilterType = DataGridFilterType.Text
);

public record FilterOption(string Label, object Value);

public record DataGridServerRequest(
    int Page,
    int PageSize,
    List<SortDescriptor>? Sorts,
    List<FilterDescriptor>? Filters,
    string? GlobalSearch
);

public record DataGridServerResponse<TItem>(
    IEnumerable<TItem> Items,
    int TotalCount
);

public record CellEditEventArgs<TItem>(
    TItem Item,
    string Field,
    object? OldValue,
    object? NewValue
);

public record RowExpandEventArgs<TItem>(
    TItem Item,
    bool IsExpanded
);

public record ColumnReorderEventArgs(
    string ColumnId,
    int OldIndex,
    int NewIndex
);

public record FilterApplyEventArgs(
    string? Field,
    FilterDescriptor? Filter
);

public record RowEditEventArgs<TItem>(
    TItem Item,
    Dictionary<string, object?> ChangedValues
);

public record RowReorderEventArgs<TItem>(
    TItem Item,
    int OldIndex,
    int NewIndex
);

public class DataGridLayout
{
    public string? Name { get; set; }
    public List<ColumnLayout> Columns { get; set; } = new();
    public List<SortDescriptor>? Sorts { get; set; }
    public List<FilterDescriptor>? Filters { get; set; }
    public int? PageSize { get; set; }
    public string? GlobalSearch { get; set; }
}

public class ColumnLayout
{
    public string Field { get; set; } = "";
    public double? Width { get; set; }
    public bool Visible { get; set; } = true;
    public PinDirection Pin { get; set; } = PinDirection.None;
    public int Order { get; set; }
}
