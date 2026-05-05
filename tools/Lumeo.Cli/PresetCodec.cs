// Duplicate of Lumeo.Theming.LumeoPresetCodec / LumeoPresetOptions.
// Kept here to avoid pulling Blazor references into the CLI.
// KEEP IN SYNC with src/Lumeo/Theming/LumeoPresetCodec.cs + LumeoPresetOptions.cs.

namespace Lumeo.Cli;

/// <summary>Duplicate of <c>Lumeo.Theming.LumeoPresetApi.BaseUrl</c>.
/// KEEP IN SYNC with src/Lumeo/Theming/LumeoPresetApi.cs.</summary>
internal static class LumeoPresetApi
{
    public const string BaseUrl = "https://api.lumeo.nativ.sh";
}

internal sealed record LumeoPreset(
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

internal static class LumeoPresetCodec
{
    private const string Base62Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const int CurrentVersion = 1;
    private const int CodeLength = 6;

    // Namespace prefixes — keep in sync with src/Lumeo/Theming/LumeoPresetCodec.cs.
    public const string LocalPrefix = "l_";
    public const string ServerPrefix = "p_";

    private static readonly (int Width, int Max)[] FieldSpec =
    {
        (3, 8), (4, 16), (1, 2), (3, 8), (3, 8),
        (3, 8), (4, 16), (2, 4), (2, 4), (1, 2),
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
                throw new ArgumentOutOfRangeException(nameof(preset), $"Field {i} value {v} exceeds {width}-bit slot (max {max - 1}).");
            bits |= ((ulong)v & ((1UL << width) - 1)) << pos;
            pos += width;
        }
        var buf = new char[CodeLength];
        for (int i = 0; i < CodeLength; i++)
        {
            buf[i] = Base62Alphabet[(int)(bits % 62)];
            bits /= 62;
        }
        return LocalPrefix + new string(buf);
    }

    public static bool TryDecode(string code, out LumeoPreset preset)
    {
        preset = new LumeoPreset(0, 0, 0, 0, 0, 0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(code)) return false;
        if (code.StartsWith(ServerPrefix, StringComparison.Ordinal)) return false;
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

        preset = new LumeoPreset(values[1], values[2], values[3], values[4], values[5],
                                 values[6], values[7], values[8], values[9]);
        return true;
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

internal static class LumeoPresetOptions
{
    public static readonly string[] Themes = { "", "blue", "orange", "green", "rose", "zinc", "violet", "amber", "teal" };
    public static readonly string[] Styles = { "default", "new-york" };
    public static readonly string[] BaseColors = { "slate", "gray", "zinc", "neutral", "stone" };
    public static readonly string[] Radii = { "0", "0.25", "0.5", "0.75", "1" };
    public static readonly string[] Fonts = { "system", "inter", "geist", "ibm-plex-sans", "jetbrains-mono", "fira-code" };
    public static readonly string[] IconLibraries = { "lucide", "bootstrap", "fluentui", "font-awesome", "google-material", "material-design", "ionicons", "devicon", "flag-icons" };
    public static readonly string[] MenuColors = { "default", "dark", "light" };
    public static readonly string[] MenuAccents = { "subtle", "bold", "outline" };

    public static string At(string[] arr, int index, string @default = "")
        => index >= 0 && index < arr.Length ? arr[index] : @default;
}
