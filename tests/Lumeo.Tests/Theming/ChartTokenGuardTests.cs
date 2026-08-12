using System.Text.RegularExpressions;
using Xunit;

namespace Lumeo.Tests.Theming;

/// <summary>
/// Source-level guard for the 2026-08 chart-palette wave (owner report, measured: chart-1
/// resolved to the same near-achromatic value as --primary in the default light theme —
/// oklch(0.2103 0.0059 285.88), chroma 0.0059 — so the single-series case, and the Gantt's
/// bars, rendered grey; chart-4/chart-5 sat only ~29 deg apart in hue and read as
/// indistinguishable amber-vs-orange).
///
/// This pins the fixed palette's PROPERTIES, not its exact values, so the next well-meaning
/// edit can still retune hue/lightness/chroma but cannot quietly reintroduce a grey or
/// near-duplicate series:
///   1. Every chart-1..5 hue in a given light/dark block is at least
///      <see cref="MinHueSeparationDegrees"/> apart from every other hue in that block.
///   2. Every chart token's chroma clears <see cref="MinChroma"/> (bans near-achromatic
///      colors like the old 0.0059/0.0000 grays).
///   3. Light and dark values differ per token (bans the old byte-identical
///      chart-4/chart-5 across modes).
///   4. The specific degenerate case is gone: --color-chart-1 no longer resolves to the
///      exact same oklch triple as --color-primary in the base (default) light theme.
///
/// Scans raw file text (like <see cref="RadiusTokenGuardTests"/>), so a regression in a
/// fallback literal fails this even if nothing else references it.
/// </summary>
public class ChartTokenGuardTests
{
    private const double MinHueSeparationDegrees = 40.0;
    private const double MinChroma = 0.05;

    private static readonly Regex ChartTokenPattern = new(
        @"--color-chart-(?<idx>\d):\s*var\(--chart-\d,\s*oklch\((?<l>-?[\d.]+)\s+(?<c>-?[\d.]+)\s+(?<h>-?[\d.]+)\)\);",
        RegexOptions.Compiled);

    private static readonly Regex PrimaryTokenPattern = new(
        @"--color-primary:\s*var\(--primary,\s*oklch\((?<l>-?[\d.]+)\s+(?<c>-?[\d.]+)\s+(?<h>-?[\d.]+)\)\);",
        RegexOptions.Compiled);

