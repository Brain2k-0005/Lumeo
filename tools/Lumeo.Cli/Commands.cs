using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Lumeo.Cli;

internal static class Commands
{
    public sealed record InitOptions(
        string? Namespace,
        string? Path,
        string? Registry,
        bool Force,
        bool Yes,
        bool WithCss,
        bool WithTailwind,
        bool NoAssets,
        bool Local);

    // Files copied in prebuilt asset mode. Kept here so `init` and `update-assets` stay in sync.
    private static readonly (string Src, string Dest)[] s_prebuiltAssets =
    {
        ("_content/Lumeo/css/lumeo.css",           "wwwroot/css/lumeo.css"),
        ("_content/Lumeo/css/lumeo-utilities.css", "wwwroot/css/lumeo-utilities.css"),
        ("_content/Lumeo/js/theme.js",             "wwwroot/js/theme.js"),
        ("_content/Lumeo/js/components.js",        "wwwroot/js/components.js"),
    };

    // ---------------------------------------------------------------- init
    public static async Task Init(InitOptions opts)
    {
        var target = System.IO.Path.Combine(Environment.CurrentDirectory, Paths.ConfigFile);
        if (File.Exists(target) && !opts.Force)
        {
            Console.Error.WriteLine(Ansi.Yellow("lumeo.json already exists. Pass --force to overwrite."));
            return;
        }

        // Auto-detect namespace from .csproj when no --namespace supplied.
        var ns = opts.Namespace;
        var detectedNs = ns ?? DetectProjectNamespace(Environment.CurrentDirectory);
        var defaultNs = !string.IsNullOrEmpty(detectedNs) ? $"{detectedNs}.Components" : "MyApp.Components";
        if (ns is null)
            ns = opts.Yes ? defaultNs : Prompts.Ask("Target namespace", defaultNs);

        var path = opts.Path;
        var defaultPath = "Components/Ui";
        if (path is null)
            path = opts.Yes ? defaultPath : Prompts.Ask("Components path", defaultPath);

        var registry = opts.Registry;
        var defaultRegistry = RegistryLoader.DefaultRegistryUrl;
        if (registry is null)
            registry = opts.Yes ? defaultRegistry : Prompts.Ask("Registry URL", defaultRegistry);

        // --- NuGet detection -------------------------------------------------
        var nugetDetected = DetectLumeoNuGet(Environment.CurrentDirectory);

        AssetsConfig? assets = null;

        if (nugetDetected)
        {
            if (opts.WithCss || opts.WithTailwind || opts.NoAssets)
            {
                Console.WriteLine(Ansi.Yellow("! NuGet detected — asset flags ignored (package provides CSS/JS)."));
            }
            else
            {
                Console.WriteLine(Ansi.Dim("Lumeo NuGet detected — no extra CSS/JS setup needed."));
            }
        }
        else
        {
            // Pick a setup mode: flag > interactive prompt > (--yes default = css).
            char mode;
            if (opts.WithCss)            mode = '1';
            else if (opts.WithTailwind)  mode = '2';
            else if (opts.NoAssets)      mode = '3';
            else if (opts.Yes)           mode = '1'; // safest CI default
            else                         mode = PromptAssetSetup();

            Console.WriteLine();
            switch (mode)
            {
                case '1':
                    Console.WriteLine(Ansi.Cyan("→ Option 1: Copy pre-built CSS/JS into wwwroot/"));
                    try { assets = await CopyPrebuiltAssetsAsync(registry, opts.Local); }
                    catch { return; } // message + exit code already set
                    break;
                case '2':
                    Console.WriteLine(Ansi.Cyan("→ Option 2: Scaffold Tailwind v4 setup"));
                    assets = ScaffoldTailwind(ns);
                    break;
                default:
                    Console.WriteLine(Ansi.Cyan("→ Option 3: Skip CSS setup"));
                    assets = new AssetsConfig { Mode = "none" };
                    PrintSkipReminder();
                    break;
            }
        }

        var cfg = new LumeoConfig
        {
            Namespace = ns,
            ComponentsPath = path,
            Registry = registry,
            Assets = assets,
            Components = new Dictionary<string, InstalledComponent>(StringComparer.OrdinalIgnoreCase),
        };
        ConfigIO.Save(cfg);

        var uiDir = System.IO.Path.Combine(Environment.CurrentDirectory, path);
        Directory.CreateDirectory(uiDir);
        var gitkeep = System.IO.Path.Combine(uiDir, ".gitkeep");
        if (!File.Exists(gitkeep)) File.WriteAllText(gitkeep, "");
        var readme = System.IO.Path.Combine(uiDir, "README.md");
        if (!File.Exists(readme)) File.WriteAllText(readme, BuildReadme(ns, path));

        Console.WriteLine();
        Console.WriteLine(Ansi.Green("OK ") + $"Wrote {Paths.ConfigFile}");
        Console.WriteLine($"  namespace       {ns}");
        Console.WriteLine($"  componentsPath  {path}");
        Console.WriteLine($"  registry        {registry}");
        if (assets is not null)
            Console.WriteLine($"  assets.mode     {assets.Mode}");
        Console.WriteLine();
        Console.WriteLine($"Next: {Ansi.Cyan("lumeo add button")}");
    }

