using Lumeo.Cli;
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

    // ── already-canonical first-party names → pass through unchanged, no warning ──

    [Theory]
    [InlineData("lucide")]
    [InlineData("bootstrap")]
    [InlineData("fluent")]
    [InlineData("material-symbols")]
    [InlineData("tabler")]
    [InlineData("phosphor")]
    [InlineData("heroicons")]
    [InlineData("remix")]
    [InlineData("iconoir")]
    [InlineData("material-symbols-rounded")]
    [InlineData("material-symbols-sharp")]
    public void Normalize_FirstPartyName_PassesThrough_NoWarning(string input)
    {
        string? warning = null;
        var result = IconLibraryNorm.Normalize(input, msg => warning = msg);

        Assert.Equal(input, result);
        Assert.Null(warning);
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
