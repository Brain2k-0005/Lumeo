namespace Lumeo.Internal;

/// <summary>
/// Conditional CSS class composition helper. Filters out null / whitespace
/// entries and joins with single spaces. Use in new components instead of
/// hand-rolled <c>string.Join(" ", new[] {...}.Where(...))</c> blocks.
/// </summary>
internal static class Cx
{
    public static string Join(params string?[] parts)
    {
        if (parts is null || parts.Length == 0) return string.Empty;
        return string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    /// <summary>Returns <paramref name="value"/> when <paramref name="when"/> is true, otherwise null.</summary>
    public static string? When(bool when, string value) => when ? value : null;
}
