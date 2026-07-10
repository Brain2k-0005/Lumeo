using System.Diagnostics;
using Xunit;

namespace Lumeo.Cli.Tests;

/// <summary>
/// Regression for the tombstoned-icon apply path (PR review, round 2): a legacy client preset that
/// decodes to an empty icon index (font-awesome / material-design / ionicons / devicon / flag-icons)
/// has NO first-party Lumeo.Icons.* pack. `apply --only icons` on such a code must warn-and-skip the
/// icon selection WITHOUT persisting a blank — it must not overwrite an existing valid first-party
/// iconLibrary in lumeo.json / wwwroot/lumeo-theme.json with "". Runs the BUILT CLI as a process.
/// </summary>
public sealed class CliApplyIconTombstoneTests : IDisposable
{
    private readonly string _lumeoDll;
    private readonly string _proj;

    public CliApplyIconTombstoneTests()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Repo root (Lumeo.slnx) not found above " + AppContext.BaseDirectory);
        var binDir = Path.Combine(repoRoot, "tools", "Lumeo.Cli", "bin");
        _lumeoDll = Directory.Exists(binDir)
            ? Directory.EnumerateFiles(binDir, "lumeo.dll", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() ?? ""
            : "";
        _proj = Path.Combine(repoRoot, $".e2e-cli-tombstone-{Guid.NewGuid():N}");
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

    private (int Exit, string Stdout, string Stderr) RunCli(params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _proj,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["DOTNET_ROLL_FORWARD"] = "Major";
        psi.ArgumentList.Add(_lumeoDll);
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)!;
        var outTask = p.StandardOutput.ReadToEndAsync();
        var errTask = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(90_000))
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException("lumeo apply did not exit in 90s");
        }
        return (p.ExitCode, outTask.GetAwaiter().GetResult(), errTask.GetAwaiter().GetResult());
    }

    [Fact]
    public void Apply_DryRun_TombstonedPreset_ShowsNormalizedValue_AndWarns()
    {
        Assert.True(File.Exists(_lumeoDll), "Built CLI (lumeo.dll) not found — build the solution first.");

        // Seed a minimal project.
        var configPath = Path.Combine(_proj, "lumeo.json");
        File.WriteAllText(configPath, "{ \"theme\": { \"iconLibrary\": \"lucide\" } }");
        Directory.CreateDirectory(Path.Combine(_proj, "wwwroot"));

        // Index 3 in IconLibraries is a tombstone (was "font-awesome") → decodes to "".
        var code = LumeoPresetCodec.Encode(new LumeoPreset(
            Theme: 0, Style: 0, BaseColor: 0, Radius: 2, Font: 0,
            IconLibrary: 3, MenuColor: 0, MenuAccent: 0, Dark: 0));

        var r = RunCli("apply", code, "--only", "icons", "--dry-run", "--yes");
        Assert.True(r.Exit == 0, $"apply --dry-run failed (exit {r.Exit}). {r.Stderr}{r.Stdout}");

        // Warning must fire during dry-run (normalization moved before display).
        Assert.Contains("no first-party pack", r.Stderr, StringComparison.OrdinalIgnoreCase);

        // The preview row must not display a raw "" — it should show "(unset)" (null after normalize).
        Assert.DoesNotContain("iconLibrary      \"\"", r.Stdout, StringComparison.Ordinal);
        // Dry-run must not have written anything.
        Assert.Equal("{ \"theme\": { \"iconLibrary\": \"lucide\" } }", File.ReadAllText(configPath));
    }

    [Fact]
    public void Apply_OnlyIcons_TombstonedPreset_PreservesExistingIconLibrary_AndWarns()
    {
        Assert.True(File.Exists(_lumeoDll), "Built CLI (lumeo.dll) not found — build the solution first.");

        // Seed a project that already picked a valid first-party icon pack.
        var configPath = Path.Combine(_proj, "lumeo.json");
        File.WriteAllText(configPath, "{ \"theme\": { \"iconLibrary\": \"lucide\", \"font\": \"inter\" } }");
        Directory.CreateDirectory(Path.Combine(_proj, "wwwroot"));
        var themeJsonPath = Path.Combine(_proj, "wwwroot", "lumeo-theme.json");
        File.WriteAllText(themeJsonPath, "{ \"iconLibrary\": \"lucide\" }");

        // Index 3 in IconLibraries is a tombstone (was "font-awesome") → decodes to "".
        var code = LumeoPresetCodec.Encode(new LumeoPreset(
            Theme: 0, Style: 0, BaseColor: 0, Radius: 2, Font: 0,
            IconLibrary: 3, MenuColor: 0, MenuAccent: 0, Dark: 0));

        var r = RunCli("apply", code, "--only", "icons", "--yes", "--silent");
        Assert.True(r.Exit == 0, $"apply failed (exit {r.Exit}). {r.Stderr}{r.Stdout}");

        // The warning must still fire (warn-and-skip, not silently swallow).
        Assert.Contains("no first-party pack", r.Stderr, StringComparison.OrdinalIgnoreCase);

        // The dead blank must NOT have been persisted over the valid selection.
        var config = File.ReadAllText(configPath);
        Assert.Contains("\"iconLibrary\": \"lucide\"", config);
        Assert.DoesNotContain("\"iconLibrary\": \"\"", config);

        var themeJson = File.ReadAllText(themeJsonPath);
        Assert.Contains("\"iconLibrary\": \"lucide\"", themeJson);
        Assert.DoesNotContain("\"iconLibrary\": \"\"", themeJson);
    }
}
