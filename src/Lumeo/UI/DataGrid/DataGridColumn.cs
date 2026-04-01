using Microsoft.AspNetCore.Components;

namespace Lumeo;

public class DataGridColumn<TItem>
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string? Title { get; set; }
    public string? Field { get; set; }
    public Func<TItem, object?>? FieldSelector { get; set; }
    public double? Width { get; set; }
    public double? MinWidth { get; set; }
    public double? MaxWidth { get; set; }
    public bool Sortable { get; set; }
    public bool Filterable { get; set; }
    public bool Pinnable { get; set; }
    public bool Resizable { get; set; } = true;
    public bool Visible { get; set; } = true;
    public bool Groupable { get; set; }
    public PinDirection Pin { get; set; } = PinDirection.None;
    public DataGridFilterType FilterType { get; set; } = DataGridFilterType.Text;
    public AggregateType Aggregate { get; set; } = AggregateType.None;
    public string? Format { get; set; }
    public string? CssClass { get; set; }
    public string? HeaderCssClass { get; set; }
    public RenderFragment<TItem>? CellTemplate { get; set; }
    public RenderFragment? HeaderTemplate { get; set; }
    public RenderFragment<CellEditContext<TItem>>? EditTemplate { get; set; }
    public Func<TItem, string>? CellClass { get; set; }
    public Comparison<object?>? CustomSort { get; set; }
    public List<FilterOption>? FilterOptions { get; set; }

    private System.Reflection.PropertyInfo? _cachedProperty;
    private string? _cachedPropertyField;

    public object? GetValue(TItem item)
    {
        if (FieldSelector is not null) return FieldSelector(item);
        if (Field is null || item is null) return null;

        if (_cachedProperty is null || _cachedPropertyField != Field)
        {
            _cachedProperty = typeof(TItem).GetProperty(Field);
            _cachedPropertyField = Field;
        }

        return _cachedProperty?.GetValue(item);
    }

    public string GetFormattedValue(TItem item)
    {
        var value = GetValue(item);
        if (value is null) return "";
        if (Format is not null) return string.Format($"{{0:{Format}}}", value);
        return value.ToString() ?? "";
    }
}

public class CellEditContext<TItem>
{
    public TItem Item { get; init; } = default!;
    public DataGridColumn<TItem> Column { get; init; } = default!;
    public object? Value { get; set; }
    public Action ValueChanged { get; init; } = default!;
}
