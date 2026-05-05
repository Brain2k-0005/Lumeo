namespace Lumeo.Theming;

/// <summary>
/// Customizer state captured on the /themes page — indexes into the option arrays
/// (baseColors, radii, fonts, etc.) defined by the customizer. The codec round-trips
/// a <see cref="LumeoPreset"/> to/from a short base62 code so users can share their
/// configuration via a single copy-paste string (e.g. <c>b4Ndd7</c>).
/// </summary>
public sealed record LumeoPreset(
    int Theme,
    int Style,
    int BaseColor,
    int Radius,
    int Font,
    int IconLibrary,
    int MenuColor,
    int MenuAccent,
    int Dark
);

/// <summary>
/// Encodes a <see cref="LumeoPreset"/> as a short base62 string and back. Each option
/// is packed into a fixed-width bit slot; the whole thing is then base62-encoded so it's
/// short (6 chars) and URL-safe. The version header lets us evolve the encoding if we
/// add new options — decoders reject unknown versions.
///
/// Namespace prefix scheme (rc.19): emitted codes are prefixed with <c>l_</c> to
/// disambiguate from server-stored Worker IDs (which use <c>p_</c>). Both spaces are
/// 6-char Base62 — without a prefix, some Worker IDs collide with valid v1 local codes
/// and silently get mis-decoded client-side. <see cref="TryDecode"/> accepts both
/// <c>l_&lt;6&gt;</c> and bare <c>&lt;6&gt;</c> (legacy) but rejects <c>p_</c>-prefixed
/// inputs so they're forced to the server.
/// </summary>
public static class LumeoPresetCodec
{
    private const string Base62Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const int CurrentVersion = 1;
    private const int CodeLength = 6; // 6 * log2(62) ≈ 35.7 bits of capacity; we use 26.

    /// <summary>Prefix for client-side codes minted by <see cref="Encode"/>.</summary>
    public const string LocalPrefix = "l_";

    /// <summary>Prefix for server-stored Worker IDs. Reserved — never accepted by <see cref="TryDecode"/>.</summary>
    public const string ServerPrefix = "p_";

    // (bitWidth, maxValue+1) per field — keep in same order as LumeoPreset positional args.
    private static readonly (int Width, int Max)[] FieldSpec =
    {
        (3, 8),    // Version
        (4, 16),   // Theme
        (1, 2),    // Style
        (3, 8),    // BaseColor
        (3, 8),    // Radius
        (3, 8),    // Font
        (4, 16),   // IconLibrary
        (2, 4),    // MenuColor
        (2, 4),    // MenuAccent
        (1, 2),    // Dark
    };

    public static string Encode(LumeoPreset preset)
    {
        var values = new[]
        {
            CurrentVersion,
            preset.Theme, preset.Style, preset.BaseColor, preset.Radius,
            preset.Font, preset.IconLibrary, preset.MenuColor, preset.MenuAccent,
            preset.Dark,
        };

        ulong bits = 0;
        int pos = 0;
        for (int i = 0; i < values.Length; i++)
        {
            var (width, max) = FieldSpec[i];
            var v = values[i];
            if (v < 0 || v >= max)
            {
                throw new ArgumentOutOfRangeException(nameof(preset), $"Field {i} value {v} exceeds {width}-bit slot (max {max - 1}).");
            }
            bits |= ((ulong)v & ((1UL << width) - 1)) << pos;
            pos += width;
        }

        // Prefix the output so consumers can route unambiguously between local codecs
        // and the server's Worker IDs (rc.19 namespace fix).
        return LocalPrefix + Base62Encode(bits);
    }

    public static bool TryDecode(string code, out LumeoPreset preset)
    {
        preset = new LumeoPreset(0, 0, 0, 0, 0, 0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(code)) return false;

        // Server-namespaced inputs must never decode locally — they belong to the Worker.
        if (code.StartsWith(ServerPrefix, StringComparison.Ordinal)) return false;

        // Strip the local prefix if present; bare 6-char inputs stay supported for
        // backward compatibility with codes minted before rc.19.
        if (code.StartsWith(LocalPrefix, StringComparison.Ordinal))
            code = code.Substring(LocalPrefix.Length);

        if (code.Length != CodeLength) return false;

        ulong bits;
        try { bits = Base62Decode(code); }
        catch { return false; }

        var values = new int[FieldSpec.Length];
        int pos = 0;
        for (int i = 0; i < FieldSpec.Length; i++)
        {
            var (width, _) = FieldSpec[i];
            values[i] = (int)((bits >> pos) & ((1UL << width) - 1));
            pos += width;
        }

        if (values[0] != CurrentVersion) return false;

        preset = new LumeoPreset(
            Theme: values[1],
            Style: values[2],
            BaseColor: values[3],
            Radius: values[4],
            Font: values[5],
            IconLibrary: values[6],
            MenuColor: values[7],
            MenuAccent: values[8],
            Dark: values[9]
        );
        return true;
    }

    private static string Base62Encode(ulong value)
    {
        var buf = new char[CodeLength];
        for (int i = 0; i < CodeLength; i++)
        {
            buf[i] = Base62Alphabet[(int)(value % 62)];
            value /= 62;
        }
        return new string(buf);
    }

    private static ulong Base62Decode(string code)
    {
        ulong value = 0;
        ulong multiplier = 1;
        for (int i = 0; i < code.Length; i++)
        {
            var idx = Base62Alphabet.IndexOf(code[i]);
            if (idx < 0) throw new FormatException($"Invalid base62 character '{code[i]}'.");
            value += (ulong)idx * multiplier;
            multiplier *= 62;
        }
        return value;
    }
}
