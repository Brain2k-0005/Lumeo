using System.CommandLine;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Lumeo.Cli;

// Clean Ctrl+C handling — exit 130 per POSIX.
Console.CancelKeyPress += (s, e) => { e.Cancel = false; Environment.Exit(130); };

var root = new RootCommand("Lumeo CLI — vendorize Lumeo components into your project.");

// --- init ---
var initNamespaceOpt = new Option<string?>("--namespace", "Target namespace for generated components (e.g. MyApp.Components).");
var initPathOpt = new Option<string?>("--path", "Components folder relative to the current directory. Default: Components/Ui");
var initRegistryOpt = new Option<string?>("--registry", "Registry URL. Default: https://lumeo.nativ.sh/registry/registry.json");
var initForceOpt = new Option<bool>("--force", "Overwrite an existing lumeo.json.");
var initYesOpt = new Option<bool>(new[] { "--yes", "-y" }, "Accept all defaults, skip prompts (CI mode).");
var initWithCssOpt = new Option<bool>("--with-css", "Copy Lumeo's pre-built CSS/JS into wwwroot/ (no Tailwind required).");
var initWithTailwindOpt = new Option<bool>("--with-tailwind", "Scaffold Tailwind v4 setup (Styles/input.css + package.json).");
var initNoAssetsOpt = new Option<bool>("--no-assets", "Skip asset setup entirely (you'll wire CSS up yourself).");
var initLocalOpt = new Option<bool>("--local", "Copy assets from a local Lumeo checkout (dev only).");
var initCmd = new Command("init", "Create a lumeo.json in the current directory.")
{
    initNamespaceOpt, initPathOpt, initRegistryOpt, initForceOpt, initYesOpt,
    initWithCssOpt, initWithTailwindOpt, initNoAssetsOpt, initLocalOpt,
};
initCmd.SetHandler(async ctx =>
{
    var opts = new Commands.InitOptions(
        Namespace: ctx.ParseResult.GetValueForOption(initNamespaceOpt),
        Path: ctx.ParseResult.GetValueForOption(initPathOpt),
        Registry: ctx.ParseResult.GetValueForOption(initRegistryOpt),
        Force: ctx.ParseResult.GetValueForOption(initForceOpt),
        Yes: ctx.ParseResult.GetValueForOption(initYesOpt),
        WithCss: ctx.ParseResult.GetValueForOption(initWithCssOpt),
        WithTailwind: ctx.ParseResult.GetValueForOption(initWithTailwindOpt),
        NoAssets: ctx.ParseResult.GetValueForOption(initNoAssetsOpt),
        Local: ctx.ParseResult.GetValueForOption(initLocalOpt));
    await Commands.Init(opts);
});

// --- add ---
var addNameArg = new Argument<string>("component", "Component name (kebab-case), e.g. 'button'.");
var addLocalOpt = new Option<bool>("--local", "Read registry + files from local filesystem (for development).");
var addYesOpt = new Option<bool>(new[] { "--yes", "-y" }, "Skip dependency prompts — auto-install.");
var addForceOpt = new Option<bool>("--force", "Overwrite all conflicts without prompting (CI).");
var addSkipOpt = new Option<bool>("--skip-existing", "Silently skip files that already exist.");
var addOverwriteOpt = new Option<bool>("--overwrite", "Alias of --force (legacy).");
var addDryOpt = new Option<bool>("--dry-run", "Print what would happen without writing files.");
var addCmd = new Command("add", "Vendor a component (and its deps) into your project.")
{
    addNameArg, addLocalOpt, addYesOpt, addForceOpt, addSkipOpt, addOverwriteOpt, addDryOpt,
};
addCmd.SetHandler(Commands.Add, addNameArg, addLocalOpt, addYesOpt, addForceOpt, addSkipOpt, addOverwriteOpt, addDryOpt);

