using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lumeo;

/// <summary>Boolean combinator for a <see cref="QueryGroup"/>.</summary>
public enum QueryCombinator
{
    And,
    Or
}

/// <summary>The data type of a <see cref="QueryField"/>. Drives the default operator
/// set and the built-in value editor rendered for a rule.</summary>
public enum QueryFieldType
{
    Text,
    Number,
    Date,
    Boolean,
    Select
}

/// <summary>
/// A field definition the consumer provides to <see cref="QueryBuilder"/>.
/// <see cref="Name"/> is the stable key written into produced rules;
/// <see cref="Label"/> is the human-facing text shown in the field picker.
/// </summary>
public sealed class QueryField
{
    /// <summary>Stable key written to <see cref="QueryRule.Field"/>. Required.</summary>
    public string Name { get; set; } = "";

    /// <summary>Human-facing label shown in the field picker. Falls back to <see cref="Name"/>.</summary>
    public string? Label { get; set; }

    /// <summary>The field's data type. Determines the default operator set and value editor.</summary>
    public QueryFieldType Type { get; set; } = QueryFieldType.Text;

    /// <summary>The operator keys allowed for this field. When <c>null</c>, the default
    /// set for <see cref="Type"/> is used (see <see cref="QueryBuilderOperators.DefaultsFor"/>).</summary>
    public IReadOnlyList<string>? Operators { get; set; }

    /// <summary>Options shown when <see cref="Type"/> is <see cref="QueryFieldType.Select"/>.
    /// Each tuple is <c>(Value, Label)</c>.</summary>
    public IReadOnlyList<(string Value, string Label)>? Options { get; set; }

    /// <summary>The display label — <see cref="Label"/> when set, otherwise <see cref="Name"/>.</summary>
    [JsonIgnore]
    public string DisplayLabel => string.IsNullOrEmpty(Label) ? Name : Label;

    /// <summary>The effective operator keys for this field.</summary>
    public IReadOnlyList<string> EffectiveOperators =>
        Operators is { Count: > 0 } ? Operators : QueryBuilderOperators.DefaultsFor(Type);
}

/// <summary>Base type for a node in a query tree — either a <see cref="QueryRule"/>
/// (a leaf condition) or a <see cref="QueryGroup"/> (a combinator with children).</summary>
[JsonDerivedType(typeof(QueryRule), "rule")]
[JsonDerivedType(typeof(QueryGroup), "group")]
public abstract class QueryNode
{
}

/// <summary>A single leaf condition: a field, an operator, and a comparison value.</summary>
public sealed class QueryRule : QueryNode
{
    /// <summary>The <see cref="QueryField.Name"/> this rule targets.</summary>
    public string Field { get; set; } = "";

    /// <summary>One of the field's allowed operator keys.</summary>
    public string Operator { get; set; } = "";

    /// <summary>The comparison value. Type depends on the field; for "between"-style
    /// operators this holds the lower bound and <see cref="Value2"/> holds the upper.</summary>
    public object? Value { get; set; }

    /// <summary>The second comparison value, used only by range operators ("between").</summary>
    public object? Value2 { get; set; }
}

/// <summary>A group: a <see cref="QueryCombinator"/> applied across a list of child
/// <see cref="QueryNode"/>s (rules and/or nested groups).</summary>
public sealed class QueryGroup : QueryNode
{
    public QueryCombinator Combinator { get; set; } = QueryCombinator.And;

    public List<QueryNode> Rules { get; set; } = new();

    /// <summary>Creates an empty root group (And, no rules).</summary>
    public static QueryGroup CreateEmpty() => new() { Combinator = QueryCombinator.And, Rules = new() };
}

/// <summary>Default operator sets per <see cref="QueryFieldType"/> and friendly display labels.</summary>
public static class QueryBuilderOperators
{
    public static readonly IReadOnlyList<string> TextDefaults =
        new[] { "=", "!=", "contains", "notContains", "startsWith", "endsWith", "isEmpty", "isNotEmpty" };

    public static readonly IReadOnlyList<string> NumberDefaults =
        new[] { "=", "!=", "<", "<=", ">", ">=", "between", "isNull", "isNotNull" };

    public static readonly IReadOnlyList<string> DateDefaults =
        new[] { "=", "!=", "<", "<=", ">", ">=", "between", "isNull", "isNotNull" };

    public static readonly IReadOnlyList<string> BooleanDefaults =
        new[] { "=", "!=" };

