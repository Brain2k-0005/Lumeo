using System.Text.RegularExpressions;
using Lumeo.Theming;
using Xunit;

namespace Lumeo.Tests.Theming;

/// <summary>
/// The CLI keeps a deliberate second copy of the preset catalog: it is an Exe with a
/// top-level Program.cs, so the test and tooling projects link its source rather than
/// reference it. A copy is fine; a copy that falls behind is not, because a preset code
/// carries INDICES. When the shipped radius was appended to the library catalog as index 5
/// and the CLI's copy still stopped at 4, `lumeo apply --preset ...` decoded that index
/// through its own fallback and handed the shared theme a different radius without saying
/// so - a silent wrong answer rather than an error.
///
/// Source-level on purpose: the CLI's catalog is not on this assembly's reference graph,
/// and the point is to fail on the file that was forgotten.
/// </summary>
public class PresetCatalogParityTests
{
    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Lumeo.slnx")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        return dir!;
    }

    /// <summary>Reads one <c>string[]</c> initialiser out of the CLI's PresetCodec.cs.</summary>
    private static string[] CliCatalog(string name)
    {
        var src = File.ReadAllText(Path.Combine(RepoRoot(), "tools/Lumeo.Cli/PresetCodec.cs"));
        var m = Regex.Match(src, @"string\[\]\s+" + Regex.Escape(name) + @"\s*=\s*\{(.*?)\}", RegexOptions.Singleline);
        Assert.True(m.Success, $"could not find the CLI's {name} catalog in PresetCodec.cs");

        return Regex.Matches(m.Groups[1].Value, "\"([^\"]*)\"")
            .Select(x => x.Groups[1].Value)
            .ToArray();
    }

    [Theory]
    [InlineData("Radii")]
    [InlineData("Styles")]
    [InlineData("BaseColors")]
    [InlineData("Fonts")]
    public void The_Cli_Catalog_Matches_The_Library_Index_For_Index(string name)
    {
        var library = name switch
        {
            "Radii" => LumeoPresetOptions.Radii,
            "Styles" => LumeoPresetOptions.Styles,
            "BaseColors" => LumeoPresetOptions.BaseColors,
            "Fonts" => LumeoPresetOptions.Fonts,
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

        // Index for index, not as sets: the index IS the encoded value, so a catalog with the
        // right entries in the wrong order decodes every existing preset code to something else.
        Assert.Equal(library, CliCatalog(name));
    }

    [Fact]
    public void The_Shipped_Radius_Is_In_Both_Catalogs()
    {
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "src/Lumeo/wwwroot/css/lumeo.css"));
        var m = Regex.Match(css, @"--radius:\s*([0-9.]+)rem");
        Assert.True(m.Success, "no --radius declaration found in lumeo.css");

        Assert.Contains(m.Groups[1].Value, LumeoPresetOptions.Radii);
        Assert.Contains(m.Groups[1].Value, CliCatalog("Radii"));
    }
}
