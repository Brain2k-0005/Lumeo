using Lumeo.Cli;
using Xunit;

namespace Lumeo.Cli.Tests;

/// <summary>
/// The CLI's <see cref="LumeoPresetCodec"/> is a deliberate duplicate of
/// <c>src/Lumeo/Theming/LumeoPresetCodec.cs</c> (kept Blazor-free for the tool).
/// These tests pin the wire format so the two copies can't silently drift, and
/// cover the base62 round-trip, the version gate, prefix handling and the
/// per-field bit-width guards.
/// </summary>
public class PresetCodecTests
{
    private static LumeoPreset Zero => new(0, 0, 0, 0, 0, 0, 0, 0, 0);

    // A preset exercising the top of every field's bit-slot:
    // Theme 0-15, Style 0-1, BaseColor 0-7, Radius 0-7, Font 0-7,
    // IconLibrary 0-15, MenuColor 0-3, MenuAccent 0-3, Dark 0-1.
    private static LumeoPreset Max => new(15, 1, 7, 7, 7, 15, 3, 3, 1);

    [Fact]
    public void Encode_Prefixes_Local_And_Is_Six_Chars()
    {
        var code = LumeoPresetCodec.Encode(Zero);

        Assert.StartsWith(LumeoPresetCodec.LocalPrefix, code);
        Assert.Equal(LumeoPresetCodec.LocalPrefix.Length + 6, code.Length);
    }

    [Fact]
    public void Encode_Of_The_Zero_Preset_Is_The_Pinned_Wire_Format()
    {
        // Only the version field (=1) is set, at bit 0 -> bits == 1 -> base62 "100000".
        // If this golden value ever changes, the encoding broke compatibility.
        Assert.Equal("l_100000", LumeoPresetCodec.Encode(Zero));
    }

    [Fact]
    public void Encode_Of_The_Sample_Preset_Is_The_Pinned_Wire_Format()
    {
        // Same preset + golden as the library suite's
        // LumeoPresetCodecPrefixTests.Encode_sample_preset_is_the_pinned_wire_format
        // — the two codec copies must agree byte-for-byte (KEEP IN SYNC).
        var sample = new LumeoPreset(1, 0, 2, 2, 1, 0, 0, 0, 0);
        Assert.Equal("l_Js5000", LumeoPresetCodec.Encode(sample));
    }

    // Args are the nine preset fields (built inside, since LumeoPreset is internal
    // and can't appear in a public test signature). Covers the zero floor, every
    // field's ceiling, and two interior mixes.
    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0, 0, 0, 0)]
    [InlineData(15, 1, 7, 7, 7, 15, 3, 3, 1)]
    [InlineData(7, 0, 4, 3, 5, 8, 2, 1, 1)]
    [InlineData(3, 1, 0, 0, 2, 0, 0, 2, 0)]
    public void Encode_Then_TryDecode_Round_Trips(
        int theme, int style, int baseColor, int radius, int font,
        int icon, int menuColor, int menuAccent, int dark)
    {
        var preset = new LumeoPreset(theme, style, baseColor, radius, font, icon, menuColor, menuAccent, dark);

        var code = LumeoPresetCodec.Encode(preset);

        Assert.True(LumeoPresetCodec.TryDecode(code, out var decoded));
        Assert.Equal(preset, decoded);
    }

    [Fact]
    public void TryDecode_Accepts_A_Bare_Code_Without_The_Local_Prefix()
    {
        var prefixed = LumeoPresetCodec.Encode(Max);
        var bare = prefixed.Substring(LumeoPresetCodec.LocalPrefix.Length);

        Assert.True(LumeoPresetCodec.TryDecode(bare, out var decoded));
        Assert.Equal(Max, decoded);
    }

    [Fact]
    public void TryDecode_Rejects_The_Server_Namespace_Prefix()
    {
        // Server-issued presets (p_) are opaque to the local codec.
        Assert.False(LumeoPresetCodec.TryDecode(LumeoPresetCodec.ServerPrefix + "100000", out _));
    }

    [Fact]
    public void TryDecode_Rejects_A_Version_Mismatch()
    {
        // All-zero bits -> version field 0, which is not the current version (1).
        Assert.False(LumeoPresetCodec.TryDecode("l_000000", out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("l_123")]        // too short
    [InlineData("l_1234567")]    // too long
    [InlineData("l_10000!")]     // invalid base62 char
    [InlineData("l_-+/=,.")]     // invalid + wrong length
    public void TryDecode_Rejects_Malformed_Codes(string code)
    {
        Assert.False(LumeoPresetCodec.TryDecode(code, out _));
    }

    [Fact]
    public void TryDecode_Of_Null_Returns_False()
    {
        Assert.False(LumeoPresetCodec.TryDecode(null!, out _));
    }

    [Fact]
    public void Encode_Throws_When_A_Field_Overflows_Its_Bit_Slot()
    {
        // Theme has a 4-bit slot (max 15); 16 doesn't fit.
        var overflow = new LumeoPreset(16, 0, 0, 0, 0, 0, 0, 0, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => LumeoPresetCodec.Encode(overflow));
    }

    [Fact]
    public void Encode_Throws_On_A_Negative_Field()
    {
        var negative = new LumeoPreset(0, 0, 0, 0, 0, 0, 0, 0, -1);

        Assert.Throws<ArgumentOutOfRangeException>(() => LumeoPresetCodec.Encode(negative));
    }
}
