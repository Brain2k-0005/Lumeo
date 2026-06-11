namespace Lumeo.Internal;

/// <summary>
/// Generates per-instance DOM ids using the convention <c>"{prefix}-{guidN}"</c>.
/// Centralised so the prefix scheme stays consistent and IDs are greppable
/// in browser devtools.
/// </summary>
internal static class LumeoIds
{
    public static string New(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    /// <summary>
    /// Returns the id that will actually land on the element: a consumer-supplied
    /// <c>id</c> in the splatted <paramref name="additionalAttributes"/> wins over
    /// the generated <paramref name="fallback"/>, because <c>@attributes</c> renders
    /// AFTER an explicit <c>id="@fallback"</c>. JS interop that targets the element
    /// (focus, click-outside, key suppression) must look it up by THIS id, or the
    /// handler silently attaches to a non-existent element.
    /// </summary>
    public static string Effective(IReadOnlyDictionary<string, object>? additionalAttributes, string fallback)
        => additionalAttributes is not null
           && additionalAttributes.TryGetValue("id", out var v)
           && v is string s && !string.IsNullOrEmpty(s)
            ? s
            : fallback;
}
