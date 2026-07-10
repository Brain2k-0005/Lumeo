using Lumeo.Cli;
using Lumeo.Docs.Services;
using Xunit;

namespace Lumeo.Cli.Tests;

/// <summary>
/// Verifies the single icon-library normalisation gate (<see cref="IconLibraryNorm.Normalize"/>)
/// across all source categories: server-preset mappable legacy aliases, unmappable legacy names
/// (font-awesome et al.), tombstoned codec indices, and already-canonical first-party names.
/// </summary>
public class IconLibraryNormTests
{
    // ── server-preset mappable legacy aliases → rewritten to canonical first-party name ──

    [Theory]
    [InlineData("fluentui",       "fluent")]
    [InlineData("google-material","material-symbols")]
    public void Normalize_MappableLegacyAlias_RewritesToCanonical(string input, string expected)
    {
        string? warning = null;
        var result = IconLibraryNorm.Normalize(input, msg => warning = msg);

        Assert.Equal(expected, result);
        Assert.NotNull(warning);
        Assert.Contains("legacy alias", warning, StringComparison.OrdinalIgnoreCase);
    }

    // ── unmappable legacy names → warned and suppressed (existing value preserved) ──

    [Theory]
    [InlineData("font-awesome")]
    [InlineData("material-design")]
    [InlineData("ionicons")]
    [InlineData("devicon")]
    [InlineData("flag-icons")]
    public void Normalize_UnmappableLegacyName_ReturnsNull_AndWarns(string input)
    {
        string? warning = null;
        var result = IconLibraryNorm.Normalize(input, msg => warning = msg);

        Assert.Null(result);
        Assert.NotNull(warning);
        Assert.Contains("no first-party pack", warning, StringComparison.OrdinalIgnoreCase);
    }

    // ── tombstoned codec index decoded to "" → same warn-and-suppress path ──

    [Fact]
    public void Normalize_EmptyString_ReturnsNull_AndWarns()
    {
        string? warning = null;
        var result = IconLibraryNorm.Normalize("", msg => warning = msg);

        Assert.Null(result);
        Assert.NotNull(warning);
        Assert.Contains("no first-party pack", warning, StringComparison.OrdinalIgnoreCase);
    }

    // ── null (icon not set by preset) → passes through silently ──

    [Fact]
    public void Normalize_Null_ReturnsNull_NoWarning()
    {
        string? warning = null;
        var result = IconLibraryNorm.Normalize(null, msg => warning = msg);

        Assert.Null(result);
        Assert.Null(warning); // must not warn — null means "not set", not "legacy"
    }

    // ── all catalog IDs (base packs + variant IDs) → pass through unchanged, no warning ──

    public static IEnumerable<object[]> AllCatalogKeys =>
        IconPackCatalog.FirstParty.Select(p => new object[] { p.Key });

    [Theory]
    [MemberData(nameof(AllCatalogKeys))]
    public void Normalize_AllCatalogIds_PassThrough_NoWarning(string key)
    {
        string? warning = null;
        var result = IconLibraryNorm.Normalize(key, msg => warning = msg);

        Assert.Equal(key, result);
        Assert.Null(warning);
    }

    // ── drift guard: CatalogKeys must equal IconPackCatalog.FirstParty keys ──────

    [Fact]
    public void CatalogKeys_MatchesIconPackCatalog_FirstParty()
    {
        var catalogKeys = IconPackCatalog.FirstParty.Select(p => p.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cliKeys = IconLibraryNorm.CatalogKeys;

        var missingInCli = catalogKeys.Except(cliKeys, StringComparer.OrdinalIgnoreCase).OrderBy(k => k).ToList();
        var extraInCli   = cliKeys.Except(catalogKeys, StringComparer.OrdinalIgnoreCase).OrderBy(k => k).ToList();

        Assert.True(missingInCli.Count == 0 && extraInCli.Count == 0,
            $"CatalogKeys drift detected.\n" +
            $"  In IconPackCatalog but missing from IconLibraryNorm.CatalogKeys: [{string.Join(", ", missingInCli)}]\n" +
            $"  In IconLibraryNorm.CatalogKeys but not in IconPackCatalog:        [{string.Join(", ", extraInCli)}]");
    }

    // ── all Packages entries have a NuGet package name (no blanks) ───────────────

    [Fact]
    public void Packages_AllEntries_HaveNonEmptyNuGetPackage()
    {
        var blanks = IconLibraryNorm.Packages
            .Where(kv => string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => kv.Key)
            .OrderBy(k => k)
            .ToList();

        Assert.True(blanks.Count == 0,
            $"Packages entries with empty NuGet name: [{string.Join(", ", blanks)}]");
    }

    // ── revert-proof: font-awesome must NEVER be written (regression guard) ──

    [Fact]
    public void Normalize_FontAwesome_IsNeverReturned_AsNonNull()
    {
        // If this fails someone reverted the server-preset legacy normalisation.
        // font-awesome has no Lumeo.Icons.* pack and must never reach lumeo.json.
        var result = IconLibraryNorm.Normalize("font-awesome");

        Assert.Null(result);
    }
}