    private static string? DetectProjectNamespace(string cwd)
    {
        try
        {
            var csproj = Directory.EnumerateFiles(cwd, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (csproj is null) return null;
            try
            {
                var doc = XDocument.Load(csproj);
                var rootNs = doc.Descendants("RootNamespace").FirstOrDefault()?.Value?.Trim();
                if (!string.IsNullOrEmpty(rootNs)) return rootNs;
            }
            catch { /* fall back to filename */ }
            return System.IO.Path.GetFileNameWithoutExtension(csproj);
        }
        catch { return null; }
    }

    /// <summary>Walk up from cwd looking for .csproj files. Scan each for a Lumeo package/project reference.
    /// Stops at git root or after 5 levels.</summary>
    private static bool DetectLumeoNuGet(string cwd)
    {
        try
        {
            var dir = new DirectoryInfo(cwd);
            for (int depth = 0; dir is not null && depth < 6; depth++, dir = dir.Parent)
            {
                foreach (var csproj in Directory.EnumerateFiles(dir.FullName, "*.csproj", SearchOption.TopDirectoryOnly))
                {
                    if (CsprojReferencesLumeo(csproj)) return true;
                }
                if (Directory.Exists(System.IO.Path.Combine(dir.FullName, ".git"))) break; // at repo root
            }
        }
        catch { /* swallow — detection is best-effort */ }
        return false;
    }

    private static bool CsprojReferencesLumeo(string csprojPath)
    {
        try
        {
            var doc = XDocument.Load(csprojPath);
            foreach (var pr in doc.Descendants("PackageReference"))
            {
                var include = pr.Attribute("Include")?.Value?.Trim();
                if (string.Equals(include, "Lumeo", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            // Monorepo case: <ProjectReference Include="..\Lumeo\Lumeo.csproj" />.
            foreach (var pr in doc.Descendants("ProjectReference"))
            {
                var include = pr.Attribute("Include")?.Value;
                if (string.IsNullOrEmpty(include)) continue;
                var fileName = System.IO.Path.GetFileNameWithoutExtension(include);
                if (string.Equals(fileName, "Lumeo", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { /* unreadable csproj — skip */ }
        return false;
    }

    private static char PromptAssetSetup()
    {
        Console.WriteLine();
        Console.WriteLine("Lumeo NuGet was not detected in any nearby .csproj.");
        Console.WriteLine();
        Console.WriteLine("Vendored components need Lumeo's CSS tokens and a Tailwind utility bundle to");
        Console.WriteLine("render correctly. Pick a setup path:");
        Console.WriteLine();
        Console.WriteLine("  [1] Copy pre-built CSS/JS into wwwroot/   (recommended — zero dependencies,");
        Console.WriteLine("      no Tailwind, no Node)");
        Console.WriteLine("  [2] Scaffold Tailwind v4 setup             (for devs who also want to write");
        Console.WriteLine("      their own Tailwind classes; uses @tailwindcss/cli)");
        Console.WriteLine("  [3] Skip                                   (I'll wire CSS up myself)");
        Console.WriteLine();

        if (!Prompts.Interactive) return '1';
        var choice = Prompts.Choice("Choice [1/2/3]: ", "123");
        return choice ?? '1';
    }

    private static async Task<AssetsConfig> CopyPrebuiltAssetsAsync(string registryUrl, bool local)
    {
        var written = new List<string>();
        foreach (var (src, dest) in s_prebuiltAssets)
        {
            var abs = System.IO.Path.Combine(Environment.CurrentDirectory, dest.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(abs)!);
            byte[] bytes;
            try
            {
                bytes = await RegistryLoader.GetAssetBytesAsync(src, local, registryUrl);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(Ansi.Red($"Failed to fetch {src}: {ex.Message}"));
                Console.Error.WriteLine(Ansi.Red("Asset setup aborted. Fix connectivity and re-run `lumeo init --with-css`."));
                Environment.ExitCode = 1;
                throw;
            }
            await File.WriteAllBytesAsync(abs, bytes);
            Console.WriteLine(Ansi.Green("  +   ") + dest);
            written.Add(dest);
        }

        Console.WriteLine();
        Console.WriteLine("Add these to your index.html / _Host.cshtml:");
        Console.WriteLine(Ansi.Dim("  <link rel=\"stylesheet\" href=\"css/lumeo.css\" />"));
        Console.WriteLine(Ansi.Dim("  <link rel=\"stylesheet\" href=\"css/lumeo-utilities.css\" />"));
        Console.WriteLine(Ansi.Dim("  <script src=\"js/theme.js\"></script>"));
        Console.WriteLine(Ansi.Dim("  <script src=\"js/components.js\" type=\"module\"></script>"));

        return new AssetsConfig
        {
            Mode = "prebuilt",
            Version = "2.0.0",
            Files = written,
        };
    }

    private static AssetsConfig ScaffoldTailwind(string ns)
    {
        // Slug from namespace — strip trailing .Components, lower-case, dashes.
        var slug = ns.EndsWith(".Components", StringComparison.OrdinalIgnoreCase)
            ? ns.Substring(0, ns.Length - ".Components".Length)
            : ns;
        slug = slug.ToLowerInvariant().Replace('.', '-');

        // Styles/input.css
        var stylesDir = System.IO.Path.Combine(Environment.CurrentDirectory, "Styles");
        Directory.CreateDirectory(stylesDir);
        var inputCssPath = System.IO.Path.Combine(stylesDir, "input.css");
        const string inputCss = """
@import "tailwindcss";
@import "./_content/Lumeo/css/lumeo.css" layer(base);  /* tokens */

/* Let Tailwind scan your own markup AND your vendored Lumeo components. */
@source "./**/*.razor";
@source "./Components/Ui/**/*.razor";
""";
        File.WriteAllText(inputCssPath, inputCss);
        Console.WriteLine(Ansi.Green("  +   ") + "Styles/input.css");

        // package.json — merge-or-create.
        var pkgPath = System.IO.Path.Combine(Environment.CurrentDirectory, "package.json");
        if (File.Exists(pkgPath))
        {
            MergeIntoPackageJson(pkgPath);
            Console.WriteLine(Ansi.Green("  ✓   ") + "package.json (merged Tailwind scripts + devDependency)");
        }
        else
        {
            var pkg = new JsonObject
            {
                ["name"] = slug,
                ["private"] = true,
                ["scripts"] = new JsonObject
                {
                    ["build:css"] = "tailwindcss -i Styles/input.css -o wwwroot/css/app.css --minify",
                    ["watch:css"] = "tailwindcss -i Styles/input.css -o wwwroot/css/app.css --watch",
                },
                ["devDependencies"] = new JsonObject
                {
                    ["@tailwindcss/cli"] = "^4.0.0",
                },
            };
            File.WriteAllText(pkgPath, pkg.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(Ansi.Green("  +   ") + "package.json");
        }

        Console.WriteLine();
        Console.WriteLine(Ansi.Green("✓ Wrote Styles/input.css"));
        Console.WriteLine(Ansi.Green("✓ Updated package.json with Tailwind build scripts"));
        Console.WriteLine();
        Console.WriteLine("Next:");
        Console.WriteLine("  " + Ansi.Cyan("npm install"));
        Console.WriteLine("  " + Ansi.Cyan("npm run watch:css"));
        Console.WriteLine();
        Console.WriteLine("Then add to your index.html:");
        Console.WriteLine(Ansi.Dim("  <link rel=\"stylesheet\" href=\"css/app.css\" />"));

        return new AssetsConfig
        {
            Mode = "tailwind",
            StylesEntry = "Styles/input.css",
            Output = "wwwroot/css/app.css",
        };
    }

    private static void MergeIntoPackageJson(string pkgPath)
    {
        var raw = File.ReadAllText(pkgPath);
        JsonNode? root;
        try { root = JsonNode.Parse(raw); }
        catch
        {
            Console.WriteLine(Ansi.Yellow("! package.json is not valid JSON; leaving it untouched."));
            return;
        }
        if (root is not JsonObject obj)
        {
            Console.WriteLine(Ansi.Yellow("! package.json root is not an object; leaving it untouched."));
            return;
        }

        var scripts = obj["scripts"] as JsonObject;
        if (scripts is null) { scripts = new JsonObject(); obj["scripts"] = scripts; }
        if (scripts["build:css"] is null)
            scripts["build:css"] = "tailwindcss -i Styles/input.css -o wwwroot/css/app.css --minify";
        if (scripts["watch:css"] is null)
            scripts["watch:css"] = "tailwindcss -i Styles/input.css -o wwwroot/css/app.css --watch";

        var devDeps = obj["devDependencies"] as JsonObject;
        if (devDeps is null) { devDeps = new JsonObject(); obj["devDependencies"] = devDeps; }
        if (devDeps["@tailwindcss/cli"] is null)
            devDeps["@tailwindcss/cli"] = "^4.0.0";

        File.WriteAllText(pkgPath, obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void PrintSkipReminder()
    {
        Console.WriteLine();
        Console.WriteLine(Ansi.Yellow("! Skipped CSS setup. Your vendored components will render without styling"));
        Console.WriteLine(Ansi.Yellow("  until you import Lumeo's CSS or wire up Tailwind yourself. See:"));
        Console.WriteLine(Ansi.Dim("  https://lumeo.nativ.sh/docs/introduction#step-5-include-styles-and-scripts"));
    }

    // ------------------------------------------------------ update-assets
    public static async Task UpdateAssets(bool local, bool force, bool dryRun)
    {
        var cfg = ConfigIO.TryLoad();
        if (cfg is null)
        {
            Console.Error.WriteLine(Ansi.Red("No lumeo.json found. Run `lumeo init` first."));
            Environment.ExitCode = 1;
            return;
        }

        var mode = cfg.Assets?.Mode ?? "none";
        if (!string.Equals(mode, "prebuilt", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(Ansi.Yellow($"assets.mode is '{mode}'. `update-assets` only applies to 'prebuilt' — nothing to do."));
            if (string.Equals(mode, "tailwind", StringComparison.OrdinalIgnoreCase))
                Console.WriteLine(Ansi.Dim("  (Tailwind users rebuild their own bundle via `npm run build:css`.)"));
            return;
        }

        int updated = 0, skipped = 0, unchanged = 0;
        foreach (var (src, dest) in s_prebuiltAssets)
        {
            var abs = System.IO.Path.Combine(Environment.CurrentDirectory, dest.Replace('/', System.IO.Path.DirectorySeparatorChar));
            byte[] upstream;
            try
            {
                upstream = await RegistryLoader.GetAssetBytesAsync(src, local, cfg.Registry);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(Ansi.Red($"  fail    {dest} — {ex.Message}"));
                Environment.ExitCode = 1;
                continue;
            }

            if (!File.Exists(abs))
            {
                if (dryRun)
                {
                    Console.WriteLine(Ansi.Green("  new     ") + dest + Ansi.Dim("  (dry-run)"));
                }
                else
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(abs)!);
                    await File.WriteAllBytesAsync(abs, upstream);
                    Console.WriteLine(Ansi.Green("  new     ") + dest);
                }
                updated++;
                continue;
            }

            var localBytes = await File.ReadAllBytesAsync(abs);
            if (BytesEqual(localBytes, upstream))
            {
                Console.WriteLine(Ansi.Green("  ok      ") + dest);
                unchanged++;
                continue;
            }

            if (force)
            {
                if (!dryRun) await File.WriteAllBytesAsync(abs, upstream);
                Console.WriteLine(Ansi.Green("  update  ") + dest + (dryRun ? Ansi.Dim("  (dry-run)") : ""));
                updated++;
                continue;
            }
            if (dryRun)
            {
                Console.WriteLine(Ansi.Magenta("  drift   ") + dest + Ansi.Dim("  (dry-run; would prompt)"));
                continue;
            }

            char? choice = null;
            while (choice is null)
            {
                Console.WriteLine();
                Console.WriteLine($"{Ansi.Yellow(dest)} has upstream changes.");
                choice = Prompts.Choice("  [o]verwrite  [s]kip  [d]iff: ", "osd");
                if (choice == 'd')
                {
                    var localText = SafeText(localBytes);
                    var upstreamText = SafeText(upstream);
                    Diffing.PrintUnified(localText, upstreamText);
                    choice = null;
                    continue;
                }
                if (choice is null) { Console.WriteLine(Ansi.Red("Aborted.")); Environment.ExitCode = 130; return; }
            }
            if (choice == 's')
            {
                Console.WriteLine(Ansi.Yellow("  skip    ") + dest);
                skipped++;
                continue;
            }
            await File.WriteAllBytesAsync(abs, upstream);
            Console.WriteLine(Ansi.Green("  update  ") + dest);
            updated++;
        }

        // Refresh the recorded version/files.
        cfg.Assets!.Version = "2.0.0";
        cfg.Assets.Files = s_prebuiltAssets.Select(a => a.Dest).ToList();
        if (!dryRun) ConfigIO.Save(cfg);

        Console.WriteLine();
        var prefix = dryRun ? Ansi.Yellow("DRY-RUN") : Ansi.Green("OK");
        Console.WriteLine($"{prefix} {updated} updated, {skipped} skipped, {unchanged} unchanged.");
    }

    private static bool BytesEqual(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    private static string SafeText(byte[] bytes)
    {
        try { return Encoding.UTF8.GetString(bytes); }
        catch { return "(binary)"; }
    }

    // ----------------------------------------------------------------- add
    public static async Task Add(string component, bool local, bool yes, bool force, bool skipExisting, bool overwrite, bool dryRun)
    {
        var cfg = ConfigIO.TryLoad();
        if (cfg is null)
        {
            Console.Error.WriteLine(Ansi.Red("No lumeo.json found. Run `lumeo init` first."));
            Environment.ExitCode = 1;
            return;
        }

        var registry = await RegistryLoader.LoadAsync(local, cfg.Registry);
        var key = component.ToLowerInvariant();
        if (!registry.Components.TryGetValue(key, out var entry))
        {
            Console.Error.WriteLine(Ansi.Red($"Unknown component '{component}'."));
            Console.Error.WriteLine($"Run {Ansi.Cyan("lumeo list")} to see available components.");
            Environment.ExitCode = 1;
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
                if (registry.Components.TryGetValue(dep, out var depEntry)) queue.Enqueue(depEntry);
        }

        if (toInstall.Count > 1 && !yes && !force && !dryRun && Prompts.Interactive)
        {
            Console.WriteLine($"{Ansi.Bold(entry.Name)} depends on:");
            foreach (var d in toInstall.Skip(1)) Console.WriteLine($"  - {d.Name}");
            if (!Prompts.Confirm("Install all?", defaultYes: true))
                toInstall = new List<RegistryEntry> { entry };
        }

        var outRoot = Path.Combine(Environment.CurrentDirectory, cfg.ComponentsPath);
        if (!dryRun) Directory.CreateDirectory(outRoot);

        var forceAll = force || overwrite;
        var skipAll = skipExisting;
        int written = 0, skipped = 0;

        foreach (var item in toInstall)
        {
            var folder = Path.Combine(outRoot, item.Name);
            if (!dryRun) Directory.CreateDirectory(folder);
            var recordedFiles = new List<string>();

            foreach (var relFile in item.Files)
            {
                var dest = Paths.ToDestPath(outRoot, relFile);
                var displayPath = Path.GetRelativePath(Environment.CurrentDirectory, dest);
                recordedFiles.Add(relFile);

                if (File.Exists(dest))
                {
                    if (skipAll || (!forceAll && dryRun))
                    {
                        Console.WriteLine(Ansi.Yellow("  skip ") + displayPath + (dryRun ? Ansi.Dim("  (dry-run; would prompt)") : ""));
                        skipped++;
                        continue;
                    }
                    if (!forceAll)
                    {
                        var upstream = await RegistryLoader.GetFileAsync(relFile, local, cfg.Registry);
                        upstream = NamespaceRewriter.Rewrite(upstream, relFile, cfg.Namespace);

                        char? choice = null;
                        while (choice is null)
                        {
                            Console.WriteLine();
                            Console.WriteLine($"{Ansi.Yellow(displayPath)} already exists.");
                            choice = Prompts.Choice(
                                "  [o]verwrite  [s]kip  [d]iff  [a]ll-overwrite  [A]bort: ",
                                "osda");
                            if (choice == 'd')
                            {
                                var localContent = await File.ReadAllTextAsync(dest);
                                Diffing.PrintUnified(localContent, upstream);
                                choice = null;
                                continue;
                            }
                            if (choice == 'A') { Console.WriteLine(Ansi.Red("Aborted.")); Environment.ExitCode = 130; return; }
                            if (choice is null) { Console.WriteLine(Ansi.Red("Aborted.")); Environment.ExitCode = 130; return; }
                        }
                        if (choice == 's') { Console.WriteLine(Ansi.Yellow("  skip ") + displayPath); skipped++; continue; }
                        if (choice == 'a') { forceAll = true; }
                        if (!dryRun)
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                            await File.WriteAllTextAsync(dest, upstream, new UTF8Encoding(false));
                        }
                        Console.WriteLine(Ansi.Green("  +   ") + displayPath + (dryRun ? Ansi.Dim("  (dry-run)") : ""));
                        written++;
                        continue;
                    }
                }

                if (dryRun)
                {
                    Console.WriteLine(Ansi.Green("  +   ") + displayPath + Ansi.Dim("  (dry-run)"));
                    written++;
                    continue;
                }
                var content = await RegistryLoader.GetFileAsync(relFile, local, cfg.Registry);
                content = NamespaceRewriter.Rewrite(content, relFile, cfg.Namespace);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                await File.WriteAllTextAsync(dest, content, new UTF8Encoding(false));
                Console.WriteLine(Ansi.Green("  +   ") + displayPath);
                written++;
            }

            if (!dryRun)
            {
                var installKey = ToKebab(item.Name);
                ConfigIO.RecordInstall(cfg, installKey, registry.Version, recordedFiles);
            }
        }

        Console.WriteLine();
        var label = toInstall.Count == 1 ? entry.Name : $"{entry.Name} (+{toInstall.Count - 1} dep{(toInstall.Count - 1 == 1 ? "" : "s")})";
        var prefix = dryRun ? Ansi.Yellow("DRY-RUN") : Ansi.Green("OK");
        var tail = skipped > 0 ? $", {skipped} skipped" : "";
        Console.WriteLine($"{prefix} Added {Ansi.Bold(label)} — {written} file{(written == 1 ? "" : "s")} written{tail} to {cfg.ComponentsPath}/");
        if (entry.CssVars.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine(Ansi.Dim("CSS variables used:"));
            Console.WriteLine("  " + string.Join(", ", entry.CssVars));
        }
    }

    // -------------------------------------------------------------- update
    public static async Task Update(string? component, bool local, bool force, bool check, bool forceReset, bool dryRun)
    {
        var cfg = ConfigIO.TryLoad();
        if (cfg is null)
        {
            Console.Error.WriteLine(Ansi.Red("No lumeo.json found. Run `lumeo init` first."));
            Environment.ExitCode = 1;
            return;
        }

        var registry = await RegistryLoader.LoadAsync(local, cfg.Registry);
        var outRoot = Path.Combine(Environment.CurrentDirectory, cfg.ComponentsPath);

        List<string> targets;
        if (!string.IsNullOrEmpty(component))
        {
            var key = component.ToLowerInvariant();
            if (!registry.Components.ContainsKey(key))
            {
                Console.Error.WriteLine(Ansi.Red($"Unknown component '{component}'."));
                Environment.ExitCode = 1;
                return;
            }
            targets = new List<string> { key };
        }
        else
        {
            targets = InstalledKeys(cfg, outRoot, registry).ToList();
            if (targets.Count == 0)
            {
                Console.WriteLine(Ansi.Yellow("No installed components detected. Nothing to update."));
                return;
            }
        }

        int driftTotal = 0, updatedTotal = 0, skippedTotal = 0;
        foreach (var key in targets)
        {
            if (!registry.Components.TryGetValue(key, out var entry)) continue;
            Console.WriteLine(Ansi.Bold(entry.Name));
            foreach (var relFile in entry.Files)
            {
                var dest = Paths.ToDestPath(outRoot, relFile);
                var displayPath = Path.GetRelativePath(Environment.CurrentDirectory, dest);

                if (!File.Exists(dest))
                {
                    Console.WriteLine(Ansi.Yellow("  missing ") + displayPath);
                    continue;
                }

                var localContent = await File.ReadAllTextAsync(dest);
                var upstream = await RegistryLoader.GetFileAsync(relFile, local, cfg.Registry);
                upstream = NamespaceRewriter.Rewrite(upstream, relFile, cfg.Namespace);

                if (Diffing.Normalize(localContent) == Diffing.Normalize(upstream))
                {
                    Console.WriteLine(Ansi.Green("  ok      ") + displayPath);
                    continue;
                }

                driftTotal++;

                if (check)
                {
                    Console.WriteLine(Ansi.Magenta("  drift   ") + displayPath);
                    continue;
                }

                if (force || forceReset)
                {
                    if (!dryRun) await File.WriteAllTextAsync(dest, upstream, new UTF8Encoding(false));
                    Console.WriteLine(Ansi.Green("  update  ") + displayPath + (dryRun ? Ansi.Dim("  (dry-run)") : ""));
                    updatedTotal++;
                    continue;
                }

                if (dryRun)
                {
                    Console.WriteLine(Ansi.Magenta("  drift   ") + displayPath + Ansi.Dim("  (dry-run; would prompt)"));
                    continue;
                }

                char? choice = null;
                while (choice is null)
                {
                    Console.WriteLine();
                    Console.WriteLine($"{Ansi.Yellow(displayPath)} has upstream changes.");
                    choice = Prompts.Choice("  [u]pdate  [s]kip  [d]iff: ", "usd");
                    if (choice == 'd') { Diffing.PrintUnified(localContent, upstream); choice = null; continue; }
                    if (choice is null) { Console.WriteLine(Ansi.Red("Aborted.")); Environment.ExitCode = 130; return; }
                }
                if (choice == 's') { Console.WriteLine(Ansi.Yellow("  skip    ") + displayPath); skippedTotal++; continue; }
                await File.WriteAllTextAsync(dest, upstream, new UTF8Encoding(false));
                Console.WriteLine(Ansi.Green("  update  ") + displayPath);
                updatedTotal++;
            }

            if (!check && !dryRun && registry.Components.TryGetValue(key, out var e2))
                ConfigIO.RecordInstall(cfg, key, registry.Version, e2.Files);
        }

        Console.WriteLine();
        if (check)
        {
            Console.WriteLine($"{Ansi.Bold("Drift")}: {driftTotal} file(s) differ from registry.");
            Environment.ExitCode = driftTotal > 0 ? 1 : 0;
        }
        else
        {
            var prefix = dryRun ? Ansi.Yellow("DRY-RUN") : Ansi.Green("OK");
            Console.WriteLine($"{prefix} {updatedTotal} updated, {skippedTotal} skipped, {driftTotal - updatedTotal - skippedTotal} unchanged drift remaining.");
        }
    }

    // -------------------------------------------------------------- remove
    public static async Task Remove(string component, bool force, bool dryRun)
    {
        var cfg = ConfigIO.TryLoad();
        if (cfg is null)
        {
            Console.Error.WriteLine(Ansi.Red("No lumeo.json found. Run `lumeo init` first."));
            Environment.ExitCode = 1;
            return;
        }

        var key = component.ToLowerInvariant();
        var outRoot = Path.Combine(Environment.CurrentDirectory, cfg.ComponentsPath);

        List<string> files;
        string displayName;
        if (cfg.Components is not null && cfg.Components.TryGetValue(key, out var installed))
        {
            files = installed.Files;
            displayName = component;
        }
        else
        {
            Registry registry;
            try { registry = await RegistryLoader.LoadAsync(false, cfg.Registry); }
            catch { registry = new Registry(); }
            if (!registry.Components.TryGetValue(key, out var entry))
            {
                Console.Error.WriteLine(Ansi.Red($"Component '{component}' is not installed and not in the registry."));
                Environment.ExitCode = 1;
                return;
            }
            files = entry.Files;
            displayName = entry.Name;

            var dependents = registry.Components
                .Where(kv => kv.Key != key && kv.Value.Dependencies.Contains(key, StringComparer.OrdinalIgnoreCase))
                .Select(kv => kv.Key)
                .Where(k => cfg.Components?.ContainsKey(k) == true)
                .ToList();
            if (dependents.Count > 0)
            {
                Console.WriteLine(Ansi.Yellow($"Warning: these installed components depend on '{component}':"));
                foreach (var d in dependents) Console.WriteLine($"  - {d}");
            }
        }

        var presentFiles = files
            .Select(rel => Paths.ToDestPath(outRoot, rel))
            .Where(File.Exists)
            .ToList();

        if (presentFiles.Count == 0)
        {
            Console.WriteLine(Ansi.Yellow($"Nothing to remove for '{component}'."));
            ConfigIO.RecordRemove(cfg, key);
            return;
        }

        if (!force && !dryRun)
        {
            Console.WriteLine($"This will delete {presentFiles.Count} file(s) from {cfg.ComponentsPath}/{displayName}/:");
            foreach (var f in presentFiles) Console.WriteLine($"  - {Path.GetRelativePath(Environment.CurrentDirectory, f)}");
            if (!Prompts.Confirm("Continue?", defaultYes: false))
            {
                Console.WriteLine(Ansi.Yellow("Aborted."));
                return;
            }
        }

        int deleted = 0;
        foreach (var abs in presentFiles)
        {
            var rel = Path.GetRelativePath(Environment.CurrentDirectory, abs);
            if (dryRun) { Console.WriteLine(Ansi.Red("  -   ") + rel + Ansi.Dim("  (dry-run)")); deleted++; continue; }
            try
            {
                File.Delete(abs);
                Console.WriteLine(Ansi.Red("  -   ") + rel);
                deleted++;
            }
            catch (Exception ex)
            {
                Console.WriteLine(Ansi.Red("  !   ") + rel + " — " + ex.Message);
            }
        }

        // Clean up empty directories under outRoot.
        if (!dryRun && Directory.Exists(outRoot))
        {
            foreach (var dir in Directory.GetDirectories(outRoot, "*", SearchOption.AllDirectories)
                         .OrderByDescending(d => d.Length))
            {
                try
                {
                    if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0)
                        Directory.Delete(dir);
                }
                catch { /* ignore */ }
            }
        }

        if (!dryRun) ConfigIO.RecordRemove(cfg, key);
        Console.WriteLine();
        var prefix = dryRun ? Ansi.Yellow("DRY-RUN") : Ansi.Green("OK");
        Console.WriteLine($"{prefix} Removed {Ansi.Bold(displayName)} — {deleted} file(s).");
    }

    // ---------------------------------------------------------------- list
    public static async Task List(bool local, string? category, bool json)
    {
        var cfg = ConfigIO.TryLoad();
        var registryUrl = cfg?.Registry ?? RegistryLoader.DefaultRegistryUrl;
        var registry = await RegistryLoader.LoadAsync(local, registryUrl);

        var filtered = registry.Components
            .Where(kv => string.IsNullOrEmpty(category) ||
                         string.Equals(kv.Value.Category, category, StringComparison.OrdinalIgnoreCase))
            .OrderBy(kv => kv.Value.Category, StringComparer.Ordinal)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .ToList();

        if (json)
        {
            var payload = new
            {
                version = registry.Version,
                count = filtered.Count,
                components = filtered.Select(kv => new
                {
                    key = kv.Key,
                    name = kv.Value.Name,
                    category = kv.Value.Category,
                    description = kv.Value.Description,
                    files = kv.Value.Files,
                    dependencies = kv.Value.Dependencies,
                    cssVars = kv.Value.CssVars,
                }),
            };
            Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }

        if (filtered.Count == 0)
        {
            Console.WriteLine(Ansi.Yellow($"No components matched category '{category}'."));
            var cats = registry.Components.Values.Select(v => v.Category).Distinct().OrderBy(s => s);
            Console.WriteLine(Ansi.Dim("Available: ") + string.Join(", ", cats));
            return;
        }

        Console.WriteLine();
        var header = category is null
            ? $"Lumeo registry v{registry.Version} — {registry.Components.Count} components"
            : $"Lumeo registry v{registry.Version} — {filtered.Count} in '{category}'";
        Console.WriteLine(Ansi.Bold(header));
        Console.WriteLine();

        int pad = Math.Max(14, filtered.Max(kv => kv.Key.Length) + 2);

        foreach (var g in filtered.GroupBy(kv => kv.Value.Category))
        {
            Console.WriteLine(Ansi.Cyan(Ansi.Bold(g.Key)));
            foreach (var kv in g)
            {
                var name = kv.Key.PadRight(pad);
                Console.WriteLine($"  {Ansi.Green(name)} {Ansi.Dim(kv.Value.Description)}");
            }
            Console.WriteLine();
        }
        Console.WriteLine($"Add one with: {Ansi.Cyan("lumeo add <name>")}");
    }

    // ---------------------------------------------------------------- diff
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
            var dest = Paths.ToDestPath(root, relFile);
            if (!File.Exists(dest))
            {
                Console.WriteLine(Ansi.Yellow("  missing ") + Path.GetRelativePath(Environment.CurrentDirectory, dest));
                missing++;
                continue;
            }
            var local0 = await File.ReadAllTextAsync(dest);
            var upstream = await RegistryLoader.GetFileAsync(relFile, local, cfg.Registry);
            upstream = NamespaceRewriter.Rewrite(upstream, relFile, cfg.Namespace);

            if (Diffing.Normalize(local0) == Diffing.Normalize(upstream)) same++;
            else
            {
                drift++;
                Console.WriteLine(Ansi.Magenta("  drift   ") + Path.GetRelativePath(Environment.CurrentDirectory, dest));
            }
        }

        Console.WriteLine();
        Console.WriteLine($"{Ansi.Bold(entry.Name)}: {Ansi.Green($"{same} in sync")}, {Ansi.Magenta($"{drift} drifted")}, {Ansi.Yellow($"{missing} missing")}.");
        if (drift > 0)
            Console.WriteLine($"Run {Ansi.Cyan($"lumeo update {component}")} to pull upstream, or {Ansi.Cyan($"lumeo add {component} --force")} to reset.");
    }

    // ------------------------------------------------------------- helpers

    /// <summary>Installed component keys from config manifest, or inferred by scanning componentsPath.</summary>
    private static IEnumerable<string> InstalledKeys(LumeoConfig cfg, string outRoot, Registry registry)
    {
        if (cfg.Components is not null && cfg.Components.Count > 0)
            return cfg.Components.Keys.Where(k => registry.Components.ContainsKey(k));

        if (!Directory.Exists(outRoot)) return Enumerable.Empty<string>();
        var keys = new List<string>();
        foreach (var kv in registry.Components)
        {
            foreach (var relFile in kv.Value.Files)
            {
                var dest = Paths.ToDestPath(outRoot, relFile);
                if (File.Exists(dest)) { keys.Add(kv.Key); break; }
            }
        }
        return keys;
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

    private static string BuildReadme(string ns, string path) => $@"# Lumeo components (vendored)

Components under `{path}/` are copies of [Lumeo](https://lumeo.nativ.sh) primitives, rewritten to live in the `{ns}` namespace. You own these files — edit them freely.

## Adding a component

```bash
lumeo add button
lumeo add dialog
```

## Updating

```bash
lumeo diff button        # see what has drifted
lumeo update button      # pull upstream (prompts per file)
lumeo update --check     # CI: exit 1 if any drift
```

## Removing

```bash
lumeo remove button      # deletes the vendored folder
```

## Listing

```bash
lumeo list
lumeo list --category Forms
lumeo list --json
```

Services (ToastService, OverlayService, ThemeService, ...) still come from the
`Lumeo` NuGet package. Only presentation components can be vendored here.
";
}
