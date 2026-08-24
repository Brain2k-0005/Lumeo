using System.Text.RegularExpressions;
using Lumeo.Theming;
using Xunit;

namespace Lumeo.Tests.Theming;

/// <summary>
/// The default radius is written down in three independent places: the stylesheet that
/// actually renders it, the JS the theme API answers with, and the preset catalog a theme
/// editor picks from. Nothing tied them together, and they drifted the moment the token
/// moved from 0.75rem to 0.625rem in the 5.0 alignment - the JS kept reporting the old
/// number, so an editor could restore the previous radius merely by writing back what it
/// had just read (Codex review of PR #430).
///
/// Source-level, like <see cref="RadiusTokenGuardTests"/>: the point is to fail on the file
/// that was forgotten, and only the files can say which one that is.
/// </summary>
public class DefaultRadiusAgreementTests
{
    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Lumeo.slnx")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        return dir!;
    }

    /// <summary>The value the stylesheet renders: the first <c>--radius</c> on <c>:root</c>.</summary>
    private static string CssDefaultRadius()
    {
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "src/Lumeo/wwwroot/css/lumeo.css"));
        var m = Regex.Match(css, @"--radius:\s*([0-9.]+)rem");
        Assert.True(m.Success, "no --radius declaration found in lumeo.css");
        return m.Groups[1].Value;
    }

    [Fact]
    public void The_Theme_Api_Reports_The_Radius_The_Stylesheet_Renders()
    {
        var js = File.ReadAllText(Path.Combine(RepoRoot(), "src/Lumeo/wwwroot/js/theme.js"));

        // getRadius()'s fallback: the branch taken when nothing is stored, which is exactly
        // the state in which the stylesheet's own value is on screen.
        var m = Regex.Match(js, @"getRadius:\s*function[^}]*?:\s*'([0-9.]+)'", RegexOptions.Singleline);
        Assert.True(m.Success, "could not find getRadius()'s stored-nothing fallback in theme.js");

        Assert.Equal(CssDefaultRadius(), m.Groups[1].Value);
    }

    [Fact]
    public void The_Preset_Catalog_Offers_The_Default_Radius()
    {
        // Offered, not at any particular index: the catalog is append-only because the index
        // is what an encoded preset stores, so the default will not sit in sorted position.
        Assert.Contains(CssDefaultRadius(), LumeoPresetOptions.Radii);
    }

    [Fact]
    public void The_Docs_Quote_The_Radius_The_Stylesheet_Renders()
    {
        // The Tweakcn guide prints an excerpt of lumeo.css so a reader can see which tokens
        // map onto shadcn's. An excerpt that has drifted from the file it quotes is worse
        // than no excerpt: it reads as authoritative and is wrong, and nothing about editing
        // lumeo.css prompts anyone to open a docs page (Codex review of PR #430).
        var page = File.ReadAllText(Path.Combine(
            RepoRoot(), "docs/Lumeo.Docs/Pages/Docs/Tweakcn.razor"));

        // Only the excerpt that claims to BE lumeo.css. The same page also shows a tweakcn
        // export the reader pastes to OVERRIDE the default - pinning that to the default
        // would assert the opposite of what the example teaches.
        var excerpt = Regex.Match(page, @"_howCode\s*=\s*@""(.*?)"";", RegexOptions.Singleline);
        Assert.True(excerpt.Success, "could not find the lumeo.css excerpt (_howCode) in Tweakcn.razor");

        var quoted = Regex.Matches(excerpt.Groups[1].Value, @"--radius:\s*([0-9.]+)rem")
            .Select(m => m.Groups[1].Value)
            .ToArray();

        Assert.NotEmpty(quoted);
        Assert.All(quoted, v => Assert.Equal(CssDefaultRadius(), v));
    }
}
