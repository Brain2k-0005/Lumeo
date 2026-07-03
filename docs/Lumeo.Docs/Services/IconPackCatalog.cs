namespace Lumeo.Docs.Services;

/// <summary>
/// One selectable first-party <c>Lumeo.Icons.*</c> pack class.
/// </summary>
/// <param name="Key">Stable id used by <see cref="IconService.ActiveLibrary"/> and the picker.</param>
/// <param name="Display">Human label shown in the customizer row.</param>
/// <param name="Family">Grouping header (all variants of a pack share one family).</param>
/// <param name="TypeName">Fully-qualified pack class (namespace <c>Lumeo.Icons</c>).</param>
/// <param name="AssemblyName">The assembly the class lives in — some packs ship several classes in one assembly.</param>
/// <param name="LazyDll">The <c>BlazorWebAssemblyLazyLoad</c> dll to pull before reflecting, or null when eager (Lucide).</param>
/// <param name="ManifestFile">The wwwroot/icons/&lt;file&gt; name list emitted by tools/Lumeo.IconGen.</param>
public sealed record IconPack(
    string Key,
    string Display,
    string Family,
    string TypeName,
    string AssemblyName,
    string? LazyDll,
    string ManifestFile);

/// <summary>
/// The authoritative list of first-party icon packs the docs can live-switch between.
/// Ordered by family so the customizer can render family headers. Mirrors the pack metadata in
/// <c>IconsGallery.razor</c> and the manifest copy target in <c>Lumeo.Docs.csproj</c> — keep the
/// three in sync when a pack is added.
/// </summary>
public static class IconPackCatalog
{
    public static readonly IReadOnlyList<IconPack> FirstParty = new[]
    {
        new IconPack("lucide", "Lucide", "Lucide", "Lumeo.Icons.Lucide", "Lumeo.Icons.Lucide", null, "lucide.json"),

        new IconPack("tabler", "Tabler", "Tabler", "Lumeo.Icons.Tabler", "Lumeo.Icons.Tabler", "Lumeo.Icons.Tabler.dll", "tabler.json"),
        new IconPack("tabler-filled", "Tabler Filled", "Tabler", "Lumeo.Icons.TablerFilled", "Lumeo.Icons.Tabler", "Lumeo.Icons.Tabler.dll", "tabler-filled.json"),

        new IconPack("phosphor", "Phosphor", "Phosphor", "Lumeo.Icons.Phosphor", "Lumeo.Icons.Phosphor", "Lumeo.Icons.Phosphor.dll", "phosphor.json"),
        new IconPack("phosphor-bold", "Phosphor Bold", "Phosphor", "Lumeo.Icons.PhosphorBold", "Lumeo.Icons.Phosphor.Bold", "Lumeo.Icons.Phosphor.Bold.dll", "phosphor-bold.json"),
        new IconPack("phosphor-fill", "Phosphor Fill", "Phosphor", "Lumeo.Icons.PhosphorFill", "Lumeo.Icons.Phosphor.Fill", "Lumeo.Icons.Phosphor.Fill.dll", "phosphor-fill.json"),
        new IconPack("phosphor-duotone", "Phosphor Duotone", "Phosphor", "Lumeo.Icons.PhosphorDuotone", "Lumeo.Icons.Phosphor.Duotone", "Lumeo.Icons.Phosphor.Duotone.dll", "phosphor-duotone.json"),
        new IconPack("phosphor-light", "Phosphor Light", "Phosphor", "Lumeo.Icons.PhosphorLight", "Lumeo.Icons.Phosphor.Light", "Lumeo.Icons.Phosphor.Light.dll", "phosphor-light.json"),
        new IconPack("phosphor-thin", "Phosphor Thin", "Phosphor", "Lumeo.Icons.PhosphorThin", "Lumeo.Icons.Phosphor.Thin", "Lumeo.Icons.Phosphor.Thin.dll", "phosphor-thin.json"),

        new IconPack("heroicons", "Heroicons", "Heroicons", "Lumeo.Icons.Heroicons", "Lumeo.Icons.Heroicons", "Lumeo.Icons.Heroicons.dll", "heroicons.json"),
        new IconPack("heroicons-solid", "Heroicons Solid", "Heroicons", "Lumeo.Icons.HeroiconsSolid", "Lumeo.Icons.Heroicons", "Lumeo.Icons.Heroicons.dll", "heroicons-solid.json"),
        new IconPack("heroicons-mini", "Heroicons Mini", "Heroicons", "Lumeo.Icons.HeroiconsMini", "Lumeo.Icons.Heroicons", "Lumeo.Icons.Heroicons.dll", "heroicons-mini.json"),
        new IconPack("heroicons-micro", "Heroicons Micro", "Heroicons", "Lumeo.Icons.HeroiconsMicro", "Lumeo.Icons.Heroicons", "Lumeo.Icons.Heroicons.dll", "heroicons-micro.json"),

        new IconPack("remix", "Remix", "Remix", "Lumeo.Icons.Remix", "Lumeo.Icons.Remix", "Lumeo.Icons.Remix.dll", "remix.json"),
        new IconPack("remix-filled", "Remix Filled", "Remix", "Lumeo.Icons.RemixFilled", "Lumeo.Icons.Remix", "Lumeo.Icons.Remix.dll", "remix-filled.json"),

        new IconPack("bootstrap", "Bootstrap", "Bootstrap", "Lumeo.Icons.Bootstrap", "Lumeo.Icons.Bootstrap", "Lumeo.Icons.Bootstrap.dll", "bootstrap.json"),

        new IconPack("iconoir", "Iconoir", "Iconoir", "Lumeo.Icons.Iconoir", "Lumeo.Icons.Iconoir", "Lumeo.Icons.Iconoir.dll", "iconoir.json"),

        new IconPack("material-symbols", "Material Symbols", "Material Symbols", "Lumeo.Icons.MaterialSymbols", "Lumeo.Icons.MaterialSymbols", "Lumeo.Icons.MaterialSymbols.dll", "material-symbols.json"),
        new IconPack("material-symbols-filled", "Material Symbols Filled", "Material Symbols", "Lumeo.Icons.MaterialSymbolsFilled", "Lumeo.Icons.MaterialSymbols", "Lumeo.Icons.MaterialSymbols.dll", "material-symbols-filled.json"),
        new IconPack("material-symbols-rounded", "Material Symbols Rounded", "Material Symbols", "Lumeo.Icons.MaterialSymbolsRounded", "Lumeo.Icons.MaterialSymbols.Rounded", "Lumeo.Icons.MaterialSymbols.Rounded.dll", "material-symbols-rounded.json"),
        new IconPack("material-symbols-rounded-filled", "Material Symbols Rounded Filled", "Material Symbols", "Lumeo.Icons.MaterialSymbolsRoundedFilled", "Lumeo.Icons.MaterialSymbols.Rounded", "Lumeo.Icons.MaterialSymbols.Rounded.dll", "material-symbols-rounded-filled.json"),
        new IconPack("material-symbols-sharp", "Material Symbols Sharp", "Material Symbols", "Lumeo.Icons.MaterialSymbolsSharp", "Lumeo.Icons.MaterialSymbols.Sharp", "Lumeo.Icons.MaterialSymbols.Sharp.dll", "material-symbols-sharp.json"),
        new IconPack("material-symbols-sharp-filled", "Material Symbols Sharp Filled", "Material Symbols", "Lumeo.Icons.MaterialSymbolsSharpFilled", "Lumeo.Icons.MaterialSymbols.Sharp", "Lumeo.Icons.MaterialSymbols.Sharp.dll", "material-symbols-sharp-filled.json"),

        new IconPack("fluent", "Fluent", "Fluent", "Lumeo.Icons.Fluent", "Lumeo.Icons.Fluent", "Lumeo.Icons.Fluent.dll", "fluent.json"),
        new IconPack("fluent-filled", "Fluent Filled", "Fluent", "Lumeo.Icons.FluentFilled", "Lumeo.Icons.Fluent", "Lumeo.Icons.Fluent.dll", "fluent-filled.json"),
    };

    private static readonly Dictionary<string, IconPack> ByKey =
        FirstParty.ToDictionary(p => p.Key, StringComparer.Ordinal);

    public static IconPack? Find(string key) =>
        ByKey.TryGetValue(key, out var p) ? p : null;

    public static bool IsFirstParty(string key) => ByKey.ContainsKey(key);

    /// <summary>Distinct lazy dlls behind the first-party packs (Lucide is eager, so excluded).</summary>
    public static readonly string[] LazyDlls =
        FirstParty.Where(p => p.LazyDll is not null)
                  .Select(p => p.LazyDll!)
                  .Distinct(StringComparer.OrdinalIgnoreCase)
                  .ToArray();

    /// <summary>Packs grouped by family, preserving declaration order — drives the picker layout.</summary>
    public static IEnumerable<IGrouping<string, IconPack>> ByFamily() =>
        FirstParty.GroupBy(p => p.Family);
}
