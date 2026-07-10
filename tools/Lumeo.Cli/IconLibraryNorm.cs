namespace Lumeo.Cli;

/// <summary>
/// Maps every CLI icon-library ID to its first-party Lumeo.Icons NuGet package and
/// provides the single normalisation gate used at <c>apply</c> Step 5b — covering
/// tombstoned codec indices (""), server-preset legacy strings (fluentui, font-awesome,
/// …), and already-canonical first-party names.
/// </summary>
internal static class IconLibraryNorm
{
    /// <summary>
    /// Maps every CLI icon-library ID (first-party and legacy aliases) to the
    /// corresponding first-party Lumeo.Icons NuGet package.
    /// Single source of truth for <c>apply</c> install + icon normalisation.
    /// Keep in sync with <see cref="LumeoPresetOptions.IconLibraries"/>.
    /// </summary>
    internal static readonly Dictionary<string, string> Packages =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // First-party packs — canonical CLI IDs matching LumeoPresetOptions.IconLibraries.
            ["lucide"]                   = "Lumeo.Icons.Lucide",
            ["bootstrap"]                = "Lumeo.Icons.Bootstrap",
            ["fluent"]                   = "Lumeo.Icons.Fluent",
            ["material-symbols"]         = "Lumeo.Icons.MaterialSymbols",
            ["material-symbols-rounded"] = "Lumeo.Icons.MaterialSymbols.Rounded",
            ["material-symbols-sharp"]   = "Lumeo.Icons.MaterialSymbols.Sharp",
            ["tabler"]                   = "Lumeo.Icons.Tabler",
            ["phosphor"]                 = "Lumeo.Icons.Phosphor",
            ["phosphor-bold"]            = "Lumeo.Icons.Phosphor.Bold",
            ["phosphor-duotone"]         = "Lumeo.Icons.Phosphor.Duotone",
            ["phosphor-fill"]            = "Lumeo.Icons.Phosphor.Fill",
            ["phosphor-light"]           = "Lumeo.Icons.Phosphor.Light",
            ["phosphor-thin"]            = "Lumeo.Icons.Phosphor.Thin",
            ["heroicons"]                = "Lumeo.Icons.Heroicons",
            ["remix"]                    = "Lumeo.Icons.Remix",
            ["iconoir"]                  = "Lumeo.Icons.Iconoir",
            // Legacy keys — present in server-stored presets created before the icon
            // decouple. Client presets at the matching codec indices now decode to the
            // canonical first-party names above, so these are only reachable from server JSON.
            ["fluentui"]                 = "Lumeo.Icons.Fluent",
            ["google-material"]          = "Lumeo.Icons.MaterialSymbols",
        };

    /// <summary>
    /// Normalises a raw icon-library value from ANY preset source (local codec index
    /// decoded to string, or server-preset JSON string) to a canonical first-party name,
    /// or <see langword="null"/> if the value must be suppressed.
    /// </summary>
    /// <remarks>
    /// This is the ONE gate for every source — do not add a parallel guard elsewhere.
    /// <list type="bullet">
    ///   <item><paramref name="value"/> is <see langword="null"/> → returns <see langword="null"/> silently (not set by preset).</item>
    ///   <item><paramref name="value"/> is <c>""</c> → tombstoned codec index; warns and returns <see langword="null"/>.</item>
    ///   <item><paramref name="value"/> is a mappable legacy alias (e.g. "fluentui") → rewrites to canonical name and warns.</item>
    ///   <item><paramref name="value"/> is an unmappable legacy name (e.g. "font-awesome") → warns and returns <see langword="null"/>.</item>
    ///   <item><paramref name="value"/> is already a canonical first-party name → returns it unchanged, no warning.</item>
    /// </list>
    /// </remarks>
    /// <param name="value">The raw <c>iconLibrary</c> string from the decoded preset.</param>
    /// <param name="warn">
    ///   Optional callback invoked with a plain-text warning message (no ANSI codes)
    ///   when the value is suppressed or rewritten.
    /// </param>
    internal static string? Normalize(string? value, Action<string>? warn = null)
    {
        if (value is null) return null;

        if (value.Length == 0)
        {
            warn?.Invoke("Icon library in this preset has no first-party pack; skipping icon selection.");
            return null;
        }

        if (Packages.TryGetValue(value, out var pkg))
        {
            // Find the canonical (first-party) key for this NuGet package — i.e. the
            // entry in Packages whose key also appears in LumeoPresetOptions.IconLibraries.
            var canonical = Packages
                .FirstOrDefault(kv =>
                    kv.Value == pkg &&
                    Array.IndexOf(LumeoPresetOptions.IconLibraries, kv.Key) >= 0)
                .Key;

            if (canonical is not null &&
                !string.Equals(canonical, value, StringComparison.OrdinalIgnoreCase))
            {
                warn?.Invoke($"Icon library '{value}' is a legacy alias — rewriting to '{canonical}'.");
                return canonical;
            }

            return value; // already canonical — pass through unchanged
        }

        // Unmappable legacy name (e.g. font-awesome, ionicons, devicon, flag-icons).
        warn?.Invoke($"Icon library '{value}' has no first-party pack; skipping icon selection.");
        return null;
    }
}
