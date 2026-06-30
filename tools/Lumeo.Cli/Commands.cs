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
        bool Local,
        bool Standalone = false);

    public sealed record AddOptions(
        string? Component,
        bool Local,
        bool Yes,
        bool Force,
        bool SkipExisting,
        bool Overwrite,
        bool DryRun,
        bool All,
        bool Diff,
        bool View,
        bool Vendor = false);

    // Files copied in prebuilt asset mode. Kept here so `init` and `update-assets` stay in sync.
    private static readonly (string Src, string Dest)[] s_prebuiltAssets =
    {
        ("_content/Lumeo/css/lumeo.css",           "wwwroot/css/lumeo.css"),
        ("_content/Lumeo/css/lumeo-utilities.css", "wwwroot/css/lumeo-utilities.css"),
        ("_content/Lumeo/js/theme.js",             "wwwroot/js/theme.js"),
        ("_content/Lumeo/js/components.js",        "wwwroot/js/components.js"),
    };

    // EVERY core runtime asset a standalone (NuGet-free) build needs mirrored under
    // wwwroot/_content/Lumeo/ — the verbatim-vendored interop dynamically imports these by their
    // `_content/Lumeo/…` URL (ComponentInteropService → js/{components,toolbar,signature-pad}.js,
    // theme.js for the FOUC guard, the two CSS files for tokens/utilities). Superset of
    // s_prebuiltAssets (which is only the host-wired subset). Keep in sync with src/Lumeo/wwwroot/{js,css}.
    private static readonly string[] s_standaloneCoreAssets =
    {
        "_content/Lumeo/js/components.js",
        "_content/Lumeo/js/theme.js",
        "_content/Lumeo/js/signature-pad.js",
        "_content/Lumeo/js/toolbar.js",
        "_content/Lumeo/css/lumeo.css",
        "_content/Lumeo/css/lumeo-utilities.css",
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
            if (opts.WithCss) mode = '1';
            else if (opts.WithTailwind) mode = '2';
            else if (opts.NoAssets) mode = '3';
            else if (opts.Yes) mode = '1'; // safest CI default
            else mode = PromptAssetSetup();

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
            Standalone = opts.Standalone,
            Components = new Dictionary<string, InstalledComponent>(StringComparer.OrdinalIgnoreCase),
        };
        ConfigIO.Save(cfg);

        var uiDir = System.IO.Path.Combine(Environment.CurrentDirectory, path);
        Directory.CreateDirectory(uiDir);
        var gitkeep = System.IO.Path.Combine(uiDir, ".gitkeep");
        if (!File.Exists(gitkeep)) File.WriteAllText(gitkeep, "");
        var readme = System.IO.Path.Combine(uiDir, "README.md");
        if (!File.Exists(readme)) File.WriteAllText(readme, BuildReadme(ns, path));

        // NOTE: the standalone root @using bridge (@using Lumeo, Lumeo.Internal, Lumeo.Services, …) is
        // deliberately NOT written here. Those namespaces do not exist until the runtime is vendored, so
        // emitting them into _Imports.razor at init time would make a bare `init --standalone` project fail
        // Razor compilation before any component is added (Codex P2). The imports are written by the first
        // `lumeo add` (after it vendors the runtime) and by `eject` — i.e. only once the namespaces they
        // reference actually exist on disk. See the EnsureStandaloneImportsAsync call sites.

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

    // Ensures the PROJECT-ROOT _Imports.razor carries every framework / Blazicons / Lumeo using the
    // standalone-vendored .razor components (and the app pages that use them) rely on. Appends only the
    // lines actually MISSING — matched WHOLE-LINE, so an existing `@using Lumeo.Components` does not mask
    // the still-required `@using Lumeo`, `@using Blazicons`, `@using Lumeo.Services`, … Idempotent; shared
    // by `init --standalone` and `eject`.
    private static async Task EnsureStandaloneImportsAsync()
    {
        string[] usings =
        {
            "@using Microsoft.AspNetCore.Components",
            "@using Microsoft.AspNetCore.Components.Forms",
            "@using Microsoft.AspNetCore.Components.Web",
            "@using Microsoft.AspNetCore.Components.Web.Virtualization",
            "@using Microsoft.JSInterop",
            "@using Blazicons",
            "@using Lumeo",
            "@using Lumeo.Internal",
            "@using Lumeo.Services",
            "@using Lumeo.Services.Localization",
        };
        var importsPath = Path.Combine(Environment.CurrentDirectory, "_Imports.razor");
        var existing = File.Exists(importsPath) ? await File.ReadAllTextAsync(importsPath) : "";
        var present = new HashSet<string>(
            existing.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()), StringComparer.Ordinal);
        var missing = usings.Where(u => !present.Contains(u)).ToList();
        if (missing.Count == 0) return;
        var prefix = existing.Length == 0 ? "" : existing.TrimEnd() + "\n";
        await File.WriteAllTextAsync(importsPath, prefix + string.Join("\n", missing) + "\n", new UTF8Encoding(false));
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

    /// <summary>Walk up from cwd looking for .csproj files. Scan each for the specified NuGet package ID.
    /// Returns the path of the first .csproj that references it, or null if none found.</summary>
    private static string? FindCsprojReferencingPackage(string cwd, string packageId)
    {
        try
        {
            var dir = new DirectoryInfo(cwd);
            for (int depth = 0; dir is not null && depth < 6; depth++, dir = dir.Parent)
            {
                foreach (var csproj in Directory.EnumerateFiles(dir.FullName, "*.csproj", SearchOption.TopDirectoryOnly))
                {
                    if (CsprojContainsPackageReference(csproj, packageId)) return csproj;
                }
                if (Directory.Exists(System.IO.Path.Combine(dir.FullName, ".git"))) break;
            }
        }
        catch { /* best-effort */ }
        return null;
    }

    private static bool CsprojContainsPackageReference(string csprojPath, string packageId)
    {
        try
        {
            var doc = XDocument.Load(csprojPath);
            foreach (var pr in doc.Descendants("PackageReference"))
            {
                var include = pr.Attribute("Include")?.Value?.Trim();
                if (string.Equals(include, packageId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            // Also check ProjectReference for monorepo setups (e.g. "Lumeo.Charts.csproj").
            foreach (var pr in doc.Descendants("ProjectReference"))
            {
                var include = pr.Attribute("Include")?.Value;
                if (string.IsNullOrEmpty(include)) continue;
                var fileName = System.IO.Path.GetFileNameWithoutExtension(include);
                if (string.Equals(fileName, packageId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { /* unreadable csproj */ }
        return false;
    }

    /// <summary>Find the first .csproj in cwd (or ancestor up to git root) — used as the
    /// target project for `dotnet add package`.</summary>
    private static string? FindConsumerCsproj(string cwd)
    {
        try
        {
            var dir = new DirectoryInfo(cwd);
            for (int depth = 0; dir is not null && depth < 6; depth++, dir = dir.Parent)
            {
                var found = Directory.EnumerateFiles(dir.FullName, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (found is not null) return found;
                if (Directory.Exists(System.IO.Path.Combine(dir.FullName, ".git"))) break;
            }
        }
        catch { /* best-effort */ }
        return null;
    }

    /// <summary>Ensure a NuGet package is referenced by the consumer project, running
    /// <c>dotnet add package</c> (with a prompt unless --yes/--force) if it isn't.
    /// Used both for satellite packages and for a component's packageDependencies
    /// (e.g. Blazicons.Lucide) — without the latter, vendored .razor won't compile.</summary>
    private static async Task EnsureNuGetPackageAsync(string pkg, string reason, AddOptions opts)
    {
        if (string.IsNullOrWhiteSpace(pkg)) return;
        if (FindCsprojReferencingPackage(Environment.CurrentDirectory, pkg) is not null) return;

        if (opts.DryRun)
        {
            Console.WriteLine(Ansi.Yellow($"  dry-run  would install NuGet package {pkg}"));
            return;
        }

        bool doInstall;
        if (opts.Yes || opts.Force)
        {
            doInstall = true;
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine(Ansi.Yellow(reason));
            doInstall = Prompts.Confirm($"Install {pkg}?", defaultYes: true);
        }

        if (!doInstall)
        {
            Console.WriteLine(Ansi.Yellow($"Skipped {pkg}. The component may not compile/render without it."));
            return;
        }

        var csprojPath = FindConsumerCsproj(Environment.CurrentDirectory);
        if (csprojPath is null)
        {
            // No consumer project to add the reference to — e.g. `lumeo add --vendor`
            // run in a bare directory. `dotnet add package` would only fail with
            // "Could not find any project", which is not an install failure to abort
            // on; point the user at the manual step instead of flipping the exit code.
            Console.WriteLine(Ansi.Yellow($"  !   No project found here — add the {pkg} NuGet package to your project manually."));
            return;
        }
        var dotnetArgs = $"add \"{csprojPath}\" package {pkg}";

        Console.WriteLine(Ansi.Dim($"  $ dotnet {dotnetArgs}"));
        var psi = new System.Diagnostics.ProcessStartInfo("dotnet", dotnetArgs)
        {
            RedirectStandardOutput = false,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        var proc = System.Diagnostics.Process.Start(psi);
        if (proc is null) return;
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
        {
            Console.Error.WriteLine(Ansi.Red($"dotnet add package {pkg} failed (exit {proc.ExitCode})."));
            if (!string.IsNullOrWhiteSpace(stderr))
                Console.Error.WriteLine(Ansi.Red(stderr.Trim()));
            Environment.ExitCode = 1;
        }
        else
        {
            Console.WriteLine(Ansi.Green($"  +   {pkg} added to project."));
        }
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

    // Asset version recorded in lumeo.json = the CLI's own assembly version, which is
    // lockstep with the Lumeo library (Directory.Build.props drives both, and the CI
    // publish job overrides it via /p:Version) — so it always reflects the shipped lib
    // version the prebuilt assets came from, instead of a hand-edited literal that drifts.
    private static readonly string s_assetVersion =
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version is { } v
            ? $"{v.Major}.{v.Minor}.{v.Build}"
            : "0.0.0";

    private const string RuntimeFolder = "_LumeoRuntime";
    private const string RuntimeRecordKey = "_lumeo-runtime";

    // In standalone mode the vendored components keep the Lumeo namespace (like the runtime), so they
    // compile without any rewriting: relative references (Services.X), shared cascading types
    // (FormField.FormFieldContext) and inter-component references all bind under Lumeo. Rewriting them
    // to the consumer namespace is exactly what broke those. Normal (NuGet) mode still rebrands.
    private static string MaybeRewrite(string content, string relFile, LumeoConfig cfg)
        => cfg.Standalone ? content : NamespaceRewriter.Rewrite(content, relFile, cfg.Namespace);

    // Vendors the full shared Lumeo runtime (RuntimeManifest.Files) into
    // <componentsPath>/_LumeoRuntime/ VERBATIM — keeping the Lumeo namespace — so the
    // user-namespace components resolve Cx, the injected services and Lumeo.* types against it.
    // Idempotent: skips entirely if already present.
    // Returns true when the standalone runtime is in place (or was already), false when it could not be
    // vendored (no 'runtime' manifest) — callers must NOT proceed to strip the NuGet on a false.
    private static async Task<bool> EnsureRuntimeVendoredAsync(LumeoConfig cfg, Registry registry, AddOptions opts)
    {
        if (registry.Runtime is null || registry.Runtime.Files.Count == 0)
        {
            Console.Error.WriteLine(Ansi.Red(
                "Standalone mode needs a registry with a 'runtime' manifest. Update the CLI/registry, or drop --standalone."));
            Environment.ExitCode = 1;
            return false;
        }
        var runtimeRoot = Path.Combine(Environment.CurrentDirectory, cfg.ComponentsPath, RuntimeFolder);
        // On a registry-version UPGRADE, REFRESH the vendored runtime (+ its assets below) — otherwise an
        // older standalone project keeps compiling/running against a stale Cx/services after the registry
        // moves on. When the recorded runtime version matches, stay self-healing (only fill in MISSING files):
        // (re)copy every missing file rather than trusting one sentinel as proof the whole closure is present.
        var recordedRuntimeVersion = cfg.Components is not null
            && cfg.Components.TryGetValue(RuntimeRecordKey, out var rtRec) ? rtRec.Version : null;
        var refreshRuntime = recordedRuntimeVersion is not null
            && !string.Equals(recordedRuntimeVersion, registry.Version, StringComparison.OrdinalIgnoreCase);
        var newlyWritten = 0;
        foreach (var rel in registry.Runtime.Files)
        {
            var dest = Path.Combine(runtimeRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(dest) && !refreshRuntime) continue;
            var content = await RegistryLoader.GetFileAsync(rel, opts.Local, cfg.Registry);  // sourcePackage defaults to "Lumeo"
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            await File.WriteAllTextAsync(dest, content, new UTF8Encoding(false));   // VERBATIM — no NamespaceRewriter
            newlyWritten++;
        }
        if (newlyWritten > 0)
        {
            Console.WriteLine(Ansi.Green("  +   ") + $"{RuntimeFolder}/ ({newlyWritten} Lumeo runtime files)");
            ConfigIO.RecordInstall(cfg, RuntimeRecordKey, registry.Version,
                registry.Runtime.Files.Select(f => $"{cfg.ComponentsPath}/{RuntimeFolder}/{f}").ToList());
        }

        // The vendored runtime + components import their JS/CSS from `_content/Lumeo/…` (baked into the
        // source verbatim — ComponentInteropService loads _content/Lumeo/js/{components,toolbar,
        // signature-pad}.js, plus theme.js and the two CSS files). With no Lumeo package there is no
        // `_content/Lumeo`, so mirror EVERY core runtime asset under wwwroot/_content/Lumeo/ where those
        // URLs resolve. Idempotent (skips files already present). These are MANDATORY for a NuGet-free
        // build — a fetch failure fails the whole vendor so callers (eject) won't then strip the package.
        foreach (var assetSrc in s_standaloneCoreAssets)
        {
            var assetDest = Path.Combine(Environment.CurrentDirectory, "wwwroot",
                assetSrc.Replace('/', Path.DirectorySeparatorChar));   // assetSrc already begins "_content/Lumeo/…"
            if (File.Exists(assetDest) && !refreshRuntime) continue;
            try
            {
                var bytes = await RegistryLoader.GetAssetBytesAsync(assetSrc, opts.Local, cfg.Registry);
                Directory.CreateDirectory(Path.GetDirectoryName(assetDest)!);
                await File.WriteAllBytesAsync(assetDest, bytes);
                Console.WriteLine(Ansi.Green("  +   ") + $"wwwroot/{assetSrc}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(Ansi.Red("  error: ") +
                    $"couldn't vendor required core asset {assetSrc}: {ex.Message}");
                Environment.ExitCode = 1;
                return false;   // standalone can't run without these — don't report a successful vendor
            }
        }
        return true;
    }

    /// <summary>
    /// Converts an existing (NuGet-referencing) Lumeo project into a fully NuGet-free standalone one:
    /// flips standalone mode on, vendors the shared runtime (the services/Cx that previously came from
    /// the package), re-vendors every installed component as source, and strips the Lumeo/satellite
    /// PackageReference(s) from the consumer .csproj.
    /// </summary>
    public static async Task Eject(bool local)
    {
        var cfg = ConfigIO.TryLoad();
        if (cfg is null)
        {
            Console.Error.WriteLine(Ansi.Red("No lumeo.json found — run 'lumeo init' first."));
            Environment.ExitCode = 1;
            return;
        }
        if (cfg.Standalone)
        {
            Console.WriteLine(Ansi.Yellow("Project is already standalone (NuGet-free). Nothing to eject."));
            return;
        }

        var registry = await RegistryLoader.LoadAsync(local, cfg.Registry);

        var opts = new AddOptions(Component: null, Local: local, Yes: true, Force: true, SkipExisting: false,
            Overwrite: true, DryRun: false, All: false, Diff: false, View: false, Vendor: true);

        // Vendor the runtime FIRST, and only flip to standalone / strip the NuGet if it succeeds.
        // Otherwise (e.g. an older registry with no 'runtime' manifest) leave the project exactly as it
        // was — no half-ejected state with the package gone but no vendored runtime to compile against.
        if (!await EnsureRuntimeVendoredAsync(cfg, registry, opts))
        {
            Console.Error.WriteLine(Ansi.Red(
                "Eject aborted — could not vendor the Lumeo runtime. The project is unchanged; update the CLI/registry and retry."));
            return;
        }
        // Persist standalone mode NOW, before the re-vendor loop: each Add() below reloads lumeo.json from
        // disk, so it must already read standalone:true — otherwise it would NamespaceRewriter the re-vendored
        // source out of the Lumeo namespace and break the build. If anything below aborts, we roll standalone
        // back to false (see the abort paths) so a retry isn't stuck on "already standalone".
        cfg.Standalone = true;
        ConfigIO.Save(cfg);

        // The re-vendored components keep the Lumeo namespace, so the project needs the same root-level
        // @using bridge that `init --standalone` writes — otherwise <Dialog>/<Button> stop resolving the
        // moment the package is gone.
        await EnsureStandaloneImportsAsync();

        // Re-vendor every already-installed component as SOURCE (satellites included), skipping the runtime
        // record. If any re-vendor fails (e.g. a stale lumeo.json key the current registry no longer knows),
        // abort BEFORE stripping the package — never leave the project with missing source and no fallback.
        var installed = cfg.Components?.Keys.Where(k => k != RuntimeRecordKey).ToList() ?? new List<string>();
        foreach (var key in installed)
        {
            Environment.ExitCode = 0;
            await Add(opts with { Component = key });
            if (Environment.ExitCode != 0)
            {
                cfg.Standalone = false;            // roll back so a retry isn't stuck on "already standalone"
                ConfigIO.Save(cfg);
                Console.Error.WriteLine(Ansi.Red(
                    $"Eject aborted while re-vendoring '{key}' — the Lumeo package was NOT removed (standalone rolled back). Resolve the error above (e.g. a stale lumeo.json component) and retry."));
                return;
            }
        }

        // All abortable work succeeded — strip the packages. (standalone was already persisted above; the
        // Add() calls have since re-saved lumeo.json with their component records, so don't re-Save our stale
        // local cfg here or it would clobber those records.)

        // Strip only the Lumeo packages we actually vendored: core (always — the runtime is vendored) plus
        // the satellite package of every re-vendored component. A satellite used via the default NuGet flow
        // (`add <component>` without --vendor) has NO vendored source, so removing its package would leave
        // the project with neither package nor source — keep it and tell the user how to finish.
        var vendoredPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Lumeo" };
        foreach (var key in installed)
            if (registry.Components.TryGetValue(key, out var ce) && !string.IsNullOrEmpty(ce.NugetPackage))
                vendoredPackages.Add(ce.NugetPackage!);

        var (removed, kept) = StripLumeoPackageReferences(vendoredPackages);
        Console.WriteLine();
        Console.WriteLine(Ansi.Green("OK ") + "Ejected — vendored components now build with no Lumeo NuGet reference.");
        if (removed.Count > 0) Console.WriteLine("  Removed PackageReference: " + string.Join(", ", removed.Distinct()));
        Console.WriteLine($"  Runtime vendored to {cfg.ComponentsPath}/{RuntimeFolder}/");
        if (kept.Count > 0)
            Console.WriteLine(Ansi.Yellow("  note: ") +
                $"left these packages in place — their components weren't vendored as source: {string.Join(", ", kept.Distinct())}. " +
                "Run `lumeo add <component> --vendor` for those, then `lumeo eject` again to go fully NuGet-free.");
    }

    // Removes <PackageReference Include="Lumeo[.Satellite]" …> entries from the consumer .csproj —
    // BOTH the self-closing form (`… />`) and the expanded form (`…><Version>4.0.0</Version></PackageReference>`).
    // Removes ONLY packages in `vendoredPackages` (those whose source we actually vendored); any other Lumeo.*
    // reference is left intact and returned in `kept` so the caller can warn. External deps (Blazicons.Lucide,
    // etc.) are never touched.
    private static (List<string> removed, List<string> kept) StripLumeoPackageReferences(HashSet<string> vendoredPackages)
    {
        var removed = new List<string>();
        var kept = new List<string>();
        var csproj = FindConsumerCsproj(Environment.CurrentDirectory);
        if (csproj is null) return (removed, kept);
        var text = File.ReadAllText(csproj);
        var stripped = System.Text.RegularExpressions.Regex.Replace(
            text,
            // `(?:[^>]*?\s)?Include=` allows other attributes (Version=, Condition=, …) BEFORE Include, so
            // `<PackageReference Version="4.0.0" Include="Lumeo" />` is matched too, not only Include-first.
            // `[""']` accepts single- OR double-quoted attribute values (`Include='Lumeo'` is valid XML too).
            @"[ \t]*<PackageReference\s+(?:[^>]*?\s)?Include=[""'](Lumeo(?:\.[A-Za-z0-9.]+)?)[""'][^>]*(?:/>|>[\s\S]*?</PackageReference>)[ \t]*\r?\n?",
            m =>
            {
                var pkg = m.Groups[1].Value;
                if (vendoredPackages.Contains(pkg)) { removed.Add(pkg); return string.Empty; }
                kept.Add(pkg);            // referenced but not vendored — leave the package in place
                return m.Value;
            },
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (stripped != text) File.WriteAllText(csproj, stripped, new UTF8Encoding(false));
        return (removed, kept);
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
            Version = s_assetVersion,
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
        cfg.Assets!.Version = s_assetVersion;
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
    public static async Task Add(AddOptions opts)
    {
        var cfg = ConfigIO.TryLoad();
        if (cfg is null)
        {
            Console.Error.WriteLine(Ansi.Red("No lumeo.json found. Run `lumeo init` first."));
            Environment.ExitCode = 1;
            return;
        }

        var registry = await RegistryLoader.LoadAsync(opts.Local, cfg.Registry);

        // --all: install everything. Skips dependency resolution (since we're doing all of them).
        if (opts.All)
        {
            if (opts.Component is not null)
            {
                Console.Error.WriteLine(Ansi.Yellow("Both 'component' and --all provided; --all takes precedence."));
            }
            var allKeys = registry.Components.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            if (allKeys.Count == 0)
            {
                Console.Error.WriteLine(Ansi.Red("Registry is empty."));
                Environment.ExitCode = 1;
                return;
            }
            if (!opts.Yes && !opts.Force && !opts.DryRun && Prompts.Interactive)
            {
                Console.WriteLine($"About to install {Ansi.Bold(allKeys.Count.ToString())} components from registry v{registry.Version}.");
                if (!Prompts.Confirm("Continue?", defaultYes: false))
                {
                    Console.WriteLine(Ansi.Yellow("Aborted."));
                    return;
                }
            }
            foreach (var k in allKeys)
            {
                var single = opts with { Component = k, All = false };
                await Add(single);
                if (Environment.ExitCode != 0 && Environment.ExitCode != 1) return; // aborted
            }
            return;
        }

        if (string.IsNullOrEmpty(opts.Component))
        {
            Console.Error.WriteLine(Ansi.Red("Missing component name. Usage: lumeo add <component>  (or --all)"));
            Environment.ExitCode = 2;
            return;
        }

        var key = opts.Component.ToLowerInvariant();
        if (!registry.Components.TryGetValue(key, out var entry))
        {
            Console.Error.WriteLine(Ansi.Red($"Unknown component '{opts.Component}'."));
            Console.Error.WriteLine($"Run {Ansi.Cyan("lumeo list")} to see available components.");
            Environment.ExitCode = 1;
            return;
        }

        // Standalone implies vendor-everything: satellites come in as SOURCE, and the shared Lumeo
        // runtime is vendored once into _LumeoRuntime/ (verbatim, Lumeo namespace) so core and
        // satellite source compile with no Lumeo PackageReference.
        var forceVendor = opts.Vendor || cfg.Standalone;
        // --diff / --view without --yes are preview-only — don't vendor the runtime (or its wwwroot assets)
        // to disk just to show a diff. Matches the writeAllowed gate used for component files + sat assets.
        var previewOnly = (opts.Diff || opts.View) && !opts.Yes;
        if (cfg.Standalone && !opts.DryRun && !previewOnly && !await EnsureRuntimeVendoredAsync(cfg, registry, opts)) return;

        // Now that the runtime is vendored (the gate above returned on failure), write the root-level
        // @using bridge so the vendored Lumeo-namespace tree + app pages resolve <Button>, Cx, services, …
        // Done HERE rather than at `init` so the imports only appear once the namespaces they reference
        // exist on disk — a bare `init --standalone` stays compilable (Codex P2). Idempotent.
        if (cfg.Standalone && !opts.DryRun && !previewOnly)
            await EnsureStandaloneImportsAsync();

        // ── Satellite NuGet package check ──────────────────────────────────────
        // If the component lives in a satellite package (nugetPackage != "Lumeo"),
        // ensure the consumer's project already has it — or prompt to install it.
        var satellitePkg = entry.NugetPackage;
        var isSatellite = !string.IsNullOrEmpty(satellitePkg)
            && !string.Equals(satellitePkg, "Lumeo", StringComparison.OrdinalIgnoreCase);
        if (isSatellite && forceVendor)
        {
            // --vendor: copy the satellite's SOURCE (handled by the loop below, which
            // resolves files from src/<package>/) + its JS assets, instead of adding
            // the NuGet package. The underlying JS library still ships via the
            // component's packageDependencies — surfaced after the files are written.
            Console.WriteLine(Ansi.Dim(
                $"  Vendoring {entry.Name} as source from {satellitePkg} (omit --vendor to add the NuGet package instead)."));
            // The DataGrid's Excel/PDF export runs through the compiled Lumeo.DataGrid.Export backend
            // (ClosedXML + QuestPDF). That backend is NOT a standalone NuGet package — it ships bundled
            // inside the Lumeo.DataGrid package and can't be vendored as source. The grid renders/sorts/
            // filters fine without it; only export needs it. Point users at the package that exists.
            if (string.Equals(ToKebab(entry.Name), "data-grid", StringComparison.OrdinalIgnoreCase))
                Console.WriteLine(Ansi.Yellow("  note: ") +
                    "Excel/PDF export uses the compiled Lumeo.DataGrid.Export backend, which ships only inside the " +
                    "Lumeo.DataGrid NuGet and can't be vendored as source. Reference the Lumeo.DataGrid package " +
                    "(add it without --vendor) if you need export — the vendored grid renders without it.");
        }
        else if (isSatellite && !((opts.Diff || opts.View) && !opts.Yes))
        {
            await EnsureNuGetPackageAsync(satellitePkg!,
                $"Component '{entry.Name}' requires the {satellitePkg} NuGet package.", opts);
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
            // Standalone: the runtime already provides the overlay host (under the Lumeo namespace),
            // so don't also vendor it into the user namespace.
            if (cfg.Standalone && registry.Runtime is { } rt && rt.Components.Contains(curKey, StringComparer.OrdinalIgnoreCase)) continue;
            toInstall.Add(cur);
            var curPackage = string.IsNullOrEmpty(cur.NugetPackage) ? "Lumeo" : cur.NugetPackage;
            if (!string.Equals(curPackage, "Lumeo", StringComparison.OrdinalIgnoreCase) && !forceVendor) continue;
            foreach (var dep in cur.Dependencies)
                if (registry.Components.TryGetValue(dep, out var depEntry)) queue.Enqueue(depEntry);
        }

        if (toInstall.Count > 1 && !opts.Yes && !opts.Force && !opts.DryRun && Prompts.Interactive
            && !opts.Diff && !opts.View)
        {
            Console.WriteLine($"{Ansi.Bold(entry.Name)} depends on:");
            foreach (var d in toInstall.Skip(1)) Console.WriteLine($"  - {d.Name}");
            if (!Prompts.Confirm("Install all?", defaultYes: true))
                toInstall = new List<RegistryEntry> { entry };
        }

        var outRoot = Path.Combine(Environment.CurrentDirectory, cfg.ComponentsPath);
        // --diff and --view without --yes are preview-only (no filesystem changes).
        bool writeAllowed = !opts.DryRun && (!(opts.Diff || opts.View) || opts.Yes);
        if (writeAllowed) Directory.CreateDirectory(outRoot);

        var forceAll = opts.Force || opts.Overwrite;
        var skipAll = opts.SkipExisting;
        int written = 0, skipped = 0;
        // Satellite wwwroot assets (echarts-interop.js, …) are shared by every
        // component of a package, so copy them at most once per package.
        var vendoredSatelliteAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in toInstall)
        {
            // Satellites (Charts, DataGrid, …) only get their source copied when
            // --vendor is set; otherwise they were routed to NuGet above, so skip
            // copying their files here. The source package also tells GetFileAsync
            // which src/<package>/ root to read each file from.
            var itemPackage = string.IsNullOrEmpty(item.NugetPackage) ? "Lumeo" : item.NugetPackage;
            var itemIsSatellite = !string.Equals(itemPackage, "Lumeo", StringComparison.OrdinalIgnoreCase);
            if (itemIsSatellite && !forceVendor) continue;

            var folder = Path.Combine(outRoot, item.Name);
            if (writeAllowed) Directory.CreateDirectory(folder);
            var recordedFiles = new List<string>();

            foreach (var relFile in item.Files)
            {
                // Standalone: if the runtime already provides this exact file (e.g.
                // UI/Overlay/DismissEventArgs.cs, which both the Overlay component and the runtime
                // contain), skip it so the consumer doesn't get a duplicate type definition (CS0101).
                if (cfg.Standalone && registry.Runtime is { } rt2 && rt2.Files.Contains(relFile))
                    continue;
                var dest = Paths.ToDestPath(outRoot, relFile);
                var displayPath = Path.GetRelativePath(Environment.CurrentDirectory, dest);
                recordedFiles.Add(relFile);

                // --diff / --view preview modes (no prompts, no writes unless --yes).
                if (opts.Diff || opts.View)
                {
                    var upstream = await RegistryLoader.GetFileAsync(relFile, opts.Local, cfg.Registry, itemPackage);
                    upstream = MaybeRewrite(upstream, relFile, cfg);

                    if (File.Exists(dest))
                    {
                        var localContent = await File.ReadAllTextAsync(dest);
                        if (Diffing.Normalize(localContent) == Diffing.Normalize(upstream))
                        {
                            Console.WriteLine(Ansi.Green("  ok      ") + displayPath);
                        }
                        else if (opts.Diff)
                        {
                            Console.WriteLine(Ansi.Magenta("  diff    ") + displayPath);
                            Diffing.PrintUnified(localContent, upstream);
                        }
                        else
                        {
                            Console.WriteLine(Ansi.Magenta("  drift   ") + displayPath + Ansi.Dim(" (existing file differs — use --diff to view)"));
                        }
                    }
                    else
                    {
                        Console.WriteLine(Ansi.Green("  new     ") + displayPath);
                        if (opts.View)
                        {
                            Console.WriteLine(Ansi.Dim($"=== {displayPath} ==="));
                            Console.WriteLine(upstream);
                            Console.WriteLine();
                        }
                    }

                    if (writeAllowed)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        await File.WriteAllTextAsync(dest, upstream, new UTF8Encoding(false));
                        written++;
                    }
                    continue;
                }

                if (File.Exists(dest))
                {
                    if (skipAll || (!forceAll && opts.DryRun))
                    {
                        Console.WriteLine(Ansi.Yellow("  skip ") + displayPath + (opts.DryRun ? Ansi.Dim("  (dry-run; would prompt)") : ""));
                        skipped++;
                        continue;
                    }
                    if (!forceAll)
                    {
                        var upstream = await RegistryLoader.GetFileAsync(relFile, opts.Local, cfg.Registry, itemPackage);
                        upstream = MaybeRewrite(upstream, relFile, cfg);

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
                        if (!opts.DryRun)
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                            await File.WriteAllTextAsync(dest, upstream, new UTF8Encoding(false));
                        }
                        Console.WriteLine(Ansi.Green("  +   ") + displayPath + (opts.DryRun ? Ansi.Dim("  (dry-run)") : ""));
                        written++;
                        continue;
                    }
                }

                if (opts.DryRun)
                {
                    Console.WriteLine(Ansi.Green("  +   ") + displayPath + Ansi.Dim("  (dry-run)"));
                    written++;
                    continue;
                }
                var content = await RegistryLoader.GetFileAsync(relFile, opts.Local, cfg.Registry, itemPackage);
                content = MaybeRewrite(content, relFile, cfg);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                await File.WriteAllTextAsync(dest, content, new UTF8Encoding(false));
                Console.WriteLine(Ansi.Green("  +   ") + displayPath);
                written++;
            }

            // Vendored satellites need their wwwroot interop assets too: their
            // components import `./_content/<package>/js/*.js`, so copy each asset
            // to wwwroot/_content/<package>/… (served at exactly that URL — no NuGet
            // package, no source rewrite required).
            if (itemIsSatellite && forceVendor && writeAllowed && !opts.DryRun
                && vendoredSatelliteAssets.Add(itemPackage))
            {
                written += await VendorSatelliteAssetsAsync(registry, itemPackage, opts.Local, cfg.Registry);
            }

            if (writeAllowed)
            {
                var installKey = ToKebab(item.Name);
                ConfigIO.RecordInstall(cfg, installKey, registry.Version, recordedFiles);
            }
        }

        // Install the external NuGet packages the VENDORED source references (e.g.
        // Blazicons.Lucide for icons). NuGet-routed satellites (no --vendor) get theirs
        // transitively, so only consider items whose source was actually copied.
        var vendoredItems = toInstall.Where(i =>
        {
            var p = string.IsNullOrEmpty(i.NugetPackage) ? "Lumeo" : i.NugetPackage;
            var sat = !string.Equals(p, "Lumeo", StringComparison.OrdinalIgnoreCase);
            return !(sat && !forceVendor);
        });
        if (writeAllowed)
        {
            foreach (var dep in vendoredItems.SelectMany(i => i.PackageDependencies)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await EnsureNuGetPackageAsync(dep, $"Vendored components reference the {dep} NuGet package.", opts);
            }
        }

        Console.WriteLine();
        var label = toInstall.Count == 1 ? entry.Name : $"{entry.Name} (+{toInstall.Count - 1} dep{(toInstall.Count - 1 == 1 ? "" : "s")})";
        var prefix = opts.DryRun ? Ansi.Yellow("DRY-RUN")
                   : (opts.Diff || opts.View) && !opts.Yes ? Ansi.Yellow("PREVIEW")
                   : Ansi.Green("OK");
        var tail = skipped > 0 ? $", {skipped} skipped" : "";
        Console.WriteLine($"{prefix} Added {Ansi.Bold(label)} — {written} file{(written == 1 ? "" : "s")} written{tail} to {cfg.ComponentsPath}/");
        if (entry.CssVars.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine(Ansi.Dim("CSS variables used:"));
            Console.WriteLine("  " + string.Join(", ", entry.CssVars));
        }
    }

    /// <summary>Resolve a satellite package's wwwroot asset paths (relative to the
    /// package source root, e.g. "wwwroot/js/echarts-interop.js"). Prefers the
    /// registry's <c>satelliteAssets</c> map (works offline AND remote); falls back
    /// to enumerating <c>src/&lt;package&gt;/wwwroot/</c> on disk in --local mode so
    /// vendoring keeps working before the registry has been regenerated.</summary>
    private static List<string> GetSatelliteAssetPaths(Registry registry, string package, bool local)
    {
        if (registry.SatelliteAssets is not null
            && registry.SatelliteAssets.TryGetValue(package, out var fromRegistry)
            && fromRegistry.Count > 0)
        {
            return fromRegistry;
        }

        if (local)
        {
            var repoRoot = Paths.FindRepoRoot(Environment.CurrentDirectory);
            if (repoRoot is not null)
            {
                var wwwroot = Path.Combine(Paths.LocalSourceRoot(repoRoot, package), "wwwroot");
                if (Directory.Exists(wwwroot))
                {
                    return Directory.EnumerateFiles(wwwroot, "*", SearchOption.AllDirectories)
                        .Where(f => !f.EndsWith(".LEGAL.txt", StringComparison.OrdinalIgnoreCase))
                        .Select(f => "wwwroot/" + Path.GetRelativePath(wwwroot, f).Replace('\\', '/'))
                        .OrderBy(p => p, StringComparer.Ordinal)
                        .ToList();
                }
            }
        }

        return new List<string>();
    }

    /// <summary>Copy a vendored satellite's wwwroot interop assets into the consumer's
    /// <c>wwwroot/_content/&lt;package&gt;/…</c> so its <c>./_content/&lt;package&gt;/js/*.js</c>
    /// module imports keep resolving without the NuGet package. Returns the number of
    /// assets written.</summary>
    private static async Task<int> VendorSatelliteAssetsAsync(
        Registry registry, string package, bool local, string registryUrl)
    {
        var assets = GetSatelliteAssetPaths(registry, package, local);
        if (assets.Count == 0)
        {
            Console.WriteLine(Ansi.Yellow(
                $"  ! No wwwroot assets found for {package}; the component's _content/{package}/ imports may 404 until you copy them manually."));
            return 0;
        }

        var written = 0;
        foreach (var assetRel in assets)
        {
            // assetRel = "wwwroot/js/echarts-interop.js" → serve at /_content/<package>/js/...
            var underWwwroot = assetRel.StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase)
                ? assetRel.Substring("wwwroot/".Length)
                : assetRel;
            var destRel = Path.Combine("wwwroot", "_content", package,
                underWwwroot.Replace('/', Path.DirectorySeparatorChar));
            var dest = Path.Combine(Environment.CurrentDirectory, destRel);
            try
            {
                var content = await RegistryLoader.GetFileAsync(assetRel, local, registryUrl, package);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                await File.WriteAllTextAsync(dest, content, new UTF8Encoding(false));
                Console.WriteLine(Ansi.Green("  +   ") + Path.GetRelativePath(Environment.CurrentDirectory, dest));
                written++;
            }
            catch (Exception ex)
            {
                // A satellite component's source imports `_content/<package>/…` for this asset, so a
                // missing asset is a runtime 404, not a cosmetic skip. Signal failure (eject's per-component
                // check then aborts before stripping; a bare `add … --vendor` exits non-zero) rather than
                // reporting success over an app that will 404 at runtime.
                Console.Error.WriteLine(Ansi.Red("  error: ") + $"couldn't vendor required asset {assetRel} for {package}: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }

        Console.WriteLine(Ansi.Dim(
            $"  {package} loads its JS library from a CDN at runtime (override the URL via window.lumeoCdn.*)."));
        return written;
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

        // Standalone projects vendor the Lumeo runtime (Cx, the services, …) as source. `update` is the
        // canonical "pull upstream" command, so refresh that runtime too — EnsureRuntimeVendoredAsync
        // re-copies it when the recorded runtime version differs from the registry's. Skip in check/dry-run.
        if (cfg.Standalone && !check && !dryRun)
        {
            var rtOpts = new AddOptions(Component: null, Local: local, Yes: true, Force: force, SkipExisting: false,
                Overwrite: false, DryRun: false, All: false, Diff: false, View: false, Vendor: true);
            await EnsureRuntimeVendoredAsync(cfg, registry, rtOpts);
        }

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
        var refreshedSatelliteAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in targets)
        {
            if (!registry.Components.TryGetValue(key, out var entry)) continue;
            Console.WriteLine(Ansi.Bold(entry.Name));
            // Satellite components live under src/<package>/, not src/Lumeo/ — resolve the
            // owning package so GetFileAsync fetches from the right root (else 404/crash).
            var entryPackage = string.IsNullOrEmpty(entry.NugetPackage) ? "Lumeo" : entry.NugetPackage;
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
                var upstream = await RegistryLoader.GetFileAsync(relFile, local, cfg.Registry, entryPackage);
                upstream = MaybeRewrite(upstream, relFile, cfg);

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

            // Refresh vendored satellite interop assets (wwwroot/_content/<package>/…) so updated Razor/C#
            // isn't left calling stale JS/CSS. Only when those assets are actually vendored locally
            // (standalone, or an earlier `add --vendor`) — a NuGet-referencing project gets them from the
            // package and must not have them copied in. Once per package.
            if (!check && !dryRun
                && !string.Equals(entryPackage, "Lumeo", StringComparison.OrdinalIgnoreCase)
                && (cfg.Standalone || Directory.Exists(Path.Combine(Environment.CurrentDirectory, "wwwroot", "_content", entryPackage)))
                && refreshedSatelliteAssets.Add(entryPackage))
            {
                await VendorSatelliteAssetsAsync(registry, entryPackage, local, cfg.Registry);
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
            var upstream = await RegistryLoader.GetFileAsync(relFile, local, cfg.Registry,
                string.IsNullOrEmpty(entry.NugetPackage) ? "Lumeo" : entry.NugetPackage);
            upstream = MaybeRewrite(upstream, relFile, cfg);

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

    // ---------------------------------------------------------------- view
    public static async Task View(string component, bool local, string? pathOverride)
    {
        // Config is optional — `view` should work even before `lumeo init`.
        var cfg = ConfigIO.TryLoad();
        var registryUrl = cfg?.Registry ?? RegistryLoader.DefaultRegistryUrl;
        var targetNamespace = cfg?.Namespace ?? "Lumeo";

        var registry = await RegistryLoader.LoadAsync(local, registryUrl);
        var key = component.ToLowerInvariant();
        if (!registry.Components.TryGetValue(key, out var entry))
        {
            Console.Error.WriteLine(Ansi.Red($"Unknown component '{component}'."));
            Console.Error.WriteLine($"Run {Ansi.Cyan("lumeo list")} to see available components.");
            Environment.ExitCode = 1;
            return;
        }

        // Pretty header on stderr so stdout stays grep-friendly.
        Console.Error.WriteLine(Ansi.Bold(entry.Name) + Ansi.Dim($" — {entry.Category}"));
        if (!string.IsNullOrEmpty(entry.Description))
            Console.Error.WriteLine(Ansi.Dim(entry.Description));
        if (entry.Dependencies.Count > 0)
            Console.Error.WriteLine(Ansi.Dim("Depends on: ") + string.Join(", ", entry.Dependencies));
        Console.Error.WriteLine();

        foreach (var relFile in entry.Files)
        {
            // --path overrides the default destination mapping purely for the banner.
            var displayRel = pathOverride is not null
                ? System.IO.Path.Combine(pathOverride, relFile.Substring(relFile.IndexOf('/') + 1)).Replace('\\', '/')
                : relFile;
            Console.WriteLine($"=== {displayRel} ===");
            string content;
            try
            {
                content = await RegistryLoader.GetFileAsync(relFile, local, registryUrl,
                    string.IsNullOrEmpty(entry.NugetPackage) ? "Lumeo" : entry.NugetPackage);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(Ansi.Red($"Failed to fetch {relFile}: {ex.Message}"));
                Environment.ExitCode = 1;
                continue;
            }
            // Only rewrite namespace if a config exists and the project isn't standalone (standalone
            // keeps the Lumeo namespace) — so we show what `add` WOULD produce.
            if (cfg is not null && !cfg.Standalone)
                content = NamespaceRewriter.Rewrite(content, relFile, targetNamespace);
            Console.WriteLine(content);
            Console.WriteLine();
        }
    }

    // ---------------------------------------------------------------- info
    public static Task Info(bool json)
    {
        var cfg = ConfigIO.TryLoad();
        var cwd = Environment.CurrentDirectory;

        var themeJsonPath = System.IO.Path.Combine(cwd, "wwwroot", "lumeo-theme.json");
        Dictionary<string, JsonElement>? themeJson = null;
        if (File.Exists(themeJsonPath))
        {
            try { themeJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(themeJsonPath)); }
            catch { /* malformed — leave null, surfaced as "invalid" in text mode */ }
        }

        if (json)
        {
            var payload = new Dictionary<string, object?>
            {
                ["cwd"] = cwd,
                ["configFound"] = cfg is not null,
                ["configPath"] = System.IO.Path.Combine(cwd, Paths.ConfigFile),
                ["namespace"] = cfg?.Namespace,
                ["componentsPath"] = cfg?.ComponentsPath,
                ["registry"] = cfg?.Registry ?? RegistryLoader.DefaultRegistryUrl,
                ["assets"] = cfg?.Assets,
                ["theme"] = cfg?.Theme,
                ["themeJsonPath"] = File.Exists(themeJsonPath) ? themeJsonPath : null,
                ["themeJson"] = themeJson,
                ["components"] = cfg?.Components,
                ["presetApi"] = LumeoPresetApi.BaseUrl,
            };
            Console.WriteLine(JsonSerializer.Serialize(payload,
                new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never }));
            return Task.CompletedTask;
        }

        Console.WriteLine();
        Console.WriteLine(Ansi.Bold("Lumeo project info"));
        Console.WriteLine();
        Console.WriteLine($"  {Ansi.Dim("cwd".PadRight(18))} {cwd}");
        if (cfg is null)
        {
            Console.WriteLine($"  {Ansi.Dim("config".PadRight(18))} {Ansi.Yellow("not found")} — run `lumeo init` to create one");
            Console.WriteLine($"  {Ansi.Dim("registry".PadRight(18))} {RegistryLoader.DefaultRegistryUrl}");
            Console.WriteLine($"  {Ansi.Dim("preset api".PadRight(18))} {LumeoPresetApi.BaseUrl}");
            return Task.CompletedTask;
        }

        Console.WriteLine($"  {Ansi.Dim("config".PadRight(18))} {System.IO.Path.Combine(cwd, Paths.ConfigFile)}");
        Console.WriteLine($"  {Ansi.Dim("namespace".PadRight(18))} {cfg.Namespace}");
        Console.WriteLine($"  {Ansi.Dim("componentsPath".PadRight(18))} {cfg.ComponentsPath}");
        Console.WriteLine($"  {Ansi.Dim("registry".PadRight(18))} {cfg.Registry}");
        Console.WriteLine($"  {Ansi.Dim("preset api".PadRight(18))} {LumeoPresetApi.BaseUrl}");
        if (cfg.Assets is not null)
            Console.WriteLine($"  {Ansi.Dim("assets.mode".PadRight(18))} {cfg.Assets.Mode}{(cfg.Assets.Version is not null ? $" (v{cfg.Assets.Version})" : "")}");

        Console.WriteLine();
        if (cfg.Theme is not null)
        {
            Console.WriteLine(Ansi.Bold("Theme (from lumeo.json)"));
            ThemeRow("theme", cfg.Theme.Theme);
            ThemeRow("style", cfg.Theme.Style);
            ThemeRow("baseColor", cfg.Theme.BaseColor);
            ThemeRow("radius", cfg.Theme.Radius);
            ThemeRow("font", cfg.Theme.Font);
            ThemeRow("iconLibrary", cfg.Theme.IconLibrary);
            ThemeRow("menuColor", cfg.Theme.MenuColor);
            ThemeRow("menuAccent", cfg.Theme.MenuAccent);
            ThemeRow("dark", cfg.Theme.Dark?.ToString());
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine(Ansi.Dim("No theme recorded. Apply one with `lumeo apply <preset>`."));
            Console.WriteLine();
        }

        if (themeJson is not null)
        {
            Console.WriteLine(Ansi.Bold("wwwroot/lumeo-theme.json") + Ansi.Dim($"  ({themeJsonPath})"));
            foreach (var kv in themeJson)
                Console.WriteLine($"  {Ansi.Dim(kv.Key.PadRight(16))} {kv.Value.ToString()}");
            Console.WriteLine();
        }

        Console.WriteLine(Ansi.Bold("Installed components"));
        if (cfg.Components is null || cfg.Components.Count == 0)
        {
            Console.WriteLine(Ansi.Dim("  (none yet — try `lumeo add button`)"));
        }
        else
        {
            int pad = Math.Max(14, cfg.Components.Keys.Max(k => k.Length) + 2);
            foreach (var kv in cfg.Components.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                var name = kv.Key.PadRight(pad);
                Console.WriteLine($"  {Ansi.Green(name)} {Ansi.Dim($"v{kv.Value.Version}")}  {Ansi.Dim($"({kv.Value.Files.Count} files)")}");
            }
        }
        return Task.CompletedTask;

        static void ThemeRow(string label, string? v)
            => Console.WriteLine($"  {Ansi.Dim(label.PadRight(14))} {v ?? Ansi.Dim("(unset)")}");
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
