using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lumeo;

/// <summary>Boolean combinator for a <see cref="QueryGroup"/>.</summary>
[JsonConverter(typeof(QueryCombinatorJsonConverter))]
public enum QueryCombinator
{
    And,
    Or
}

// Hand-written string<->enum converter (kept trivial and reflection-free) rather than the
// framework JsonStringEnumConverter, whose non-generic form isn't guaranteed trim-clean and
// whose generic form (JsonStringEnumConverter<TEnum>) isn't available on the net8.0 TFM this
// assembly also targets. Enum.TryParse<TEnum>/.ToString() with a closed, compile-time-known
// TEnum are trim-safe (#354).
internal sealed class QueryCombinatorJsonConverter : JsonConverter<QueryCombinator>
{
    public override QueryCombinator Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // reader.GetString() throws InvalidOperationException (not JsonException) for a
        // non-string token such as {"combinator":0,...}. FromJson only catches JsonException,
        // so an InvalidOperationException would escape past its documented
        // return-null-on-parse-failure contract instead of being treated as an invalid query.
        // Reject non-string tokens with JsonException up front so every malformed shape takes
        // the same documented path (#364 review).
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Invalid QueryCombinator token '{reader.TokenType}'; expected a string.");

        var s = reader.GetString();
        // FromJson is documented to return null on parse failure (catches JsonException).
        // Silently coercing an invalid/corrupted combinator to And would instead hand back
        // a *different* query than the one that was saved, so an unrecognized value must
        // fail the parse rather than default (#364 review).
        return Enum.TryParse<QueryCombinator>(s, ignoreCase: true, out var value)
            ? value
            : throw new JsonException($"Invalid QueryCombinator value '{s}'.");
    }

    public override void Write(Utf8JsonWriter writer, QueryCombinator value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}

// QueryRule.Value/Value2 are declared `object?` — a genuinely open slot a consumer can box
// anything into. Relying on System.Text.Json's built-in polymorphic `object` resolution (even
// with a reflection-based resolver chained after the source-gen context) proved unreliable in
// this exact combination (source-gen context + List<T> of a polymorphic base + an `object`
// member): it kept resolving strictly against the context's own closed type list regardless of
// what was chained after it. Handling the (small, well-known) set of leaf value shapes
// explicitly — the same string/double/bool the built-in editors produce, plus every other
// framework numeric type a consumer might reasonably box in directly — sidesteps type-info
// resolution for `object` entirely: no reflection, no chain, no resolver ambiguity (#354).
internal sealed class QueryValueJsonConverter : JsonConverter<object?>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => ReadNumber(ref reader),
            _ => throw new JsonException($"Unsupported QueryRule value token '{reader.TokenType}'."),
        };

    // GetDouble() alone rounds anything above 2^53 (long IDs) or high-precision decimals
    // (money amounts) before ToExpression ever sees them, silently corrupting the saved
    // value. Try the narrowest exact representation first — Int64 for whole numbers,
    // Decimal for fixed-point fractional values — and only fall back to Double for
    // magnitudes/precision neither can hold (e.g. very large exponents) (#364 review).
    private static object ReadNumber(ref Utf8JsonReader reader)
    {
        if (reader.TryGetInt64(out var l)) return l;
        if (reader.TryGetDecimal(out var m)) return m;
        return reader.GetDouble();
    }

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case null: writer.WriteNullValue(); break;
            case string s: writer.WriteStringValue(s); break;
            case bool b: writer.WriteBooleanValue(b); break;
            case double d: writer.WriteNumberValue(d); break;
            case float f: writer.WriteNumberValue(f); break;
            case int i: writer.WriteNumberValue(i); break;
            case long l: writer.WriteNumberValue(l); break;
            case short sh: writer.WriteNumberValue(sh); break;
            case decimal m: writer.WriteNumberValue(m); break;
            // DateOnly/DateTime.ToString() is culture-sensitive (e.g. "12.07.2026" under
            // de-DE), but ConvertValue parses the reloaded string back with
            // CultureInfo.InvariantCulture. Under a non-invariant culture that mismatch
            // makes FromJson -> ToExpression fail to reload the query or compare against
            // the wrong date. Round-trip format ("O") is both invariant and exactly what
            // DateOnly/DateTime.Parse(..., InvariantCulture) expects (#364 review).
            case DateOnly dOnly: writer.WriteStringValue(dOnly.ToString("O", System.Globalization.CultureInfo.InvariantCulture)); break;
            case DateTime dt: writer.WriteStringValue(dt.ToString("O", System.Globalization.CultureInfo.InvariantCulture)); break;
            default:
                // Outside the framework scalar types above, fall back to ToString() — the same
                // coercion ConvertValue (the LINQ-expression side of this same value) already
                // applies to anything that isn't one of its recognized target types.
                writer.WriteStringValue(value.ToString());
                break;
        }
    }
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
    [JsonPropertyName("field")] public string Field { get; set; } = "";

    /// <summary>One of the field's allowed operator keys.</summary>
    [JsonPropertyName("operator")] public string Operator { get; set; } = "";

    /// <summary>The comparison value. Type depends on the field; for "between"-style
    /// operators this holds the lower bound and <see cref="Value2"/> holds the upper.</summary>
    [JsonPropertyName("value"), JsonConverter(typeof(QueryValueJsonConverter))]
    public object? Value { get; set; }

    /// <summary>The second comparison value, used only by range operators ("between").</summary>
    [JsonPropertyName("value2"), JsonConverter(typeof(QueryValueJsonConverter))]
    public object? Value2 { get; set; }
}

