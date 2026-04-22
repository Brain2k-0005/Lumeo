namespace Lumeo.Theming;

/// <summary>
/// Single source of truth for the option catalog referenced by <see cref="LumeoPresetCodec"/>.
/// The index of each entry is the value stored in the encoded preset — so never reorder,
/// only append. If you need to remove an option, replace its slot with a tombstone (keep the
/// array length stable) so old preset codes still decode.
/// </summary>
public static class LumeoPresetOptions
{
    public static readonly string[] Themes =
    {
        "",         // 0 Default
        "blue",     // 1
        "orange",   // 2
        "green",    // 3
        "rose",     // 4
        "zinc",     // 5
        "violet",   // 6
        "amber",    // 7
        "teal",     // 8
    };

    public static readonly string[] Styles =
    {
        "default",   // 0
        "new-york",  // 1
    };

    public static readonly string[] BaseColors =
    {
        "slate",    // 0
        "gray",     // 1
        "zinc",     // 2
        "neutral",  // 3
        "stone",    // 4
    };

    public static readonly string[] Radii =
    {
        "0",      // 0 None
        "0.25",   // 1 Small
        "0.5",    // 2 Default
        "0.75",   // 3 Medium
        "1",      // 4 Large
    };

    public static readonly string[] Fonts =
    {
        "system",          // 0
        "inter",           // 1
        "geist",           // 2
        "ibm-plex-sans",   // 3
        "jetbrains-mono",  // 4
        "fira-code",       // 5
    };

    public static readonly string[] IconLibraries =
    {
        "lucide",           // 0
        "bootstrap",        // 1
        "fluentui",         // 2
        "font-awesome",     // 3
        "google-material",  // 4
        "material-design",  // 5
        "ionicons",         // 6
        "devicon",          // 7
        "flag-icons",       // 8
    };

    public static readonly string[] MenuColors =
    {
        "default",   // 0
        "dark",      // 1
        "light",     // 2
    };

    public static readonly string[] MenuAccents =
    {
        "subtle",    // 0
        "bold",      // 1
        "outline",   // 2
    };

    public static int IndexOf(string[] arr, string value, int @default = 0)
    {
        var i = Array.IndexOf(arr, value);
        return i < 0 ? @default : i;
    }

    public static string At(string[] arr, int index, string @default = "")
    {
        return index >= 0 && index < arr.Length ? arr[index] : @default;
    }
}
