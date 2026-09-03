using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;

namespace Lumeo;

/// <summary>How the rules of a <see cref="FilterGroup"/> combine.</summary>
public enum FilterCombinator { And, Or }

/// <summary>The kind of value a <see cref="FilterField"/> holds; picks the operator catalogue and the editor.</summary>
public enum FilterValueKind { Text, Number, Range, Date, Select, MultiSelect, Boolean }

/// <summary>How many values an operator takes: none (<c>empty</c>), one, many (<c>is any of</c>) or a range.</summary>
public enum FilterArity { None, One, Many, Range }

/// <summary>Why a query changed; carried by <see cref="FilterChangeDetails"/>.</summary>
public enum FilterChangeReason { Add, Update, Remove, Duplicate, Negate, Reorder, Combinator, Clear }

/// <summary>The step a rule being built or amended is at.</summary>
public enum FilterDraftStep { Field, Operator, Value }

/// <summary>Whether an editor is filling a brand-new rule (<c>Create</c>) or amending an existing one (<c>Amend</c>).</summary>
public enum FilterEditorHost { Create, Amend }

/// <summary>Why a rule or group is not ready to filter; see <see cref="FilterQueries.CollectIssues"/>.</summary>
public enum FilterIssueReason { MissingOperator, MissingValue, IncompleteRange, ReversedRange, EmptyGroup, Custom, UnknownField, UnknownOperator }

/// <summary>Which part of a row an issue points at.</summary>
public enum FilterIssueColumn { Operator, Value, Group, Field }

/// <summary>The two shapes of <see cref="Filters"/>: a chip row, or a nested condition builder.</summary>
public enum FiltersVariant { Basic, Advanced }

/// <summary>Where the advanced builder renders: behind a trigger in a popover, or inline in the page.</summary>
public enum FiltersAdvancedMode { Popover, Inline }

/// <summary>How selected options sort inside an option list when <see cref="FilterField.PinSelected"/> is on.</summary>
public enum FilterSortSelected { None, Label, Snapshot }

/// <summary>A node of the query tree: a <see cref="FilterRule"/> or a <see cref="FilterGroup"/>. The
/// tree round-trips through <c>System.Text.Json</c>: nodes carry a <c>$type</c> discriminator and a
/// rule's value comes back as the shape it went in (string, number, bool, list, range or date).</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(FilterRule), "rule")]
[JsonDerivedType(typeof(FilterGroup), "group")]
[JsonDerivedType(typeof(FilterQuery), "query")]
public abstract record FilterNode(string Id);

/// <summary>One condition: a field (as a path, so nested fields work), an operator and a value.
/// <see cref="Value"/> is a scalar (string, double, bool, DateOnly), an <c>IReadOnlyList&lt;string&gt;</c>
/// for a <see cref="FilterArity.Many"/> operator, or a <see cref="FilterRange"/> for a range.</summary>
public sealed record FilterRule(string Id, IReadOnlyList<string> Path, string Operator, [property: JsonConverter(typeof(FilterValueJsonConverter))] object? Value = null, bool Negated = false) : FilterNode(Id)
{
    /// <summary>The first path segment: the top-level field id.</summary>
    [JsonIgnore] public string Field => Path.Count > 0 ? Path[0] : "";

    /// <summary>The value as a list: empty for null, the list itself for a many-valued rule, the two
    /// bounds for a range, otherwise the one scalar.</summary>
    [JsonIgnore] public IReadOnlyList<object?> Values => FilterValues.AsList(Value);
}

/// <summary>Rules (and nested groups) joined by one combinator.</summary>
public record FilterGroup(string Id, FilterCombinator Combinator, IReadOnlyList<FilterNode> Rules) : FilterNode(Id);

/// <summary>The root of a filter tree: always a group. <see cref="FilterQueries"/> holds every
/// operation on it, <see cref="FilterQueries.Flatten"/> turns it into flat terms for an API.</summary>
public sealed record FilterQuery(string Id, FilterCombinator Combinator, IReadOnlyList<FilterNode> Rules) : FilterGroup(Id, Combinator, Rules)
{
    /// <summary>An empty <c>and</c> query with the id <c>root</c>.</summary>
    public static FilterQuery Empty() => new("root", FilterCombinator.And, Array.Empty<FilterNode>());

    /// <summary>A query holding the given rules.</summary>
    public static FilterQuery Of(params FilterNode[] rules) => new("root", FilterCombinator.And, rules);