/// <summary>A group: a <see cref="QueryCombinator"/> applied across a list of child
/// <see cref="QueryNode"/>s (rules and/or nested groups).</summary>
public sealed class QueryGroup : QueryNode
{
    [JsonPropertyName("combinator")] public QueryCombinator Combinator { get; set; } = QueryCombinator.And;

    [JsonPropertyName("rules")] public List<QueryNode> Rules { get; set; } = new();

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

// Self-contained source-generated JSON metadata for the QueryGroup/QueryRule/QueryNode tree —
// deliberately NOT the shared Lumeo.Serialization.LumeoJsonContext. `lumeo add query-builder`
// vendors only this file's three UI/QueryBuilder/* files into the consumer's own project (see
// the query-builder registry entry); LumeoJsonContext is `internal` to the Lumeo assembly, so a
// vendored reference to it cannot compile — neither in NuGet-package mode (the consumer has no
// InternalsVisibleTo grant into Lumeo.dll) nor in standalone/eject mode (Serialization/ isn't
// part of the vendored runtime closure either). Keeping the context in the SAME file that gets
// vendored + namespace-rewritten means it always compiles standalone, in the package, and
// ejected alike (#364 review).
[JsonSerializable(typeof(QueryGroup))]
[JsonSerializable(typeof(QueryRule))]
[JsonSerializable(typeof(QueryNode))]
internal sealed partial class QueryBuilderJsonContext : JsonSerializerContext
{
}

/// <summary>Serialization + LINQ helpers for <see cref="QueryGroup"/> trees.</summary>
public static class QueryBuilderSerialization
{
    // Two lightweight wrappers around the SAME source-generated shape (QueryBuilderJsonContext),
    // one per direction so WriteIndented (pretty JSON preview) / PropertyNameCaseInsensitive
    // (tolerant reads) can differ without touching Default's own options. No reflection
    // resolver needed here: QueryRule.Value/Value2 (the only genuinely open members) carry an
    // explicit [JsonConverter(typeof(QueryValueJsonConverter))], so nothing in this tree ever
    // needs a runtime Type -> JsonTypeInfo lookup outside the compiled shape (#354).
    private static readonly QueryBuilderJsonContext WriteContext = new(
        new JsonSerializerOptions(QueryBuilderJsonContext.Default.Options) { WriteIndented = true });

