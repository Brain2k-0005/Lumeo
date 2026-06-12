using System.Text.Json;

namespace Lumeo;

public static class DataGridFilterOperator
{
    /// <summary>
    /// Unwraps a <see cref="JsonElement"/> into its CLR primitive. Filter
    /// values that travel through JSON (persisted layouts, the SavedLayout
    /// parameter, named layouts) deserialize as <see cref="JsonElement"/>,
    /// which is not <see cref="IComparable"/> — number and date comparisons
    /// then silently degraded to lexicographic string compares ("9" &gt; "10").
    /// Applied at layout load AND defensively at evaluation time.
    /// </summary>
    public static object? Normalize(object? value) => value is JsonElement je
        ? je.ValueKind switch
        {
            JsonValueKind.String => je.GetString(),
            JsonValueKind.Number => je.TryGetInt64(out var l) ? l : je.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => je.ToString()
        }
        : value;

    public static bool Evaluate(FilterDescriptor filter, object? value)
    {
        if (filter.Operator == FilterOperator.IsEmpty) return value is null || value.ToString() == "";
        if (filter.Operator == FilterOperator.IsNotEmpty) return value is not null && value.ToString() != "";
        if (value is null) return false;

        var filterValue = Normalize(filter.Value);
        var filterTo = Normalize(filter.ValueTo);

        var strValue = value.ToString() ?? "";
        var filterStr = filterValue?.ToString() ?? "";

        // Handle Select filter: value must match one of the comma-separated options
        if (filter.FilterType == DataGridFilterType.Select)
        {
            var options = filterValue?.ToString()
                ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? Array.Empty<string>();
            return options.Length == 0 || options.Any(opt => opt.Equals(strValue, StringComparison.OrdinalIgnoreCase));
        }

        return filter.Operator switch
        {
            FilterOperator.Contains => strValue.Contains(filterStr, StringComparison.OrdinalIgnoreCase),
            FilterOperator.NotContains => !strValue.Contains(filterStr, StringComparison.OrdinalIgnoreCase),
            FilterOperator.Equals => filter.FilterType is DataGridFilterType.Number or DataGridFilterType.Date
                ? CompareValues(value, filterValue) == 0
                : strValue.Equals(filterStr, StringComparison.OrdinalIgnoreCase),
            FilterOperator.NotEquals => filter.FilterType is DataGridFilterType.Number or DataGridFilterType.Date
                ? CompareValues(value, filterValue) != 0
                : !strValue.Equals(filterStr, StringComparison.OrdinalIgnoreCase),
            FilterOperator.StartsWith => strValue.StartsWith(filterStr, StringComparison.OrdinalIgnoreCase),
            FilterOperator.EndsWith => strValue.EndsWith(filterStr, StringComparison.OrdinalIgnoreCase),
            FilterOperator.GreaterThan => CompareValues(value, filterValue) > 0,
            FilterOperator.GreaterThanOrEqual => CompareValues(value, filterValue) >= 0,
            FilterOperator.LessThan => CompareValues(value, filterValue) < 0,
            FilterOperator.LessThanOrEqual => CompareValues(value, filterValue) <= 0,
            FilterOperator.Between => CompareValues(value, filterValue) >= 0 && CompareValues(value, filterTo) <= 0,
            _ => true
        };
    }

    private static int CompareValues(object? a, object? b)
    {
        a = Normalize(a);
        b = Normalize(b);
        if (a is IComparable ca && b is IComparable cb)
        {
            try { return ca.CompareTo(Convert.ChangeType(cb, ca.GetType())); }
            catch (Exception) { /* Type conversion failed — fall back to string comparison */ return string.Compare(a?.ToString(), b?.ToString(), StringComparison.OrdinalIgnoreCase); }
        }
        return string.Compare(a?.ToString(), b?.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