    /// <summary>The complete rules as flat terms (field, operator, values, negated), ready for an API call.</summary>
    public IReadOnlyList<FilterTerm> Flatten() => FilterQueries.FlattenTerms(this);

    /// <summary>The number of rules in the tree.</summary>
    [JsonIgnore] public int Count => FilterQueries.Count(this);

    /// <summary>True when no rule exists.</summary>
    [JsonIgnore] public bool IsEmpty => Count == 0;
}

/// <summary>Reads a rule value back into the shape the bar works with: a string, a number, a bool,
/// a list of strings, a <see cref="FilterRange"/> (an object with <c>from</c>/<c>to</c>) or a
/// <see cref="FilterDateValue"/> (an object with <c>date</c>/<c>relative</c>). Writes whatever the
/// value is.</summary>
public sealed class FilterValueJsonConverter : JsonConverter<object?>
{
    public override bool HandleNull => true;

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => FromElement(JsonElement.ParseValue(ref reader), options);

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case null: writer.WriteNullValue(); return;
            // A bare day or instant would come back as a string; a tagged object keeps its type.
            case DateOnly day: writer.WriteStartObject(); writer.WriteString("$date", day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); writer.WriteEndObject(); return;
            case DateTime dt: writer.WriteStartObject(); writer.WriteString("$datetime", dt.ToString("o", CultureInfo.InvariantCulture)); writer.WriteEndObject(); return;
            case DateTimeOffset dto: writer.WriteStartObject(); writer.WriteString("$datetimeoffset", dto.ToString("o", CultureInfo.InvariantCulture)); writer.WriteEndObject(); return;
            default: JsonSerializer.Serialize(writer, value, value.GetType(), options); return;
        }
    }

    internal static object? FromElement(JsonElement e, JsonSerializerOptions options) => e.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Array => FromArray(e, options),
        JsonValueKind.Object => FromObject(e, options),
        _ => null,
    };

    private static object FromArray(JsonElement e, JsonSerializerOptions options)
    {
        var items = e.EnumerateArray().Select(x => FromElement(x, options)).ToList();
        return items.All(i => i is string) ? items.Cast<string>().ToList() : items;
    }

    private static object FromObject(JsonElement e, JsonSerializerOptions options)
    {
        JsonElement? Prop(string name)
        {
            foreach (var p in e.EnumerateObject())
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return p.Value;
            return null;
        }
        if (Prop("$date") is { } day && DateOnly.TryParseExact(day.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDay)) return parsedDay;
        if (Prop("$datetime") is { } instant && DateTime.TryParse(instant.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedInstant)) return parsedInstant;
        if (Prop("$datetimeoffset") is { } offset && DateTimeOffset.TryParse(offset.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedOffset)) return parsedOffset;
        if (Prop("from") is not null || Prop("to") is not null)
            return new FilterRange(Prop("from") is { } f ? FromElement(f, options) : null, Prop("to") is { } t ? FromElement(t, options) : null);
        if (Prop("date") is not null || Prop("relative") is not null)
            return JsonSerializer.Deserialize<FilterDateValue>(e.GetRawText(), options) ?? new FilterDateValue();
        return e.Clone();
    }
}

/// <summary>The two bounds of a range value; either may be null while the user is still typing.</summary>
public sealed record FilterRange(object? From, object? To);

/// <summary>A complete rule flattened for an API: the field path, the operator and its values.</summary>
public sealed record FilterTerm(IReadOnlyList<string> Path, string Field, string Operator, IReadOnlyList<object?> Values, bool Negated);

