namespace Lumeo;

/// <summary>How a <see cref="PivotMeasure{TItem}"/> aggregates the matching source rows.</summary>
public enum PivotAggregate
{
    Sum,
    Average,
    Count,
    Min,
    Max
}

/// <summary>
/// A value field for a <see cref="PivotGrid{TItem}"/> together with the
/// aggregation applied across the source rows in each cell.
/// </summary>
/// <typeparam name="TItem">The flat source row type.</typeparam>
public sealed class PivotMeasure<TItem>
{
    /// <summary>Header label for this measure.</summary>
    public string Header { get; init; } = "";

    /// <summary>Extracts the numeric value to aggregate from a source row.</summary>
    public Func<TItem, decimal> Value { get; init; } = _ => 0m;

    /// <summary>The aggregation function. Default is <see cref="PivotAggregate.Sum"/>.</summary>
    public PivotAggregate Aggregate { get; init; } = PivotAggregate.Sum;

    /// <summary>Optional formatter for the aggregated number. When null, a sensible default is used.</summary>
    public Func<decimal, string>? Format { get; init; }

    public PivotMeasure() { }

    public PivotMeasure(string header, Func<TItem, decimal> value,
        PivotAggregate aggregate = PivotAggregate.Sum, Func<decimal, string>? format = null)
    {
        Header = header;
        Value = value;
        Aggregate = aggregate;
        Format = format;
    }

    internal decimal Compute(IReadOnlyList<TItem> items)
    {
        if (Aggregate == PivotAggregate.Count)
            return items.Count;
        if (items.Count == 0)
            return 0m;
        return Aggregate switch
        {
            PivotAggregate.Sum => items.Sum(Value),
            PivotAggregate.Average => items.Average(Value),
            PivotAggregate.Min => items.Min(Value),
            PivotAggregate.Max => items.Max(Value),
            _ => items.Sum(Value)
        };
    }

    internal string FormatValue(decimal value)
    {
        if (Format is not null) return Format(value);
        // Counts are whole; everything else gets two decimals only when it isn't integral.
        if (Aggregate == PivotAggregate.Count) return value.ToString("N0");
        return value == decimal.Truncate(value) ? value.ToString("N0") : value.ToString("N2");
    }
}
