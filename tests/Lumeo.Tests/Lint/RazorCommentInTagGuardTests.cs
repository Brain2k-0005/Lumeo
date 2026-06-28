using System.IO;
using System.Collections.Generic;
using Xunit;

namespace Lumeo.Tests.Lint;

/// <summary>
/// Guards against a render-time crash class that is INVISIBLE to bUnit and to the
/// browser pageerror sweep: a Razor comment placed BETWEEN an element's attributes,
/// e.g.
/// <code>
/// &lt;input @oninput="X"
///        @* a multi-line comment here *@
///        @onkeydown="Y" /&gt;
/// </code>
/// Razor emits that comment as a LITERAL attribute name; in a real browser Blazor then
/// calls setAttribute("@* ... *@", ...) which throws InvalidCharacterError and takes the
/// whole circuit down. bUnit's AngleSharp DOM tolerates the bad name (no throw), and the
/// crash is a circuit error not a window 'pageerror', so neither catches it — only this
/// structural scan or a real-browser render does. (Bit Cascader + PdfViewer.)
/// Comments belong OUTSIDE the opening tag.
/// </summary>
public class RazorCommentInTagGuardTests
{
    [Fact]
    public void No_Razor_comment_appears_inside_an_element_opening_tag()
    {
        var srcRoot = Path.Combine(FindRepoRoot(), "src");
        Assert.True(Directory.Exists(srcRoot), $"src not found at {srcRoot}");

        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(srcRoot, "*.razor", SearchOption.AllDirectories))
        {
            foreach (var line in ScanCommentsInTags(File.ReadAllText(file)))
                offenders.Add($"{Path.GetRelativePath(srcRoot, file).Replace('\\', '/')}:{line}");
        }

        Assert.True(offenders.Count == 0,
            "Razor comment(s) found INSIDE an element opening tag (will crash in a real browser — " +
            "move the comment OUTSIDE the tag):\n  " + string.Join("\n  ", offenders));
    }

    // Mirror of the standalone detector: track tag state, skip comment BODIES entirely
    // (their apostrophes/quotes are not string delimiters), only track attribute-value
    // quotes while inside a tag. Drops the @code{} block to avoid C# generics/comparisons.
    private static IEnumerable<int> ScanCommentsInTags(string s)
    {
        var ci = s.IndexOf("\n@code", System.StringComparison.Ordinal);
        if (ci >= 0) s = s[..ci];

        int line = 1;
        bool inTag = false, inS = false, inD = false, inComment = false;
        var hits = new List<int>();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i], n = i + 1 < s.Length ? s[i + 1] : '\0';
            if (c == '\n') line++;
            if (inComment) { if (c == '*' && n == '@') { inComment = false; i++; } continue; }
            if (c == '@' && n == '*') { if (inTag) hits.Add(line); inComment = true; i++; continue; }
            if (inTag)
            {
                if (inD) { if (c == '"') inD = false; continue; }
                if (inS) { if (c == '\'') inS = false; continue; }
                if (c == '"') { inD = true; continue; }
                if (c == '\'') { inS = true; continue; }
                if (c == '>') { inTag = false; continue; }
            }
            else if (c == '<' && (char.IsLetter(n) || n == '/')) { inTag = true; }
        }
        return hits;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Lumeo.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate Lumeo repo root (no Lumeo.slnx found above the test bin dir).");
    }
}