/// <summary>An option of a select or multiselect field.</summary>
public sealed record FilterChoice(string Value, string Label)
{
    public RenderFragment? Icon { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string>? Keywords { get; init; }
    public bool Disabled { get; init; }
    /// <summary>An option that cannot be combined with the others ("none of the above"): picking it
    /// clears every other pick, and picking another clears it.</summary>
    public bool Exclusive { get; init; }
    public object? Data { get; init; }
}

/// <summary>An operator a field offers. <see cref="Inverse"/> names the operator that negation
/// swaps to (<c>is</c> to <c>is_not</c>); without one, negation sets <see cref="FilterRule.Negated"/>.</summary>
public sealed record FilterOperatorDef(string Value, string Label, FilterArity Arity = FilterArity.One, string? Inverse = null, bool Hidden = false);

/// <summary>What an async option loader receives: the search text, the cursor of the page after the
/// last one, and a token that cancels a superseded request.</summary>
public sealed record FilterLoadRequest(string Query, string? Cursor, CancellationToken CancellationToken);

/// <summary>What an async option loader returns. <see cref="HasMore"/> defaults to whether a
/// <see cref="NextCursor"/> was returned.</summary>
public sealed record FilterLoadResult(IReadOnlyList<FilterChoice> Items, string? NextCursor = null, bool? HasMore = null);

/// <summary>An attribute the user can filter by.</summary>
public sealed class FilterField
{
    /// <summary>The id, unique among its siblings; the rule stores it in its path.</summary>
    public required string Id { get; init; }
    /// <summary>What the field list and the chip show.</summary>
    public required string Label { get; init; }
    public RenderFragment? Icon { get; init; }
    public string? Description { get; init; }
    /// <summary>Extra words the field-list search matches on.</summary>
    public IReadOnlyList<string>? Keywords { get; init; }
    /// <summary>A count shown next to a branch in the field list; defaults to the number of children.</summary>
    public int? Count { get; init; }
    /// <summary>Child fields: the field becomes a branch the picker drills into.</summary>
    public IReadOnlyList<FilterField>? Fields { get; init; }
    /// <summary>Whether a branch can itself be picked as a field.</summary>
    public bool Selectable { get; init; }
    public bool Disabled { get; init; }
    /// <summary>The value kind; picks the operator catalogue and the editor. Defaults to text.</summary>
    public FilterValueKind Kind { get; init; } = FilterValueKind.Text;
    /// <summary>Static options of a select or multiselect field.</summary>
    public IReadOnlyList<FilterChoice>? Options { get; init; }
    /// <summary>Async options: called with the search text (debounced) and a cursor for the next page.</summary>
    public Func<FilterLoadRequest, Task<FilterLoadResult>>? LoadOptions { get; init; }
    /// <summary>Resolves stored values to options, so a chip restored from a saved query shows labels
    /// before the list was ever opened.</summary>
    public Func<IReadOnlyList<string>, Task<IReadOnlyList<FilterChoice>>>? ResolveValues { get; init; }
    /// <summary>Operators replacing the catalogue for this field.</summary>
    public IReadOnlyList<FilterOperatorDef>? Operators { get; init; }
    /// <summary>The operator a new rule starts with; defaults to the first visible one.</summary>
    public string? DefaultOperator { get; init; }
    /// <summary>The key of a registered editor to use instead of the one the kind implies.</summary>
    public string? Editor { get; init; }
    /// <summary>A custom editor for this field alone.</summary>
    public RenderFragment<FilterEditorContext>? EditorTemplate { get; init; }
    /// <summary>Custom rendering of the value segment.</summary>
    public RenderFragment<FilterValueContext>? ValueTemplate { get; init; }
    /// <summary>Custom text for the value segment (and its accessible name).</summary>
    public Func<FilterValueContext, string>? ValueText { get; init; }
    /// <summary>Placeholder of the value editor, or of the option search.</summary>
    public string? Placeholder { get; init; }
    /// <summary>Whether the option list shows a search box. Defaults to true.</summary>
    public bool Searchable { get; init; } = true;
    /// <summary>Keep the selected options at the top of the list.</summary>
    public bool PinSelected { get; init; }
    public FilterSortSelected SortSelected { get; init; } = FilterSortSelected.None;
    /// <summary>Extra validation: return a message to flag the rule, null to accept.</summary>
    public Func<FilterValidateContext, string?>? Validate { get; init; }
    /// <summary>Classes merged onto the editor panel (a width, typically).</summary>
    public string? Class { get; init; }
    public object? Data { get; init; }