    private static readonly QueryBuilderJsonContext ReadContext = new(
        new JsonSerializerOptions(QueryBuilderJsonContext.Default.Options) { PropertyNameCaseInsensitive = true });

    public static string ToJson(QueryGroup query) =>
        JsonSerializer.Serialize(query, WriteContext.QueryGroup);

    public static QueryGroup? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize(json, ReadContext.QueryGroup);
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
    public static Expression<Func<T, bool>>? ToExpression<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(QueryGroup query)
    {
        var param = Expression.Parameter(typeof(T), "x");
        var body = BuildGroup<T>(query, param);
        return body is null ? null : Expression.Lambda<Func<T, bool>>(body, param);
    }

    private static Expression? BuildGroup<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(QueryGroup group, ParameterExpression param)
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

    private static Expression? BuildRule<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(QueryRule rule, ParameterExpression param)
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

    // typeof(object).GetMethod(...) resolves against a compile-time-known type, so — unlike
    // the string-methodName Expression.Call overload this replaced — this doesn't need
    // RequiresUnreferencedCode: the trimmer preserves object.ToString() unconditionally.
    private static readonly System.Reflection.MethodInfo ObjectToStringMethod =
        typeof(object).GetMethod(nameof(object.ToString), Type.EmptyTypes)!;

    private static Expression EnsureString(Expression member) =>
        member.Type == typeof(string) ? member : Expression.Call(member, ObjectToStringMethod);

    /// <summary>A rule value is "blank" when it is null or an empty/whitespace string —
    /// i.e. the editor was left untouched. Such a bound is dropped rather than coerced to default(T).</summary>
    private static bool IsBlank(object? value) =>
        value is null || (value is string s && string.IsNullOrWhiteSpace(s));

    // targetType is always a bound property's type discovered via BuildRule<T>'s
    // DAM(PublicProperties)-preserved T, narrowed to the framework value types QueryBuilder's
    // field editors actually produce (see QueryBuilderGroup.razor: bool/double/string plus,
    // via QueryFieldType.Date, DateOnly/DateTime) or an enum. Activator.CreateInstance(Type)
    // here only ever targets one of those — all framework-preserved regardless of trimming —
    // so this is safe even though the trimmer can't prove it statically (#354).
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification =
        "targetType is a value type resolved from a DAM(PublicProperties)-preserved TItem " +
        "(bool/double/DateOnly/DateTime/Guid/enum/etc.) — see comment above.")]
    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value is null) return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        if (targetType.IsInstanceOfType(value)) return value;

        // value.ToString() (no format/provider) is culture-sensitive for IFormattable sources —
        // e.g. a decimal 1.5m (the exact-precision branch QueryValueJsonConverter.ReadNumber now
        // returns for fractional numbers) renders as "1,5" under de-DE. The InvariantCulture parse
        // below then misreads that comma as a thousands separator ("1,5" -> 15) instead of a
        // decimal point. Format IFormattable values invariantly up front so this string hop never
        // depends on CurrentCulture (#364 review).
        var s = value as string ?? (value is IFormattable f ? f.ToString(null, System.Globalization.CultureInfo.InvariantCulture) : value.ToString());
        if (string.IsNullOrEmpty(s)) return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

        if (targetType.IsEnum) return Enum.Parse(targetType, s, ignoreCase: true);
        if (targetType == typeof(DateOnly)) return DateOnly.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        // RoundtripKind preserves the Utc/Local/Unspecified Kind a "O"-format string carries
        // (Z or a numeric offset). Without it, DateTime.Parse silently converts an offset-bearing
        // string to local time, so a query saved with a UTC DateTime reloads shifted by the
        // client's timezone and compares against the wrong instant (#364 review).
        if (targetType == typeof(DateTime)) return DateTime.Parse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
        if (targetType == typeof(Guid)) return Guid.Parse(s);
        return Convert.ChangeType(s, targetType, System.Globalization.CultureInfo.InvariantCulture);
    }
}
