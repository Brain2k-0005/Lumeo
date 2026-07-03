using System.Text;

namespace Lumeo.IconGen;

/// <summary>
/// The generic, pack-agnostic name + color transforms used to turn an upstream icon file into a
/// C# member. Pure and static so they are trivially unit-testable (Phase 1 wires tests to these).
/// </summary>
public static class NameTransform
{
    /// <summary>
    /// Turns an upstream kebab-case icon name (e.g. Lucide <c>trash-2</c>, Tabler <c>brand-github</c>)
    /// into a Blazicons-compatible PascalCase identifier (<c>Trash2</c>, <c>BrandGithub</c>): split on
    /// <c>-</c>, upper-case the first letter of each segment, concatenate (digits append directly, so
    /// <c>trash-2</c> → <c>Trash2</c> exactly as Blazicons produced). A result that would start with a
    /// digit (e.g. Tabler <c>123</c>) is prefixed with <c>_</c> to stay a valid C# identifier.
    /// </summary>
    public static string ToPascal(string kebab)
    {
        var sb = new StringBuilder(kebab.Length);
        foreach (var part in kebab.Split('-', StringSplitOptions.RemoveEmptyEntries))
        {
            sb.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1) sb.Append(part.AsSpan(1));
        }

        var id = sb.ToString();
        if (id.Length == 0) return id;
        return char.IsDigit(id[0]) ? "_" + id : id;
    }

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