    public static readonly IReadOnlyList<string> SelectDefaults =
        new[] { "=", "!=", "in", "notIn", "isNull", "isNotNull" };

    /// <summary>Friendly display labels for operator keys.</summary>
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        ["="] = "equals",
        ["!="] = "not equals",
        ["<"] = "less than",
        ["<="] = "less than or equal",
        [">"] = "greater than",
        [">="] = "greater than or equal",
        ["contains"] = "contains",
        ["notContains"] = "does not contain",
        ["startsWith"] = "starts with",
        ["endsWith"] = "ends with",
        ["between"] = "between",
        ["in"] = "in",
        ["notIn"] = "not in",
        ["isEmpty"] = "is empty",
        ["isNotEmpty"] = "is not empty",
        ["isNull"] = "is null",
        ["isNotNull"] = "is not null"
    };

    /// <summary>Operators that take no value editor.</summary>
    public static readonly IReadOnlySet<string> NoValueOperators =
        new HashSet<string> { "isEmpty", "isNotEmpty", "isNull", "isNotNull" };

    /// <summary>Operators that take a second value editor (a range).</summary>
    public static readonly IReadOnlySet<string> RangeOperators = new HashSet<string> { "between" };

    public static IReadOnlyList<string> DefaultsFor(QueryFieldType type) => type switch
    {
        QueryFieldType.Text => TextDefaults,
        QueryFieldType.Number => NumberDefaults,
        QueryFieldType.Date => DateDefaults,
        QueryFieldType.Boolean => BooleanDefaults,
        QueryFieldType.Select => SelectDefaults,
        _ => TextDefaults
    };

    public static string LabelFor(string op) => Labels.TryGetValue(op, out var l) ? l : op;
}

