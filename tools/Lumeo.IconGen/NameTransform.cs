using System.Text;

namespace Lumeo.IconGen;

/// <summary>
/// The generic, pack-agnostic name + color transforms used to turn an upstream icon file into a
/// C# member. Pure and static so they are trivially unit-testable (Phase 1 wires tests to these).
/// </summary>
public static class NameTransform
{
    // Segment separators recognised when PascalCasing an upstream name. Hyphen covers Lucide/Tabler/
    // Phosphor/… kebab-case; underscore covers Material Symbols (<c>account_circle</c>) and Fluent
    // (<c>access_time</c>). No existing hyphen pack carries underscores, so adding it is additive.
    private static readonly char[] Separators = { '-', '_' };

    /// <summary>
    /// Turns an upstream kebab-/snake-case icon name (e.g. Lucide <c>trash-2</c>, Tabler
    /// <c>brand-github</c>, Material <c>account_circle</c>) into a Blazicons-compatible PascalCase
    /// identifier (<c>Trash2</c>, <c>BrandGithub</c>, <c>AccountCircle</c>): split on <c>-</c>/<c>_</c>,
    /// upper-case the first letter of each segment, concatenate (digits append directly, so
    /// <c>trash-2</c> → <c>Trash2</c> exactly as Blazicons produced). A result that would start with a
    /// digit (e.g. Tabler <c>123</c>, Material <c>3d_rotation</c>) is prefixed with <c>_</c> to stay a
    /// valid C# identifier.
    /// </summary>
    public static string ToPascal(string kebab)
    {
        var sb = new StringBuilder(kebab.Length);
        foreach (var part in kebab.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            sb.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1) sb.Append(part.AsSpan(1));
        }

        var id = sb.ToString();
        if (id.Length == 0) return id;
        return char.IsDigit(id[0]) ? "_" + id : id;
    }

    /// <summary>
    /// Strips a trailing <c>-{suffix}</c> weight marker from an upstream Phosphor file name so the
    /// generated member carries only the base name (the weight lives in the class). For example
    /// <c>StripSuffix("house-duotone", "duotone")</c> → <c>house</c>. The suffix is removed only when
    /// it appears as a whole trailing hyphen-delimited segment; names that merely end in the letters
    /// (there are none in Phosphor, but the guard keeps it safe) are returned unchanged.
    /// </summary>
    public static string StripSuffix(string name, string suffix)
    {
        var tail = "-" + suffix;
        return name.EndsWith(tail, StringComparison.Ordinal) ? name[..^tail.Length] : name;
    }

    /// <summary>
    /// Strips a literal trailing <paramref name="suffix"/> from an upstream file name (no hyphen is
    /// implied, unlike <see cref="StripSuffix"/>). Used for Fluent's size/style marker, where the
    /// 24px cut is selected and the whole <c>_24_regular</c> / <c>_24_filled</c> tail is removed so
    /// <c>access_time_24_regular</c> and <c>access_time_24_filled</c> both yield base <c>access_time</c>
    /// on the <c>Fluent</c> / <c>FluentFilled</c> classes. Non-matching names pass through unchanged.
    /// </summary>
    public static string StripExact(string name, string suffix)
        => name.EndsWith(suffix, StringComparison.Ordinal) ? name[..^suffix.Length] : name;

    // Values that are already renderer-neutral and must NOT be rewritten to currentColor.
    private static readonly HashSet<string> NeutralColors = new(StringComparer.OrdinalIgnoreCase)
    {
        "none", "currentcolor", "transparent", "inherit", "context-fill", "context-stroke",
    };

    /// <summary>
    /// Scrubs a literal <c>fill</c>/<c>stroke</c> value to <c>currentColor</c> so the icon inherits
    /// the ambient text color. Renderer-neutral keywords (<c>none</c>, <c>transparent</c>, …) and
    /// values already set to <c>currentColor</c> pass through untouched, as do empty values.
    /// </summary>
    public static string ScrubColor(string value)
    {
        var v = value.Trim();
        if (v.Length == 0 || NeutralColors.Contains(v)) return value;
        return "currentColor";
    }
}
