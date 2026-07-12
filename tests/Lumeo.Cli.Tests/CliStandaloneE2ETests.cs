using System.Diagnostics;
using Xunit;

namespace Lumeo.Cli.Tests;

/// <summary>
/// End-to-end for the NuGet-free "standalone" eject path: `init --standalone` + `add` must vendor
/// the shared Lumeo runtime into _LumeoRuntime/ (verbatim, Lumeo namespace), never add a Lumeo
/// PackageReference, and — the hard proof — a project that adds a component WITH component
/// dependencies AND service usage (Dialog → Button + OverlayService + ILumeoLocalizer + Cx) must
/// `dotnet build` green with no Lumeo package referenced. Runs the BUILT CLI as a process.
/// </summary>
public sealed class CliStandaloneE2ETests : IDisposable
{
    private readonly string _repoRoot;
    private readonly string _lumeoDll;
    private readonly string _proj;

    public CliStandaloneE2ETests()
    {
        _repoRoot = FindRepoRoot(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Repo root (Lumeo.slnx) not found above " + AppContext.BaseDirectory);
        var binDir = Path.Combine(_repoRoot, "tools", "Lumeo.Cli", "bin");
        _lumeoDll = Directory.Exists(binDir)
            ? Directory.EnumerateFiles(binDir, "lumeo.dll", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() ?? ""
            : "";
        _proj = Path.Combine(_repoRoot, $".e2e-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_proj);
    }

    public void Dispose()
    {
        try { Directory.Delete(_proj, recursive: true); } catch { /* best-effort */ }
    }

    private static string? FindRepoRoot(string start)
    {
        for (var d = new DirectoryInfo(start); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Lumeo.slnx"))) return d.FullName;
        return null;
    }

    private (int Exit, string Stdout, string Stderr) RunCli(params string[] args) => RunCli(args, timeoutMs: 90_000);

    // Overload with a caller-supplied timeout — the eject-gate tests vendor ALL (or several)
    // components in one call instead of one, so the default 90s CLI timeout is too tight.
    private (int Exit, string Stdout, string Stderr) RunCli(string[] args, int timeoutMs) => Run(_lumeoDll, args, timeoutMs, prefixDll: true);

    // Build the scaffolded project with the SAME dotnet host that is running the tests — the .NET 10
    // SDK is off-PATH (~/.dotnet), so shelling out to a bare "dotnet" could hit a different runtime.
    private (int Exit, string Stdout, string Stderr) RunDotnet(params string[] args) => RunDotnet(args, timeoutMs: 420_000);

    // Overload with a caller-supplied timeout — building a project with every registered
    // component vendored in is heavier than the single/handful-of-component builds elsewhere
    // in this file, so the eject-gate-full test asks for more headroom than the default 7 min.
    private (int Exit, string Stdout, string Stderr) RunDotnet(string[] args, int timeoutMs) => Run(DotnetHost(), args, timeoutMs, prefixDll: false);

    // Best-effort snapshot of lumeo.json's installed-components map, for failure messages —
    // "fails loudly with the component list" when the eject-gate build breaks. Never throws:
    // a malformed/partial lumeo.json (e.g. `add --all` aborted mid-way) just yields "(unknown)".
    private string InstalledComponentsSummary()
    {
        try
        {
            var path = Path.Combine(_proj, "lumeo.json");
            if (!File.Exists(path)) return "(no lumeo.json — nothing installed yet)";
            var cfg = System.Text.Json.JsonSerializer.Deserialize<LumeoConfigSnapshot>(File.ReadAllText(path),
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var keys = cfg?.Components?.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();
            return keys.Count == 0 ? "(none recorded)" : $"{keys.Count} component(s): {string.Join(", ", keys)}";
        }
        catch (Exception ex)
        {
            return $"(could not read lumeo.json: {ex.Message})";
        }
    }

    // Minimal shape for reading lumeo.json's components map without depending on the CLI's
    // internal (non-public) LumeoConfig type.
    private sealed class LumeoConfigSnapshot
    {
        public Dictionary<string, object>? Components { get; set; }
    }

    private static string DotnetHost()
    {
        // Resolve a dotnet that has an SDK (the test host process may be a runtime-only apphost).
        var exe = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("DOTNET_ROOT") is { Length: > 0 } r ? Path.Combine(r, exe) : null,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", exe),
        };
        foreach (var c in candidates) if (c is not null && File.Exists(c)) return c;
        return "dotnet"; // on PATH (CI)
    }

    // A NuGet-free Razor class library: the framework reference only. Icons are first-party (the
    // vendored runtime carries SvgGlyph + LumeoIcons), so standalone needs no external icon package
    // and the CLI never shells out to `dotnet add package` for one.
    private static string MinimalCsproj() =>
        "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><TargetFramework>net10.0</TargetFramework>"
      + "<Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>"
      + "<ItemGroup><PackageReference Include=\"Microsoft.AspNetCore.Components.Web\" Version=\"10.0.6\" />"
      + "</ItemGroup></Project>";

    // Same as MinimalCsproj, but pre-references every external (non-Lumeo) NuGet package any
    // registry component's packageDependencies can require (Mammoth/Markdig/QRCoder as of this
    // writing — src/Lumeo.Editor, src/Lumeo.FileViewer, src/Lumeo respectively). `add --all`
    // touches all of them, and EnsureNuGetPackageAsync skips its own `dotnet add package` shell-
    // out once a csproj already references the package (Commands.cs FindCsprojReferencingPackage
    // check) — so pre-referencing here avoids depending on that subprocess launch succeeding
    // (it resolves a bare "dotnet" on PATH, which is a different/older SDK than the roll-forward
    // dotnet host this harness itself uses; see DotnetHost()). Versions match the pinned versions
    // in the satellites' own .csproj files.
    private static string AllExternalPackagesCsproj() =>
        "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><TargetFramework>net10.0</TargetFramework>"
      + "<Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>"
      + "<ItemGroup><PackageReference Include=\"Microsoft.AspNetCore.Components.Web\" Version=\"10.0.6\" />"
      + "<PackageReference Include=\"Mammoth\" Version=\"1.11.0\" />"
      + "<PackageReference Include=\"Markdig\" Version=\"0.37.0\" />"
      + "<PackageReference Include=\"QRCoder\" Version=\"1.8.0\" />"
      + "</ItemGroup></Project>";

    private (int Exit, string Stdout, string Stderr) Run(string program, string[] args, int timeoutMs, bool prefixDll)
    {
        var psi = new ProcessStartInfo(prefixDll ? "dotnet" : program)
        {
            WorkingDirectory = _proj,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["DOTNET_ROLL_FORWARD"] = "Major";
        if (prefixDll) psi.ArgumentList.Add(program);
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)!;
        var outTask = p.StandardOutput.ReadToEndAsync();
        var errTask = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(timeoutMs))
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"process did not exit in {timeoutMs}ms: {program} {string.Join(' ', args)}");
        }
        return (p.ExitCode, outTask.GetAwaiter().GetResult(), errTask.GetAwaiter().GetResult());
    }

    [Fact]
    public void Init_Standalone_Marks_Config_And_Defers_Imports()
    {
        Assert.True(File.Exists(_lumeoDll), "Built CLI (lumeo.dll) not found — build the solution first.");
        var init = RunCli("init", "--yes", "--standalone", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets");
        Assert.True(init.Exit == 0, $"init failed (exit {init.Exit}). {init.Stderr}{init.Stdout}");

        Assert.Contains("\"standalone\": true", File.ReadAllText(Path.Combine(_proj, "lumeo.json")));
        // The standalone @using Lumeo bridge is DEFERRED until `add`/`eject` vendors the runtime: those
        // namespaces (Lumeo, Lumeo.Internal, Lumeo.Services, …) don't exist on disk until then, so writing
        // them into _Imports.razor at init would make a bare `init --standalone` project fail Razor
        // compilation before any component is added (Codex P2). init must NOT create _Imports.razor — the
        // first `add` writes it (asserted in Standalone_Add_Vendors_Runtime_Keeping_Lumeo_Namespace).
        Assert.False(File.Exists(Path.Combine(_proj, "_Imports.razor")),
            "init --standalone must NOT scaffold _Imports.razor before the runtime is vendored");
    }

    [Fact]
    public void Standalone_Add_Vendors_Runtime_Keeping_Lumeo_Namespace()
    {
        File.WriteAllText(Path.Combine(_proj, "App.csproj"), MinimalCsproj());
        RunCli("init", "--yes", "--standalone", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets");
        var add = RunCli("add", "dialog", "--local", "--yes", "--force");
        Assert.True(add.Exit == 0, $"add failed (exit {add.Exit}). {add.Stderr}{add.Stdout}");

        var cx = Path.Combine(_proj, "Components", "Ui", "_LumeoRuntime", "Internal", "Cx.cs");
        Assert.True(File.Exists(cx), $"runtime Cx.cs not vendored to {cx}\n{add.Stdout}");
        Assert.Contains("namespace Lumeo", File.ReadAllText(cx));   // runtime keeps the Lumeo namespace (NOT rewritten)

        Assert.True(File.Exists(Path.Combine(_proj, "Components", "Ui", "_LumeoRuntime", "Extensions", "LumeoServiceExtensions.cs")),
            "AddLumeo extension not vendored");
        Assert.True(File.Exists(Path.Combine(_proj, "Components", "Ui", "_LumeoRuntime", "Size.cs")),
            "root-level Size enum not vendored");

        // The standalone @using Lumeo bridge is written by `add` (deferred from init), once the runtime it
        // references actually exists on disk. It goes in the PROJECT-ROOT _Imports.razor so it cascades to
        // app pages too — Razor imports only flow downward.
        var imports = Path.Combine(_proj, "_Imports.razor");
        Assert.True(File.Exists(imports), $"standalone `add` did not write the deferred project-root _Imports.razor\n{add.Stdout}");
        Assert.Contains("@using Lumeo", File.ReadAllText(imports));
    }

    [Fact]
    public void Standalone_Add_ConfirmButton_Also_Vendors_The_Overlay_Host()
    {
        // Codex P2 — ConfirmButton drives overlays IMPERATIVELY via IOverlayService
        // (Overlay.ShowAlertDialogAsync) and never renders <OverlayProvider> itself, so a
        // project that only has ConfirmButton installed must still get OverlayProvider
        // vendored as a transitive dependency — otherwise standalone/eject strips the Lumeo
        // package and the overlay host type no longer exists anywhere in the project.
        File.WriteAllText(Path.Combine(_proj, "App.csproj"), MinimalCsproj());
        RunCli("init", "--yes", "--standalone", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets");
        var add = RunCli("add", "confirm-button", "--local", "--yes", "--force");
        Assert.True(add.Exit == 0, $"add failed (exit {add.Exit}). {add.Stderr}{add.Stdout}");

        Assert.True(File.Exists(Path.Combine(_proj, "Components", "Ui", "ConfirmButton", "ConfirmButton.razor")),
            "ConfirmButton itself was not vendored");
        Assert.True(File.Exists(Path.Combine(_proj, "Components", "Ui", "Overlay", "OverlayProvider.razor")),
            $"OverlayProvider was not vendored as a transitive dependency of ConfirmButton\n{add.Stdout}");
    }

    [Fact]
    public void Standalone_Add_Emits_No_Lumeo_PackageReference()
    {
        File.WriteAllText(Path.Combine(_proj, "App.csproj"), MinimalCsproj());
        RunCli("init", "--yes", "--standalone", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets");
        var add = RunCli("add", "dialog", "--local", "--yes", "--force");
        Assert.True(add.Exit == 0, $"add failed (exit {add.Exit}). {add.Stderr}{add.Stdout}");

        var csproj = File.ReadAllText(Path.Combine(_proj, "App.csproj"));
        Assert.DoesNotContain("Include=\"Lumeo\"", csproj);
        Assert.DoesNotContain("Include=\"Lumeo.", csproj);
    }

    [Fact]
    public void Standalone_Project_Builds_NuGetFree_With_Component_Deps_And_Services()
    {
        // A Razor class library referencing only the framework + the external icon package — NO Lumeo.
        File.WriteAllText(Path.Combine(_proj, "App.csproj"), MinimalCsproj());

        Assert.Equal(0, RunCli("init", "--yes", "--standalone", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets").Exit);
        // Dialog brings a component dependency (Button) AND uses services (OverlayService,
        // ILumeoLocalizer) + Cx — exactly the cases the runtime closure must satisfy.
        var add = RunCli("add", "dialog", "--local", "--yes", "--force");
        Assert.True(add.Exit == 0, $"add dialog failed (exit {add.Exit}). {add.Stderr}{add.Stdout}");

        var build = RunDotnet("build", "-c", "Debug", "--nologo");
        Assert.True(build.Exit == 0, $"standalone build FAILED — the runtime closure is incomplete:\n{build.Stdout}\n{build.Stderr}");
        Assert.DoesNotContain("Include=\"Lumeo\"", File.ReadAllText(Path.Combine(_proj, "App.csproj")));
    }

    [Fact]
    public void Eject_Converts_A_Normal_Project_To_Standalone()
    {
        // A NORMAL (non-standalone) project that references the Lumeo package + an unrelated external dep.
        File.WriteAllText(Path.Combine(_proj, "App.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><TargetFramework>net10.0</TargetFramework>"
          + "<ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup>"
          + "<PackageReference Include=\"Microsoft.AspNetCore.Components.Web\" Version=\"10.0.6\" />"
          + "<PackageReference Include=\"Mammoth\" Version=\"1.11.0\" />"
          + "<PackageReference Include=\"Lumeo\" Version=\"4.0.0\" /></ItemGroup></Project>");

        Assert.Equal(0, RunCli("init", "--yes", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets").Exit);
        Assert.Equal(0, RunCli("add", "dialog", "--local", "--yes", "--force").Exit);

        var eject = RunCli("eject", "--local");
        Assert.True(eject.Exit == 0, $"eject failed (exit {eject.Exit}). {eject.Stderr}{eject.Stdout}");

        Assert.Contains("\"standalone\": true", File.ReadAllText(Path.Combine(_proj, "lumeo.json")));
        Assert.True(File.Exists(Path.Combine(_proj, "Components", "Ui", "_LumeoRuntime", "Internal", "Cx.cs")),
            "eject did not vendor the runtime");

        var csproj = File.ReadAllText(Path.Combine(_proj, "App.csproj"));
        Assert.DoesNotContain("Include=\"Lumeo\"", csproj);            // the Lumeo package was stripped
        Assert.Contains("Include=\"Mammoth\"", csproj);                // external (non-Lumeo) deps are left intact
    }

    [Fact]
    public void Eject_On_Already_Standalone_Project_Finishes_Stripping_A_Later_Vendored_Satellite()
    {
        // Reproduces Codex's exact P2 scenario: the FIRST eject can't vendor every Lumeo.* package
        // reference as source — here, a stray "Lumeo.Motion" reference (AnimatedBeam was never added
        // via the CLI, so it has no lumeo.json record and no vendored source) — so it must KEEP that
        // PackageReference and tell the user to `lumeo add <component> --vendor` then re-run `eject`.
        // Before the fix, the SECOND eject hit an unconditional `if (cfg.Standalone) return;` (standalone
        // was already persisted true by the first run), making it a total no-op and leaving the kept
        // package stuck in the csproj forever with no CLI path to finish.
        File.WriteAllText(Path.Combine(_proj, "App.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><TargetFramework>net10.0</TargetFramework>"
          + "<ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup>"
          + "<PackageReference Include=\"Microsoft.AspNetCore.Components.Web\" Version=\"10.0.6\" />"
          + "<PackageReference Include=\"Mammoth\" Version=\"1.11.0\" />"
          + "<PackageReference Include=\"Lumeo\" Version=\"4.0.0\" />"
          + "<PackageReference Include=\"Lumeo.Motion\" Version=\"4.0.0\" /></ItemGroup></Project>");

        Assert.Equal(0, RunCli("init", "--yes", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets").Exit);
        Assert.Equal(0, RunCli("add", "dialog", "--local", "--yes", "--force").Exit);

        var firstEject = RunCli("eject", "--local");
        Assert.True(firstEject.Exit == 0, $"first eject failed (exit {firstEject.Exit}). {firstEject.Stderr}{firstEject.Stdout}");
        Assert.Contains("\"standalone\": true", File.ReadAllText(Path.Combine(_proj, "lumeo.json")));
        var afterFirst = File.ReadAllText(Path.Combine(_proj, "App.csproj"));
        Assert.DoesNotContain("Include=\"Lumeo\"", afterFirst);             // Dialog's package WAS stripped (now vendored as source)
        Assert.Contains("Include=\"Lumeo.Motion\"", afterFirst);            // AnimatedBeam's package was KEPT — never vendored

        // The remedy the first eject's own console message points at.
        Assert.Equal(0, RunCli("add", "animated-beam", "--local", "--yes", "--force", "--vendor").Exit);

        var secondEject = RunCli("eject", "--local");
        Assert.True(secondEject.Exit == 0, $"second eject failed (exit {secondEject.Exit}). {secondEject.Stderr}{secondEject.Stdout}");
        Assert.DoesNotContain("Include=\"Lumeo.Motion\"", File.ReadAllText(Path.Combine(_proj, "App.csproj")));
    }

    [Fact]
    public void Standalone_Satellite_DataGrid_Builds_NuGetFree()
    {
        File.WriteAllText(Path.Combine(_proj, "App.csproj"), MinimalCsproj());
        Assert.Equal(0, RunCli("init", "--yes", "--standalone", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets").Exit);
        // DataGrid is a satellite (Lumeo.DataGrid) with ~15 component deps incl. form controls —
        // exercises satellite source+JS vendoring AND the form/FormFieldContext type-dep closure.
        Assert.Equal(0, RunCli("add", "data-grid", "--local", "--yes", "--force").Exit);

        var build = RunDotnet("build", "-c", "Debug", "--nologo");
        Assert.True(build.Exit == 0, $"DataGrid standalone build FAILED:\n{build.Stdout}\n{build.Stderr}");
        Assert.DoesNotContain("Include=\"Lumeo\"", File.ReadAllText(Path.Combine(_proj, "App.csproj")));  // no Lumeo / Lumeo.DataGrid
    }

    [Fact]
    public void Standalone_Satellite_Editor_Builds_NuGetFree_With_External_Package()
    {
        // The Editor satellite's WordImporter needs the external Mammoth package — pre-reference it (it
        // is not Lumeo, so it stays a NuGet dependency that the registry now reports for the consumer).
        File.WriteAllText(Path.Combine(_proj, "App.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><TargetFramework>net10.0</TargetFramework>"
          + "<Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup>"
          + "<PackageReference Include=\"Microsoft.AspNetCore.Components.Web\" Version=\"10.0.6\" />"
          + "<PackageReference Include=\"Mammoth\" Version=\"1.11.0\" /></ItemGroup></Project>");
        Assert.Equal(0, RunCli("init", "--yes", "--standalone", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets").Exit);
        Assert.Equal(0, RunCli("add", "rich-text-editor", "--local", "--yes", "--force").Exit);

        var build = RunDotnet("build", "-c", "Debug", "--nologo");
        Assert.True(build.Exit == 0, $"Editor standalone build FAILED:\n{build.Stdout}\n{build.Stderr}");
        Assert.DoesNotContain("Include=\"Lumeo\"", File.ReadAllText(Path.Combine(_proj, "App.csproj")));
    }

    // ── Eject-gate: the standing CI guarantee (.github/workflows/eject-gate.yml) ──────────────
    //
    // The individual tests above proved specific corners (a core component, a component with
    // deps+services, the imperative-overlay pattern, two satellites). This pair generalizes that
    // to the WHOLE registry so drift in any one component's vendored-runtime closure is caught,
    // not just the handful spot-checked above.
    //
    //   - Standalone_All_Components_Eject_And_Build_NuGetFree (Category=EjectGateFull): every
    //     registered component, one project, one build. Slow (164 components) — excluded from
    //     the default per-PR `dotnet test Lumeo.slnx` run (ci.yml filters out Category=
    //     EjectGateFull) and instead run by eject-gate.yml on its own weekly/dispatch/release
    //     cadence.
    //   - Standalone_Smoke_Five_Components_Build_NuGetFree (Category=EjectGateSmoke): a small,
    //     fast representative slice. Carries NO exclusion filter, so it runs automatically in
    //     the existing per-PR suite as a cheap early warning between full-gate runs.

    [Fact]
    [Trait("Category", "EjectGateFull")]
    public void Standalone_All_Components_Eject_And_Build_NuGetFree()
    {
        File.WriteAllText(Path.Combine(_proj, "App.csproj"), AllExternalPackagesCsproj());
        Assert.Equal(0, RunCli("init", "--yes", "--standalone", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets").Exit);

        // `--all` vendors every registry entry, core AND satellites — standalone mode forces
        // vendor-as-source for satellites too (no --vendor needed), so this is the true "eject
        // everything" path a consumer's `lumeo add --all` on a fresh standalone project takes.
        var add = RunCli(new[] { "add", "--all", "--local", "--yes", "--force" }, timeoutMs: 300_000);
        Assert.True(add.Exit == 0,
            $"`add --all` failed (exit {add.Exit}) before a build was even attempted.\n"
          + $"Installed so far: {InstalledComponentsSummary()}\n--- stderr ---\n{add.Stderr}\n--- stdout ---\n{add.Stdout}");

        var build = RunDotnet(new[] { "build", "-c", "Debug", "--nologo" }, timeoutMs: 900_000);
        Assert.True(build.Exit == 0,
            $"EJECT GATE BROKEN — the standalone NuGet-free build failed with every registered "
          + $"component vendored in. {InstalledComponentsSummary()}\n"
          + $"--- dotnet build stdout ---\n{build.Stdout}\n--- dotnet build stderr ---\n{build.Stderr}");

        var csproj = File.ReadAllText(Path.Combine(_proj, "App.csproj"));
        Assert.DoesNotContain("Include=\"Lumeo\"", csproj);
        Assert.DoesNotContain("Include=\"Lumeo.", csproj);
    }

    [Fact]
    [Trait("Category", "EjectGateSmoke")]
    public void Standalone_Smoke_Five_Components_Build_NuGetFree()
    {
        // Five components chosen to each exercise a different corner of the vendoring closure:
        //   button           - plain core component, no deps.
        //   dialog           - component deps (Button) + services (OverlayService, ILumeoLocalizer) + Cx.
        //   confirm-button   - drives an overlay IMPERATIVELY (no <OverlayProvider> in its own markup);
        //                      the transitive OverlayProvider vendoring has broken before (Codex P2).
        //   icon             - the registry's icon-rendering component; exercises the vendored
        //                      first-party Icons/LumeoIcons.g.cs runtime asset (the closest
        //                      registry-addable proxy for "one icon pack" — the optional
        //                      Lumeo.Icons.* NuGet packs selected via `lumeo apply --icons` are a
        //                      separate opt-in feature, not part of the zero-PackageReference core
        //                      guarantee this gate proves).
        //   data-grid        - satellite (Lumeo.DataGrid) with a large component-dependency closure
        //                      incl. form controls.
        File.WriteAllText(Path.Combine(_proj, "App.csproj"), MinimalCsproj());
        Assert.Equal(0, RunCli("init", "--yes", "--standalone", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets").Exit);

        foreach (var component in new[] { "button", "dialog", "confirm-button", "icon", "data-grid" })
        {
            var add = RunCli("add", component, "--local", "--yes", "--force");
            Assert.True(add.Exit == 0, $"add {component} failed (exit {add.Exit}). {add.Stderr}{add.Stdout}");
        }

        var build = RunDotnet("build", "-c", "Debug", "--nologo");
        Assert.True(build.Exit == 0,
            $"Eject-gate SMOKE build FAILED — {InstalledComponentsSummary()}\n{build.Stdout}\n{build.Stderr}");

        var csproj = File.ReadAllText(Path.Combine(_proj, "App.csproj"));
        Assert.DoesNotContain("Include=\"Lumeo\"", csproj);
        Assert.DoesNotContain("Include=\"Lumeo.", csproj);
    }
}
