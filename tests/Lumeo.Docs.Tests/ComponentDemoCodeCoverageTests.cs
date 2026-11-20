using System.Text.RegularExpressions;
using Xunit;

namespace Lumeo.Docs.Tests;

/// <summary>
/// #553: a ComponentDemo without a Code parameter renders an empty panel when the
/// reader clicks its Code tab ("Code does not show"). ~195 demos across ~120 pages
/// had no snippet. Every demo must carry one.
/// </summary>
public class ComponentDemoCodeCoverageTests
{
    // An opening tag, tolerating '>' inside quoted attribute values.
    private static readonly Regex DemoTag = new(
        "<ComponentDemo\\b((?:[^>\"]|\"[^\"]*\")*)>", RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex CodeAttr = new("\\bCode\\s*=", RegexOptions.Compiled);

    private static string PagesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "Lumeo.Docs", "Pages");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("docs/Lumeo.Docs/Pages not found above the test output directory");
    }

    [Fact]
    public void Every_ComponentDemo_Supplies_A_Code_Snippet()
    {
        var missing = new List<string>();
        foreach (var file in Directory.EnumerateFiles(PagesDir(), "*.razor", SearchOption.AllDirectories))
        {
            var src = File.ReadAllText(file).Replace("\r\n", "\n");
            foreach (Match m in DemoTag.Matches(src))
            {
                if (CodeAttr.IsMatch(m.Groups[1].Value)) continue;
                var line = src.Take(m.Index).Count(c => c == '\n') + 1;
                missing.Add($"{Path.GetFileName(file)}:{line}");
            }
        }

        Assert.True(missing.Count == 0,
            "ComponentDemo without Code= shows an empty Code tab:\n" + string.Join("\n", missing));
    }
}
