using System.Text;
using System.Text.RegularExpressions;

namespace Lumeo.Docs.Services;

/// <summary>Result of parsing a tweakcn / shadcn CSS export.</summary>
public sealed record ThemeParseResult(
    IReadOnlyDictionary<string, string> Light,
    IReadOnlyDictionary<string, string> Dark,
    IReadOnlyList<string> Warnings)
{
    public bool IsEmpty => Light.Count == 0 && Dark.Count == 0;
}

/// <summary>
/// Extracts the <c>--name: value;</c> pairs of the <c>:root</c> and <c>.dark</c> blocks from a pasted
/// tweakcn / shadcn export. Comments, <c>@layer base</c> / <c>@media</c> wrappers and unrelated rules are
/// tolerated; <c>@theme</c> blocks are skipped (they only alias the same variables). Pure and
/// client-side, no I/O.
/// </summary>
public static class ThemePlaygroundParser
{
    /// <summary>Colour tokens Lumeo maps through <c>--color-*</c> (shadcn name to Lumeo token).</summary>
    public static readonly IReadOnlySet<string> ColorTokens = new HashSet<string>(StringComparer.Ordinal)
    {
        "background", "foreground", "card", "card-foreground", "popover", "popover-foreground",
        "primary", "primary-foreground", "secondary", "secondary-foreground", "muted", "muted-foreground",
        "accent", "accent-foreground", "destructive", "destructive-foreground", "border", "input", "ring",
        "chart-1", "chart-2", "chart-3", "chart-4", "chart-5",
        "sidebar", "sidebar-foreground", "sidebar-primary", "sidebar-primary-foreground",
        "sidebar-accent", "sidebar-accent-foreground", "sidebar-border", "sidebar-ring",
    };

    private static readonly HashSet<string> OtherReadTokens = new(StringComparer.Ordinal)
    {
        "radius", "spacing", "font-sans", "font-mono", "font-serif",
        "shadow-2xs", "shadow-xs", "shadow-sm", "shadow", "shadow-md", "shadow-lg", "shadow-xl", "shadow-2xl",
    };

    public static bool IsRead(string name) => ColorTokens.Contains(name) || OtherReadTokens.Contains(name);

    private static readonly Regex Decl = new(@"--([A-Za-z0-9_-]+)\s*:\s*([^;]+?)\s*(?:;|$)", RegexOptions.Compiled);

    public static ThemeParseResult Parse(string? css)
    {
        var light = new Dictionary<string, string>(StringComparer.Ordinal);
        var dark = new Dictionary<string, string>(StringComparer.Ordinal);
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(css))
            return new ThemeParseResult(light, dark, warnings);

        Walk(StripComments(css), light, dark);

