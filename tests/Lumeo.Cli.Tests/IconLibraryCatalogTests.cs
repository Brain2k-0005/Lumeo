using Lumeo.Cli;
using Xunit;

namespace Lumeo.Cli.Tests;

/// <summary>
/// Verifies that the CLI icon-library catalog (<see cref="LumeoPresetOptions.IconLibraries"/>)
/// exposes only first-party Lumeo.Icons.* pack names and that the codec round-trip and
/// legacy-compat decode behave correctly after removing the Blazicons entries.
/// </summary>
public class IconLibraryCatalogTests
{
    // ── encoder rejects every former Blazicons / legacy name ──────────────────

    [Theory]
    [InlineData("font-awesome")]
    [InlineData("material-design")]
    [InlineData("ionicons")]
    [InlineData("devicon")]
    [InlineData("flag-icons")]
    [InlineData("fluentui")]
    [InlineData("google-material")]
    public void IconLibraries_DoesNotContain_LegacyName(string legacyName)
    {
        // Idx() uses Array.FindIndex, so a name absent from the array is rejected with an error.
        Assert.DoesNotContain(legacyName, LumeoPresetOptions.IconLibraries, StringComparer.OrdinalIgnoreCase);
    }

    // ── catalog contains every first-party pack encodeable in the 4-bit slot ─

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
    public void IconLibraries_Contains_FirstPartyName(string name)
    {
        Assert.Contains(name, LumeoPresetOptions.IconLibraries, StringComparer.OrdinalIgnoreCase);
    }

    // ── tombstoned legacy indices decode to empty string, not the old name ───

    [Theory]
    [InlineData(3)]  // was "font-awesome"
    [InlineData(5)]  // was "material-design"
    [InlineData(6)]  // was "ionicons"
    [InlineData(7)]  // was "devicon"
    [InlineData(8)]  // was "flag-icons"
    public void Decode_TombstonedIndex_ReturnsEmpty(int index)
    {
        var decoded = LumeoPresetOptions.At(LumeoPresetOptions.IconLibraries, index, "lucide");
        // The tombstone "" is in range, so At() returns "" (not the default "lucide").
        Assert.Equal("", decoded);
    }

    // ── legacy Blazicons indices now decode to their first-party equivalents ──

    [Fact]
    public void Decode_Index2_ReturnsFluentNotFluentUi()
    {
        // Old client presets stored index 2 for "fluentui". After the fix the slot holds
        // "fluent" — the canonical first-party name — so apply installs Lumeo.Icons.Fluent.
        Assert.Equal("fluent", LumeoPresetOptions.At(LumeoPresetOptions.IconLibraries, 2, "lucide"));
    }

    [Fact]
    public void Decode_Index4_ReturnsMaterialSymbolsNotGoogleMaterial()
    {
        // Old client presets stored index 4 for "google-material". After the fix the slot
        // holds "material-symbols" — the canonical first-party name.
        Assert.Equal("material-symbols", LumeoPresetOptions.At(LumeoPresetOptions.IconLibraries, 4, "lucide"));
    }

    // ── first-party names round-trip at their expected stable indices ─────────

    [Theory]
    [InlineData("lucide",                   0)]
    [InlineData("bootstrap",                1)]
    [InlineData("fluent",                   2)]
    [InlineData("material-symbols",         4)]
    [InlineData("tabler",                   9)]
    [InlineData("phosphor",                10)]
    [InlineData("heroicons",               11)]
    [InlineData("remix",                   12)]
    [InlineData("iconoir",                 13)]
    [InlineData("material-symbols-rounded",14)]
    [InlineData("material-symbols-sharp",  15)]
    public void FirstParty_IndexIsStable(string name, int expectedIndex)
    {
        Assert.Equal(expectedIndex, Array.IndexOf(LumeoPresetOptions.IconLibraries, name));
        Assert.Equal(name, LumeoPresetOptions.At(LumeoPresetOptions.IconLibraries, expectedIndex));
    }

    // ── codec stays within the 4-bit field limit of 16 entries ───────────────

    [Fact]
    public void IconLibraries_HasAtMost16Entries()
    {
        // The IconLibrary field is 4 bits wide (max index 15, per LumeoPresetCodec.FieldSpec).
        Assert.True(LumeoPresetOptions.IconLibraries.Length <= 16,
            $"IconLibraries has {LumeoPresetOptions.IconLibraries.Length} entries but the codec only allows 16.");
    }
}
