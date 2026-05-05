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
var initRegistryOpt = new Option<string?>("--registry", "Registry URL. Default: jsDelivr CDN at cdn.jsdelivr.net/gh/Brain2k-0005/Lumeo@<version>/src/Lumeo/wwwroot/registry/registry.json");
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
var addNameArg = new Argument<string?>("component", () => null, "Component name (kebab-case), e.g. 'button'. Omit when --all is used.");
var addLocalOpt = new Option<bool>("--local", "Read registry + files from local filesystem (for development).");
var addYesOpt = new Option<bool>(new[] { "--yes", "-y" }, "Skip dependency prompts — auto-install.");
var addForceOpt = new Option<bool>("--force", "Overwrite all conflicts without prompting (CI).");
var addSkipOpt = new Option<bool>("--skip-existing", "Silently skip files that already exist.");
var addOverwriteOpt = new Option<bool>("--overwrite", "Alias of --force (legacy).");
var addDryOpt = new Option<bool>("--dry-run", "Print what would happen without writing files.");
var addAllOpt = new Option<bool>("--all", "Install every component in the registry.");
var addDiffOpt = new Option<bool>("--diff", "Show a unified diff against existing files; skip writing unless --yes is also set.");
var addViewOpt = new Option<bool>("--view", "Preview content that WOULD be written for new files; skip writing unless --yes is also set.");
var addCmd = new Command("add", "Vendor a component (and its deps) into your project.")
{
    addNameArg, addLocalOpt, addYesOpt, addForceOpt, addSkipOpt, addOverwriteOpt, addDryOpt,
    addAllOpt, addDiffOpt, addViewOpt,
};
addCmd.SetHandler(async ctx =>
{
    var name = ctx.ParseResult.GetValueForArgument(addNameArg);
    var opts = new Commands.AddOptions(
        Component: name,
        Local: ctx.ParseResult.GetValueForOption(addLocalOpt),
        Yes: ctx.ParseResult.GetValueForOption(addYesOpt),
        Force: ctx.ParseResult.GetValueForOption(addForceOpt),
        SkipExisting: ctx.ParseResult.GetValueForOption(addSkipOpt),
        Overwrite: ctx.ParseResult.GetValueForOption(addOverwriteOpt),
        DryRun: ctx.ParseResult.GetValueForOption(addDryOpt),
        All: ctx.ParseResult.GetValueForOption(addAllOpt),
        Diff: ctx.ParseResult.GetValueForOption(addDiffOpt),
        View: ctx.ParseResult.GetValueForOption(addViewOpt));
    await Commands.Add(opts);
});

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

// --- apply (flat, shadcn parity) + theme apply (deprecated alias) ---
// Shared options builder so the two commands stay in sync.
Command BuildApplyCommand(string name, string description, bool deprecated)
{
    var presetArg = new Argument<string?>("preset", () => null, "Preset code from the /themes customizer (6 base62 chars, e.g. b4Ndd7). Optional if --preset is provided.");
    var presetOpt = new Option<string?>("--preset", "Preset code (alternative to positional arg).");
    var onlyOpt = new Option<string?>("--only", "Comma-separated list of parts to apply: theme,font,icons,radius,menu,style,baseColor,dark. Defaults to all.");
    var dryOpt = new Option<bool>("--dry-run", "Print decoded preset without writing anything.");
    var yesOpt = new Option<bool>(new[] { "--yes", "-y" }, "Skip the confirmation prompt before writing lumeo.json.");
    var silentOpt = new Option<bool>("--silent", "Suppress non-error output.");

    var cmd = new Command(name, description)
    {
        presetArg, presetOpt, onlyOpt, dryOpt, yesOpt, silentOpt,
    };
    cmd.SetHandler(async ctx =>
    {
        var positional = ctx.ParseResult.GetValueForArgument(presetArg);
        var via = ctx.ParseResult.GetValueForOption(presetOpt);
        var preset = positional ?? via ?? "";
        var only = ctx.ParseResult.GetValueForOption(onlyOpt);
        var dryRun = ctx.ParseResult.GetValueForOption(dryOpt);
        var yes = ctx.ParseResult.GetValueForOption(yesOpt);
        var silent = ctx.ParseResult.GetValueForOption(silentOpt);
        if (deprecated && !silent)
            Console.Error.WriteLine(Ansi.Yellow("(deprecated) `lumeo theme apply` — use `lumeo apply` instead."));
        await ThemeCommands.Apply(preset, only, dryRun, yes, silent);
    });
    return cmd;
}
var applyCmd = BuildApplyCommand("apply", "Apply a theme preset (writes lumeo.json + wwwroot/lumeo-theme.json).", deprecated: false);
var themeApplyCmd = BuildApplyCommand("apply", "(Deprecated) Apply a theme preset. Use `lumeo apply` instead.", deprecated: true);
var themeCmd = new Command("theme", "Theme subcommands (deprecated — use `lumeo apply`).");
themeCmd.AddCommand(themeApplyCmd);

