using System.Diagnostics;
using Xunit;

namespace Lumeo.Cli.Tests;

/// <summary>
/// End-to-end smoke for the vendoring path users actually run — the command
/// handlers that the pure-helper unit tests never touch. Runs the BUILT CLI as a
/// process: `init` a throwaway project, `add` a core component from the local
/// source (--local), and verify the file lands on disk with its namespace
/// rewritten. Exercises registry load, dependency resolution, NamespaceRewriter
/// integration, dest-path mapping and the file writes.
/// </summary>
public sealed class CliVendorE2ETests : IDisposable
{
    private readonly string _repoRoot;
    private readonly string _lumeoDll;
    private readonly string _proj;

    public CliVendorE2ETests()
    {
        _repoRoot = FindRepoRoot(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Repo root (Lumeo.slnx) not found above " + AppContext.BaseDirectory);

        // The solution build (CI Build step, or a local build) produces
        // tools/Lumeo.Cli/bin/<cfg>/net10.0[/<rid>]/lumeo.dll.
        var binDir = Path.Combine(_repoRoot, "tools", "Lumeo.Cli", "bin");
        _lumeoDll = Directory.Exists(binDir)
            ? Directory.EnumerateFiles(binDir, "lumeo.dll", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() ?? ""
            : "";

        // Throwaway project INSIDE the repo so `--local` (which walks up to Lumeo.slnx)
        // resolves the local registry + source. Cleaned up in Dispose.
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

    private (int Exit, string Stdout, string Stderr) RunCli(params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _proj,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["DOTNET_ROLL_FORWARD"] = "Major"; // tolerate a newer installed runtime
        psi.ArgumentList.Add(_lumeoDll);
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)!;
        var outTask = p.StandardOutput.ReadToEndAsync();
        var errTask = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(60_000))
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException("CLI did not exit in 60s: " + string.Join(' ', args));
        }
        return (p.ExitCode, outTask.GetAwaiter().GetResult(), errTask.GetAwaiter().GetResult());
    }

    [Fact]
    public void Init_Then_Add_Vendors_A_Core_Component_With_Rewritten_Namespace()
    {
        Assert.True(File.Exists(_lumeoDll),
            "Built CLI (lumeo.dll) not found under tools/Lumeo.Cli/bin — build the solution first.");

        var init = RunCli("init", "--yes", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets");
        Assert.True(init.Exit == 0, $"init failed (exit {init.Exit}). stderr: {init.Stderr}\nstdout: {init.Stdout}");
        Assert.True(File.Exists(Path.Combine(_proj, "lumeo.json")), "init did not write lumeo.json");

        var add = RunCli("add", "button", "--local", "--yes", "--force");
        Assert.True(add.Exit == 0, $"add failed (exit {add.Exit}). stderr: {add.Stderr}\nstdout: {add.Stdout}");

        var razor = Path.Combine(_proj, "Components", "Ui", "Button", "Button.razor");
        Assert.True(File.Exists(razor), $"Button.razor was not vendored to {razor}.\nadd stdout:\n{add.Stdout}");

        // The vendored file's root @namespace must be rewritten from Lumeo to the target.
        var content = File.ReadAllText(razor);
        Assert.Contains("@namespace Acme.Ui", content);
    }

    [Fact]
    public void List_Local_Loads_The_Registry_And_Prints_Components()
    {
        Assert.True(File.Exists(_lumeoDll), "Built CLI (lumeo.dll) not found — build the solution first.");

        var r = RunCli("list", "--local");
        Assert.True(r.Exit == 0, $"list failed (exit {r.Exit}). stderr: {r.Stderr}");
        Assert.Contains("Button", r.Stdout);
    }
}
