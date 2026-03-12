namespace Lumeo;

public static class DataGridFilterOperator
{
    public static bool Evaluate(FilterDescriptor filter, object? value)
    {
        if (filter.Operator == FilterOperator.IsEmpty) return value is null || value.ToString() == "";
        if (filter.Operator == FilterOperator.IsNotEmpty) return value is not null && value.ToString() != "";
        if (value is null) return false;

        var strValue = value.ToString() ?? "";
        var filterStr = filter.Value?.ToString() ?? "";

        // Handle Select filter: value must match one of the comma-separated options
        if (filter.FilterType == DataGridFilterType.Select)
        {
            var options = filter.Value?.ToString()
                ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? Array.Empty<string>();
            return options.Length == 0 || options.Any(opt => opt.Equals(strValue, StringComparison.OrdinalIgnoreCase));
        }

        return filter.Operator switch
        {
            FilterOperator.Contains => strValue.Contains(filterStr, StringComparison.OrdinalIgnoreCase),
            FilterOperator.NotContains => !strValue.Contains(filterStr, StringComparison.OrdinalIgnoreCase),
            FilterOperator.Equals => filter.FilterType is DataGridFilterType.Number or DataGridFilterType.Date
                ? CompareValues(value, filter.Value) == 0
                : strValue.Equals(filterStr, StringComparison.OrdinalIgnoreCase),
            FilterOperator.NotEquals => filter.FilterType is DataGridFilterType.Number or DataGridFilterType.Date
                ? CompareValues(value, filter.Value) != 0
                : !strValue.Equals(filterStr, StringComparison.OrdinalIgnoreCase),
            FilterOperator.StartsWith => strValue.StartsWith(filterStr, StringComparison.OrdinalIgnoreCase),
            FilterOperator.EndsWith => strValue.EndsWith(filterStr, StringComparison.OrdinalIgnoreCase),
            FilterOperator.GreaterThan => CompareValues(value, filter.Value) > 0,
            FilterOperator.GreaterThanOrEqual => CompareValues(value, filter.Value) >= 0,
            FilterOperator.LessThan => CompareValues(value, filter.Value) < 0,
            FilterOperator.LessThanOrEqual => CompareValues(value, filter.Value) <= 0,
            FilterOperator.Between => CompareValues(value, filter.Value) >= 0 && CompareValues(value, filter.ValueTo) <= 0,
            _ => true
        };
    }

    private static int CompareValues(object? a, object? b)
    {
        if (a is IComparable ca && b is IComparable cb)
        {
            try { return ca.CompareTo(Convert.ChangeType(cb, ca.GetType())); }
            catch { return string.Compare(a?.ToString(), b?.ToString(), StringComparison.OrdinalIgnoreCase); }
        }
        return string.Compare(a?.ToString(), b?.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
