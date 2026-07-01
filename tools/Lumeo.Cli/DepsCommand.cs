using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lumeo.Cli;

/// <summary>
/// Implements the <c>lumeo deps install</c> command.
/// Downloads all (or a filtered set of) CDN dependencies into the project's
/// wwwroot tree so the app can run without hitting public CDNs at runtime.
/// </summary>
internal static class DepsCommand
{
    // ──────────────────────────────────────────────────────────────────────────
    // DTOs (mirror cdn-deps.json schema)
    // ──────────────────────────────────────────────────────────────────────────

    internal sealed class CdnDepEntry
    {
        [JsonPropertyName("key")]     public string Key     { get; set; } = "";
        [JsonPropertyName("package")] public string Package { get; set; } = "";
        [JsonPropertyName("version")] public string Version { get; set; } = "";
        [JsonPropertyName("url")]     public string Url     { get; set; } = "";
        [JsonPropertyName("owner")]   public string Owner   { get; set; } = "";
    }

    internal sealed class CdnDepsManifest
    {
        [JsonPropertyName("version")]   public string         Version   { get; set; } = "";
        [JsonPropertyName("generated")] public string         Generated { get; set; } = "";
        [JsonPropertyName("deps")]      public CdnDepEntry[]  Deps      { get; set; } = [];
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Constants
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Remote fallback URL if no embedded or local manifest is found.</summary>
    private const string RemoteCdnDepsUrl = "https://lumeo.nativ.sh/registry/cdn-deps.json";

    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ──────────────────────────────────────────────────────────────────────────
    // Public entry point
    // ──────────────────────────────────────────────────────────────────────────

    public static async Task<int> InstallAsync(
        string? lib,
        string? target,
        bool writeBootstrap,
        bool force,
        bool dryRun)
    {
        // 1. Resolve target project directory.
        var targetDir = ResolveTargetDir(target);
        if (targetDir is null)
        {
            Console.Error.WriteLine(Ansi.Red("error: ") + "No *.csproj found in the current directory. " +
                                    "Run from your Blazor project root or pass --target <dir>.");
            Environment.ExitCode = 1;
            return 1;
        }

        // 2. Load cdn-deps manifest.
        CdnDepsManifest manifest;
        try
        {
            manifest = await LoadManifestAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(Ansi.Red("error: ") + $"Failed to load cdn-deps.json: {ex.Message}");
            Environment.ExitCode = 1;
            return 1;
        }

        // 3. Filter by --lib.
        var deps = FilterDeps(manifest.Deps, lib);
        if (deps.Length == 0)
        {
            Console.Error.WriteLine(Ansi.Yellow("warn: ") + $"No deps match --lib {lib ?? "(all)"}.");
            return 0;
        }

        // 4. Download each dep.
        Console.WriteLine(Ansi.Bold("Lumeo CDN vendor install"));
        Console.WriteLine(Ansi.Dim($"  manifest: {manifest.Version} ({manifest.Generated[..10]})"));
        Console.WriteLine(Ansi.Dim($"  target:   {targetDir}"));
        if (dryRun) Console.WriteLine(Ansi.Yellow("  [dry-run] no files will be written"));
        Console.WriteLine();

        var vendorRoot = Path.Combine(targetDir, "wwwroot", "lib", "lumeo-vendor");
        var results    = new List<(CdnDepEntry Dep, string LocalPath, string Status)>();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        foreach (var dep in deps)
        {
            // ESM-only deps (e.g. esm.sh base URL, no downloadable file path) are not
            // self-hostable via simple HTTP download.  Skip them with an informational note.
            if (IsEsmOnly(dep.Url))
            {
                results.Add((dep, "(esm-only — cannot self-host)", "esm-only"));
                continue;
            }

            var fileName  = Path.GetFileName(new Uri(dep.Url).LocalPath);
            // Sanitize package name for filesystem (strip @scope/ prefix, map / to -)
            var pkgFolder = dep.Package.TrimStart('@').Replace('/', '-');
            var localPath = Path.Combine(vendorRoot, pkgFolder, fileName);
            var relPath   = Path.GetRelativePath(targetDir, localPath);

            if (!force && File.Exists(localPath))
            {
                results.Add((dep, relPath, "skip"));
                continue;
            }

            if (dryRun)
            {
                results.Add((dep, relPath, "download"));
                continue;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                using var stream = await http.GetStreamAsync(dep.Url);
                using var fs     = new FileStream(localPath, FileMode.Create, FileAccess.Write);
                await stream.CopyToAsync(fs);
                results.Add((dep, relPath, "ok"));
            }
            catch (Exception ex)
            {
                results.Add((dep, relPath, $"FAIL: {ex.Message}"));
            }
        }

        // 5. Print summary table.
        PrintTable(results, dryRun);

        // 6. Generate bootstrap script.
        if (writeBootstrap)
        {
            var bootstrapPath = Path.Combine(targetDir, "wwwroot", "js", "lumeo-cdn-init.js");
            // Bootstrap ONLY the deps actually downloaded this run (the --lib-filtered set), not the whole
            // manifest. Rewriting an un-downloaded key to /lib/lumeo-vendor/… would 404 instead of letting
            // it fall back to its public CDN — the bug when `deps install --lib Lumeo.Charts` rewrote Map/
            // PdfViewer too.
            var script = BuildBootstrapScript(deps);

            if (!dryRun)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(bootstrapPath)!);
                await File.WriteAllTextAsync(bootstrapPath, script, new UTF8Encoding(false));
                Console.WriteLine();
                Console.WriteLine(Ansi.Green("OK ") + $"Wrote {Path.GetRelativePath(targetDir, bootstrapPath)}");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine(Ansi.Dim("[dry-run] Would write: " + Path.GetRelativePath(targetDir, bootstrapPath)));
            }

