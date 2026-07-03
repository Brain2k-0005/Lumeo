namespace Lumeo.IconGen;

/// <summary>
/// The Phase 1 pack family: every shippable icon pack described as one or more <see cref="PackConfig"/>
/// instances, keyed by a CLI mode name. Unlike the Phase 0 <c>lumeo-icons</c> mode (which vendors only
/// the union of icons referenced under <c>src/</c> into the core), pack modes emit the FULL upstream
/// set into a dedicated <c>src/Lumeo.Icons.*</c> project.
/// </summary>
public static class PackCatalog
{
    // Pinned upstream versions — bump here (and re-run) to re-vendor a newer release.
    private const string LucideVersion = "1.23.0";
    private const string TablerVersion = "3.44.0";
    private const string PhosphorVersion = "2.0.8";

    private const string LucideZipUrl =
        "https://github.com/lucide-icons/lucide/releases/download/1.23.0/lucide-icons-1.23.0.zip";
    private const string LucideZipName = "lucide-icons-1.23.0.zip";

    private const string TablerZipUrl =
        "https://github.com/tabler/tabler-icons/archive/refs/tags/v3.44.0.zip";
    private const string TablerZipName = "tabler-icons-3.44.0.zip";

    private const string PhosphorZipUrl =
        "https://github.com/phosphor-icons/core/archive/refs/tags/v2.0.8.zip";
    private const string PhosphorZipName = "phosphor-core-2.0.8.zip";

    /// <summary>All pack mode names (excludes the Phase 0 <c>lumeo-icons</c> mode and <c>all</c>).</summary>
    public static readonly IReadOnlyList<string> AllModes = new[]
    {
        "lucide", "tabler",
        "phosphor", "phosphor-bold", "phosphor-fill",
        "phosphor-duotone", "phosphor-light", "phosphor-thin",
    };

    /// <summary>
    /// Resolves a CLI mode to the pack config(s) it generates. <c>tabler</c> emits two classes
    /// (<c>Tabler</c> outline + <c>TablerFilled</c>) into one project; each Phosphor weight is its
    /// own mode/project. <c>all</c> returns every pack.
    /// </summary>
    public static IReadOnlyList<PackConfig> Resolve(string mode, string repoRoot) => mode switch
    {
        "lucide" => new[] { Lucide(repoRoot) },
        "tabler" => new[] { TablerOutline(repoRoot), TablerFilled(repoRoot) },
        "phosphor" => new[] { Phosphor(repoRoot, "Phosphor", "Lumeo.Icons.Phosphor", "regular", suffix: null) },
        "phosphor-bold" => new[] { Phosphor(repoRoot, "PhosphorBold", "Lumeo.Icons.Phosphor.Bold", "bold", "bold") },
        "phosphor-fill" => new[] { Phosphor(repoRoot, "PhosphorFill", "Lumeo.Icons.Phosphor.Fill", "fill", "fill") },
        "phosphor-duotone" => new[] { Phosphor(repoRoot, "PhosphorDuotone", "Lumeo.Icons.Phosphor.Duotone", "duotone", "duotone") },
        "phosphor-light" => new[] { Phosphor(repoRoot, "PhosphorLight", "Lumeo.Icons.Phosphor.Light", "light", "light") },
        "phosphor-thin" => new[] { Phosphor(repoRoot, "PhosphorThin", "Lumeo.Icons.Phosphor.Thin", "thin", "thin") },
        "all" => AllModes.SelectMany(m => Resolve(m, repoRoot)).ToArray(),
        _ => throw new ArgumentException($"Unknown pack mode '{mode}'. Known: {string.Join(", ", AllModes)}, all."),
    };

    private static string IconsDir(string repoRoot, string project) =>
        Path.Combine(repoRoot, "src", project, "Icons");

    // Selects .svg files that sit DIRECTLY inside a directory whose path ends with <paramref name="dirSuffix"/>
    // (e.g. "icons/outline" or "assets/regular"), version-prefixed repo root and all. No recursion into
    // sub-folders (there are none, but this keeps the filter exact).
    private static Func<string, bool> FlatDir(string dirSuffix) => entry =>
    {
        var n = entry.Replace('\\', '/');
        if (!n.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) return false;
        var slash = n.LastIndexOf('/');
        if (slash < 0) return false;
        return n.AsSpan(0, slash).EndsWith(dirSuffix, StringComparison.OrdinalIgnoreCase);
    };

    private static PackConfig Lucide(string repoRoot) => new()
    {
        PackName = "Lucide",
        Version = LucideVersion,
        ZipUrl = LucideZipUrl,
        ZipCacheName = LucideZipName,
        // Lucide's release zip lays SVGs out flat under "icons/".
        EntryFilter = e => e.Replace('\\', '/').StartsWith("icons/", StringComparison.OrdinalIgnoreCase)
                           && e.EndsWith(".svg", StringComparison.OrdinalIgnoreCase),
        Style = GenRenderStyle.Stroke,
        StrokeWidth = 2,
        Namespace = "Lumeo.Icons",
        ClassName = "Lucide",
        OutputDir = IconsDir(repoRoot, "Lumeo.Icons.Lucide"),
        OutputBaseName = "Lucide",
        LicenseHeader = Licenses.LucideIsc,
        ChunkSize = 500,
        // Vendor the brand marks Lucide dropped from core (github, …) so the standalone pack is a
        // superset of what Blazicons.Lucide consumers relied on.
        Overrides = LucideOverrides.Map,
    };

    private static PackConfig TablerOutline(string repoRoot) => new()
    {
        PackName = "Tabler",
        Version = TablerVersion,
        ZipUrl = TablerZipUrl,
        ZipCacheName = TablerZipName,
        EntryFilter = FlatDir("icons/outline"),
        Style = GenRenderStyle.Stroke,
        StrokeWidth = 2,
        Namespace = "Lumeo.Icons",
        ClassName = "Tabler",
        OutputDir = IconsDir(repoRoot, "Lumeo.Icons.Tabler"),
        OutputBaseName = "Tabler",
        LicenseHeader = Licenses.TablerMit,
        ChunkSize = 500,
    };

    private static PackConfig TablerFilled(string repoRoot) => new()
    {
        PackName = "Tabler",
        Version = TablerVersion,
        ZipUrl = TablerZipUrl,
        ZipCacheName = TablerZipName,
        EntryFilter = FlatDir("icons/filled"),
        Style = GenRenderStyle.Fill,
        Namespace = "Lumeo.Icons",
        ClassName = "TablerFilled",
        OutputDir = IconsDir(repoRoot, "Lumeo.Icons.Tabler"),
        OutputBaseName = "TablerFilled",
        LicenseHeader = Licenses.TablerMit,
        ChunkSize = 500,
    };

    private static PackConfig Phosphor(string repoRoot, string className, string project, string weightDir, string? suffix) => new()
    {
        PackName = "Phosphor",
        Version = PhosphorVersion,
        ZipUrl = PhosphorZipUrl,
        ZipCacheName = PhosphorZipName,
        EntryFilter = FlatDir("assets/" + weightDir),
        Style = GenRenderStyle.Fill,
        Namespace = "Lumeo.Icons",
        ClassName = className,
        OutputDir = IconsDir(repoRoot, project),
        OutputBaseName = className,
        LicenseHeader = Licenses.PhosphorMit,
        ChunkSize = 500,
        // Strip the weight suffix (e.g. "-duotone") so the member is the plain base name; the weight
        // lives in the class. Regular has no suffix (identity).
        UpstreamNameTransform = suffix is null ? null : name => NameTransform.StripSuffix(name, suffix),
    };
}
