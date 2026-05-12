namespace Lumeo;

/// <summary>
/// Describes a field used to build a row- or column-hierarchy level in a
/// <see cref="PivotGrid{TItem}"/>. The distinct values produced by
/// <see cref="Selector"/> form the groups at that level.
/// </summary>
/// <typeparam name="TItem">The flat source row type.</typeparam>
public sealed class PivotField<TItem>
{
    /// <summary>Header label shown for this field level.</summary>
    public string Header { get; init; } = "";

    /// <summary>Extracts the grouping value for this field from a source row.</summary>
    public Func<TItem, object?> Selector { get; init; } = _ => null;

    /// <summary>Optional formatter for the displayed group label. Receives the raw value.</summary>
    public Func<object?, string>? Format { get; init; }

    /// <summary>Optional custom ordering of the distinct values. When null, values are sorted naturally.</summary>
    public IComparer<object?>? SortComparer { get; init; }

    public PivotField() { }

    public PivotField(string header, Func<TItem, object?> selector,
        Func<object?, string>? format = null, IComparer<object?>? sortComparer = null)
    {
        Header = header;
        Selector = selector;
        Format = format;
        SortComparer = sortComparer;
    }

    internal string FormatValue(object? value)
        => Format is not null ? Format(value) : (value?.ToString() ?? "—");
}
