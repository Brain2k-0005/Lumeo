using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
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

    /// <summary>
    /// Raised by the grid when this column's <see cref="Visible"/> state is changed
    /// through the built-in UI (the column-chooser toggle). Wired up from a
    /// <see cref="DataGridColumnDef{TItem}"/>'s <c>VisibleChanged</c> so a consumer's
    /// <c>@bind-Visible</c> stays in sync with user toggles. Not raised for
    /// consumer-driven changes (that direction is already the source of truth), which
    /// prevents a feedback loop. Internal: consumers bind via the column-def component.
    /// </summary>
    internal EventCallback<bool> VisibleChanged { get; set; }

    public bool Groupable { get; set; }
    /// <summary>
    /// Whether this column can be reordered via header drag-and-drop or the
    /// Toggle Columns menu arrows. When false, the column is pinned to its
    /// current position regardless of the grid-level <c>Reorderable</c> flag.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool Reorderable { get; set; } = true;
    public PinDirection Pin { get; set; } = PinDirection.None;
    public DataGridFilterType FilterType { get; set; } = DataGridFilterType.Text;
    public AggregateType Aggregate { get; set; } = AggregateType.None;
    public string? Format { get; set; }
    /// <summary>Optional .NET format string used by the footer aggregate strip — overrides
    /// the cell <see cref="Format"/> for the totals row (e.g. cell shows <c>"C2"</c> and
    /// totals show <c>"N0"</c>). When null the footer falls back to <see cref="Format"/>,
    /// then to the historic <c>"N2"</c> default.</summary>
    public string? FooterFormat { get; set; }
    /// <summary>Id of the <see cref="DataGridColumnGroup{TItem}"/> this column belongs to,
    /// when present. Drives the extra header row that spans grouped columns with a
    /// single labelled <c>th</c>. Null for ungrouped columns.</summary>
    public string? ColumnGroupId { get; set; }
    public string? CssClass { get; set; }
    public string? HeaderCssClass { get; set; }
    public RenderFragment<TItem>? CellTemplate { get; set; }
    public RenderFragment? HeaderTemplate { get; set; }
    public RenderFragment<CellEditContext<TItem>>? EditTemplate { get; set; }
    public Func<TItem, string>? CellClass { get; set; }
    public Comparison<object?>? CustomSort { get; set; }
    public List<FilterOption>? FilterOptions { get; set; }

    /// <summary>
    /// Optional whitelist of filter operators shown in the column filter UI.
    /// When null, the grid exposes the default set for the column's <see cref="FilterType"/>.
    /// When set, only operators present in this list are offered (still intersected with the
    /// built-in defaults so unsupported combinations are not surfaced accidentally).
    /// </summary>
    public List<FilterOperator>? Operators { get; set; }

    /// <summary>
    /// Optional custom filter UI for this column. When provided, the default filter body
    /// (operator + value inputs) is replaced with this render fragment, allowing the consumer
    /// to implement any filter experience (sliders, multi-selects, relative-date pickers, ...).
    /// The context exposes the current <see cref="FilterDescriptor"/> for the column
    /// (or null) and an Apply callback the consumer invokes to commit the filter.
    /// Passing null to Apply clears the filter.
    /// </summary>
    public RenderFragment<DataGridFilterTemplateContext>? FilterTemplate { get; set; }

    // Per-column cache of compiled getters, keyed by field name. A compiled
    // Func<TItem, object?> is ~10x faster than PropertyInfo.GetValue and the
    // delta matters: GetValue runs once per cell per render.
    //
    // Behavior preservation vs. the previous reflection-based path:
    //   * Unknown field on TItem → PropertyInfo.GetProperty(...) returned null and
    //     GetValue returned null. The compiled path mirrors this by caching a
    //     null delegate and returning null.
    //   * Nested paths (e.g. "Address.City") are supported with null-safe
    //     traversal: any null intermediate yields null instead of throwing
    //     NullReferenceException — the previous code would have thrown on
    //     nested paths since GetProperty doesn't traverse dots; this is a
    //     net behavior gain and does not regress any existing flat path.
    private readonly ConcurrentDictionary<string, Func<TItem, object?>?> _gettersByField = new(StringComparer.Ordinal);

    public object? GetValue(TItem item)
    {
        if (FieldSelector is not null) return FieldSelector(item);
        if (Field is null || item is null) return null;

        var getter = _gettersByField.GetOrAdd(Field, static f => BuildGetter(f));
        return getter?.Invoke(item);
    }

    private static Func<TItem, object?>? BuildGetter(string fieldPath)
    {
        var param = Expression.Parameter(typeof(TItem), "x");
        Expression current = param;
        var currentType = typeof(TItem);

        // Walk dotted segments, chaining .Property accesses. Track each segment
        // so we can guard against null intermediates with Expression.Condition.
        var segments = fieldPath.Split('.');
        var nullChecks = new List<Expression>();

        foreach (var segment in segments)
        {
            var prop = currentType.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance);
            if (prop is null)
            {
                // Unknown property at any depth → match reflection's null return.
                return null;
            }

            // For reference-typed intermediates, add a null-check on the value
            // BEFORE we descend into it. Value-typed intermediates can't be null
            // (Nullable<T> is handled via .Value but is uncommon for these paths;
            // we leave that to ordinary access — matches old single-level behavior).
            if (!current.Type.IsValueType && !ReferenceEquals(current, param))
            {
                nullChecks.Add(Expression.Equal(current, Expression.Constant(null, current.Type)));
            }

            current = Expression.Property(current, prop);
            currentType = prop.PropertyType;
        }

        // Box the final value to object so the delegate returns object?.
        Expression body = currentType.IsValueType
            ? Expression.Convert(current, typeof(object))
            : current;

        // If we accumulated null-guards, fold them into a single short-circuit:
        // (intermediate1 == null || intermediate2 == null || ...) ? null : body
        if (nullChecks.Count > 0)
        {
            Expression anyNull = nullChecks[0];
            for (var i = 1; i < nullChecks.Count; i++)
                anyNull = Expression.OrElse(anyNull, nullChecks[i]);

            body = Expression.Condition(
                anyNull,
                Expression.Constant(null, typeof(object)),
                body);
        }

        return Expression.Lambda<Func<TItem, object?>>(body, param).Compile();
    }

    /// <summary>
    /// Formats a row's value for display, using <see cref="CultureInfo.CurrentCulture"/>
    /// so ASP.NET request localization affects dates/numbers automatically.
    /// </summary>
    public string GetFormattedValue(TItem item)
        => GetFormattedValue(item, CultureInfo.CurrentCulture);

    /// <summary>
    /// Formats a row's value for display using the supplied culture. Prefers
    /// <see cref="IFormattable"/> to guarantee culture-aware output for numbers
    /// and dates; falls back to <see cref="object.ToString"/> for other types.
    /// </summary>
    public string GetFormattedValue(TItem item, CultureInfo culture)
    {
        var value = GetValue(item);
        if (value is null) return "";
        if (value is IFormattable f)
            return f.ToString(Format, culture);
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