            Console.WriteLine();
            Console.WriteLine(Ansi.Bold("Next: add this line to your App.razor <head> (before Blazor's framework script):"));
            Console.WriteLine(Ansi.Cyan("""    <script src="js/lumeo-cdn-init.js"></script>"""));
        }

        var failCount = results.Count(r => r.Status.StartsWith("FAIL"));
        if (failCount > 0)
        {
            Environment.ExitCode = 1;
            return 1;
        }
        return 0;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true for ESM import specifier URLs that resolve server-side
    /// (e.g. esm.sh bare imports, URLs with no file extension in the path).
    /// These cannot be meaningfully downloaded as a static file.
    /// </summary>
    private static bool IsEsmOnly(string url)
    {
        // Pure base URL with no path beyond the host (e.g. "https://esm.sh")
        var uri = new Uri(url);
        var path = uri.LocalPath.TrimEnd('/');
        if (string.IsNullOrEmpty(path) || path == "/") return true;

        // Paths that end in a version specifier like "@6", "@5" — ESM module references.
        // e.g. https://esm.sh/@fullcalendar/core@6  → last segment is "core@6"
        var lastSegment = Path.GetFileName(path);
        if (System.Text.RegularExpressions.Regex.IsMatch(lastSegment, @"@\d"))
            return true;

        // Paths with no extension at all and not a known file-style path
        // e.g. https://esm.sh/algoliasearch/lite  → "lite" (no extension)
        if (!lastSegment.Contains('.'))
            return true;

        return false;
    }

    private static string? ResolveTargetDir(string? target)
    {
        if (target is not null)
            return Path.GetFullPath(target);

        // Default: walk up from cwd looking for a *.csproj.
        var dir = new DirectoryInfo(Environment.CurrentDirectory);
        for (int depth = 0; dir is not null && depth < 5; depth++, dir = dir.Parent)
        {
            if (Directory.EnumerateFiles(dir.FullName, "*.csproj", SearchOption.TopDirectoryOnly).Any())
                return dir.FullName;
        }
        return null;
    }

    /// <summary>
    /// Load cdn-deps.json in priority order:
    ///   1. Embedded resource in this assembly (bundled with the CLI tool)
    ///   2. Relative path ./registry/cdn-deps.json in the cwd (consuming project)
    ///   3. Remote CDN URL https://lumeo.nativ.sh/registry/cdn-deps.json
    /// </summary>
    private static async Task<CdnDepsManifest> LoadManifestAsync()
    {
        // 1. Embedded resource
        var asm = Assembly.GetExecutingAssembly();
        var embeddedName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("cdn-deps.json", StringComparison.OrdinalIgnoreCase));
        if (embeddedName is not null)
        {
            using var stream = asm.GetManifestResourceStream(embeddedName)!;
            var manifest = await JsonSerializer.DeserializeAsync<CdnDepsManifest>(stream, s_jsonOpts);
            if (manifest is not null)
            {
                Console.WriteLine(Ansi.Dim("  source: embedded in CLI assembly"));
                return manifest;
            }
        }