// --- update ---
var updateNameArg = new Argument<string?>("component", () => null, "Component to update. Omit to update all installed components.");
var updateLocalOpt = new Option<bool>("--local", "Read registry + files from local filesystem.");
var updateForceOpt = new Option<bool>("--force", "Update without prompts.");
var updateCheckOpt = new Option<bool>("--check", "Report drift without writing. Exit 1 if drift found, 0 if clean.");
var updateResetOpt = new Option<bool>("--force-reset", "Restore to registry version, discarding local edits.");
var updateDryOpt = new Option<bool>("--dry-run", "Print what would happen without writing files.");
var updateCmd = new Command("update", "Pull upstream changes for a component (or all installed).")
{
    updateNameArg, updateLocalOpt, updateForceOpt, updateCheckOpt, updateResetOpt, updateDryOpt,
};
updateCmd.SetHandler(Commands.Update, updateNameArg, updateLocalOpt, updateForceOpt, updateCheckOpt, updateResetOpt, updateDryOpt);

// --- remove ---
var removeNameArg = new Argument<string>("component", "Component name to remove (kebab-case).");
var removeForceOpt = new Option<bool>("--force", "Skip confirmation prompt.");
var removeDryOpt = new Option<bool>("--dry-run", "Print what would happen without deleting files.");
var removeCmd = new Command("remove", "Delete a vendored component from your project.")
{
    removeNameArg, removeForceOpt, removeDryOpt,
};
removeCmd.SetHandler(Commands.Remove, removeNameArg, removeForceOpt, removeDryOpt);

// --- list ---
var listLocalOpt = new Option<bool>("--local", "Read registry from local filesystem.");
var listCategoryOpt = new Option<string?>("--category", "Filter to a single category (e.g. Forms).");
var listJsonOpt = new Option<bool>("--json", "Dump registry entries as JSON for tooling consumption.");
var listCmd = new Command("list", "List all available components grouped by category.")
{
    listLocalOpt, listCategoryOpt, listJsonOpt,
};
listCmd.SetHandler(Commands.List, listLocalOpt, listCategoryOpt, listJsonOpt);

// --- diff ---
var diffNameArg = new Argument<string>("component", "Component name (kebab-case).");
var diffLocalOpt = new Option<bool>("--local", "Read registry + files from local filesystem.");
var diffCmd = new Command("diff", "Show files that have drifted from the registry version.")
{
    diffNameArg, diffLocalOpt,
};
diffCmd.SetHandler(Commands.Diff, diffNameArg, diffLocalOpt);

// --- update-assets ---
var updateAssetsLocalOpt = new Option<bool>("--local", "Read assets from a local Lumeo checkout.");
var updateAssetsForceOpt = new Option<bool>("--force", "Overwrite all files without prompting.");
var updateAssetsDryOpt = new Option<bool>("--dry-run", "Print what would happen without writing files.");
var updateAssetsCmd = new Command("update-assets", "Refresh copied CSS/JS assets (when assets.mode == prebuilt).")
{
    updateAssetsLocalOpt, updateAssetsForceOpt, updateAssetsDryOpt,
};
updateAssetsCmd.SetHandler(Commands.UpdateAssets, updateAssetsLocalOpt, updateAssetsForceOpt, updateAssetsDryOpt);

root.AddCommand(initCmd);
root.AddCommand(addCmd);
root.AddCommand(updateCmd);
root.AddCommand(removeCmd);
root.AddCommand(listCmd);
root.AddCommand(diffCmd);
root.AddCommand(updateAssetsCmd);

var parseExit = await root.InvokeAsync(args);
// Respect handler-set Environment.ExitCode (e.g. `update --check` → 1 on drift).
return Environment.ExitCode != 0 ? Environment.ExitCode : parseExit;

// ============================================================================
// Core types + helpers. Commands live in Commands.cs.
namespace Lumeo.Cli
{
    internal static class Ansi
    {
        public static bool Enabled { get; } = !Console.IsOutputRedirected
            && Environment.GetEnvironmentVariable("NO_COLOR") is null;
        public static string Wrap(string code, string s) => Enabled ? $"\u001b[{code}m{s}\u001b[0m" : s;
        public static string Bold(string s) => Wrap("1", s);
        public static string Dim(string s) => Wrap("2", s);
        public static string Cyan(string s) => Wrap("36", s);
        public static string Green(string s) => Wrap("32", s);
        public static string Yellow(string s) => Wrap("33", s);
        public static string Red(string s) => Wrap("31", s);
        public static string Magenta(string s) => Wrap("35", s);
    }

