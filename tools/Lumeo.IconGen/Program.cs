using System.Formats.Tar;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Lumeo.IconGen;

// Lumeo.IconGen — generic, config-driven icon-pack generator.
//
// Phase 0 mode ("lumeo-icons", the default): vendor the exact set of Lucide icons that Lumeo's own
// source references into src/Lumeo/Icons/LumeoIcons.g.cs, so the core owns its icons with no
// third-party icon dependency.
// The union is derived from truth (a regex scan of src/**/*.{cs,razor}), so it never drifts.
//
// Phase 1 pack modes: emit the FULL upstream set of a pack into a dedicated src/Lumeo.Icons.*
// project (Lucide, Tabler[+TablerFilled], Phosphor + 5 weights). "all" generates every pack.
//
//   lumeo-icon-gen [lumeo-icons | lucide | tabler | phosphor[-bold|-fill|-duotone|-light|-thin] | all]
//                  [--repo <root>] [--zip <cachedZip>]

var mode = args.FirstOrDefault(a => !a.StartsWith('-')) ?? "lumeo-icons";
var repoRoot = OptionValue("--repo") ?? FindRepoRoot();
var zipOverride = OptionValue("--zip");

var cacheDir = Path.Combine(repoRoot, "tools", "Lumeo.IconGen", ".cache");
Directory.CreateDirectory(cacheDir);

// Phase 1: pack modes emit a full upstream set per pack project.
if (mode != "lumeo-icons")
{
    IReadOnlyList<PackConfig> configs;
    try { configs = PackCatalog.Resolve(mode, repoRoot); }
    catch (ArgumentException ex) { Console.Error.WriteLine(ex.Message); return 2; }

    foreach (var cfg in configs)
    {
        var zip = zipOverride ?? Path.Combine(cacheDir, cfg.ZipCacheName);
        if (!File.Exists(zip))
        {
            Console.WriteLine($"[fetch] downloading {cfg.ZipUrl}");
            await Download(cfg.ZipUrl, zip);
        }

        var loaded = LoadPack(zip, cfg);
        var icons = loaded
            .Select(kv => new EmitIcon(kv.Key, kv.Value.Upstream, kv.Value.Icon))
            .OrderBy(i => i.Name, StringComparer.Ordinal)
            .ToList();

        var emitted = IconEmitter.Emit(cfg, icons);
        WriteNotices(cfg);

        var csFiles = emitted.Where(f => f.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)).ToList();
        var csSize = csFiles.Sum(f => new FileInfo(f).Length);
        Console.WriteLine(
            $"[done]  {cfg.ClassName}: {icons.Count} icons, {cfg.PackName} v{cfg.Version}, " +
            $"{csFiles.Count} .g.cs file(s), {csSize / 1024.0:F1} KiB.");
    }
    return 0;
}

var config = new PackConfig
{
    PackName = "Lucide",
    Version = "1.23.0",
    ZipUrl = "https://github.com/lucide-icons/lucide/releases/download/1.23.0/lucide-icons-1.23.0.zip",
    ZipCacheName = "lucide-icons-1.23.0.zip",
    EntryFilter = e => e.StartsWith("icons/", StringComparison.OrdinalIgnoreCase)
                       && e.EndsWith(".svg", StringComparison.OrdinalIgnoreCase),
    Style = GenRenderStyle.Stroke,
    StrokeWidth = 2,
    Namespace = "Lumeo",
    ClassName = "LumeoIcons",
    OutputDir = Path.Combine(repoRoot, "src", "Lumeo", "Icons"),
    OutputBaseName = "LumeoIcons",
    LicenseHeader = Licenses.LucideIsc,
    ChunkSize = 500,
    Overrides = LucideOverrides.Map,
    // Restricted below to the union scanned from src.
};

// 1. Derive the required union from truth: every Lucide.X / LumeoIcons.X referenced under src/.
var union = ScanUnion(Path.Combine(repoRoot, "src"));
Console.WriteLine($"[union] {union.Count} distinct icons referenced under src/.");

config = config with { NameFilter = union.Contains };

// 2. Load the whole upstream pack (PascalCase name -> parsed icon), then merge overrides.
var zipPath = zipOverride ?? Path.Combine(cacheDir, config.ZipCacheName);
if (!File.Exists(zipPath))
{
    Console.WriteLine($"[fetch] downloading {config.ZipUrl}");
    await Download(config.ZipUrl, zipPath);
}

var pack = LoadPack(zipPath, config);
Console.WriteLine($"[pack]  {pack.Count} icons parsed from {Path.GetFileName(zipPath)}.");

// 3. Filter to the union and verify coverage — print & fail on any miss.
var selected = pack
    .Where(kv => config.NameFilter!(kv.Key))
    .Select(kv => new EmitIcon(kv.Key, kv.Value.Upstream, kv.Value.Icon))
    .OrderBy(i => i.Name, StringComparer.Ordinal)
    .ToList();

var misses = union.Where(n => !pack.ContainsKey(n)).OrderBy(n => n, StringComparer.Ordinal).ToList();
if (misses.Count > 0)
{
    Console.Error.WriteLine($"[MISS] {misses.Count} referenced icon(s) not covered:");
    foreach (var m in misses) Console.Error.WriteLine($"       - {m}");
    return 1;
}
Console.WriteLine($"[cover] all {union.Count} referenced icons covered ({selected.Count} emitted).");

