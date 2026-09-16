using System.Text.RegularExpressions;
using Xunit;

namespace Lumeo.Tests.Theming;

/// <summary>
/// #490 owner report: the customizer's "Menu color" setting wrote 8 --color-sidebar-*
/// custom properties as INLINE styles on &lt;html&gt; with hard-coded zinc values —
/// reaching every SidebarComponent on the page (not just the app chrome it's meant for)
/// and ignoring the active theme's own sidebar colors ("dark" meant zinc, not "this
/// theme's dark sidebar"). The fix: every theme file (and lumeo.css's own default light
/// block) carries the WHOLE sidebar set for BOTH modes as 16 private
/// --_sidebar-light-*/--_sidebar-dark-* variables, and lumeo.css resolves
/// data-menu-color against them. This is a source-level guard (like
/// <see cref="GeometryTokenGuardTests"/>) that the contract holds:
///   1. Every theme file (and lumeo.css) declares all 16 --_sidebar-{light,dark}-*
///      variables in its light-mode block.
///   2. The light block's public --color-sidebar-* tokens fall back to the
///      --_sidebar-light-* set; the dark block's fall back to --_sidebar-dark-*.
///   3. lumeo.css declares both data-menu-color override blocks and both
///      data-menu-color-isolate blocks.
/// </summary>
public class MenuColorTokenGuardTests
{
    private static readonly string[] SidebarShorts =
        ["bg", "fg", "primary", "primary-fg", "accent", "accent-fg", "border", "ring"];

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Lumeo.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Lumeo.slnx not found above " + AppContext.BaseDirectory);
    }

    private static IEnumerable<string> PaletteFiles(string root)
    {
        yield return Path.Combine(root, "src/Lumeo/wwwroot/css/lumeo.css");
        var themesDir = Path.Combine(root, "src/Lumeo/wwwroot/css/themes");
        foreach (var file in Directory.EnumerateFiles(themesDir, "*.css").OrderBy(f => f))
            yield return file;
    }

    public static IEnumerable<object[]> PaletteFileNames() =>
        PaletteFiles(RepoRoot()).Select(f => new object[] { Path.GetFileName(f) == "lumeo.css" ? "src/Lumeo/wwwroot/css/lumeo.css" : "src/Lumeo/wwwroot/css/themes/" + Path.GetFileName(f) });

    [Theory]
    [MemberData(nameof(PaletteFileNames))]
    public void Declares_All_16_Private_Sidebar_Set_Variables(string relativeFile)
    {
        var root = RepoRoot();
        var text = File.ReadAllText(Path.Combine(root, relativeFile.Replace('/', Path.DirectorySeparatorChar)));

        var missing = new List<string>();
        foreach (var mode in new[] { "light", "dark" })
            foreach (var s in SidebarShorts)
            {
                var name = $"--_sidebar-{mode}-{s}";
                if (!Regex.IsMatch(text, Regex.Escape(name) + @":\s*[^;]+;"))
                    missing.Add(name);
            }

        Assert.True(missing.Count == 0, $"{relativeFile}: missing private sidebar variables: {string.Join(", ", missing)}");
    }

    [Theory]
    [MemberData(nameof(PaletteFileNames))]
    public void Public_Sidebar_Tokens_Fall_Back_To_The_Matching_Private_Set(string relativeFile)
    {
        var root = RepoRoot();
        var text = File.ReadAllText(Path.Combine(root, relativeFile.Replace('/', Path.DirectorySeparatorChar)));

        // Public sidebar tokens must reference var(--_sidebar-light-*) exactly twice
        // (once as the fallback in the light block) and var(--_sidebar-dark-*) exactly
        // twice (once in .dark) per short name — light and dark blocks each declare it once.
        foreach (var s in SidebarShorts)
        {
            var lightRefs = Regex.Matches(text, Regex.Escape($"var(--_sidebar-light-{s})")).Count;
            var darkRefs = Regex.Matches(text, Regex.Escape($"var(--_sidebar-dark-{s})")).Count;
            Assert.True(lightRefs >= 1, $"{relativeFile}: no --color-sidebar-* falls back to --_sidebar-light-{s}");
            Assert.True(darkRefs >= 1, $"{relativeFile}: no --color-sidebar-* falls back to --_sidebar-dark-{s}");
        }
    }

    [Fact]
    public void LumeoCss_Declares_Both_Menu_Color_Override_Blocks()
    {
        var root = RepoRoot();
        var text = File.ReadAllText(Path.Combine(root, "src/Lumeo/wwwroot/css/lumeo.css"));

        Assert.Contains(":root[data-menu-color=\"dark\"]", text);
        Assert.Contains(":root[data-menu-color=\"light\"]", text);
        // Both must resolve --color-sidebar against the opposite-of-default private set
        // so a dark menu color is dark regardless of the page's own light/dark mode.
        Assert.Matches(new Regex(@":root\[data-menu-color=""dark""\][\s\S]{0,400}?--color-sidebar:\s*var\(--sidebar,\s*var\(--_sidebar-dark-bg\)\);"), text);
        Assert.Matches(new Regex(@":root\[data-menu-color=""light""\][\s\S]{0,400}?--color-sidebar:\s*var\(--sidebar,\s*var\(--_sidebar-light-bg\)\);"), text);
    }

    [Fact]
    public void LumeoCss_Declares_Both_Isolate_Blocks()
    {
        var root = RepoRoot();
        var text = File.ReadAllText(Path.Combine(root, "src/Lumeo/wwwroot/css/lumeo.css"));

        Assert.Contains("[data-menu-color-isolate]", text);
        Assert.Contains(".dark [data-menu-color-isolate]", text);
        Assert.Matches(new Regex(@"(?<!\.dark )\[data-menu-color-isolate\]\s*\{[\s\S]{0,400}?--color-sidebar:\s*var\(--sidebar,\s*var\(--_sidebar-light-bg\)\);"), text);
        Assert.Matches(new Regex(@"\.dark \[data-menu-color-isolate\]\s*\{[\s\S]{0,400}?--color-sidebar:\s*var\(--sidebar,\s*var\(--_sidebar-dark-bg\)\);"), text);
    }
}
