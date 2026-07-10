namespace Lumeo.IconGen;

/// <summary>How the generated pack's icons are painted — mirrors <c>Lumeo.IconRenderStyle</c>.</summary>
public enum GenRenderStyle
{
    /// <summary>Outline icons — emitted as <c>IconSource.Stroke(...)</c>.</summary>
    Stroke,

    /// <summary>Solid icons — emitted as <c>IconSource.Fill(...)</c>.</summary>
    Fill,
}

/// <summary>
/// Generic, per-pack generation config. One instance describes one upstream icon family completely:
/// where to fetch it, which files to include, how to name/paint the icons, and where to emit the
/// generated C#. Phase 0 uses a single instance (Lucide → <c>LumeoIcons</c>); Phase 1 adds one per
/// pack (Tabler, Phosphor weights, …) without touching the pipeline.
/// </summary>
public sealed record PackConfig
{
    /// <summary>Human label for logs, e.g. <c>"Lucide"</c>.</summary>
    public required string PackName { get; init; }

    /// <summary>Pinned upstream version recorded in the generated header, e.g. <c>"1.23.0"</c>.</summary>
    public required string Version { get; init; }

    /// <summary>Upstream release zip URL containing the SVGs.</summary>
    public required string ZipUrl { get; init; }

    /// <summary>Local cache filename for the zip (under <c>.cache</c>).</summary>
    public required string ZipCacheName { get; init; }

    /// <summary>
    /// Predicate over a zip entry's full path selecting the SVG files to include
    /// (e.g. entry starts with <c>"icons/"</c> and ends with <c>".svg"</c>).
    /// </summary>
    public required Func<string, bool> EntryFilter { get; init; }

    /// <summary>Render style for every icon in the pack.</summary>
    public required GenRenderStyle Style { get; init; }

    /// <summary>Root stroke width for <see cref="GenRenderStyle.Stroke"/> packs (Lucide/Tabler = 2).</summary>
    public double StrokeWidth { get; init; } = 2;

    /// <summary>Fully-qualified emitted namespace, e.g. <c>"Lumeo"</c> or <c>"Lumeo.Icons"</c>.</summary>
    public required string Namespace { get; init; }

    /// <summary>Emitted class name, e.g. <c>"LumeoIcons"</c>, <c>"Lucide"</c>, <c>"Tabler"</c>.</summary>
    public required string ClassName { get; init; }

    /// <summary>Absolute output directory for the <c>.g.cs</c> chunk(s) + <c>manifest.json</c>.</summary>
    public required string OutputDir { get; init; }

    /// <summary>Base file name (without extension) for the generated chunk(s), e.g. <c>"LumeoIcons"</c>.</summary>
    public required string OutputBaseName { get; init; }

    /// <summary>License header text (comment body) embedded verbatim atop each generated file.</summary>
    public required string LicenseHeader { get; init; }

    /// <summary>Max icons per generated partial file (IDE sanity). A single file is emitted below this.</summary>
    public int ChunkSize { get; init; } = 500;

    /// <summary>
    /// Optional filter restricting emission to a specific set of PascalCase names (Phase 0 emits only
    /// the union of icons referenced under <c>src/</c>). <c>null</c> emits every icon in the pack.
    /// </summary>
    public Func<string, bool>? NameFilter { get; init; }

    /// <summary>
    /// Optional transform applied to the raw upstream file name (without extension) BEFORE
    /// PascalCasing. Phosphor weights encode the weight in the file name (e.g.
    /// <c>house-duotone.svg</c>); the weight lives in the class (<c>PhosphorDuotone</c>), not the
    /// member, so the suffix is stripped here to yield <c>House</c>. <c>null</c> is identity.
    /// </summary>
    public Func<string, string>? UpstreamNameTransform { get; init; }

    /// <summary>
    /// Overrides / additions keyed by PascalCase name, merged over the upstream SVGs. Used to vendor
    /// icons that upstream dropped (e.g. Lucide removed brand marks like <c>github</c> from core).
    /// Value is a raw <c>&lt;svg&gt;</c> document parsed the same way as an upstream file.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Overrides { get; init; }
}

/// <summary>Built-in overrides for the Lucide pack.</summary>
public static class LucideOverrides
{
    // Lucide removed brand icons (github, etc.) from its core set, but Lumeo references
    // `Lucide.Github`, so the vendored set must still cover it. This is the canonical Lucide
    // github mark (ISC, from Lucide's last brand-icon release), 24x24 stroke.
    public static readonly IReadOnlyDictionary<string, string> Map = new Dictionary<string, string>
    {
        ["Github"] =
            """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
              <path d="M15 22v-4a4.8 4.8 0 0 0-1-3.5c3 0 6-2 6-5.5.08-1.25-.27-2.48-1-3.5.28-1.15.28-2.35 0-3.5 0 0-1 0-3 1.5-2.64-.5-5.36-.5-8 0C6 2 5 2 5 2c-.3 1.15-.3 2.35 0 3.5A5.403 5.403 0 0 0 4 9c0 3.5 3 5.5 6 5.5-.39.49-.68 1.05-.85 1.65-.17.6-.22 1.23-.15 1.85v4" />
              <path d="M9 18c-4.51 2-5-2-7-2" />
            </svg>
            """,
    };
}
