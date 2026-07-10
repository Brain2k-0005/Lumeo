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
    private const string HeroiconsVersion = "2.2.0";
    private const string RemixVersion = "4.9.1";
    private const string BootstrapVersion = "1.13.1";
    private const string IconoirVersion = "7.11.1";
    // Material Symbols / Fluent ship no GitHub release zip we want to clone (the google/microsoft
    // repos are multi-GB); the version is the npm mirror package version, whose tarball we vendor.
    private const string MaterialSymbolsVersion = "0.45.5"; // npm @material-symbols/svg-400 dist-tags.latest
    private const string FluentVersion = "1.1.331";         // npm @fluentui/svg-icons dist-tags.latest

    private const string LucideZipUrl =
        "https://github.com/lucide-icons/lucide/releases/download/1.23.0/lucide-icons-1.23.0.zip";
    private const string LucideZipName = "lucide-icons-1.23.0.zip";

    private const string TablerZipUrl =
        "https://github.com/tabler/tabler-icons/archive/refs/tags/v3.44.0.zip";
    private const string TablerZipName = "tabler-icons-3.44.0.zip";

    private const string PhosphorZipUrl =
        "https://github.com/phosphor-icons/core/archive/refs/tags/v2.0.8.zip";
    private const string PhosphorZipName = "phosphor-core-2.0.8.zip";

    private const string HeroiconsZipUrl =
        "https://github.com/tailwindlabs/heroicons/archive/refs/tags/v2.2.0.zip";
    private const string HeroiconsZipName = "heroicons-2.2.0.zip";

    private const string RemixZipUrl =
        "https://github.com/Remix-Design/RemixIcon/archive/refs/tags/v4.9.1.zip";
    private const string RemixZipName = "remixicon-4.9.1.zip";

    private const string BootstrapZipUrl =
        "https://github.com/twbs/icons/archive/refs/tags/v1.13.1.zip";
    private const string BootstrapZipName = "bootstrap-icons-1.13.1.zip";

    private const string IconoirZipUrl =
        "https://github.com/iconoir-icons/iconoir/archive/refs/tags/v7.11.1.zip";
    private const string IconoirZipName = "iconoir-7.11.1.zip";

    // npm mirror tarballs (.tgz = gzip'd tar; ForEachSvg handles them like the GitHub .zips). Material
    // Symbols: @material-symbols/svg-400 lays SVGs flat under outlined/ rounded/ sharp/ with
    // "<name>.svg" (fill=0) + "<name>-fill.svg" (fill=1). Fluent: @fluentui/svg-icons ships all sizes
    // under icons/ as "<name>_<size>_<regular|filled>.svg"; the 24px cut is selected below.
    private const string MaterialSymbolsZipUrl =
        "https://registry.npmjs.org/@material-symbols/svg-400/-/svg-400-0.45.5.tgz";
    private const string MaterialSymbolsZipName = "material-symbols-svg-400-0.45.5.tgz";

    private const string FluentZipUrl =
        "https://registry.npmjs.org/@fluentui/svg-icons/-/svg-icons-1.1.331.tgz";
    private const string FluentZipName = "fluentui-svg-icons-1.1.331.tgz";

    /// <summary>All pack mode names (excludes the Phase 0 <c>lumeo-icons</c> mode and <c>all</c>).</summary>
    public static readonly IReadOnlyList<string> AllModes = new[]
    {
        "lucide", "tabler",
        "phosphor", "phosphor-bold", "phosphor-fill",
        "phosphor-duotone", "phosphor-light", "phosphor-thin",
        "heroicons", "remix", "bootstrap", "iconoir",
        "material-symbols", "material-symbols-rounded", "material-symbols-sharp",
        "fluent",
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
        "heroicons" => Heroicons(repoRoot),
        "remix" => new[] { RemixVariant2(repoRoot, "Remix", "line"), RemixVariant2(repoRoot, "RemixFilled", "fill") },
        "bootstrap" => new[] { Bootstrap(repoRoot) },
        "iconoir" => new[] { Iconoir(repoRoot) },
        "material-symbols" => MaterialStyle(repoRoot, "outlined", "MaterialSymbols", "Lumeo.Icons.MaterialSymbols"),
        "material-symbols-rounded" => MaterialStyle(repoRoot, "rounded", "MaterialSymbolsRounded", "Lumeo.Icons.MaterialSymbols.Rounded"),
        "material-symbols-sharp" => MaterialStyle(repoRoot, "sharp", "MaterialSymbolsSharp", "Lumeo.Icons.MaterialSymbols.Sharp"),
        "fluent" => new[] { FluentVariant(repoRoot, "Fluent", "regular"), FluentVariant(repoRoot, "FluentFilled", "filled") },
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

    // Selects .svg files sitting DIRECTLY inside a single top-level directory named exactly
    // <paramref name="dirName"/> under the zip's version-prefixed root — i.e. the path is
    // "<root>/<dirName>/<file>.svg" (exactly three segments). Unlike FlatDir's suffix match this
    // rejects sibling folders whose name merely ENDS with the same text (Bootstrap ships loose SVGs
    // under docs/.../favicons/ and docs/.../icons/, both of which a "icons" suffix would wrongly grab).
    private static Func<string, bool> TopLevelDir(string dirName) => entry =>
    {
        var n = entry.Replace('\\', '/');
        if (!n.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) return false;
        var parts = n.Split('/');
        return parts.Length == 3 && parts[1].Equals(dirName, StringComparison.OrdinalIgnoreCase);
    };

    // Selects one RemixIcon style: files "<root>/icons/<Category>/<name>-<variant>.svg" (four
    // segments; category folders live one level under icons/). <paramref name="variant"/> is
    // "line" or "fill" — both are fill-rendered path sets; the variant marker is stripped from the
    // member name (below) so the same base name lives on the Remix / RemixFilled classes.
    private static Func<string, bool> RemixVariantFilter(string variant) => entry =>
    {
        var n = entry.Replace('\\', '/');
        if (!n.EndsWith("-" + variant + ".svg", StringComparison.OrdinalIgnoreCase)) return false;
        var parts = n.Split('/');
        return parts.Length == 4 && parts[1].Equals("icons", StringComparison.OrdinalIgnoreCase);
    };

    // Selects one Material Symbols style + fill from an npm tarball: files
    // "package/<styleDir>/<name>.svg" (fill=0) or "package/<styleDir>/<name>-fill.svg" (fill=1),
    // exactly three path segments (rejects any nested sibling). <paramref name="filled"/> picks the
    // "-fill.svg" set; false picks the plain (non-fill) set — the two share base names across the
    // MaterialSymbols* / MaterialSymbols*Filled classes.
    private static Func<string, bool> MaterialFilter(string styleDir, bool filled) => entry =>
    {
        var n = entry.Replace('\\', '/');
        if (!n.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) return false;
        var parts = n.Split('/');
        if (parts.Length != 3) return false;
        if (!parts[0].Equals("package", StringComparison.OrdinalIgnoreCase)) return false;
        if (!parts[1].Equals(styleDir, StringComparison.OrdinalIgnoreCase)) return false;
        var isFill = n.EndsWith("-fill.svg", StringComparison.OrdinalIgnoreCase);
        return filled ? isFill : !isFill;
    };

    // Selects one Fluent size+style from an npm tarball: top-level files
    // "package/icons/<name>_24_<regular|filled>.svg" (exactly three segments — the locale subfolders
    // package/icons/<locale>/… are four segments and thus rejected). Only the 24px cut is vendored.
    private static Func<string, bool> FluentFilter(string variant) => entry =>
    {
        var n = entry.Replace('\\', '/');
        var parts = n.Split('/');
        if (parts.Length != 3) return false;
        if (!parts[0].Equals("package", StringComparison.OrdinalIgnoreCase)) return false;
        if (!parts[1].Equals("icons", StringComparison.OrdinalIgnoreCase)) return false;
        return parts[2].EndsWith("_24_" + variant + ".svg", StringComparison.OrdinalIgnoreCase);
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
        // superset of the icons Lumeo's own source references.
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

    // Heroicons ships four synchronized variants in ONE package (project Lumeo.Icons.Heroicons):
    // 24/outline is stroke (heroicons outline strokes at 1.5, NOT 2), while 24/solid, 20/solid and
    // 16/solid are fill sets whose native viewBoxes (24/20/16) are read per-file by the parser. The
    // Mini (20) and Micro (16) solid sets carry fill-rule/clip-rule="evenodd" — preserved verbatim.
    private static IReadOnlyList<PackConfig> Heroicons(string repoRoot)
    {
        const string project = "Lumeo.Icons.Heroicons";
        PackConfig Variant(string className, string subDir, GenRenderStyle style, double strokeWidth) => new()
        {
            PackName = "Heroicons",
            Version = HeroiconsVersion,
            ZipUrl = HeroiconsZipUrl,
            ZipCacheName = HeroiconsZipName,
            EntryFilter = FlatDir(subDir),
            Style = style,
            StrokeWidth = strokeWidth,
            Namespace = "Lumeo.Icons",
            ClassName = className,
            OutputDir = IconsDir(repoRoot, project),
            OutputBaseName = className,
            LicenseHeader = Licenses.HeroiconsMit,
            ChunkSize = 500,
        };

        return new[]
        {
            Variant("Heroicons", "optimized/24/outline", GenRenderStyle.Stroke, 1.5),
            Variant("HeroiconsSolid", "optimized/24/solid", GenRenderStyle.Fill, 2),
            Variant("HeroiconsMini", "optimized/20/solid", GenRenderStyle.Fill, 2),
            Variant("HeroiconsMicro", "optimized/16/solid", GenRenderStyle.Fill, 2),
        };
    }

    // RemixIcon: both the "-line" and "-fill" variants are fill-rendered 24px path sets living under
    // category sub-folders (icons/<Category>/<name>-line.svg). The variant marker is stripped so
    // `home-2-line` and `home-2-fill` both yield member `Home2` — on the Remix / RemixFilled classes
    // respectively — both in ONE package (project Lumeo.Icons.Remix). Apache-2.0 upstream.
    private static PackConfig RemixVariant2(string repoRoot, string className, string variant) => new()
    {
        PackName = "RemixIcon",
        Version = RemixVersion,
        ZipUrl = RemixZipUrl,
        ZipCacheName = RemixZipName,
        EntryFilter = RemixVariantFilter(variant),
        Style = GenRenderStyle.Fill,
        Namespace = "Lumeo.Icons",
        ClassName = className,
        OutputDir = IconsDir(repoRoot, "Lumeo.Icons.Remix"),
        OutputBaseName = className,
        LicenseHeader = Licenses.RemixApache2,
        ChunkSize = 500,
        UpstreamNameTransform = name => NameTransform.StripSuffix(name, variant),
    };

    // Bootstrap Icons: ~2,000 fill-based 16px icons, flat under the top-level icons/ folder. The
    // "-fill" naming convention is Bootstrap's own (bell vs bell-fill are distinct icons), so the
    // suffix is KEPT as part of the member name (BellFill), unlike Phosphor/Remix.
    private static PackConfig Bootstrap(string repoRoot) => new()
    {
        PackName = "Bootstrap Icons",
        Version = BootstrapVersion,
        ZipUrl = BootstrapZipUrl,
        ZipCacheName = BootstrapZipName,
        EntryFilter = TopLevelDir("icons"),
        Style = GenRenderStyle.Fill,
        Namespace = "Lumeo.Icons",
        ClassName = "Bootstrap",
        OutputDir = IconsDir(repoRoot, "Lumeo.Icons.Bootstrap"),
        OutputBaseName = "Bootstrap",
        LicenseHeader = Licenses.BootstrapMit,
        ChunkSize = 500,
    };

    // Iconoir: stroke-based 24px icons under icons/regular/ (icons/solid/ is a separate, smaller set
    // we don't vendor). Iconoir strokes at 1.5, not 2.
    private static PackConfig Iconoir(string repoRoot) => new()
    {
        PackName = "Iconoir",
        Version = IconoirVersion,
        ZipUrl = IconoirZipUrl,
        ZipCacheName = IconoirZipName,
        EntryFilter = FlatDir("icons/regular"),
        Style = GenRenderStyle.Stroke,
        StrokeWidth = 1.5,
        Namespace = "Lumeo.Icons",
        ClassName = "Iconoir",
        OutputDir = IconsDir(repoRoot, "Lumeo.Icons.Iconoir"),
        OutputBaseName = "Iconoir",
        LicenseHeader = Licenses.IconoirMit,
        ChunkSize = 500,
    };

    // Material Symbols STANDARD CUT — weight 400 only. Each style (outlined/rounded/sharp) is its own
    // package holding two fill-rendered classes: the base "<name>.svg" set (fill=0) and the
    // "<name>-fill.svg" set (fill=1, "-fill" stripped so both share the base member name). The native
    // viewBox (0 -960 960 960) is preserved per-file by the parser; SvgGlyph scales via viewBox. Names
    // are snake_case (account_circle) — ToPascal splits on '_'. Apache-2.0 upstream.
    private static IReadOnlyList<PackConfig> MaterialStyle(string repoRoot, string styleDir, string className, string project)
    {
        PackConfig Variant(string cls, bool filled) => new()
        {
            PackName = "Material Symbols",
            Version = MaterialSymbolsVersion,
            ZipUrl = MaterialSymbolsZipUrl,
            ZipCacheName = MaterialSymbolsZipName,
            EntryFilter = MaterialFilter(styleDir, filled),
            Style = GenRenderStyle.Fill,
            Namespace = "Lumeo.Icons",
            ClassName = cls,
            OutputDir = IconsDir(repoRoot, project),
            OutputBaseName = cls,
            LicenseHeader = Licenses.MaterialSymbolsApache2,
            ChunkSize = 500,
            // Filled files carry a "-fill" suffix; strip it so House ↔ HouseFilled share the base name.
            UpstreamNameTransform = filled ? name => NameTransform.StripSuffix(name, "fill") : null,
        };

        return new[] { Variant(className, false), Variant(className + "Filled", true) };
    }

    // Fluent (Fluent UI System Icons) STANDARD CUT — 24px only. One package, two fill-rendered classes:
    // Fluent (regular) + FluentFilled (filled). The "_24_regular" / "_24_filled" size+style marker is
    // stripped so access_time_24_regular and access_time_24_filled both yield member AccessTime on the
    // respective class. Native 24x24 viewBox. MIT upstream.
    private static PackConfig FluentVariant(string repoRoot, string className, string variant) => new()
    {
        PackName = "Fluent",
        Version = FluentVersion,
        ZipUrl = FluentZipUrl,
        ZipCacheName = FluentZipName,
        EntryFilter = FluentFilter(variant),
        Style = GenRenderStyle.Fill,
        Namespace = "Lumeo.Icons",
        ClassName = className,
        OutputDir = IconsDir(repoRoot, "Lumeo.Icons.Fluent"),
        OutputBaseName = className,
        LicenseHeader = Licenses.FluentMit,
        ChunkSize = 500,
        UpstreamNameTransform = name => NameTransform.StripExact(name, "_24_" + variant),
    };
}