    /// <summary>True when the field has child fields.</summary>
    public bool IsBranch => Fields is { Count: > 0 };
    /// <summary>True when the field can be the target of a rule.</summary>
    public bool IsSelectable => !Disabled && (!IsBranch || Selectable);
    /// <summary>True for a leaf the picker can commit directly.</summary>
    public bool IsPickable => !Disabled && !IsBranch;
    /// <summary>True when the field's values are backed by options, static or loaded.</summary>
    public bool HasOptions => Options is { Count: > 0 } || LoadOptions is not null;
}

/// <summary>Why and what changed, handed to <c>OnQueryChanged</c> and <c>OnBeforeQueryChange</c>.</summary>
public sealed record FilterChangeDetails(FilterChangeReason Reason, FilterRule? Rule, FilterField? Field);

/// <summary>The payload of <see cref="Filters.OnQueryChanged"/>: the new query and why it changed.</summary>
public sealed record FilterQueryChange(FilterQuery Query, FilterChangeDetails Details);

/// <summary>A rule or group that is not ready to filter.</summary>
public sealed record FilterIssue(string NodeId, FilterIssueColumn Column, FilterIssueReason Reason, string? Message = null);

/// <summary>What a value renderer or <see cref="FilterField.ValueText"/> receives.</summary>
public sealed record FilterValueContext(object? Value, IReadOnlyList<object?> Values, FilterField Field, FilterOperatorDef Operator, IReadOnlyList<FilterChoice> Options, FilterLabels Labels);

/// <summary>What <see cref="FilterField.Validate"/> receives.</summary>
public sealed record FilterValidateContext(object? Value, IReadOnlyList<object?> Values, FilterField Field, FilterOperatorDef Operator, FilterArity Arity, FilterRule Rule, FilterLabels Labels);

/// <summary>Helpers for the loosely typed rule value.</summary>
public static class FilterValues
{
    /// <summary>The value as a list: empty for null or "", the list for many, both bounds for a range, else the scalar.</summary>
    public static IReadOnlyList<object?> AsList(object? value) => value switch
    {
        null => Array.Empty<object?>(),
        "" => Array.Empty<object?>(),
        FilterRange r => new[] { r.From, r.To },
        string s => new object?[] { s },
        IReadOnlyList<string> many => many.Cast<object?>().ToList(),
        IReadOnlyList<object?> list => list,
        System.Collections.IEnumerable e and not string => e.Cast<object?>().ToList(),
        _ => new[] { value },
    };

    /// <summary>True for null, an empty string or an empty list.</summary>
    public static bool IsBlank(object? value) => value is null || value is "" || (value is System.Collections.IEnumerable e and not string && !e.Cast<object?>().Any());

    /// <summary>The value as strings, for option lookups.</summary>
    public static IReadOnlyList<string> AsStrings(object? value) => AsList(value).Where(v => v is not null).Select(v => v!.ToString() ?? "").ToList();

    /// <summary>Reshapes a value for a new operator's arity: many becomes its first entry for one,
    /// one becomes a list for many, a range keeps its bounds; a no-value operator drops it.</summary>
    public static object? Coerce(object? value, FilterArity from, FilterArity to)
    {
        if (to == FilterArity.None) return null;
        if (value is null) return null;
        if (from == to) return value;
        var list = AsList(value);
        return to switch
        {
            FilterArity.Many => list.Where(v => v is not null).Select(v => v!.ToString()!).ToList(),
            FilterArity.One => list.Count > 0 ? list[0] : null,
            FilterArity.Range => new FilterRange(list.Count > 0 ? list[0] : null, list.Count > 1 ? list[1] : null),
            _ => value,
        };
    }

    /// <summary>Compares two range bounds when both are numbers, dates or date strings; null when they are not comparable.</summary>
    public static int? CompareBounds(object? from, object? to)
    {
        if (from is null || to is null) return null;
        if (TryNumber(from, out var a) && TryNumber(to, out var b)) return a.CompareTo(b);
        if (TryDate(from, out var da) && TryDate(to, out var db)) return da.CompareTo(db);
        return null;
    }

    private static bool TryNumber(object value, out double number)
    {
        switch (value)
        {
            case double d: number = d; return true;
            case int i: number = i; return true;
            case long l: number = l; return true;
            case decimal m: number = (double)m; return true;
            case float f: number = f; return true;
            case string s when double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p): number = p; return true;
            default: number = 0; return false;
        }
    }

    private static bool TryDate(object value, out DateTime date)
    {
        switch (value)
        {
            case DateOnly d: date = d.ToDateTime(TimeOnly.MinValue); return true;
            case DateTime dt: date = dt; return true;
            case DateTimeOffset dto: date = dto.DateTime; return true;
            case FilterDateValue fd when fd.Resolve() is { } r: date = r.ToDateTime(TimeOnly.MinValue); return true;
            case string s when DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var p): date = p; return true;
            default: date = default; return false;
        }
    }
}