/// <summary>Serialization + LINQ helpers for <see cref="QueryGroup"/> trees.</summary>
public static class QueryBuilderSerialization
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ToJson(QueryGroup query) =>
        JsonSerializer.Serialize(query, JsonOptions);

    public static QueryGroup? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<QueryGroup>(json, ReadOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Compiles a query tree to a LINQ predicate <c>Expression&lt;Func&lt;T,bool&gt;&gt;</c>.
    /// Returns <c>null</c> when the tree is empty (no rules anywhere). Unknown operators or
    /// missing properties throw <see cref="InvalidOperationException"/>. String comparisons
    /// for <c>contains</c>/<c>startsWith</c>/<c>endsWith</c> are case-insensitive (ordinal).
    /// </summary>
    public static Expression<Func<T, bool>>? ToExpression<T>(QueryGroup query)
    {
        var param = Expression.Parameter(typeof(T), "x");
        var body = BuildGroup<T>(query, param);
        return body is null ? null : Expression.Lambda<Func<T, bool>>(body, param);
    }

    private static Expression? BuildGroup<T>(QueryGroup group, ParameterExpression param)
    {
        var parts = new List<Expression>();
        foreach (var node in group.Rules)
        {
            Expression? part = node switch
            {
                QueryGroup g => BuildGroup<T>(g, param),
                QueryRule r => BuildRule<T>(r, param),
                _ => null
            };
            if (part is not null) parts.Add(part);
        }

        if (parts.Count == 0) return null;

        var acc = parts[0];
        for (var i = 1; i < parts.Count; i++)
        {
            acc = group.Combinator == QueryCombinator.And
                ? Expression.AndAlso(acc, parts[i])
                : Expression.OrElse(acc, parts[i]);
        }
        return acc;
    }

    private static Expression? BuildRule<T>(QueryRule rule, ParameterExpression param)
    {
        if (string.IsNullOrEmpty(rule.Field) || string.IsNullOrEmpty(rule.Operator)) return null;

        var prop = typeof(T).GetProperty(rule.Field,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (prop is null)
            throw new InvalidOperationException($"Type '{typeof(T).Name}' has no public property '{rule.Field}'.");

        var member = Expression.Property(param, prop);
        var memberType = prop.PropertyType;
        var underlying = Nullable.GetUnderlyingType(memberType) ?? memberType;

        switch (rule.Operator)
        {
            case "isNull":
                return memberType.IsValueType && Nullable.GetUnderlyingType(memberType) is null
                    ? Expression.Constant(false)
                    : Expression.Equal(member, Expression.Constant(null, memberType));
            case "isNotNull":
                return memberType.IsValueType && Nullable.GetUnderlyingType(memberType) is null
                    ? Expression.Constant(true)
                    : Expression.NotEqual(member, Expression.Constant(null, memberType));
            case "isEmpty":
                return Expression.OrElse(
                    Expression.Equal(member, Expression.Constant(null, memberType)),
                    Expression.Equal(EnsureString(member), Expression.Constant("")));
            case "isNotEmpty":
                return Expression.AndAlso(
                    Expression.NotEqual(member, Expression.Constant(null, memberType)),
                    Expression.NotEqual(EnsureString(member), Expression.Constant("")));
        }

        if (rule.Operator is "contains" or "notContains" or "startsWith" or "endsWith")
        {
            var methodName = rule.Operator switch
            {
                "startsWith" => nameof(string.StartsWith),
                "endsWith" => nameof(string.EndsWith),
                _ => nameof(string.Contains)
            };
            var method = typeof(string).GetMethod(methodName, new[] { typeof(string), typeof(StringComparison) })!;
            var needle = Expression.Constant(rule.Value?.ToString() ?? "");
            var call = Expression.Call(EnsureString(member), method, needle, Expression.Constant(StringComparison.OrdinalIgnoreCase));
            var notNull = Expression.NotEqual(EnsureString(member), Expression.Constant(null, typeof(string)));
            var guarded = Expression.AndAlso(notNull, call);
            return rule.Operator == "notContains" ? Expression.Not(guarded) : guarded;
        }

        if (rule.Operator == "between")
        {
            // A "between" with a missing bound is an open-ended range, not a comparison
            // against default(T). Dropping the absent term avoids a silent `member <= 0`
            // (or `member >= 0`) that would exclude almost everything. Both bounds missing
            // is an unconstrained range — true for every row.
            var hasLo = !IsBlank(rule.Value);
            var hasHi = !IsBlank(rule.Value2);
            Expression? lower = hasLo
                ? Expression.GreaterThanOrEqual(member, Expression.Constant(ConvertValue(rule.Value, underlying), memberType))
                : null;
            Expression? upper = hasHi
                ? Expression.LessThanOrEqual(member, Expression.Constant(ConvertValue(rule.Value2, underlying), memberType))
                : null;
            return (lower, upper) switch
            {
                ({ } lo, { } hi) => Expression.AndAlso(lo, hi),
                ({ } lo, null) => lo,
                (null, { } hi) => hi,
                _ => Expression.Constant(true)
            };
        }

        if (rule.Operator is "in" or "notIn")
        {
            var values = (rule.Value?.ToString() ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Expression? acc = null;
            foreach (var v in values)
            {
                var eq = Expression.Equal(member, Expression.Constant(ConvertValue(v, underlying), memberType));
                acc = acc is null ? eq : Expression.OrElse(acc, eq);
            }
            acc ??= Expression.Constant(false);
            return rule.Operator == "notIn" ? Expression.Not(acc) : acc;
        }

        var constant = Expression.Constant(ConvertValue(rule.Value, underlying), memberType);
        return rule.Operator switch
        {
            "=" => Expression.Equal(member, constant),
            "!=" => Expression.NotEqual(member, constant),
            "<" => Expression.LessThan(member, constant),
            "<=" => Expression.LessThanOrEqual(member, constant),
            ">" => Expression.GreaterThan(member, constant),
            ">=" => Expression.GreaterThanOrEqual(member, constant),
            _ => throw new InvalidOperationException($"Unsupported operator '{rule.Operator}'.")
        };
    }

    private static Expression EnsureString(Expression member) =>
        member.Type == typeof(string) ? member : Expression.Call(member, nameof(object.ToString), Type.EmptyTypes);

    /// <summary>A rule value is "blank" when it is null or an empty/whitespace string —
    /// i.e. the editor was left untouched. Such a bound is dropped rather than coerced to default(T).</summary>
    private static bool IsBlank(object? value) =>
        value is null || (value is string s && string.IsNullOrWhiteSpace(s));

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value is null) return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        if (targetType.IsInstanceOfType(value)) return value;

        var s = value as string ?? value.ToString();
        if (string.IsNullOrEmpty(s)) return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

        if (targetType.IsEnum) return Enum.Parse(targetType, s, ignoreCase: true);
        if (targetType == typeof(DateOnly)) return DateOnly.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        if (targetType == typeof(DateTime)) return DateTime.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        if (targetType == typeof(Guid)) return Guid.Parse(s);
        return Convert.ChangeType(s, targetType, System.Globalization.CultureInfo.InvariantCulture);
    }
}
