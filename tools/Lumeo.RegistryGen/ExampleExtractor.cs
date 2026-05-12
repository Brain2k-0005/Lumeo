using System.Text.RegularExpressions;

namespace Lumeo.RegistryGen;

/// <summary>
/// Scrapes the docs site's component pages for the working Razor snippets
/// that sit behind each <c>&lt;ComponentDemo Title="..." Code="@_fooCode"&gt;</c>
/// and the matching <c>private string _fooCode = @"...";</c> field, so the
/// MCP can hand an LLM a real, copy-pasteable example instead of guessing.
///
/// The mapping is by convention:
///   docs/Lumeo.Docs/Pages/Components/{Name}Page.razor  →  component "{Name}"
///   docs/Lumeo.Docs/Pages/Components/Charts/{Name}ChartPage.razor → "{Name}"
/// </summary>
public static class ExampleExtractor
{
    public sealed record Example(string Title, string Code);

    // <ComponentDemo Title="Variants" Code="@_variantCode"> ... possibly Description="..." in any order
    private static readonly Regex DemoTag = new(
        @"<ComponentDemo\b[^>]*?\bTitle=""(?<title>[^""]+)""[^>]*?\bCode=""@(?<field>_\w+)""",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // also handle Code="..." appearing before Title="..."
    private static readonly Regex DemoTagAlt = new(
        @"<ComponentDemo\b[^>]*?\bCode=""@(?<field>_\w+)""[^>]*?\bTitle=""(?<title>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // private string _variantCode = @"....";   (C# verbatim string — "" is an escaped quote)
    private static readonly Regex FieldDecl = new(
        @"private\s+string\s+(?<field>_\w+)\s*=\s*@""(?<code>(?:[^""]|"""")*)""\s*;",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>Returns up to <paramref name="maxPerComponent"/> examples for the
    /// named component, or an empty list if no docs page exists / has demos.</summary>
    public static IReadOnlyList<Example> ForComponent(string repoRoot, string componentName, int maxPerComponent = 4)
    {
        var pagesDir = Path.Combine(repoRoot, "docs", "Lumeo.Docs", "Pages", "Components");
        var candidates = new[]
        {
            Path.Combine(pagesDir, $"{componentName}Page.razor"),
            Path.Combine(pagesDir, "Charts", $"{componentName}ChartPage.razor"),
            // a few components live under a differently-named page (e.g. "Ai" page covers AgentMessageList etc.)
        };

        var pagePath = candidates.FirstOrDefault(File.Exists);
        if (pagePath is null) return Array.Empty<Example>();

        string src;
        try { src = File.ReadAllText(pagePath); }
        catch { return Array.Empty<Example>(); }

        // field name -> code
        var codeByField = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in FieldDecl.Matches(src))
        {
            var field = m.Groups["field"].Value;
            var code = m.Groups["code"].Value.Replace("\"\"", "\""); // un-escape verbatim quotes
            codeByField[field] = code.Trim();
        }
        if (codeByField.Count == 0) return Array.Empty<Example>();

        // title -> field (preserve document order, dedupe)
        var ordered = new List<(string Title, string Field)>();
        var seenFields = new HashSet<string>(StringComparer.Ordinal);
        void Collect(Regex rx)
        {
            foreach (Match m in rx.Matches(src))
            {
                var field = m.Groups["field"].Value;
                var title = m.Groups["title"].Value;
                if (seenFields.Add(field)) ordered.Add((title, field));
            }
        }
        Collect(DemoTag);
        Collect(DemoTagAlt);

        var result = new List<Example>();
        foreach (var (title, field) in ordered)
        {
            if (!codeByField.TryGetValue(field, out var code) || string.IsNullOrWhiteSpace(code)) continue;
            result.Add(new Example(title, code));
            if (result.Count >= maxPerComponent) break;
        }
        return result;
    }

    public sealed record PatternInfo(string Title, string Route, string Description, IReadOnlyList<Example> Examples);

    /// <summary>Reads <c>docs/Lumeo.Docs/Pages/Patterns/*.razor</c> — the full-page
    /// "block" examples (dashboard, auth, chat, kanban, …) — and returns their
    /// title, route, blurb and the Razor snippet behind each demo so the MCP can
    /// hand an LLM a complete, real layout instead of a component-by-component build-up.</summary>
    public static IReadOnlyList<PatternInfo> Patterns(string repoRoot)
    {
        var dir = Path.Combine(repoRoot, "docs", "Lumeo.Docs", "Pages", "Patterns");
        if (!Directory.Exists(dir)) return Array.Empty<PatternInfo>();

        var pageRx = new Regex(@"@page\s+""(?<route>[^""]+)""", RegexOptions.Compiled);
        var headerRx = new Regex(@"<PageHeader\b[^>]*?\bTitle=""(?<title>[^""]+)""[^>]*?\bDescription=""(?<desc>[^""]*)""", RegexOptions.Compiled | RegexOptions.Singleline);

        var list = new List<PatternInfo>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.razor").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            string src;
            try { src = File.ReadAllText(file); }
            catch { continue; }

            var route = pageRx.Match(src) is { Success: true } pm ? pm.Groups["route"].Value : "";
            var hm = headerRx.Match(src);
            var title = hm.Success ? hm.Groups["title"].Value : Path.GetFileNameWithoutExtension(file).Replace("Pattern", "");
            var desc = hm.Success ? hm.Groups["desc"].Value : "";

            // Reuse the demo/field machinery for the pattern's code snippets.
            var codeByField = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match m in FieldDecl.Matches(src))
                codeByField[m.Groups["field"].Value] = m.Groups["code"].Value.Replace("\"\"", "\"").Trim();
            var examples = new List<Example>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in DemoTag.Matches(src))
            {
                var f = m.Groups["field"].Value;
                if (!seen.Add(f)) continue;
                if (codeByField.TryGetValue(f, out var code) && !string.IsNullOrWhiteSpace(code))
                    examples.Add(new Example(m.Groups["title"].Value, code));
            }
            list.Add(new PatternInfo(title, route, desc, examples));
        }
        return list;
    }

    /// <summary>Extracts the theme CSS-variable tokens (the <c>--color-*</c> and
    /// <c>--radius*</c> custom properties) from <c>lumeo.css</c>, so the MCP can
    /// tell an LLM "use bg-primary / text-foreground, never raw hex".</summary>
    public static IReadOnlyList<(string Token, string CssVar)> ThemeTokens(string repoRoot)
    {
        var cssPath = Path.Combine(repoRoot, "src", "Lumeo", "wwwroot", "css", "lumeo.css");
        if (!File.Exists(cssPath)) return Array.Empty<(string, string)>();
        string css;
        try { css = File.ReadAllText(cssPath); }
        catch { return Array.Empty<(string, string)>(); }

        // --color-primary: ...;  --color-primary-foreground: ...;  --radius: ...;  --radius-xl: ...;
        var rx = new Regex(@"--(?<name>(?:color-[\w-]+)|radius[\w-]*)\s*:", RegexOptions.Compiled);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<(string, string)>();
        foreach (Match m in rx.Matches(css))
        {
            var name = m.Groups["name"].Value;
            if (!seen.Add(name)) continue;
            // --color-primary  → utility "primary" (used as bg-primary / text-primary / border-primary)
            // --radius-xl       → "radius-xl"
            var token = name.StartsWith("color-") ? name["color-".Length..] : name;
            list.Add((token, $"--{name}"));
        }
        return list.OrderBy(t => t.Item1, StringComparer.Ordinal).ToList();
    }
}
