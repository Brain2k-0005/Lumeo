using Lumeo.Services;
using Xunit;

namespace Lumeo.Tests.Helpers;

/// <summary>
/// Parses the trim-safe interop payload of <c>registerPreventDefaultKeys</c> back into
/// <see cref="PreventDefaultKeyRule"/>s for assertions.
///
/// The interop layer deliberately serializes the rules as
/// <c>List&lt;Dictionary&lt;string, object?&gt;&gt;</c> instead of the record type: under a
/// trimmed publish the linker strips constructor parameter names (and removes the
/// record's reflection-only parameterless ctor), making JSRuntime's reflection
/// serializer throw <c>ConstructorContainsNullParameterNames</c> at runtime — this
/// crashed nearly every docs page live. Tests therefore capture dictionaries at the
/// bUnit JSInterop boundary; this helper restores the typed view so the existing
/// rule assertions stay type-safe and readable.
/// </summary>
public static class PreventDefaultRuleCapture
{
    public static IReadOnlyList<PreventDefaultKeyRule> Parse(object? argument)
    {
        var parsed = TryParse(argument);
        Assert.NotNull(parsed);
        return parsed!;
    }

    public static IReadOnlyList<PreventDefaultKeyRule>? TryParse(object? argument)
    {
        if (argument is not System.Collections.IEnumerable items || argument is string) return null;
        var rules = new List<PreventDefaultKeyRule>();
        foreach (var item in items)
        {
            if (item is not Dictionary<string, object?> d) return null;
            rules.Add(new PreventDefaultKeyRule(
                (string)d["key"]!,
                RequireNoModifiers: d.TryGetValue("requireNoModifiers", out var m) && m is true,
                SkipComposing: d.TryGetValue("skipComposing", out var c) && c is true,
                SkipEditable: d.TryGetValue("skipEditable", out var e) && e is true));
        }
        return rules;
    }
}
