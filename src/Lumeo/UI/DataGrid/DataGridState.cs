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

/// <summary>
/// Context passed to <see cref="DataGridColumn{TItem}.FilterTemplate"/> when a consumer
/// provides a custom filter UI for a column. The template is responsible for rendering its
/// own controls and invoking <see cref="Apply"/> with the new <see cref="FilterDescriptor"/>
/// when the user commits, or null to clear the filter.
/// </summary>
public record DataGridFilterTemplateContext(
    string? Field,
    FilterDescriptor? CurrentFilter,
    Func<FilterDescriptor?, Task> Apply
);

public record DataGridServerRequest(
    int Page,
    int PageSize,
    List<SortDescriptor>? Sorts,
    List<FilterDescriptor>? Filters,
    string? GlobalSearch,
    string? GroupBy = null,
    CancellationToken CancellationToken = default
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

public record DataGridGroupSection<TItem>(string Key, List<TItem> Items, int TotalCount);

public record DataGridNamedLayout(
    string Id,
    string Name,
    string Scope,  // "Personal", "Global", "SystemDefault"
    DataGridLayout Layout
);

/// <summary>
/// Public, JSON-serializable snapshot of a DataGrid's persistable state. This is
/// the canonical shape used by <c>DataGrid&lt;TItem&gt;.ExportLayout()</c> and
/// <c>DataGrid&lt;TItem&gt;.ApplyLayoutJsonAsync()</c>. Consumers can store the
/// resulting JSON in any backend — DB, file, remote API, cookies — and round-trip
/// it later. The <see cref="Version"/> field is bumped whenever the schema changes
/// so older payloads can be migrated or rejected.
/// </summary>
public record DataGridLayoutSnapshot(
    int Version,
    List<DataGridColumnLayout> Columns,
    List<SortDescriptor> Sorts,
    List<FilterDescriptor> Filters,
    string? GlobalSearch,
    int CurrentPage,
    int PageSize,
    string? GroupBy
);

/// <summary>
/// Per-column layout entry inside a <see cref="DataGridLayoutSnapshot"/>.
/// </summary>
public record DataGridColumnLayout(
    string Field,
    int Order,
    bool Visible,
    double? Width,
    PinDirection? Pin
);