// 4. Emit.
var written = IconEmitter.Emit(config, selected);
foreach (var f in written) Console.WriteLine($"[write] {Path.GetRelativePath(repoRoot, f)}");
Console.WriteLine($"[done]  {config.ClassName}: {selected.Count} icons, {config.PackName} v{config.Version}.");
return 0;


// ---- helpers ----

string? OptionValue(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "src", "Lumeo", "Lumeo.csproj")))
            return dir.FullName;
        dir = dir.Parent;
    }
    // Fallback: walk up from cwd.
    dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "src", "Lumeo", "Lumeo.csproj")))
            return dir.FullName;
        dir = dir.Parent;
    }
    throw new InvalidOperationException("Could not locate repo root (src/Lumeo/Lumeo.csproj).");
}

// Scans src for `Lucide.X` and `LumeoIcons.X` references (so it stays correct after the core is
// migrated off Lucide onto LumeoIcons). Excludes bin/obj and generated *.g.cs to avoid self-inflation.
static HashSet<string> ScanUnion(string srcRoot)
{
    var rx = new Regex(@"\b(?:Lucide|LumeoIcons)\.([A-Za-z][A-Za-z0-9]*)", RegexOptions.Compiled);
    var names = new HashSet<string>(StringComparer.Ordinal);

    foreach (var file in Directory.EnumerateFiles(srcRoot, "*.*", SearchOption.AllDirectories))
    {
        if (!(file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
              || file.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))) continue;
        if (file.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)) continue;

        var norm = file.Replace('\\', '/');
        if (norm.Contains("/bin/") || norm.Contains("/obj/")) continue;

        foreach (Match m in rx.Matches(File.ReadAllText(file)))
            names.Add(m.Groups[1].Value);
    }
    return names;
}

// PascalName -> (upstream kebab name, parsed icon). Upstream SVGs first, then overrides win.
static Dictionary<string, (string Upstream, ParsedIcon Icon)> LoadPack(string archivePath, PackConfig config)
{
    var result = new Dictionary<string, (string, ParsedIcon)>(StringComparer.Ordinal);

    ForEachSvg(archivePath, config.EntryFilter, (name, svgText) =>
    {
        var raw = Path.GetFileNameWithoutExtension(name);
        var upstream = config.UpstreamNameTransform?.Invoke(raw) ?? raw;
        var pascal = NameTransform.ToPascal(upstream);
        var parsed = SvgParser.Parse(svgText);

        if (!result.TryAdd(pascal, (upstream, parsed)))
            Console.Error.WriteLine($"[warn] name collision on '{pascal}' ({upstream}) — keeping first.");
    });

    if (config.Overrides is not null)
        foreach (var (pascal, svg) in config.Overrides)
            result[pascal] = (pascal.ToLowerInvariant(), SvgParser.Parse(svg));

    return result;
}

// Enumerates the SVG entries of an upstream archive, invoking <paramref name="onEntry"/> with each
// entry's forward-slash path and full text. GitHub release archives are .zip; the npm mirror
// tarballs used for Material Symbols / Fluent are gzip'd tar (.tgz) — both are handled transparently
// so a pack config need only point ZipUrl/ZipCacheName at whichever the upstream ships.
static void ForEachSvg(string archivePath, Func<string, bool> filter, Action<string, string> onEntry)
{
    var lower = archivePath.ToLowerInvariant();
    if (lower.EndsWith(".tgz") || lower.EndsWith(".tar.gz"))
    {
        using var fs = File.OpenRead(archivePath);
        using var gz = new GZipStream(fs, CompressionMode.Decompress);
        using var tar = new TarReader(gz);
        while (tar.GetNextEntry() is { } entry)
        {
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile)) continue;
            var name = entry.Name.Replace('\\', '/');
            if (!filter(name) || entry.DataStream is null) continue;
            using var reader = new StreamReader(entry.DataStream);
            onEntry(name, reader.ReadToEnd());
        }
        return;
    }

    using var zip = ZipFile.OpenRead(archivePath);
    foreach (var entry in zip.Entries)
    {
        var name = entry.FullName.Replace('\\', '/');
        if (!filter(name)) continue;
        using var reader = new StreamReader(entry.Open());
        onEntry(name, reader.ReadToEnd());
    }
}

// Writes the pack's upstream license text as THIRD-PARTY-NOTICES.txt at the project root (parent of
// the Icons/ output dir) so the csproj can pack it into the .nupkg alongside the compiled icons.
static void WriteNotices(PackConfig config)
{
    var projectDir = Path.GetDirectoryName(config.OutputDir);
    if (projectDir is null) return;
    var path = Path.Combine(projectDir, "THIRD-PARTY-NOTICES.txt");
    var text = config.LicenseHeader.Replace("\r\n", "\n").TrimEnd() + "\n";
    File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
}

static async Task Download(string url, string dest)
{
    using var http = new HttpClient();
    http.DefaultRequestHeaders.UserAgent.ParseAdd("Lumeo.IconGen/1.0");
    await using var stream = await http.GetStreamAsync(url);
    await using var file = File.Create(dest);
    await stream.CopyToAsync(file);
}