    private readonly record struct Oklch(double L, double C, double H);

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Lumeo.slnx")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        return dir!;
    }

    private static List<Oklch> ExtractChartTokens(string text) =>
        ChartTokenPattern.Matches(text)
            .Select(m => new Oklch(
                double.Parse(m.Groups["l"].Value),
                double.Parse(m.Groups["c"].Value),
                double.Parse(m.Groups["h"].Value)))
            .ToList();

    private static List<Oklch> ExtractPrimaryTokens(string text) =>
        PrimaryTokenPattern.Matches(text)
            .Select(m => new Oklch(
                double.Parse(m.Groups["l"].Value),
                double.Parse(m.Groups["c"].Value),
                double.Parse(m.Groups["h"].Value)))
            .ToList();

    /// <summary>Circular hue distance in degrees, 0..180.</summary>
    private static double HueDistance(double h1, double h2)
    {
        var d = Math.Abs(h1 - h2) % 360;
        return d > 180 ? 360 - d : d;
    }

    private static IEnumerable<(string File, string Text)> AllPaletteFiles()
    {
        var root = RepoRoot();
        yield return ("src/Lumeo/wwwroot/css/lumeo.css",
            File.ReadAllText(Path.Combine(root, "src/Lumeo/wwwroot/css/lumeo.css")));

        var themesDir = Path.Combine(root, "src/Lumeo/wwwroot/css/themes");
        foreach (var file in Directory.EnumerateFiles(themesDir, "*.css").OrderBy(f => f))
        {
            var rel = "src/Lumeo/wwwroot/css/themes/" + Path.GetFileName(file);
            yield return (rel, File.ReadAllText(file));
        }
    }

    public static IEnumerable<object[]> PaletteFileNames() =>
        AllPaletteFiles().Select(f => new object[] { f.File });

    [Theory]
    [MemberData(nameof(PaletteFileNames))]
    public void Each_LightAndDark_Block_Has_Five_Well_Separated_Vivid_Chart_Hues(string relativeFile)
    {
        var root = RepoRoot();
        var text = File.ReadAllText(Path.Combine(root, relativeFile.Replace('/', Path.DirectorySeparatorChar)));
        var tokens = ExtractChartTokens(text);

        Assert.True(tokens.Count == 10,
            $"{relativeFile}: expected exactly 10 chart tokens (5 light + 5 dark), found {tokens.Count}.");

        var light = tokens.Take(5).ToList();
        var dark = tokens.Skip(5).Take(5).ToList();

        foreach (var (blockName, block) in new[] { ("light", light), ("dark", dark) })
        {
            // Chroma floor — bans near-achromatic (grey) chart colors.
            var achromatic = block
                .Select((t, i) => (idx: i + 1, t))
                .Where(x => x.t.C < MinChroma)
                .ToList();
            Assert.True(achromatic.Count == 0,
                $"{relativeFile} [{blockName}]: chart token(s) below chroma floor {MinChroma} " +
                "(reads as grey): " +
                string.Join(", ", achromatic.Select(x => $"chart-{x.idx}=C{x.t.C}")));

            // Pairwise hue separation — bans near-duplicate hues (the amber/orange 29 deg bug).
            var tooClose = new List<string>();
            for (var i = 0; i < block.Count; i++)
            {
                for (var j = i + 1; j < block.Count; j++)
                {
                    var d = HueDistance(block[i].H, block[j].H);
                    if (d < MinHueSeparationDegrees)
                        tooClose.Add($"chart-{i + 1}/chart-{j + 1} only {d:F1} deg apart");
                }
            }
            Assert.True(tooClose.Count == 0,
                $"{relativeFile} [{blockName}]: chart hues closer than {MinHueSeparationDegrees} deg: " +
                string.Join("; ", tooClose));
        }

        // Light and dark must differ per token (bans byte-identical light/dark values).
        var identical = Enumerable.Range(0, 5)
            .Where(i => light[i] == dark[i])
            .Select(i => $"chart-{i + 1}")
            .ToList();
        Assert.True(identical.Count == 0,
            $"{relativeFile}: light and dark chart tokens are identical for: " + string.Join(", ", identical));
    }

    [Fact]
    public void BaseTheme_Chart1_No_Longer_Equals_Primary_In_Light_Mode()
    {
        // The specific degenerate case reported by the owner: in the default (Zinc-identity)
        // light theme, --color-chart-1 resolved to the exact same oklch triple as
        // --color-primary (both near-achromatic), so a single-series chart rendered grey.
        var root = RepoRoot();
        var text = File.ReadAllText(Path.Combine(root, "src/Lumeo/wwwroot/css/lumeo.css"));

        var chartTokens = ExtractChartTokens(text);
        var primaryTokens = ExtractPrimaryTokens(text);

        Assert.True(chartTokens.Count >= 1, "expected at least one chart-1 token in lumeo.css");
        Assert.True(primaryTokens.Count >= 1, "expected at least one --color-primary token in lumeo.css");

        var lightChart1 = chartTokens[0];
        var lightPrimary = primaryTokens[0];

        Assert.NotEqual(lightPrimary, lightChart1);
    }

    /// <summary>
    /// 2026-08 chart-independence wave (owner directive: "die Charts sollten
    /// unabhängig eingefärbt werden können von der Primary-Farbe"): the 7 named
    /// themes (amber/blue/green/orange/rose/teal/violet) used to set
    /// <c>chart-1 = --primary</c> literally, then rotate chart-2..5 by 72 deg
    /// around it — so switching a consumer's brand/accent colour silently
    /// repainted every chart's first data series (and the Gantt's default bar
    /// fill, which falls back to <c>--color-chart-1</c>). This generalizes
    /// <see cref="BaseTheme_Chart1_No_Longer_Equals_Primary_In_Light_Mode"/>
    /// (base theme, chart-1, light only) to EVERY palette file, EVERY chart
    /// token (chart-1..5, not just chart-1 — a future edit could easily leave
    /// chart-1 decoupled while accidentally re-anchoring chart-2..5), and BOTH
    /// modes: no chart token may resolve to the exact same oklch triple as
    /// that same file/mode's --color-primary. This is the test that would have
    /// caught the amber/blue/green/orange/rose/teal/violet coupling this wave
    /// fixed — <see cref="BaseTheme_Chart1_No_Longer_Equals_Primary_In_Light_Mode"/>
    /// never scanned the theme files at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(PaletteFileNames))]
    public void No_Chart_Token_Equals_Primary_In_Any_Theme_Or_Mode(string relativeFile)
    {
        var root = RepoRoot();
        var text = File.ReadAllText(Path.Combine(root, relativeFile.Replace('/', Path.DirectorySeparatorChar)));

        var chartTokens = ExtractChartTokens(text);
        var primaryTokens = ExtractPrimaryTokens(text);

        Assert.True(chartTokens.Count == 10,
            $"{relativeFile}: expected exactly 10 chart tokens (5 light + 5 dark), found {chartTokens.Count}.");
        Assert.True(primaryTokens.Count == 2,
            $"{relativeFile}: expected exactly 2 --color-primary tokens (light + dark), found {primaryTokens.Count}.");

        var lightPrimary = primaryTokens[0];
        var darkPrimary = primaryTokens[1];
        var lightCharts = chartTokens.Take(5).ToList();
        var darkCharts = chartTokens.Skip(5).Take(5).ToList();

        var lightCollisions = lightCharts
            .Select((t, i) => (idx: i + 1, t))
            .Where(x => x.t == lightPrimary)
            .Select(x => $"chart-{x.idx}")
            .ToList();
        Assert.True(lightCollisions.Count == 0,
            $"{relativeFile} [light]: chart token(s) equal --color-primary ({lightPrimary}): " +
            string.Join(", ", lightCollisions));

        var darkCollisions = darkCharts
            .Select((t, i) => (idx: i + 1, t))
            .Where(x => x.t == darkPrimary)
            .Select(x => $"chart-{x.idx}")
            .ToList();
        Assert.True(darkCollisions.Count == 0,
            $"{relativeFile} [dark]: chart token(s) equal --color-primary ({darkPrimary}): " +
            string.Join(", ", darkCollisions));
    }
}