// --- view ---
var viewNameArg = new Argument<string>("component", "Component name (kebab-case).");
var viewLocalOpt = new Option<bool>("--local", "Read component source from local filesystem.");
var viewPathOpt = new Option<string?>("--path", "Override components path (rarely needed).");
var viewCmd = new Command("view", "Print a component's source files to stdout without writing anything.")
{
    viewNameArg, viewLocalOpt, viewPathOpt,
};
viewCmd.SetHandler(Commands.View, viewNameArg, viewLocalOpt, viewPathOpt);

// --- info ---
var infoJsonOpt = new Option<bool>("--json", "Emit machine-readable JSON instead of pretty text.");
var infoCmd = new Command("info", "Print project state (config, installed components, theme, registry).")
{
    infoJsonOpt,
};
infoCmd.SetHandler(Commands.Info, infoJsonOpt);

// --- preset encode ---
var encodeThemeOpt = new Option<string?>("--theme", "Theme name: default, blue, orange, green, rose, zinc, violet, amber, teal.");
var encodeStyleOpt = new Option<string?>("--style", "Style: default | new-york.");
var encodeBaseOpt = new Option<string?>("--base", "Base color: slate | gray | zinc | neutral | stone.");
var encodeRadiusOpt = new Option<string?>("--radius", "Radius: 0 | 0.25 | 0.5 | 0.75 | 1.");
var encodeFontOpt = new Option<string?>("--font", "Font: system | inter | geist | ibm-plex-sans | jetbrains-mono | fira-code.");
var encodeIconsOpt = new Option<string?>("--icons", "Icon library: lucide | bootstrap | fluentui | font-awesome | google-material | material-design | ionicons | devicon | flag-icons.");
var encodeMenuColorOpt = new Option<string?>("--menu-color", "Menu color: default | dark | light.");
var encodeMenuAccentOpt = new Option<string?>("--menu-accent", "Menu accent: subtle | bold | outline.");
var encodeDarkOpt = new Option<bool>("--dark", "Enable dark mode.");
var encodeCommandOpt = new Option<bool>("--command", "Print the full `lumeo apply --preset <code>` command instead of just the code.");
var presetEncodeCmd = new Command("encode", "Generate a 6-char preset code from individual option flags.")
{
    encodeThemeOpt, encodeStyleOpt, encodeBaseOpt, encodeRadiusOpt, encodeFontOpt,
    encodeIconsOpt, encodeMenuColorOpt, encodeMenuAccentOpt, encodeDarkOpt, encodeCommandOpt,
};
presetEncodeCmd.SetHandler(async ctx =>
{
    await ThemeCommands.Encode(
        theme: ctx.ParseResult.GetValueForOption(encodeThemeOpt),
        style: ctx.ParseResult.GetValueForOption(encodeStyleOpt),
        baseColor: ctx.ParseResult.GetValueForOption(encodeBaseOpt),
        radius: ctx.ParseResult.GetValueForOption(encodeRadiusOpt),
        font: ctx.ParseResult.GetValueForOption(encodeFontOpt),
        icons: ctx.ParseResult.GetValueForOption(encodeIconsOpt),
        menuColor: ctx.ParseResult.GetValueForOption(encodeMenuColorOpt),
        menuAccent: ctx.ParseResult.GetValueForOption(encodeMenuAccentOpt),
        dark: ctx.ParseResult.GetValueForOption(encodeDarkOpt),
        commandOnly: ctx.ParseResult.GetValueForOption(encodeCommandOpt));
});
var presetCmd = new Command("preset", "Preset codec utilities (encode / decode).");
presetCmd.AddCommand(presetEncodeCmd);

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
root.AddCommand(viewCmd);
root.AddCommand(infoCmd);
root.AddCommand(applyCmd);
root.AddCommand(updateAssetsCmd);
root.AddCommand(presetCmd);
root.AddCommand(themeCmd);

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

    internal sealed class LumeoThemeConfig
    {
        [JsonPropertyName("theme")] public string? Theme { get; set; }
        [JsonPropertyName("style")] public string? Style { get; set; }
        [JsonPropertyName("baseColor")] public string? BaseColor { get; set; }
        [JsonPropertyName("radius")] public string? Radius { get; set; }
        [JsonPropertyName("font")] public string? Font { get; set; }
        [JsonPropertyName("iconLibrary")] public string? IconLibrary { get; set; }
        [JsonPropertyName("menuColor")] public string? MenuColor { get; set; }
        [JsonPropertyName("menuAccent")] public string? MenuAccent { get; set; }
        [JsonPropertyName("dark")] public bool? Dark { get; set; }
    }

    internal sealed class LumeoConfig
    {
        [JsonPropertyName("namespace")] public string Namespace { get; set; } = "MyApp.Components";
        [JsonPropertyName("componentsPath")] public string ComponentsPath { get; set; } = "Components/Ui";
        [JsonPropertyName("registry")] public string Registry { get; set; } = RegistryLoader.DefaultRegistryUrl;
        [JsonPropertyName("assets")] public AssetsConfig? Assets { get; set; }
        [JsonPropertyName("theme")] public LumeoThemeConfig? Theme { get; set; }
        [JsonPropertyName("components")] public Dictionary<string, InstalledComponent>? Components { get; set; }
    }

    internal sealed class RegistryEntry
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("category")] public string Category { get; set; } = "Utility";
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("nugetPackage")] public string? NugetPackage { get; set; }
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
        internal static readonly HttpClient s_http = new() { Timeout = TimeSpan.FromSeconds(20) };
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

            var url = registryUrl ?? DefaultRegistryUrl;
            var r2 = await s_http.GetFromJsonAsync<Registry>(url, s_opts);
            return r2 ?? throw new InvalidOperationException($"Failed to fetch registry: {url}");
        }

        /// <summary>Default registry URL — jsDelivr mirrors github content globally over CDN,
        /// no hosting setup required. Pinned to an RC tag so the registry and the CLI binary
        /// always agree on schema/shape.</summary>
        public const string DefaultRegistryUrl =
            "https://cdn.jsdelivr.net/gh/Brain2k-0005/Lumeo@v2.0.0-rc.9/src/Lumeo/wwwroot/registry/registry.json";

        /// <summary>Derive the base URL to fetch component source files from the registry URL.
        /// Supports three layouts:
        ///   1) jsDelivr / raw.githubusercontent.com / any URL ending in ".../Lumeo/wwwroot/registry/registry.json"
        ///      → strip trailing "wwwroot/registry/registry.json" so file paths resolve under src/Lumeo/
        ///   2) Self-hosted with legacy "/raw/" convention (".../registry.json" → ".../raw/")
        ///   3) Anything else: append "/raw/" to the trimmed URL
        /// </summary>
        public static string DeriveFileBaseUrl(string registryUrl)
        {
            const string marker = "/wwwroot/registry/registry.json";
            if (registryUrl.EndsWith(marker, StringComparison.OrdinalIgnoreCase))
            {
                // Strip marker, keep trailing slash so relative path (e.g. "UI/Button/Button.razor") lands at src/Lumeo/UI/...
                return registryUrl.Substring(0, registryUrl.Length - marker.Length + 1);
            }
            if (registryUrl.Contains("/registry.json", StringComparison.OrdinalIgnoreCase))
                return registryUrl.Replace("/registry.json", "/raw/", StringComparison.OrdinalIgnoreCase);
            return registryUrl.TrimEnd('/') + "/raw/";
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
            var baseUrl = DeriveFileBaseUrl(registryUrl);
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
                const string prefix = "_content/Lumeo/";
                var stripped = assetRelPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? assetRelPath.Substring(prefix.Length) : assetRelPath;
                var abs = Path.Combine(Paths.LocalSourceRoot(repoRoot), "wwwroot",
                    stripped.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(abs)) throw new FileNotFoundException($"Local asset not found: {abs}");
                return await File.ReadAllBytesAsync(abs);
            }
            // Strip "_content/Lumeo/" → relative wwwroot path, then fetch via the same base
            // the file fetcher uses (which already knows how to resolve src/Lumeo/).
            const string contentPrefix = "_content/Lumeo/";
            var rel = assetRelPath.StartsWith(contentPrefix, StringComparison.OrdinalIgnoreCase)
                ? assetRelPath.Substring(contentPrefix.Length) : assetRelPath;
            var baseUrl = DeriveFileBaseUrl(registryUrl);
            // Assets live under src/Lumeo/wwwroot/; DeriveFileBaseUrl returns src/Lumeo/ so add wwwroot/.
            var url = $"{baseUrl}wwwroot/{rel}";
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
