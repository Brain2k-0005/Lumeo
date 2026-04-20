using System.CommandLine;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Lumeo.Cli;

var root = new RootCommand("Lumeo CLI — vendorize Lumeo components into your project.");

// --- init ---
var initNamespaceOpt = new Option<string?>("--namespace", "Target namespace for generated components (e.g. MyApp.Components).");
var initPathOpt = new Option<string?>("--path", "Components folder relative to the current directory. Default: Components/Ui");
var initRegistryOpt = new Option<string?>("--registry", "Registry URL. Default: https://lumeo.nativ.sh/registry/registry.json");
var initForceOpt = new Option<bool>("--force", "Overwrite an existing lumeo.json.");
var initCmd = new Command("init", "Create a lumeo.json in the current directory.")
{
    initNamespaceOpt, initPathOpt, initRegistryOpt, initForceOpt,
};
initCmd.SetHandler(Commands.Init, initNamespaceOpt, initPathOpt, initRegistryOpt, initForceOpt);

// --- add ---
var addNameArg = new Argument<string>("component", "Component name (kebab-case), e.g. 'button'.");
var addLocalOpt = new Option<bool>("--local", "Read registry + files from local filesystem (for development).");
var addYesOpt = new Option<bool>(new[] { "--yes", "-y" }, "Skip dependency prompts — auto-install.");
var addOverwriteOpt = new Option<bool>("--overwrite", "Overwrite files that already exist.");
var addCmd = new Command("add", "Vendor a component (and its deps) into your project.")
{
    addNameArg, addLocalOpt, addYesOpt, addOverwriteOpt,
};
addCmd.SetHandler(Commands.Add, addNameArg, addLocalOpt, addYesOpt, addOverwriteOpt);

// --- list ---
var listLocalOpt = new Option<bool>("--local", "Read registry from local filesystem.");
var listCmd = new Command("list", "List all available components grouped by category.")
{
    listLocalOpt,
};
listCmd.SetHandler(Commands.List, listLocalOpt);

// --- diff ---
var diffNameArg = new Argument<string>("component", "Component name (kebab-case).");
var diffLocalOpt = new Option<bool>("--local", "Read registry + files from local filesystem.");
var diffCmd = new Command("diff", "Show files that have drifted from the registry version.")
{
    diffNameArg, diffLocalOpt,
};
diffCmd.SetHandler(Commands.Diff, diffNameArg, diffLocalOpt);

root.AddCommand(initCmd);
root.AddCommand(addCmd);
root.AddCommand(listCmd);
root.AddCommand(diffCmd);

return await root.InvokeAsync(args);

