using Lumeo.Internal;

namespace Lumeo;

/// <summary>
/// Public conditional CSS class composition helper — Lumeo's equivalent of the
/// shadcn/ui <c>cn()</c> utility. Filters out null / whitespace entries, joins with
/// single spaces, and (via <see cref="Merge"/>) resolves Tailwind utility conflicts so
/// the last-wins. Use it in your own components built on top of Lumeo to merge a
/// component's base classes with a caller-supplied <c>Class</c> exactly the way Lumeo's
/// own components do. Lives in the root <c>Lumeo</c> namespace so it is in scope wherever
/// Lumeo components are.
/// </summary>
public static class Cx
{
    public static string Join(params string?[] parts)
    {
        if (parts is null || parts.Length == 0) return string.Empty;
        return string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    /// <summary>Returns <paramref name="value"/> when <paramref name="when"/> is true, otherwise null.</summary>
    public static string? When(bool when, string value) => when ? value : null;

    /// <summary>
    /// Joins the parts (like <see cref="Join"/>) then resolves Tailwind utility
    /// conflicts so that, for each conflict group, only the LAST occurrence in
    /// source order survives. Mirrors the npm <c>tailwind-merge</c> semantics
    /// closely enough for Lumeo's usage. Non-conflicting / unknown classes are
    /// always preserved in their original order.
    /// </summary>
    public static string Merge(params string?[] parts)
    {
        if (parts is null || parts.Length == 0) return string.Empty;

        // Flatten all parts into individual tokens.
        var tokens = new List<string>();
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            foreach (var tok in part.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                tokens.Add(tok);
        }
        if (tokens.Count == 0) return string.Empty;

        return TailwindMerge.Resolve(tokens);
    }
}
