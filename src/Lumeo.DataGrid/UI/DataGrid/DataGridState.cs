namespace Lumeo;

/// <summary>
/// Flags enum controlling which export formats appear in the <see cref="DataGrid{TItem}"/>
/// toolbar's Export dropdown. Defaults to <see cref="All"/> so existing grids keep
/// listing every format. Set to a subset (e.g. <c>Csv | Excel</c>) to hide individual
/// formats — useful in WebAssembly where QuestPDF's PDF export throws
/// <see cref="System.PlatformNotSupportedException"/>.
/// </summary>
[Flags]
public enum DataGridExportFormat
{
    /// <summary>No formats available — the Export button is hidden entirely.</summary>
    None = 0,
    /// <summary>Comma-separated values.</summary>
    Csv = 1 << 0,
    /// <summary>Excel spreadsheet (currently emitted as CSV with BOM).</summary>
    Excel = 1 << 1,
    /// <summary>Portable Document Format (requires host platform support).</summary>
    Pdf = 1 << 2,
    /// <summary>All supported export formats.</summary>
    All = Csv | Excel | Pdf,
}

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

/// <summary>
/// Issued by the grid when it needs a page of server-side data.
///
/// <para>
/// <b>Cancellation contract:</b> <see cref="CancellationToken"/> is bumped on
/// every new request — pass it down to your HTTP client / repository call
/// (<c>HttpClient.GetAsync(url, request.CancellationToken)</c>) so that
/// out-of-order completions are dropped. If you ignore the token and the
/// older response resolves after a newer one, the older payload will
/// overwrite the visible page (the grid only guards its own
/// <c>IsLoading</c> flag against the same race, not the data you assign
/// to <c>Items</c>).
/// </para>
/// </summary>
public record DataGridServerRequest(
    int Page,
    int PageSize,
    List<SortDescriptor>? Sorts,
    List<FilterDescriptor>? Filters,
    string? GlobalSearch,
    string? GroupBy = null,
    CancellationToken CancellationToken = default
)
{
    /// <summary>True while this is still the grid's most recent request.
    /// Becomes false once a newer request supersedes it. Check this (or
    /// <see cref="CancellationToken"/>) immediately before assigning the
    /// fetched rows to your bound <c>Items</c> so an out-of-order completion
    /// can't overwrite a newer page:
    /// <code>
    /// var data = await Api.QueryAsync(req.Page, req.CancellationToken);
    /// if (req.IsCurrent) Items = data.Rows;
    /// </code>
    /// </summary>
    public bool IsCurrent => !CancellationToken.IsCancellationRequested;
}

public record DataGridServerResponse<TItem>(
    IEnumerable<TItem> Items,
    int TotalCount
);

/// <summary>
/// Request issued by virtualised server mode (<c>Virtualized=true</c> +
/// <c>OnRangeRequest</c>). Unlike <see cref="DataGridServerRequest"/>,
/// this is range-based — the grid asks for a window of N items starting
/// at <see cref="StartIndex"/>, driven by the user's scroll position
/// rather than page numbers. Sort/filter/search context is still passed
/// so the backend can apply the same query as the rest of the grid.
/// </summary>
public record DataGridRangeRequest(
    int StartIndex,
    int Count,
    List<SortDescriptor>? Sorts,
    List<FilterDescriptor>? Filters,
    string? GlobalSearch,
    CancellationToken CancellationToken = default
);

/// <summary>
/// Response from a virtualised server-mode range fetch. <see cref="Items"/>
/// is the slice the backend returned for the requested window;
/// <see cref="TotalCount"/> is the total number of matching rows server-side
/// (used to size the scroll spacers).
/// </summary>
public record DataGridRangeResponse<TItem>(
    IReadOnlyList<TItem> Items,
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

public record ColumnPinEventArgs(
    string ColumnId,
    PinDirection Direction
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

/// <summary>
/// A single row's pending change set in <see cref="DataGridEditMode.Batch"/> mode.
/// <see cref="IsNew"/> is true for rows created via the "+ Add row" trigger
/// (in which case <see cref="ChangedFields"/> still carries every field the user
/// touched). <see cref="ChangedFields"/> maps the column <c>Field</c> to its new value.
/// </summary>
public record DataGridBatchChange<TItem>(
    TItem Item,
    Dictionary<string, object?> ChangedFields,
    bool IsNew
);

/// <summary>
/// Event args for <see cref="DataGrid{TItem}.OnBatchSave"/>. Carries the buffered
/// edits when the user clicks "Save all". <see cref="Modified"/> are existing rows
/// with pending field changes; <see cref="Added"/> are new rows from the add-row
/// trigger. <see cref="All"/> is the concatenation for convenience. The consumer
/// applies these to its backing store; on success the grid's buffer is cleared
/// automatically once the handler returns without throwing.
/// </summary>
public record DataGridBatchSaveEventArgs<TItem>(
    IReadOnlyList<DataGridBatchChange<TItem>> Modified,
    IReadOnlyList<DataGridBatchChange<TItem>> Added
)
{
    public IEnumerable<DataGridBatchChange<TItem>> All => Modified.Concat(Added);
}

public class DataGridLayout
{
    public string? Name { get; set; }
    public List<ColumnLayout> Columns { get; set; } = new();
    public List<SortDescriptor>? Sorts { get; set; }
    public List<FilterDescriptor>? Filters { get; set; }
    public int? PageSize { get; set; }
    public string? GlobalSearch { get; set; }
    /// <summary>
    /// Ordered list of fields the group-panel was grouping by when the
    /// snapshot was taken. Restored verbatim into the grid's runtime
    /// grouping state so the chip-strip survives a reload. Null/empty
    /// means no grouping was active.
    /// </summary>
    public List<string>? GroupByFields { get; set; }
}

public class ColumnLayout
{
    public string Field { get; set; } = "";
    public double? Width { get; set; }
    public bool Visible { get; set; } = true;
    public PinDirection Pin { get; set; } = PinDirection.None;
    public int Order { get; set; }
}

public record DataGridGroupSection<TItem>(string Key, List<TItem> Items, int TotalCount)
{
    /// <summary>Per-group aggregate strip (one entry per column that declares an
    /// <see cref="AggregateType"/>). Populated by the grid when grouping is active.</summary>
    public IReadOnlyList<DataGridGroupAggregate> Aggregates { get; init; } = Array.Empty<DataGridGroupAggregate>();
}

/// <summary>A single per-group aggregate value, keyed by the column it belongs to.</summary>
/// <param name="ColumnId">The owning <see cref="DataGridColumn{TItem}.Id"/>.</param>
/// <param name="Label">Localized aggregate label (e.g. "Sum").</param>
/// <param name="Value">Pre-formatted aggregate value.</param>
public record DataGridGroupAggregate(string ColumnId, string Label, string Value);

/// <summary>
/// A node in a multi-level grouping tree. <see cref="Children"/> is non-empty for
/// every level except the deepest; <see cref="Items"/> carries the leaf rows at the
/// deepest level (empty for intermediate nodes). <see cref="Path"/> is a stable
/// "/"-joined identity used for expand/collapse state ("Region/Country").
/// </summary>
public record DataGridGroupNode<TItem>(
    string Key,
    string Path,
    int Level,
    string Field,
    int TotalCount,
    List<TItem> Items,
    List<DataGridGroupNode<TItem>> Children)
{
    public IReadOnlyList<DataGridGroupAggregate> Aggregates { get; init; } = Array.Empty<DataGridGroupAggregate>();
}

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
    string? GroupBy,
    List<string>? GroupByFields = null
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
