namespace Lumeo;

/// <summary>The operator catalogue per value kind (ReUI's), and the helpers that resolve, default,
/// negate and coerce operators. Labels come from <see cref="FilterLabels.OperatorLabels"/>.</summary>
public static class FilterOperators
{
    /// <summary>The catalogue entries per kind, without labels.</summary>
    private static readonly IReadOnlyDictionary<FilterValueKind, (string Value, FilterArity Arity, string? Inverse)[]> Catalogue =
        new Dictionary<FilterValueKind, (string, FilterArity, string?)[]>
        {
            [FilterValueKind.Text] = new (string, FilterArity, string?)[]
            {
                ("contains", FilterArity.One, "not_contains"), ("not_contains", FilterArity.One, "contains"),
                ("starts_with", FilterArity.One, null), ("ends_with", FilterArity.One, null),
                ("is", FilterArity.One, "is_not"), ("is_not", FilterArity.One, "is"),
                ("empty", FilterArity.None, "not_empty"), ("not_empty", FilterArity.None, "empty"),
            },
            [FilterValueKind.Number] = new (string, FilterArity, string?)[]
            {
                ("eq", FilterArity.One, "neq"), ("neq", FilterArity.One, "eq"),
                ("gt", FilterArity.One, "lte"), ("gte", FilterArity.One, "lt"),
                ("lt", FilterArity.One, "gte"), ("lte", FilterArity.One, "gt"),
                ("between", FilterArity.Range, "not_between"), ("not_between", FilterArity.Range, "between"),
                ("empty", FilterArity.None, "not_empty"), ("not_empty", FilterArity.None, "empty"),
            },
            [FilterValueKind.Range] = new (string, FilterArity, string?)[]
            {
                ("between", FilterArity.Range, "not_between"), ("not_between", FilterArity.Range, "between"),
                ("empty", FilterArity.None, "not_empty"), ("not_empty", FilterArity.None, "empty"),
            },
            [FilterValueKind.Date] = new (string, FilterArity, string?)[]
            {
                ("is", FilterArity.One, "is_not"), ("is_not", FilterArity.One, "is"),
                ("is_before", FilterArity.One, "is_on_or_after"), ("is_after", FilterArity.One, "is_on_or_before"),
                ("is_on_or_before", FilterArity.One, "is_after"), ("is_on_or_after", FilterArity.One, "is_before"),
                ("between", FilterArity.Range, "not_between"), ("not_between", FilterArity.Range, "between"),
                ("empty", FilterArity.None, "not_empty"), ("not_empty", FilterArity.None, "empty"),
            },
            [FilterValueKind.Select] = new (string, FilterArity, string?)[]
            {
                ("is", FilterArity.One, "is_not"), ("is_not", FilterArity.One, "is"),
                ("is_any_of", FilterArity.Many, "is_none_of"), ("is_none_of", FilterArity.Many, "is_any_of"),
                ("empty", FilterArity.None, "not_empty"), ("not_empty", FilterArity.None, "empty"),
            },
            [FilterValueKind.MultiSelect] = new (string, FilterArity, string?)[]
            {
                ("has_any_of", FilterArity.Many, "has_none_of"), ("has_all_of", FilterArity.Many, null),
                ("has_none_of", FilterArity.Many, "has_any_of"),
                ("empty", FilterArity.None, "not_empty"), ("not_empty", FilterArity.None, "empty"),
            },
            [FilterValueKind.Boolean] = new (string, FilterArity, string?)[]
            {
                ("is", FilterArity.One, "is_not"), ("is_not", FilterArity.One, "is"),
                ("empty", FilterArity.None, "not_empty"), ("not_empty", FilterArity.None, "empty"),
            },
        };

    /// <summary>Every operator value the catalogue knows, for label tables.</summary>
    public static IReadOnlyList<string> AllValues { get; } = new[]
    {
        "contains", "not_contains", "starts_with", "ends_with", "is", "is_not", "is_any_of", "is_none_of",
        "has_any_of", "has_all_of", "has_none_of", "eq", "neq", "gt", "gte", "lt", "lte", "between", "not_between",
        "is_before", "is_after", "is_on_or_before", "is_on_or_after", "empty", "not_empty",
    };

    /// <summary>Builds the labelled catalogue for every kind from an operator-label table.</summary>
    public static IReadOnlyDictionary<FilterValueKind, IReadOnlyList<FilterOperatorDef>> Build(IReadOnlyDictionary<string, string> labels)
    {
        var built = new Dictionary<FilterValueKind, IReadOnlyList<FilterOperatorDef>>();
        foreach (var (kind, entries) in Catalogue)
        {
            built[kind] = entries.Select(e => new FilterOperatorDef(e.Value, labels.TryGetValue(e.Value, out var l) ? l : e.Value, e.Arity, e.Inverse)).ToList();
        }
        return built;
    }

    /// <summary>The operators a field offers: its own list, or the catalogue entry for its kind.</summary>
    public static IReadOnlyList<FilterOperatorDef> Resolve(FilterField field, IReadOnlyDictionary<FilterValueKind, IReadOnlyList<FilterOperatorDef>> catalogue)
    {
        if (field.Operators is not null) return field.Operators;
        return catalogue.TryGetValue(field.Kind, out var ops) ? ops : catalogue[FilterValueKind.Text];
    }

    /// <summary>The operators without the hidden ones.</summary>
    public static IReadOnlyList<FilterOperatorDef> Visible(IReadOnlyList<FilterOperatorDef> operators) => operators.Where(o => !o.Hidden).ToList();

    /// <summary>The operator with this value, or null.</summary>
    public static FilterOperatorDef? Get(IReadOnlyList<FilterOperatorDef> operators, string? value)
        => string.IsNullOrEmpty(value) ? null : operators.FirstOrDefault(o => o.Value == value);

    /// <summary>An unknown operator counts as taking one value.</summary>
    public static FilterArity ArityOf(FilterOperatorDef? op) => op?.Arity ?? FilterArity.One;

    /// <summary>False only for an operator that takes no value (<c>empty</c>, <c>not_empty</c>).</summary>
    public static bool TakesValue(FilterOperatorDef? op) => ArityOf(op) != FilterArity.None;

    /// <summary>The field's <see cref="FilterField.DefaultOperator"/> when it exists, else the first visible operator.</summary>
    public static string? Default(FilterField field, IReadOnlyList<FilterOperatorDef> operators)
    {
        if (!string.IsNullOrEmpty(field.DefaultOperator) && Get(operators, field.DefaultOperator) is { } named) return named.Value;
        return Visible(operators).FirstOrDefault()?.Value ?? operators.FirstOrDefault()?.Value;
    }

    /// <summary>Negates: swaps to the declared inverse when there is one, otherwise flips the negated flag.</summary>
    public static (string? Operator, bool Negated) Negate(FilterOperatorDef? op, IReadOnlyList<FilterOperatorDef> operators, bool negated)
    {
        if (op?.Inverse is not null && Get(operators, op.Inverse) is { } inverse) return (inverse.Value, negated);
        return (op?.Value, !negated);
    }
}