        // 2. Local file (consuming project or repo root)
        var localCandidates = new[]
        {
            Path.Combine(Environment.CurrentDirectory, "registry", "cdn-deps.json"),
            Path.Combine(Environment.CurrentDirectory, "src", "Lumeo", "registry", "cdn-deps.json"),
        };
        foreach (var candidate in localCandidates)
        {
            if (File.Exists(candidate))
            {
                var json = await File.ReadAllTextAsync(candidate);
                var manifest = JsonSerializer.Deserialize<CdnDepsManifest>(json, s_jsonOpts);
                if (manifest is not null)
                {
                    Console.WriteLine(Ansi.Dim($"  source: {Path.GetRelativePath(Environment.CurrentDirectory, candidate)}"));
                    return manifest;
                }
            }
        }

        // 3. Remote fallback
        Console.WriteLine(Ansi.Dim($"  source: {RemoteCdnDepsUrl}"));
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var remoteJson = await http.GetStringAsync(RemoteCdnDepsUrl);
        var remote = JsonSerializer.Deserialize<CdnDepsManifest>(remoteJson, s_jsonOpts)
                     ?? throw new InvalidOperationException("Failed to parse remote cdn-deps.json.");
        return remote;
    }

    private static CdnDepEntry[] FilterDeps(CdnDepEntry[] all, string? lib)
    {
        if (string.IsNullOrEmpty(lib) || string.Equals(lib, "all", StringComparison.OrdinalIgnoreCase))
            return all;

        return all.Where(d =>
            string.Equals(d.Owner, lib, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(d.Package, lib, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(d.Key, lib, StringComparison.OrdinalIgnoreCase)
        ).ToArray();
    }

    private static void PrintTable(List<(CdnDepEntry Dep, string LocalPath, string Status)> results, bool dryRun)
    {
        // Column widths
        int keyW  = Math.Max(6, results.Max(r => r.Dep.Key.Length));
        int pathW = Math.Max(10, results.Max(r => r.LocalPath.Length));

        // Header
        var sep = new string('-', keyW + pathW + 14);
        Console.WriteLine(sep);
        Console.WriteLine(
            PadRight("KEY",    keyW + 2) +
            PadRight("PATH",   pathW + 2) +
            "STATUS");
        Console.WriteLine(sep);

        foreach (var (dep, path, status) in results)
        {
            var statusStr = status switch
            {
                "ok"       => Ansi.Green("ok"),
                "skip"     => Ansi.Dim("skip (exists)"),
                "download" => Ansi.Cyan("would download"),
                "esm-only" => Ansi.Yellow("esm-only (skipped)"),
                _          => Ansi.Red(status),
            };
            Console.WriteLine(
                PadRight(dep.Key, keyW + 2) +
                PadRight(path, pathW + 2) +
                statusStr);
        }
        Console.WriteLine(sep);

        var ok   = results.Count(r => r.Status == "ok" || (dryRun && r.Status == "download"));
        var skip = results.Count(r => r.Status == "skip");
        var esm  = results.Count(r => r.Status == "esm-only");
        var fail = results.Count(r => r.Status.StartsWith("FAIL"));
        Console.Write($"  {Ansi.Green($"{ok} downloaded")}  {Ansi.Dim($"{skip} skipped")}");
        if (esm > 0)  Console.Write($"  {Ansi.Yellow($"{esm} esm-only")}");
        if (fail > 0) Console.Write($"  {Ansi.Red($"{fail} failed")}");
        Console.WriteLine();
    }

    private static string BuildBootstrapScript(CdnDepEntry[] deps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// Auto-generated by `lumeo deps install` — do not edit by hand.");
        sb.AppendLine("// Re-run the CLI to regenerate after a Lumeo version bump.");
        sb.AppendLine("window.lumeoCdn = {");

        foreach (var dep in deps)
        {
            if (string.IsNullOrEmpty(dep.Url)) continue;

            // ESM-only deps cannot be self-hosted via simple file download.
            if (IsEsmOnly(dep.Url))
            {
                sb.AppendLine($"    // {dep.Key}: '{dep.Url}', // ESM-only — keep CDN default");
                continue;
            }

            // Build the local vendor path from the URL's file name and the package folder.
            var fileName  = Path.GetFileName(new Uri(dep.Url).LocalPath);
            var pkgFolder = dep.Package.TrimStart('@').Replace('/', '-');
            var vendorPath = $"/lib/lumeo-vendor/{pkgFolder}/{fileName}";
            sb.AppendLine($"    {dep.Key}: '{vendorPath}',");
        }

        sb.AppendLine("};");
        return sb.ToString();
    }

    private static string PadRight(string s, int width) =>
        s.Length >= width ? s + "  " : s + new string(' ', width - s.Length);
}
