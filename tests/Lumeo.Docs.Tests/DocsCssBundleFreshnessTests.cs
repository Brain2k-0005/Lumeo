using System.Text.RegularExpressions;
using Xunit;

namespace Lumeo.Docs.Tests;

/// <summary>
/// PR #530 root cause: the docs site does NOT load the NuGet-shipped
/// <c>src/Lumeo/wwwroot/css/lumeo-utilities.css</c> at all — it loads its own, separately
/// compiled <c>docs/Lumeo.Docs/wwwroot/css/tailwind.out.css</c> (built by
/// <c>npm run css:build</c> inside <c>docs/Lumeo.Docs</c>, scanning library + docs markup
/// directly). Rebuilding the library's own bundle via the repo-root <c>npm run build:css</c>
/// does NOT touch this file. When a component gains a new Tailwind class (especially one
/// gated behind a responsive variant like <c>sm:</c>) and nobody re-runs the docs site's own
/// <c>css:build</c>, the class is silently absent from what the docs site actually serves —
/// no build error, no test failure, just a class attribute that resolves to nothing. This is
/// exactly what happened to <c>--lumeo-menu-item-h</c>'s <c>sm:min-h-[var(...)]</c> override on
/// DropdownMenuItem/SubTrigger and ContextMenuItem/SubTrigger: it rendered correctly (bUnit
/// caught the class string), but produced no visual effect on the docs site because the
/// compiled bundle simply didn't contain the rule yet.
///
/// This guard scans every <c>.razor</c> file under the same roots
/// <see cref="GeometryTokenGuardTests"/> (over in Lumeo.Tests) checks — the library's own
/// source-of-truth token consumers — for any Tailwind class that reads a <c>--lumeo-</c>
/// custom property via an arbitrary value (<c>[var(--lumeo-...)]</c>), optionally behind a
/// variant prefix (e.g. <c>sm:</c>), and asserts the docs site's compiled
/// <c>tailwind.out.css</c> actually contains a rule for it (CSS-escaped selector form). A
/// missing rule here means <c>npm run css:build</c> needs to be re-run inside
/// <c>docs/Lumeo.Docs</c> before the change ships.
/// </summary>
public class DocsCssBundleFreshnessTests
{
    private static string FindRepoRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Lumeo.slnx"))) return d.FullName;
        throw new InvalidOperationException("Lumeo.slnx not found above " + AppContext.BaseDirectory);
    }

    private static IEnumerable<string> RazorFiles(string root) =>
        new[] { "src/Lumeo/UI", "src/Lumeo.DataGrid/UI" }
            .Select(p => Path.Combine(root, p)).Where(Directory.Exists)
            .SelectMany(p => Directory.EnumerateFiles(p, "*.razor", SearchOption.AllDirectories));

    // Matches an (optionally variant-prefixed) Tailwind arbitrary-value class that reads a
    // --lumeo- custom property, e.g. "sm:min-h-[var(--lumeo-menu-item-h,...)]" or
    // "min-h-[var(--lumeo-grid-row-h,...)]". Captures the whole class token.
    private static readonly Regex TokenClassPattern =
        new(@"(?<![\w-])(?:[a-z0-9-]+:)*[a-z][a-zA-Z0-9-]*\[var\(--lumeo-[a-z0-9-]+(?:,[^\]]*)?\)\]", RegexOptions.Compiled);

    // Turns a literal Tailwind class into the selector Tailwind emits for it: every
    // character Tailwind's own escaper treats as needing a backslash in a class selector
    // (: [ ] ( ) , . * / % # and space) gets one. Mirrors what @tailwindcss/cli produces —
    // verified against the compiled bundle for the token classes this repo already ships.
    private static string ToEscapedSelector(string cls)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in cls)
        {
            if (":[](),.*/%# ".IndexOf(c) >= 0) sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }

    public static IEnumerable<object[]> TokenClasses()
    {
        var root = FindRepoRoot();
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in RazorFiles(root))
            foreach (Match m in TokenClassPattern.Matches(File.ReadAllText(file)))
                found.Add(m.Value);
        return found.Select(c => new object[] { c });
    }

    [Theory]
    [MemberData(nameof(TokenClasses))]
    public void Docs_Bundle_Contains_A_Rule_For_Every_Lumeo_Token_Class(string tailwindClass)
    {
        var root = FindRepoRoot();
        var bundlePath = Path.Combine(root, "docs", "Lumeo.Docs", "wwwroot", "css", "tailwind.out.css");
        Assert.True(File.Exists(bundlePath), $"{bundlePath} does not exist — run `npm run css:build` in docs/Lumeo.Docs.");
        var css = File.ReadAllText(bundlePath);

        var selector = ToEscapedSelector(tailwindClass);
        Assert.True(css.Contains(selector, StringComparison.Ordinal),
            $"docs/Lumeo.Docs/wwwroot/css/tailwind.out.css has no rule for '.{selector}' " +
            $"(source class '{tailwindClass}'). Re-run `npm run css:build` inside docs/Lumeo.Docs.");
    }
}
