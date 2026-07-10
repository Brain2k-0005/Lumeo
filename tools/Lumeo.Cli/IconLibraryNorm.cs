namespace Lumeo.Cli;

/// <summary>
/// Maps every CLI icon-library ID to its first-party Lumeo.Icons NuGet package and
/// provides the single normalisation gate used at <c>apply</c> Step 1b — covering
/// tombstoned codec indices (""), server-preset legacy strings (fluentui, font-awesome,
/// …), already-canonical first-party names, and customizer variant IDs
/// (fluent-filled, tabler-filled, heroicons-solid, …).
/// </summary>
internal static class IconLibraryNorm
{
    /// <summary>
    /// All first-party pack IDs that the customizer/IconPackCatalog exposes —
    /// both base packs (also in <see cref="LumeoPresetOptions.IconLibraries"/>) and
    /// variant IDs (e.g. fluent-filled, tabler-filled, heroicons-solid).
    /// Mirrors <c>IconPackCatalog.FirstParty.Select(p => p.Key)</c>; a guard test in
    /// Lumeo.Cli.Tests diffs this set against the live catalog so drift fails CI.
    /// </summary>
    internal static readonly HashSet<string> CatalogKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "lucide",
        "tabler", "tabler-filled",
        "phosphor", "phosphor-bold", "phosphor-fill", "phosphor-duotone", "phosphor-light", "phosphor-thin",
        "heroicons", "heroicons-solid", "heroicons-mini", "heroicons-micro",
        "remix", "remix-filled",
        "bootstrap",
        "iconoir",
        "material-symbols", "material-symbols-filled",
        "material-symbols-rounded", "material-symbols-rounded-filled",
        "material-symbols-sharp", "material-symbols-sharp-filled",
        "fluent", "fluent-filled",
    };

    /// <summary>
    /// Maps every CLI icon-library ID (first-party base packs, catalog variant IDs, and
    /// legacy aliases) to the corresponding first-party Lumeo.Icons NuGet package.
    /// Single source of truth for <c>apply</c> install + icon normalisation.
    /// All keys in <see cref="CatalogKeys"/> must be present here.
    /// </summary>
    internal static readonly Dictionary<string, string> Packages =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // ── base packs (also in LumeoPresetOptions.IconLibraries) ──────────────
            ["lucide"]                         = "Lumeo.Icons.Lucide",
            ["bootstrap"]                      = "Lumeo.Icons.Bootstrap",
            ["fluent"]                         = "Lumeo.Icons.Fluent",
            ["material-symbols"]               = "Lumeo.Icons.MaterialSymbols",
            ["material-symbols-rounded"]       = "Lumeo.Icons.MaterialSymbols.Rounded",
            ["material-symbols-sharp"]         = "Lumeo.Icons.MaterialSymbols.Sharp",
            ["tabler"]                         = "Lumeo.Icons.Tabler",
            ["phosphor"]                       = "Lumeo.Icons.Phosphor",
            ["phosphor-bold"]                  = "Lumeo.Icons.Phosphor.Bold",
            ["phosphor-duotone"]               = "Lumeo.Icons.Phosphor.Duotone",
            ["phosphor-fill"]                  = "Lumeo.Icons.Phosphor.Fill",
            ["phosphor-light"]                 = "Lumeo.Icons.Phosphor.Light",
            ["phosphor-thin"]                  = "Lumeo.Icons.Phosphor.Thin",
            ["heroicons"]                      = "Lumeo.Icons.Heroicons",
            ["remix"]                          = "Lumeo.Icons.Remix",
            ["iconoir"]                        = "Lumeo.Icons.Iconoir",
            // ── catalog variant IDs (same NuGet as their base pack) ───────────────
            ["fluent-filled"]                  = "Lumeo.Icons.Fluent",
            ["tabler-filled"]                  = "Lumeo.Icons.Tabler",
            ["heroicons-solid"]                = "Lumeo.Icons.Heroicons",
            ["heroicons-mini"]                 = "Lumeo.Icons.Heroicons",
            ["heroicons-micro"]                = "Lumeo.Icons.Heroicons",
            ["remix-filled"]                   = "Lumeo.Icons.Remix",
            ["material-symbols-filled"]        = "Lumeo.Icons.MaterialSymbols",
            ["material-symbols-rounded-filled"]= "Lumeo.Icons.MaterialSymbols.Rounded",
            ["material-symbols-sharp-filled"]  = "Lumeo.Icons.MaterialSymbols.Sharp",
            // ── legacy keys — present in server-stored presets created before the
            //    icon decouple. Not in CatalogKeys — Normalize rewrites them. ───────
            ["fluentui"]                       = "Lumeo.Icons.Fluent",
            ["google-material"]                = "Lumeo.Icons.MaterialSymbols",
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
    ///   <item><paramref name="value"/> is a first-party catalog ID (base pack or variant) → passes through unchanged, no warning.</item>
    ///   <item><paramref name="value"/> is a mappable legacy alias (e.g. "fluentui") → rewrites to canonical name and warns.</item>
    ///   <item><paramref name="value"/> is an unmappable legacy name (e.g. "font-awesome") → warns and returns <see langword="null"/>.</item>
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

        // All first-party catalog IDs (base packs + variant IDs) pass through unchanged.
        if (CatalogKeys.Contains(value)) return value;

        // Legacy alias — still in Packages but no longer in the catalog (e.g. "fluentui").
        // Rewrite to the canonical base-pack name (the matching key in IconLibraries).
        if (Packages.TryGetValue(value, out var pkg))
        {
            var canonical = Packages
                .FirstOrDefault(kv =>
                    kv.Value == pkg &&
                    Array.IndexOf(LumeoPresetOptions.IconLibraries, kv.Key) >= 0)
                .Key;

            if (canonical is not null)
            {
                warn?.Invoke($"Icon library '{value}' is a legacy alias — rewriting to '{canonical}'.");
                return canonical;
            }
        }

        // Unmappable legacy name (e.g. font-awesome, ionicons, devicon, flag-icons).
        warn?.Invoke($"Icon library '{value}' has no first-party pack; skipping icon selection.");
        return null;
    }
}
