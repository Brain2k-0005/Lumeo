using System.Diagnostics;
using System.Text.RegularExpressions;
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

    private (int Exit, string Stdout, string Stderr) RunCli(params string[] args) => Run(_lumeoDll, args, 90_000, prefixDll: true);

    // Build the scaffolded project with the SAME dotnet host that is running the tests — the .NET 10
    // SDK is off-PATH (~/.dotnet), so shelling out to a bare "dotnet" could hit a different runtime.
    private (int Exit, string Stdout, string Stderr) RunDotnet(params string[] args) => Run(DotnetHost(), args, 420_000, prefixDll: false);

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
    public void Default_Add_Toast_Vendors_No_Internal_Lumeo_References()
    {
        // A NORMAL (non-standalone) project that references the Lumeo package — the DEFAULT
        // `lumeo add <component>` path. Core UI components are ALWAYS vendored as owned SOURCE
        // (shadcn-style), regardless of standalone mode; only the shared runtime substrate
        // (Lumeo.Internal/Lumeo.Services/…) stays behind the NuGet package reference here — it is
        // NOT vendored outside --standalone/eject. PR #357 round-3 (P1): Toast.razor referenced
        // `Lumeo.Internal.LumeoIds` and `Lumeo.Services.DelayedDispatch` — both `internal` to the
        // Lumeo assembly — so the vendored copy failed to compile in the consumer app with
        // inaccessible-type errors the moment it was built; no restored Lumeo NuGet package grants
        // InternalsVisibleTo to an arbitrary consumer assembly. Guards the whole vendored Toast
        // family against reintroducing an internal reference under this path.
        File.WriteAllText(Path.Combine(_proj, "App.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><TargetFramework>net10.0</TargetFramework>"
          + "<ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup>"
          + "<PackageReference Include=\"Microsoft.AspNetCore.Components.Web\" Version=\"10.0.6\" />"
          + "<PackageReference Include=\"Lumeo\" Version=\"4.0.0\" /></ItemGroup></Project>");

        Assert.Equal(0, RunCli("init", "--yes", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets").Exit);
        var add = RunCli("add", "toast", "--local", "--yes", "--force");
        Assert.True(add.Exit == 0, $"add toast failed (exit {add.Exit}). {add.Stderr}{add.Stdout}");

        var toastDir = Path.Combine(_proj, "Components", "Ui", "Toast");
        Assert.True(Directory.Exists(toastDir), $"Toast was not vendored to {toastDir}\n{add.Stdout}");

        var vendoredFiles = Directory.EnumerateFiles(toastDir, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(vendoredFiles);

        foreach (var file in vendoredFiles)
        {
            // Strip `//`/`///` line comments before scanning: the fix for this exact finding
            // documents ITSELF in prose ("not Lumeo.Internal.LumeoIds — that helper is
            // internal…"), which would otherwise trip this same assertion as a false positive.
            // The guard cares about live CODE references, not explanatory comments.
            var code = string.Join('\n', File.ReadAllLines(file)
                .Select(line => line.IndexOf("//", StringComparison.Ordinal) is >= 0 and var i ? line[..i] : line));
            Assert.DoesNotContain("Lumeo.Internal", code);
            // Referencing DelayedDispatch UNQUALIFIED (e.g. via a stray `@using Lumeo.Services`)
            // would compile fine in the source tree but not in a consumer app that never gets
            // that using — catch the unqualified form too, not just the fully-qualified one.
            Assert.DoesNotContain("DelayedDispatch", code);
        }

        // The default path keeps the Lumeo package reference (only satellites/--vendor/
        // --standalone strip it) — this test is specifically about the vendored SOURCE compiling
        // against that still-present package, i.e. never reaching into its internals.
        Assert.Contains("Include=\"Lumeo\"", File.ReadAllText(Path.Combine(_proj, "App.csproj")));
    }

    [Fact]
    public void Add_Vendor_Toast_Compiles_Against_The_Officially_Supported_Template_Setup()
    {
        // PR #357 round-4 (P1) — "kill the class": every prior vendoring finding this PR
        // (round-2 namespace rewriting, round-3 internal-type refs) was only ever caught by
        // TEXT-scanning the vendored output for known-bad patterns, never by actually compiling
        // it. That missed a THIRD, worse break: Toast.razor called a brand-new
        // IComponentInteropService member that doesn't exist in ANY currently-published Lumeo
        // package (it ships only once this very change is released), so the officially
        // documented/templated consumer setup failed to build the instant Toast was added — no
        // string scan would ever have caught that class of bug. Reproduce that setup for real:
        // the exact csproj shape + _Imports.razor `dotnet new lumeo-app` scaffolds (see
        // templates/Lumeo.Templates/templates/lumeo-app), with its REAL, live-restored Lumeo
        // PackageReference version — and assert `dotnet build` is green. Any future component
        // whose vendored source needs a Lumeo API newer than what's actually published now fails
        // THIS suite, not a review round.
        Assert.True(File.Exists(_lumeoDll), "Built CLI (lumeo.dll) not found — build the solution first.");

        var templateCsprojPath = Path.Combine(_repoRoot, "templates", "Lumeo.Templates", "templates", "lumeo-app", "MyApp.csproj");
        Assert.True(File.Exists(templateCsprojPath), $"Official app template csproj not found at {templateCsprojPath}.");
        var templateCsproj = File.ReadAllText(templateCsprojPath);
        var versionMatch = Regex.Match(templateCsproj, "Include=\"Lumeo\"\\s+Version=\"([^\"]+)\"");
        Assert.True(versionMatch.Success, "Could not read the officially templated Lumeo package version from " + templateCsprojPath);
        var lumeoVersion = versionMatch.Groups[1].Value;

        File.WriteAllText(Path.Combine(_proj, "App.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><TargetFramework>net10.0</TargetFramework>"
          + "<Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup>"
          + "<PackageReference Include=\"Microsoft.AspNetCore.Components.Web\" Version=\"10.0.6\" />"
          + $"<PackageReference Include=\"Lumeo\" Version=\"{lumeoVersion}\" /></ItemGroup></Project>");

        // The official template's _Imports.razor, MINUS its blanket `@using Lumeo` — that using
        // is there so a fresh (nothing-vendored-yet) app can use Lumeo's PACKAGE components
        // unqualified; once `lumeo add toast` vendors Acme.Ui.Toast/ToastProvider/ToastViewport
        // (same short names, same file's implicit-same-namespace visibility), keeping `@using
        // Lumeo` ALSO in scope makes every `<Toast>`/`<ToastProvider>`/`<ToastViewport>` tag
        // ambiguous between the vendored type and the package's — Razor's tag-helper matching
        // resolves that not with an "ambiguous reference" error but by unioning both descriptors'
        // parameters, surfacing as bogus RZ10009 "parameter used twice" errors instead. `lumeo
        // add`'s own docs never claim `@using Lumeo` is required for the vendoring path (`init`
        // doesn't touch _Imports.razor outside `--standalone`), and `Lumeo.Services` (still
        // needed for ToastService/ToastOptions) has no such collision since nothing is vendored
        // under that sub-namespace.
        File.WriteAllText(Path.Combine(_proj, "_Imports.razor"),
            "@using Microsoft.AspNetCore.Components.Forms\n"
          + "@using Microsoft.AspNetCore.Components.Routing\n"
          + "@using Microsoft.AspNetCore.Components.Web\n"
          + "@using Microsoft.AspNetCore.Components.Web.Virtualization\n"
          + "@using Microsoft.JSInterop\n"
          + "@using Lumeo.Services\n");

        Assert.Equal(0, RunCli("init", "--yes", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets").Exit);
        var add = RunCli("add", "toast", "--local", "--yes", "--force");
        Assert.True(add.Exit == 0, $"add toast failed (exit {add.Exit}). {add.Stderr}{add.Stdout}");

        var build = RunDotnet("build", "-c", "Debug", "--nologo");
        Assert.True(build.Exit == 0,
            $"vendored Toast failed to compile against the officially-templated Lumeo {lumeoVersion} setup:\n{build.Stdout}\n{build.Stderr}");
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
}