    internal sealed class InstalledComponent
    {
        [JsonPropertyName("version")] public string Version { get; set; } = "";
        [JsonPropertyName("files")] public List<string> Files { get; set; } = new();
    }

    internal sealed class AssetsConfig
    {
        /// <summary>One of: "prebuilt", "tailwind", "none".</summary>
        [JsonPropertyName("mode")] public string Mode { get; set; } = "none";
        [JsonPropertyName("version")] public string? Version { get; set; }
        [JsonPropertyName("files")] public List<string>? Files { get; set; }
        [JsonPropertyName("stylesEntry")] public string? StylesEntry { get; set; }
        [JsonPropertyName("output")] public string? Output { get; set; }
    }

    internal sealed class LumeoConfig
    {
        [JsonPropertyName("namespace")] public string Namespace { get; set; } = "MyApp.Components";
        [JsonPropertyName("componentsPath")] public string ComponentsPath { get; set; } = "Components/Ui";
        [JsonPropertyName("registry")] public string Registry { get; set; } = "https://lumeo.nativ.sh/registry/registry.json";
        [JsonPropertyName("assets")] public AssetsConfig? Assets { get; set; }
        [JsonPropertyName("components")] public Dictionary<string, InstalledComponent>? Components { get; set; }
    }

    internal sealed class RegistryEntry
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("category")] public string Category { get; set; } = "Utility";
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("files")] public List<string> Files { get; set; } = new();
        [JsonPropertyName("dependencies")] public List<string> Dependencies { get; set; } = new();
        [JsonPropertyName("cssVars")] public List<string> CssVars { get; set; } = new();
        [JsonPropertyName("registryUrl")] public string? RegistryUrl { get; set; }
    }

    internal sealed class Registry
    {
        [JsonPropertyName("version")] public string Version { get; set; } = "";
        [JsonPropertyName("components")] public Dictionary<string, RegistryEntry> Components { get; set; } = new();
    }

    internal static class Paths
    {
        public const string ConfigFile = "lumeo.json";

        public static string? FindRepoRoot(string start)
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Lumeo.slnx"))) return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        public static string LocalRegistryPath(string repoRoot) =>
            Path.Combine(repoRoot, "src", "Lumeo", "registry", "registry.json");

        public static string LocalSourceRoot(string repoRoot) =>
            Path.Combine(repoRoot, "src", "Lumeo");

        /// <summary>Map registry-relative path ("UI/Button/Button.razor") to a destination under componentsPath.</summary>
        public static string ToDestPath(string componentsRoot, string registryRelFile)
        {
            var destRel = registryRelFile.Substring(registryRelFile.IndexOf('/', StringComparison.Ordinal) + 1);
            return Path.Combine(componentsRoot, destRel.Replace('/', Path.DirectorySeparatorChar));
        }
    }

    internal static class ConfigIO
    {
        private static readonly JsonSerializerOptions s_opts = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static LumeoConfig? TryLoad()
        {
            var path = Path.Combine(Environment.CurrentDirectory, Paths.ConfigFile);
            if (!File.Exists(path)) return null;
            try { return JsonSerializer.Deserialize<LumeoConfig>(File.ReadAllText(path), s_opts); }
            catch { return null; }
        }

        public static void Save(LumeoConfig cfg)
        {
            var path = Path.Combine(Environment.CurrentDirectory, Paths.ConfigFile);
            File.WriteAllText(path, JsonSerializer.Serialize(cfg, s_opts));
        }

        public static void RecordInstall(LumeoConfig cfg, string key, string version, IEnumerable<string> files)
        {
            cfg.Components ??= new Dictionary<string, InstalledComponent>(StringComparer.OrdinalIgnoreCase);
            cfg.Components[key] = new InstalledComponent
            {
                Version = version,
                Files = files.ToList(),
            };
            Save(cfg);
        }

        public static void RecordRemove(LumeoConfig cfg, string key)
        {
            if (cfg.Components is null) return;
            cfg.Components.Remove(key);
            Save(cfg);
        }
    }

    internal static class RegistryLoader
    {
        private static readonly HttpClient s_http = new() { Timeout = TimeSpan.FromSeconds(20) };
        private static readonly JsonSerializerOptions s_opts = new() { PropertyNameCaseInsensitive = true };

        public static async Task<Registry> LoadAsync(bool local, string? registryUrl)
        {
            if (local)
            {
                var repoRoot = Paths.FindRepoRoot(Environment.CurrentDirectory)
                               ?? throw new InvalidOperationException("--local requires running inside the Lumeo repository (no Lumeo.slnx found).");
                var path = Paths.LocalRegistryPath(repoRoot);
                if (!File.Exists(path)) throw new FileNotFoundException($"Local registry not found: {path}");
                var r = JsonSerializer.Deserialize<Registry>(File.ReadAllText(path), s_opts);
                return r ?? throw new InvalidOperationException("Failed to parse local registry.");
            }

            var url = registryUrl ?? "https://lumeo.nativ.sh/registry/registry.json";
            var r2 = await s_http.GetFromJsonAsync<Registry>(url, s_opts);
            return r2 ?? throw new InvalidOperationException($"Failed to fetch registry: {url}");
        }

        public static async Task<string> GetFileAsync(string relativePath, bool local, string registryUrl)
        {
            if (local)
            {
                var repoRoot = Paths.FindRepoRoot(Environment.CurrentDirectory)
                               ?? throw new InvalidOperationException("--local requires running inside the Lumeo repo.");
                var abs = Path.Combine(Paths.LocalSourceRoot(repoRoot), relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(abs)) throw new FileNotFoundException($"Local file not found: {abs}");
                return await File.ReadAllTextAsync(abs);
            }
            var baseUrl = registryUrl.Contains("/registry.json")
                ? registryUrl.Replace("/registry.json", "/raw/")
                : registryUrl.TrimEnd('/') + "/raw/";
            return await s_http.GetStringAsync(baseUrl + relativePath);
        }

        /// <summary>Fetch a static asset under _content/Lumeo/*. Local mode maps to src/Lumeo/wwwroot/*.</summary>
        public static async Task<byte[]> GetAssetBytesAsync(string assetRelPath, bool local, string registryUrl)
        {
            // assetRelPath is like "_content/Lumeo/css/lumeo.css".
            if (local)
            {
                var repoRoot = Paths.FindRepoRoot(Environment.CurrentDirectory)
                               ?? throw new InvalidOperationException("--local requires running inside the Lumeo repo.");
                // Strip the "_content/Lumeo/" prefix to map to src/Lumeo/wwwroot.
                var stripped = assetRelPath;
                const string prefix = "_content/Lumeo/";
                if (stripped.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    stripped = stripped.Substring(prefix.Length);
                var abs = Path.Combine(Paths.LocalSourceRoot(repoRoot), "wwwroot",
                    stripped.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(abs)) throw new FileNotFoundException($"Local asset not found: {abs}");
                return await File.ReadAllBytesAsync(abs);
            }
            // Base site URL, derived from registry URL. Registry is https://lumeo.nativ.sh/registry/registry.json;
            // strip "registry/registry.json" to get the site root.
            var siteBase = registryUrl;
            var idx = siteBase.IndexOf("/registry/", StringComparison.OrdinalIgnoreCase);
            if (idx > 0) siteBase = siteBase.Substring(0, idx);
            siteBase = siteBase.TrimEnd('/');
            var url = $"{siteBase}/{assetRelPath}";
            return await s_http.GetByteArrayAsync(url);
        }
    }

    internal static class NamespaceRewriter
    {
        public static string Rewrite(string content, string filePath, string targetNamespace)
        {
            if (filePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            {
                content = Regex.Replace(content, @"^@namespace\s+Lumeo(\.[A-Za-z0-9_.]*)?$",
                    m => $"@namespace {targetNamespace}{m.Groups[1].Value}",
                    RegexOptions.Multiline);
            }
            else if (filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                content = Regex.Replace(content, @"^namespace\s+Lumeo(\.[A-Za-z0-9_.]*)?(\s*[;{])",
                    m => $"namespace {targetNamespace}{m.Groups[1].Value}{m.Groups[2].Value}",
                    RegexOptions.Multiline);
            }
            return content;
        }
    }

    internal static class Diffing
    {
        public static string Normalize(string s) => s.Replace("\r\n", "\n");

        /// <summary>Simple line-by-line diff via LCS. Removed lines red, added green, context dim.</summary>
        public static void PrintUnified(string oldContent, string newContent, int context = 2)
        {
            var oldLines = Normalize(oldContent).Split('\n');
            var newLines = Normalize(newContent).Split('\n');

            int n = oldLines.Length, m = newLines.Length;
            var dp = new int[n + 1, m + 1];
            for (int i = n - 1; i >= 0; i--)
                for (int j = m - 1; j >= 0; j--)
                    dp[i, j] = oldLines[i] == newLines[j] ? dp[i + 1, j + 1] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);

            var ops = new List<(char op, string line)>();
            int ii = 0, jj = 0;
            while (ii < n && jj < m)
            {
                if (oldLines[ii] == newLines[jj]) { ops.Add((' ', oldLines[ii])); ii++; jj++; }
                else if (dp[ii + 1, jj] >= dp[ii, jj + 1]) { ops.Add(('-', oldLines[ii])); ii++; }
                else { ops.Add(('+', newLines[jj])); jj++; }
            }
            while (ii < n) { ops.Add(('-', oldLines[ii++])); }
            while (jj < m) { ops.Add(('+', newLines[jj++])); }

            var changed = new HashSet<int>();
            for (int i = 0; i < ops.Count; i++) if (ops[i].op != ' ') changed.Add(i);
            if (changed.Count == 0) { Console.WriteLine(Ansi.Dim("  (no differences)")); return; }

            bool skipping = false;
            for (int i = 0; i < ops.Count; i++)
            {
                bool near = false;
                for (int k = Math.Max(0, i - context); k <= Math.Min(ops.Count - 1, i + context); k++)
                    if (changed.Contains(k)) { near = true; break; }

                if (!near)
                {
                    if (!skipping) { Console.WriteLine(Ansi.Dim("  ...")); skipping = true; }
                    continue;
                }
                skipping = false;

                var (op, line) = ops[i];
                switch (op)
                {
                    case '-': Console.WriteLine(Ansi.Red("- " + line)); break;
                    case '+': Console.WriteLine(Ansi.Green("+ " + line)); break;
                    default: Console.WriteLine(Ansi.Dim("  " + line)); break;
                }
            }
        }
    }

    internal static class Prompts
    {
        public static bool Interactive => !Console.IsInputRedirected && !Console.IsOutputRedirected;

        /// <summary>Prompt with default — Enter accepts the default.</summary>
        public static string Ask(string label, string fallback)
        {
            if (!Interactive) return fallback;
            Console.Write($"{label} [{Ansi.Cyan(fallback)}]: ");
            var s = Console.ReadLine();
            return string.IsNullOrWhiteSpace(s) ? fallback : s.Trim();
        }

        public static bool Confirm(string question, bool defaultYes = false)
        {
            if (!Interactive) return defaultYes;
            var hint = defaultYes ? "[Y/n]" : "[y/N]";
            Console.Write($"{question} {hint} ");
            var s = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(s)) return defaultYes;
            return s is "y" or "yes";
        }

        /// <summary>Single-key choice. chars is an ordered set of accepted lowercase keys.
        /// Returns the lowercase char (or capital 'A' for abort), or null if Esc aborted.</summary>
        public static char? Choice(string prompt, string chars)
        {
            if (!Interactive) return null;
            Console.Write(prompt);
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Escape) { Console.WriteLine(); return null; }
                var raw = key.KeyChar;
                if (raw == 'A') { Console.WriteLine("A"); return 'A'; }
                var lower = char.ToLowerInvariant(raw);
                if (chars.Contains(lower)) { Console.WriteLine(lower); return lower; }
            }
        }
    }
}
