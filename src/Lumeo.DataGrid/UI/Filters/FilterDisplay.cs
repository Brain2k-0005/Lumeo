namespace Lumeo;

/// <summary>What a rule reads as: the operator label (negated or not) and the value text the chip
/// and the advanced row show, resolved through options, dates and the labels.</summary>
public static class FilterDisplay
{
    public static string OperatorLabel(FiltersContext ctx, FilterRule rule, FilterOperatorDef? op)
        => op is not null
            ? (rule.Negated ? string.Format(ctx.Labels.Negated, op.Label) : op.Label)
            : (rule.Operator.Length > 0 ? rule.Operator : ctx.Labels.SelectCondition);

    public static bool IsOptionBacked(FilterField field) => field.Kind is FilterValueKind.Select or FilterValueKind.MultiSelect || field.HasOptions;

    /// <summary>The value segment's text: the field's <see cref="FilterField.ValueText"/>, else a
    /// placeholder for nothing, both bounds of a range, one resolved label, or a count.</summary>
    public static string ValueText(FiltersContext ctx, FilterRule rule, FilterField field, FilterOperatorDef? op)
    {
        if (field.ValueText is not null) return field.ValueText(ValueContext(ctx, rule, field, op));
        var labels = ctx.Labels;
        var value = rule.Value;
        var emptyLabel = IsOptionBacked(field) ? labels.SelectPlaceholder : field.Placeholder ?? labels.ValuePlaceholder;
        if (FilterValues.IsBlank(value)) return emptyLabel;
        if (FilterOperators.ArityOf(op) == FilterArity.Range && value is FilterRange range)
            return string.Format(labels.ValueRange, One(ctx, field, range.From), One(ctx, field, range.To));
        if (value is not string && value is System.Collections.IEnumerable list)
        {
            var items = list.Cast<object?>().ToList();
            if (items.Count == 0) return emptyLabel;
            if (items.Count == 1) return One(ctx, field, items[0]);
            return string.Format(labels.ValueCount, items.Count);
        }
        return One(ctx, field, value);
    }

    /// <summary>The value text with every entry of a many-valued rule spelled out, for a tooltip or a locked bar.</summary>
    public static string FullText(FiltersContext ctx, FilterRule rule, FilterField field, FilterOperatorDef? op)
    {
        var text = ValueText(ctx, rule, field, op);
        var values = rule.Values;
        if (field.ValueText is not null || rule.Value is string || rule.Value is not System.Collections.IEnumerable || FilterOperators.ArityOf(op) == FilterArity.Range || values.Count < 2)
            return text;
        return string.Format(ctx.Labels.ValueDetail, text, string.Join(", ", values.Select(v => One(ctx, field, v))));
    }

    public static string One(FiltersContext ctx, FilterField field, object? value)
    {
        var labels = ctx.Labels;
        switch (value)
        {
            case null: return "";
            case bool b: return b ? labels.True : labels.False;
            case FilterDateValue d: return FilterDates.Format(d, labels);
            case DateOnly d: return FilterDates.Format(FilterDateValue.Of(d), labels);
            case DateTime dt: return FilterDates.Format(FilterDateValue.Of(DateOnly.FromDateTime(dt)), labels);
            default:
                var text = value.ToString() ?? "";
                return ctx.ResolveOption(field, text)?.Label ?? text;
        }
    }

    public static FilterValueContext ValueContext(FiltersContext ctx, FilterRule rule, FilterField field, FilterOperatorDef? op)
    {
        var values = rule.Values;
        var options = values.Select(v => v is null ? null : ctx.ResolveOption(field, v.ToString()!)).Where(o => o is not null).Select(o => o!).ToList();
        return new FilterValueContext(rule.Value, values, field, op ?? new FilterOperatorDef(rule.Operator, rule.Operator), options, ctx.Labels);
    }

    /// <summary>True when a row edits its value in place: a text or number field with a one-valued
    /// operator and no options, custom editor or custom display.</summary>
    public static bool UsesInlineEditor(FiltersContext ctx, FilterField field, FilterOperatorDef? op)
    {
        if (field.Editor is not null || field.EditorTemplate is not null) return false;
        if (field.HasOptions) return false;
        if (field.ValueTemplate is not null || field.ValueText is not null || ctx.ValueTemplate is not null) return false;
        if (field.Kind is not (FilterValueKind.Text or FilterValueKind.Number)) return false;
        return FilterOperators.ArityOf(op) == FilterArity.One;
    }
}