        var names = light.Keys.Concat(dark.Keys).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
        foreach (var n in names)
        {
            if (IsRead(n)) continue;
            warnings.Add(Explain(n));
        }
        return new ThemeParseResult(light, dark, warnings);
    }

    private static string Explain(string name)
    {
        if (name.StartsWith("tracking", StringComparison.Ordinal) || name == "letter-spacing")
            return $"--{name}: Lumeo reads --tracking-tight / -wide / ... by name, not tweakcn's --tracking-normal, so tracking is not applied.";
        if (name is "radius-sm" or "radius-md" or "radius-lg" or "radius-xl")
            return $"--{name}: ignored, Lumeo derives it from --radius. Set only --radius.";
        if (name.StartsWith("shadow-", StringComparison.Ordinal))
            return $"--{name}: not read by Lumeo (only the full --shadow-2xs ... --shadow-2xl values are).";
        if (name.StartsWith("color-", StringComparison.Ordinal))
            return $"--{name}: Tailwind alias, not needed; the unprefixed shadcn name is what Lumeo reads.";
        if (name.StartsWith("font-", StringComparison.Ordinal))
            return $"--{name}: Lumeo only reads --font-sans, --font-mono and --font-serif.";
        return $"--{name}: not read by Lumeo, ignored.";
    }

    private static string StripComments(string css)
    {
        var sb = new StringBuilder(css.Length);
        for (int i = 0; i < css.Length; i++)
        {
            if (css[i] == '/' && i + 1 < css.Length && css[i + 1] == '*')
            {
                int end = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0) break;
                i = end + 1;
                continue;
            }
            sb.Append(css[i]);
        }
        return sb.ToString();
    }

    /// <summary>Walks rules at one nesting level; recurses into @layer/@media/@supports, skips @theme.</summary>
    private static void Walk(string css, Dictionary<string, string> light, Dictionary<string, string> dark)
    {
        int i = 0;
        while (i < css.Length)
        {
            int open = css.IndexOf('{', i);
            if (open < 0) return;
            int semi = css.IndexOf(';', i);
            if (semi >= 0 && semi < open) { i = semi + 1; continue; } // @import ...;
            var prelude = css.Substring(i, open - i).Trim();
            int close = MatchBrace(css, open);
            var body = css.Substring(open + 1, close - open - 1);
            i = close + 1;

            if (prelude.StartsWith('@'))
            {
                if (prelude.StartsWith("@theme", StringComparison.OrdinalIgnoreCase)) continue;
                if (prelude.StartsWith("@layer", StringComparison.OrdinalIgnoreCase)
                    || prelude.StartsWith("@media", StringComparison.OrdinalIgnoreCase)
                    || prelude.StartsWith("@supports", StringComparison.OrdinalIgnoreCase))
                    Walk(body, light, dark);
                continue;
            }

            foreach (var sel in prelude.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var target = sel switch
                {
                    ":root" or "html" or ":host" => light,
                    ".dark" or "html.dark" or ":root.dark" or ".dark:root" => dark,
                    _ => null,
                };
                if (target is null) continue;
                foreach (Match m in Decl.Matches(body))
                    target[m.Groups[1].Value] = m.Groups[2].Value.Trim();
            }
        }
    }

    private static int MatchBrace(string s, int open)
    {
        int depth = 0;
        for (int i = open; i < s.Length; i++)
        {
            if (s[i] == '{') depth++;
            else if (s[i] == '}' && --depth == 0) return i;
        }
        return s.Length; // unterminated: take the rest
    }

    /// <summary>
    /// Inline style for the preview container. Lumeo's <c>--color-*</c> tokens are computed at
    /// <c>:root</c> from <c>var(--primary, ...)</c>, so a bare scoped <c>--primary</c> never reaches the
    /// components; the matching <c>--color-*</c> (and the derived radius scale) are declared here too.
    /// </summary>
    public static string ToScopedStyle(IReadOnlyDictionary<string, string> vars)
    {
        var sb = new StringBuilder();
        foreach (var (name, value) in vars)
        {
            if (!IsRead(name)) continue;
            sb.Append("--").Append(name).Append(':').Append(Sanitize(value)).Append(';');
            if (ColorTokens.Contains(name))
                sb.Append("--color-").Append(name).Append(':').Append(Sanitize(value)).Append(';');
        }
        if (vars.ContainsKey("radius"))
            sb.Append("--radius-sm:calc(var(--radius) - 4px);--radius-md:calc(var(--radius) - 2px);--radius-lg:var(--radius);--radius-xl:calc(var(--radius) + 4px);--lumeo-button-radius:var(--radius-md);");
        if (vars.TryGetValue("font-sans", out var f))
            sb.Append("font-family:").Append(Sanitize(f)).Append(';');
        return sb.ToString();
    }

    // The value lands in a style attribute; it cannot end a declaration or open a rule.
    private static string Sanitize(string v) => v.Replace(";", "").Replace("{", "").Replace("}", "").Replace("<", "");

    /// <summary>Unlayered <c>:root{}</c> / <c>.dark{}</c> block of the read variables, ready to paste.</summary>
    public static string ToOverrideCss(ThemeParseResult r)
    {
        var sb = new StringBuilder();
        Block(sb, ":root", r.Light);
        if (r.Dark.Count > 0) { if (sb.Length > 0) sb.Append('\n'); Block(sb, ".dark", r.Dark); }
        return sb.ToString();
    }

    private static void Block(StringBuilder sb, string sel, IReadOnlyDictionary<string, string> vars)
    {
        var read = vars.Where(kv => IsRead(kv.Key)).ToList();
        if (read.Count == 0) return;
        sb.Append(sel).Append(" {\n");
        foreach (var (k, v) in read) sb.Append("  --").Append(k).Append(": ").Append(v).Append(";\n");
        sb.Append("}\n");
    }
}
