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

    // A NuGet-free Razor class library: framework + the external icon package only. Blazicons is
    // pre-referenced so the CLI never shells out to `dotnet add package` (which needs an SDK on PATH).
    private static string MinimalCsproj() =>
        "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><TargetFramework>net10.0</TargetFramework>"
      + "<Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>"
      + "<ItemGroup><PackageReference Include=\"Microsoft.AspNetCore.Components.Web\" Version=\"10.0.6\" />"
      + "<PackageReference Include=\"Blazicons.Lucide\" Version=\"2.1.3\" /></ItemGroup></Project>";

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
    public void Init_Standalone_Marks_Config_And_Scaffolds_Using()
    {
        Assert.True(File.Exists(_lumeoDll), "Built CLI (lumeo.dll) not found — build the solution first.");
        var init = RunCli("init", "--yes", "--standalone", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets");
        Assert.True(init.Exit == 0, $"init failed (exit {init.Exit}). {init.Stderr}{init.Stdout}");

        Assert.Contains("\"standalone\": true", File.ReadAllText(Path.Combine(_proj, "lumeo.json")));
        var imports = Path.Combine(_proj, "Components", "Ui", "_Imports.razor");
        Assert.True(File.Exists(imports), "standalone init did not scaffold _Imports.razor");
        Assert.Contains("@using Lumeo", File.ReadAllText(imports));
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
        // A NORMAL (non-standalone) project that references the Lumeo package + the external icon dep.
        File.WriteAllText(Path.Combine(_proj, "App.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><TargetFramework>net10.0</TargetFramework>"
          + "<ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup>"
          + "<PackageReference Include=\"Microsoft.AspNetCore.Components.Web\" Version=\"10.0.6\" />"
          + "<PackageReference Include=\"Blazicons.Lucide\" Version=\"2.1.3\" />"
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
        Assert.Contains("Include=\"Blazicons.Lucide\"", csproj);        // external deps are left intact
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
          + "<PackageReference Include=\"Blazicons.Lucide\" Version=\"2.1.3\" />"
          + "<PackageReference Include=\"Mammoth\" Version=\"1.11.0\" /></ItemGroup></Project>");
        Assert.Equal(0, RunCli("init", "--yes", "--standalone", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets").Exit);
        Assert.Equal(0, RunCli("add", "rich-text-editor", "--local", "--yes", "--force").Exit);

        var build = RunDotnet("build", "-c", "Debug", "--nologo");
        Assert.True(build.Exit == 0, $"Editor standalone build FAILED:\n{build.Stdout}\n{build.Stderr}");
        Assert.DoesNotContain("Include=\"Lumeo\"", File.ReadAllText(Path.Combine(_proj, "App.csproj")));
    }
}
