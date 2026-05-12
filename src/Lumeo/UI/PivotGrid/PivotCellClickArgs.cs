namespace Lumeo;

/// <summary>
/// Context for a clicked data cell in a <see cref="PivotGrid{TItem}"/>, useful for drill-down.
/// </summary>
public sealed class PivotCellClickArgs
{
    /// <summary>The row-hierarchy path (one value per row field) identifying the clicked row.</summary>
    public IReadOnlyList<object?> RowKeys { get; }

    /// <summary>The column-hierarchy path identifying the clicked column. Empty when there is no column field.</summary>
    public IReadOnlyList<object?> ColumnKeys { get; }

    /// <summary>Header of the measure that was clicked.</summary>
    public string Measure { get; }

    /// <summary>The underlying source rows that contributed to the clicked cell.</summary>
    public IReadOnlyList<object?> Items { get; }

    public PivotCellClickArgs(IReadOnlyList<object?> rowKeys, IReadOnlyList<object?> columnKeys,
        string measure, IReadOnlyList<object?> items)
    {
        RowKeys = rowKeys;
        ColumnKeys = columnKeys;
        Measure = measure;
        Items = items;
    }
}