// ============================================================================
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

    internal sealed class LumeoConfig
    {
        [JsonPropertyName("namespace")] public string Namespace { get; set; } = "MyApp.Components";
        [JsonPropertyName("componentsPath")] public string ComponentsPath { get; set; } = "Components/Ui";
        [JsonPropertyName("registry")] public string Registry { get; set; } = "https://lumeo.nativ.sh/registry/registry.json";
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
            // Derive file URL from registry base (swap /registry.json → /raw/<relative>).
            var baseUrl = registryUrl.Contains("/registry.json")
                ? registryUrl.Replace("/registry.json", "/raw/")
                : registryUrl.TrimEnd('/') + "/raw/";
            return await s_http.GetStringAsync(baseUrl + relativePath);
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

    internal static class Commands
    {
        public static Task Init(string? ns, string? path, string? registry, bool force)
        {
            var target = Path.Combine(Environment.CurrentDirectory, Paths.ConfigFile);
            if (File.Exists(target) && !force)
            {
                Console.Error.WriteLine(Ansi.Yellow($"lumeo.json already exists. Pass --force to overwrite."));
                return Task.CompletedTask;
            }

            ns ??= Prompt("Target namespace", "MyApp.Components");
            path ??= Prompt("Components path", "Components/Ui");
            registry ??= "https://lumeo.nativ.sh/registry/registry.json";

            var cfg = new LumeoConfig { Namespace = ns, ComponentsPath = path, Registry = registry };
            ConfigIO.Save(cfg);

            var uiDir = Path.Combine(Environment.CurrentDirectory, path);
            Directory.CreateDirectory(uiDir);
            var readme = Path.Combine(uiDir, "README.md");
            if (!File.Exists(readme))
            {
                File.WriteAllText(readme, BuildReadme(ns, path));
            }

            Console.WriteLine();
            Console.WriteLine(Ansi.Green("OK ") + $"Wrote {Paths.ConfigFile}");
            Console.WriteLine($"  namespace       {ns}");
            Console.WriteLine($"  componentsPath  {path}");
            Console.WriteLine($"  registry        {registry}");
            Console.WriteLine();
            Console.WriteLine($"Next: {Ansi.Cyan("lumeo add button")}");
            return Task.CompletedTask;
        }

        public static async Task Add(string component, bool local, bool yes, bool overwrite)
        {
            var cfg = ConfigIO.TryLoad();
            if (cfg is null)
            {
                Console.Error.WriteLine(Ansi.Red("No lumeo.json found. Run `lumeo init` first."));
                return;
            }

            var registry = await RegistryLoader.LoadAsync(local, cfg.Registry);
            var key = component.ToLowerInvariant();
            if (!registry.Components.TryGetValue(key, out var entry))
            {
                Console.Error.WriteLine(Ansi.Red($"Unknown component '{component}'."));
                Console.Error.WriteLine($"Run {Ansi.Cyan("lumeo list")} to see available components.");
                return;
            }

            // Resolve dependency chain (BFS).
            var toInstall = new List<RegistryEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<RegistryEntry>();
            queue.Enqueue(entry);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                var curKey = ToKebab(cur.Name);
                if (!seen.Add(curKey)) continue;
                toInstall.Add(cur);
                foreach (var dep in cur.Dependencies)
                {
                    if (registry.Components.TryGetValue(dep, out var depEntry))
                        queue.Enqueue(depEntry);
                }
            }

            if (toInstall.Count > 1 && !yes)
            {
                Console.WriteLine($"{Ansi.Bold(entry.Name)} depends on:");
                foreach (var d in toInstall.Skip(1))
                    Console.WriteLine($"  - {d.Name}");
                Console.Write("Install all? [Y/n] ");
                var resp = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (resp is "n" or "no")
                {
                    toInstall = new List<RegistryEntry> { entry };
                }
            }

            var outRoot = Path.Combine(Environment.CurrentDirectory, cfg.ComponentsPath);
            Directory.CreateDirectory(outRoot);

            int totalFiles = 0;
            foreach (var item in toInstall)
            {
                var folder = Path.Combine(outRoot, item.Name);
                Directory.CreateDirectory(folder);
                foreach (var relFile in item.Files)
                {
                    var fileName = Path.GetFileName(relFile);
                    var destRel = relFile.Substring(relFile.IndexOf('/', StringComparison.Ordinal) + 1); // strip "UI/"
                    // destRel now "<Component>/<Sub>/<File>" — reroot under outRoot directly.
                    var dest = Path.Combine(outRoot, destRel.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

                    if (File.Exists(dest) && !overwrite)
                    {
                        Console.WriteLine(Ansi.Yellow($"  skip ") + $"{Path.GetRelativePath(Environment.CurrentDirectory, dest)} (exists; pass --overwrite)");
                        continue;
                    }

                    var content = await RegistryLoader.GetFileAsync(relFile, local, cfg.Registry);
                    content = NamespaceRewriter.Rewrite(content, relFile, cfg.Namespace);
                    await File.WriteAllTextAsync(dest, content, new UTF8Encoding(false));
                    totalFiles++;
                    Console.WriteLine(Ansi.Green("  +   ") + Path.GetRelativePath(Environment.CurrentDirectory, dest));
                }
            }

            Console.WriteLine();
            var label = toInstall.Count == 1 ? entry.Name : $"{entry.Name} (+{toInstall.Count - 1} dep{(toInstall.Count - 1 == 1 ? "" : "s")})";
            Console.WriteLine($"{Ansi.Green("OK")} Added {Ansi.Bold(label)} — {totalFiles} file{(totalFiles == 1 ? "" : "s")} to {cfg.ComponentsPath}/");
            if (entry.CssVars.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine(Ansi.Dim("CSS variables used:"));
                Console.WriteLine("  " + string.Join(", ", entry.CssVars));
            }
        }

        public static async Task List(bool local)
        {
            var cfg = ConfigIO.TryLoad();
            var registryUrl = cfg?.Registry ?? "https://lumeo.nativ.sh/registry/registry.json";
            var registry = await RegistryLoader.LoadAsync(local, registryUrl);

            var grouped = registry.Components
                .OrderBy(kv => kv.Value.Category, StringComparer.Ordinal)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .GroupBy(kv => kv.Value.Category);

            Console.WriteLine();
            Console.WriteLine(Ansi.Bold($"Lumeo registry v{registry.Version} — {registry.Components.Count} components"));
            Console.WriteLine();

            foreach (var g in grouped)
            {
                Console.WriteLine(Ansi.Cyan(Ansi.Bold(g.Key)));
                foreach (var kv in g)
                {
                    var name = kv.Key.PadRight(24);
                    Console.WriteLine($"  {Ansi.Green(name)} {Ansi.Dim(kv.Value.Description)}");
                }
                Console.WriteLine();
            }
            Console.WriteLine($"Add one with: {Ansi.Cyan("lumeo add <name>")}");
        }

        public static async Task Diff(string component, bool local)
        {
            var cfg = ConfigIO.TryLoad();
            if (cfg is null) { Console.Error.WriteLine(Ansi.Red("No lumeo.json found. Run `lumeo init` first.")); return; }

            var registry = await RegistryLoader.LoadAsync(local, cfg.Registry);
            var key = component.ToLowerInvariant();
            if (!registry.Components.TryGetValue(key, out var entry))
            {
                Console.Error.WriteLine(Ansi.Red($"Unknown component '{component}'.")); return;
            }

            var root = Path.Combine(Environment.CurrentDirectory, cfg.ComponentsPath);
            var drift = 0; var missing = 0; var same = 0;
            foreach (var relFile in entry.Files)
            {
                var destRel = relFile.Substring(relFile.IndexOf('/', StringComparison.Ordinal) + 1);
                var dest = Path.Combine(root, destRel.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(dest))
                {
                    Console.WriteLine(Ansi.Yellow("  missing ") + Path.GetRelativePath(Environment.CurrentDirectory, dest));
                    missing++;
                    continue;
                }
                var local0 = await File.ReadAllTextAsync(dest);
                var upstream = await RegistryLoader.GetFileAsync(relFile, local, cfg.Registry);
                upstream = NamespaceRewriter.Rewrite(upstream, relFile, cfg.Namespace);

                if (NormalizeLineEndings(local0) == NormalizeLineEndings(upstream))
                {
                    same++;
                }
                else
                {
                    drift++;
                    Console.WriteLine(Ansi.Magenta("  drift   ") + Path.GetRelativePath(Environment.CurrentDirectory, dest));
                }
            }

            Console.WriteLine();
            Console.WriteLine($"{Ansi.Bold(entry.Name)}: {Ansi.Green($"{same} in sync")}, {Ansi.Magenta($"{drift} drifted")}, {Ansi.Yellow($"{missing} missing")}.");
            if (drift > 0)
                Console.WriteLine($"Run {Ansi.Cyan($"lumeo add {component} --overwrite")} to restore registry versions.");
        }

        // --- helpers ---

        private static string Prompt(string label, string fallback)
        {
            if (Console.IsInputRedirected) return fallback;
            Console.Write($"{label} [{fallback}]: ");
            var s = Console.ReadLine();
            return string.IsNullOrWhiteSpace(s) ? fallback : s.Trim();
        }

        private static string ToKebab(string s)
        {
            var sb = new StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (char.IsUpper(c))
                {
                    if (i > 0 && (char.IsLower(s[i - 1]) || (i + 1 < s.Length && char.IsLower(s[i + 1]))))
                        sb.Append('-');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string NormalizeLineEndings(string s) => s.Replace("\r\n", "\n");

        private static string BuildReadme(string ns, string path) => $@"# Lumeo components (vendored)

Components under `{path}/` are copies of [Lumeo](https://lumeo.nativ.sh) primitives, rewritten to live in the `{ns}` namespace. You own these files — edit them freely.

## Adding a component

```bash
lumeo add button
lumeo add dialog
```

## Updating

```bash
lumeo diff button      # see what has drifted
lumeo add button --overwrite
```

## Listing

```bash
lumeo list
```

Services (ToastService, OverlayService, ThemeService, ...) still come from the
`Lumeo` NuGet package. Only presentation components can be vendored here.
";
    }
}
