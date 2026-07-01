using Lumeo.Theming;
using Xunit;

namespace Lumeo.Tests.Theming;

/// <summary>
/// Regression tests for the rc.19 namespace-prefix scheme on preset codes.
///
/// Both the local codec and the Cloudflare Worker mint 6-char Base62 IDs, and the
/// CLI used to "try local decode first, fall back to server" — which silently
/// mis-routed any Worker ID that happened to be a valid v1 local code (e.g. the
/// reviewer's reproducer "H3FLCh"). The fix prefixes new local codes with "l_"
/// and new server IDs with "p_", and the codec rejects "p_"-prefixed inputs.
/// </summary>
public class LumeoPresetCodecPrefixTests
{
    private static LumeoPreset SamplePreset() => new(
        Theme: 1, Style: 0, BaseColor: 2, Radius: 2,
        Font: 1, IconLibrary: 0, MenuColor: 0, MenuAccent: 0, Dark: 0);

    [Fact]
    public void Encode_emits_l_prefix()
    {
        var code = LumeoPresetCodec.Encode(SamplePreset());

        Assert.StartsWith("l_", code);
        Assert.Equal(8, code.Length); // "l_" + 6-char Base62 payload
    }

    // --- Wire-format golden values (cross-sync guard) ---
    //
    // The CLI ships a hand-maintained DUPLICATE of this codec
    // (tools/Lumeo.Cli/PresetCodec.cs, "KEEP IN SYNC"). These golden strings are
    // pinned identically in Lumeo.Cli.Tests.PresetCodecTests — if either copy's
    // alphabet, version, field widths or bit-packing drifts, exactly one suite's
    // golden breaks, surfacing the divergence instead of letting the two encoders
    // silently disagree about the same code.

    [Fact]
    public void Encode_zero_preset_is_the_pinned_wire_format()
    {
        var zero = new LumeoPreset(0, 0, 0, 0, 0, 0, 0, 0, 0);

        // Only the version field (=1) is set, at bit 0 -> bits == 1 -> base62 "100000".
        Assert.Equal("l_100000", LumeoPresetCodec.Encode(zero));
    }

    [Fact]
    public void Encode_sample_preset_is_the_pinned_wire_format()
    {
        Assert.Equal("l_Js5000", LumeoPresetCodec.Encode(SamplePreset()));
    }

    [Fact]
    public void Decode_accepts_both_prefixed_and_legacy()
    {
        var preset = SamplePreset();
        var prefixed = LumeoPresetCodec.Encode(preset);
        Assert.StartsWith("l_", prefixed);

        var bare = prefixed.Substring(2); // legacy unprefixed form

        Assert.True(LumeoPresetCodec.TryDecode(prefixed, out var fromPrefixed));
        Assert.True(LumeoPresetCodec.TryDecode(bare, out var fromBare));

        Assert.Equal(preset, fromPrefixed);
        Assert.Equal(preset, fromBare);
    }

    [Fact]
    public void Decode_rejects_p_prefix()
    {
        // Server-namespaced inputs must never decode locally — they belong to the Worker.
        Assert.False(LumeoPresetCodec.TryDecode("p_H3FLCh", out _));

        // Same payload sans prefix is an arbitrary 6-char Base62 string; whether it
        // decodes locally is an artefact of the legacy collision and isn't the
        // point here — only that "p_" is rejected outright.
    }

    [Fact]
    public void Reproducer_H3FLCh_no_longer_decoded_locally_when_p_prefixed()
    {
        // The external review reproduced a Worker ID ("H3FLCh") that decoded as a
        // valid v1 local code under the old "try local first" resolver. With the
        // prefix scheme, the Worker now ships it as "p_H3FLCh" and the local
        // codec must refuse it — forcing the CLI to hit the server.
        Assert.False(LumeoPresetCodec.TryDecode("p_H3FLCh", out _));

        // Bare "H3FLCh" (legacy unprefixed) may still attempt local decode. This
        // is documented backward-compat behaviour; the prefix scheme only fixes
        // forward-shipped codes. The CLI's resolver narrows the window further by
        // applying a strict version check before committing to local.
    }
}
