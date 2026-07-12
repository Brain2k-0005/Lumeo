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

    // PR #357 round-2/round-4: two DIFFERENT vendoring breaks hit the exact same
    // line of Toast.razor. Round-2 (Codex, P1) was a namespace-rewriting bug — an
    // UNQUALIFIED `@implements IToastEnterCallback` compiled fine in the library
    // tree but 404'd once rewritten to the consumer namespace. Round-4 (P1) then
    // found the deeper problem the round-2 fix (fully-qualifying it) papered
    // over: `IToastEnterCallback`/`IComponentInteropService.AttachToastEnterEnd`
    // were BOTH brand-new Lumeo interop surface added in the same unreleased
    // change as Toast.razor itself, so a consumer whose referenced Lumeo package
    // predates that surface (i.e. anyone, until this ships) fails to compile the
    // moment `lumeo add toast` runs — no amount of qualifying the reference fixes
    // a type that doesn't exist yet in the referenced assembly. The real fix was
    // to drop the JS-callback entrance-detection path entirely (a plain local
    // timer has zero Lumeo-surface dependency); this guard keeps it dropped, and
    // `Add_Vendor_Toast_Compiles_Against_The_Officially_Supported_Template_Setup`
    // (CliStandaloneE2ETests) proves the vendored output actually BUILDS, not
    // just that these strings are absent.
    [Fact]
    public void Add_Vendor_Toast_Never_Reintroduces_The_Brand_New_Enter_Callback_Interop()
    {
        Assert.True(File.Exists(_lumeoDll),
            "Built CLI (lumeo.dll) not found under tools/Lumeo.Cli/bin — build the solution first.");

        var init = RunCli("init", "--yes", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets");
        Assert.True(init.Exit == 0, $"init failed (exit {init.Exit}). stderr: {init.Stderr}\nstdout: {init.Stdout}");

        var add = RunCli("add", "toast", "--local", "--yes", "--force");
        Assert.True(add.Exit == 0, $"add failed (exit {add.Exit}). stderr: {add.Stderr}\nstdout: {add.Stdout}");

        var razor = Path.Combine(_proj, "Components", "Ui", "Toast", "Toast.razor");
        Assert.True(File.Exists(razor), $"Toast.razor was not vendored to {razor}.\nadd stdout:\n{add.Stdout}");

        var content = File.ReadAllText(razor);
        Assert.Contains("@namespace Acme.Ui", content);
        // Strip `//` line comments before scanning: Toast.razor's own fix for this exact finding
        // documents itself in prose (explaining what it used to call and why it no longer does),
        // which would otherwise trip this assertion as a false positive. The guard cares about
        // live CODE references, not explanatory comments — same rationale as
        // Default_Add_Toast_Vendors_No_Internal_Lumeo_References (CliStandaloneE2ETests).
        var code = string.Join('\n', content.Split('\n')
            .Select(line => line.IndexOf("//", StringComparison.Ordinal) is >= 0 and var i ? line[..i] : line));
        Assert.DoesNotContain("IToastEnterCallback", code);
        Assert.DoesNotContain("AttachToastEnterEnd", code);
    }

    // PR #357 round-10 (finding 2): the sibling-tag qualification set (round-9, finding 2) was built
    // per-ITEM inside the vendoring loop — RazorComponentNames(item.Files) — instead of from the FULL
    // set of everything this `add` invocation vendors. That's a no-op for a component with no
    // dependencies (its own item IS the whole batch), but ConfirmButton depends on `button` — the BFS
    // above pulls Button in as a SEPARATE `toInstall` entry, and ConfirmButton.razor's own markup has
    // a bare `<Button ...>...</Button>` reference to it. Under the round-9 scoping, ConfirmButton's
    // own siblingComponentNames was just ["ConfirmButton"] — Button was never in it — so that bare
    // tag was left unqualified, reproducing the exact RZ10009 "two TagHelperDescriptors, same short
    // name" ambiguity finding 2 of round-9 was supposed to close (the officially templated app keeps
    // `@using Lumeo` in _Imports.razor, which still resolves the bare `<Button>` tag against the
    // PACKAGE's Button after Acme.Ui.Button is ALSO vendored). Building the set from the full
    // `toInstall` (batch + resolved dependencies) — this test's actual assertion — closes it.
    [Fact]
    public void Add_Vendor_Qualifies_A_Transitively_Pulled_Dependencys_Bare_Tag_Reference()
    {
        Assert.True(File.Exists(_lumeoDll),
            "Built CLI (lumeo.dll) not found under tools/Lumeo.Cli/bin — build the solution first.");

        var init = RunCli("init", "--yes", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets");
        Assert.True(init.Exit == 0, $"init failed (exit {init.Exit}). stderr: {init.Stderr}\nstdout: {init.Stdout}");

        // confirm-button depends on button (+ overlay) — the BFS resolves both into the SAME `add`
        // invocation's toInstall, as separate RegistryEntry items.
        var add = RunCli("add", "confirm-button", "--local", "--yes", "--force");
        Assert.True(add.Exit == 0, $"add failed (exit {add.Exit}). stderr: {add.Stderr}\nstdout: {add.Stdout}");

        // The transitively-pulled dependency itself was vendored too, under its own rebranded
        // namespace.
        var buttonRazor = Path.Combine(_proj, "Components", "Ui", "Button", "Button.razor");
        Assert.True(File.Exists(buttonRazor), $"Button.razor (ConfirmButton's dependency) was not vendored to {buttonRazor}.\nadd stdout:\n{add.Stdout}");
        Assert.Contains("@namespace Acme.Ui", File.ReadAllText(buttonRazor));

        var confirmButtonRazor = Path.Combine(_proj, "Components", "Ui", "ConfirmButton", "ConfirmButton.razor");
        Assert.True(File.Exists(confirmButtonRazor), $"ConfirmButton.razor was not vendored to {confirmButtonRazor}.\nadd stdout:\n{add.Stdout}");
        var content = File.ReadAllText(confirmButtonRazor);
        Assert.Contains("@namespace Acme.Ui", content);

        // The bare `<Button ...>`/`</Button>` markup reference must be fully qualified to
        // Acme.Ui.Button — never left bare, which would collide with the still-in-scope `@using
        // Lumeo` package reference the officially templated app's _Imports.razor keeps.
        Assert.Contains("<Acme.Ui.Button ", content);
        Assert.Contains("</Acme.Ui.Button>", content);
        Assert.DoesNotContain("<Button ", content);
        Assert.DoesNotContain("</Button>", content);
    }

    // PR #357 round-11 (Codex finding) — `update`/`diff`/`view` built their sibling-tag
    // qualification set from ONLY the target entry's own files (RazorComponentNames(entry.Files)),
    // never walking `entry.Dependencies` the way the Add loop's BFS does. That's invisible right
    // after `add` (the freshly-vendored file IS already fully qualified), but every SUBSEQUENT
    // `update`/`diff` on confirm-button re-fetches upstream and re-qualifies it with only
    // ["ConfirmButton"] in scope — Button (a separate RegistryEntry) never makes the set, so the
    // freshly-rewritten upstream comes back with `<Button ...>` BARE while the local file on disk
    // still reads `<Acme.Ui.Button ...>` from `add`. Diffing.Normalize does NOT ignore that
    // difference, so `diff`/`update --check` would report ConfirmButton as perpetually "drifted"
    // against its own just-installed copy, and `update --force`/`-y` would overwrite the correct,
    // qualified local file with the broken bare-tag version — reintroducing the exact RZ10009
    // ambiguity the Add-side fix (test above) closed. This test's actual assertion is that NEITHER
    // command reports drift on a component whose local copy came straight from `add`.
    [Fact]
    public void Update_Check_And_Diff_Report_No_Drift_On_A_Component_With_A_Transitive_Dependency()
    {
        Assert.True(File.Exists(_lumeoDll),
            "Built CLI (lumeo.dll) not found under tools/Lumeo.Cli/bin — build the solution first.");

        var init = RunCli("init", "--yes", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets");
        Assert.True(init.Exit == 0, $"init failed (exit {init.Exit}). stderr: {init.Stderr}\nstdout: {init.Stdout}");

        var add = RunCli("add", "confirm-button", "--local", "--yes", "--force");
        Assert.True(add.Exit == 0, $"add failed (exit {add.Exit}). stderr: {add.Stderr}\nstdout: {add.Stdout}");

        // Re-fetching and re-qualifying upstream for `confirm-button` alone (ignoring that Button
        // is a sibling dependency) must NOT disagree with what `add` just wrote to disk.
        var check = RunCli("update", "confirm-button", "--local", "--check");
        Assert.True(check.Exit == 0,
            $"update --check failed (exit {check.Exit}). stderr: {check.Stderr}\nstdout: {check.Stdout}");
        Assert.Contains("Drift: 0 file(s)", check.Stdout);
        Assert.DoesNotContain("  drift   ", check.Stdout);

        var diff = RunCli("diff", "confirm-button", "--local");
        Assert.True(diff.Exit == 0, $"diff failed (exit {diff.Exit}). stderr: {diff.Stderr}\nstdout: {diff.Stdout}");
        Assert.Contains("0 drifted", diff.Stdout);
    }

    [Fact]
    public void Add_Vendor_Copies_Satellite_Source_And_Its_Wwwroot_Asset()
    {
        Assert.True(File.Exists(_lumeoDll),
            "Built CLI (lumeo.dll) not found under tools/Lumeo.Cli/bin — build the solution first.");

        var init = RunCli("init", "--yes", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets");
        Assert.True(init.Exit == 0, $"init failed (exit {init.Exit}). stderr: {init.Stderr}\nstdout: {init.Stdout}");

        // `chart` lives in the Lumeo.Charts satellite. Without --vendor it routes to
        // NuGet; with --vendor it copies the SOURCE from src/Lumeo.Charts/ AND its
        // wwwroot interop JS (which is NOT in the component's file list).
        var add = RunCli("add", "chart", "--local", "--yes", "--force", "--vendor");
        Assert.True(add.Exit == 0, $"add --vendor failed (exit {add.Exit}). stderr: {add.Stderr}\nstdout: {add.Stdout}");

        // 1) The satellite SOURCE is vendored with its namespace rewritten.
        var razor = Path.Combine(_proj, "Components", "Ui", "Chart", "Chart.razor");
        Assert.True(File.Exists(razor), $"Chart.razor was not vendored to {razor}.\nadd stdout:\n{add.Stdout}");
        var razorContent = File.ReadAllText(razor);
        Assert.Contains("@namespace Acme.Ui", razorContent);

        // 2) The wwwroot interop asset lands under wwwroot/_content/<package>/ so the
        //    component's `./_content/Lumeo.Charts/js/echarts-interop.js` import — which
        //    is intentionally NOT namespace-rewritten — resolves to the copied file.
        var asset = Path.Combine(_proj, "wwwroot", "_content", "Lumeo.Charts", "js", "echarts-interop.js");
        Assert.True(File.Exists(asset), $"satellite interop asset was not vendored to {asset}.\nadd stdout:\n{add.Stdout}");
        Assert.Contains("_content/Lumeo.Charts/js/echarts-interop.js", razorContent);

        // 3) The copied asset is byte-for-byte the upstream source (no rewrite).
        var upstream = Path.Combine(_repoRoot, "src", "Lumeo.Charts", "wwwroot", "js", "echarts-interop.js");
        Assert.Equal(File.ReadAllText(upstream), File.ReadAllText(asset));

        // 4) Icons are first-party now — vendoring chart surfaces NO external icon package
        //    (Chart.razor renders via SvgGlyph/LumeoIcons from the vendored runtime).
        Assert.DoesNotContain("Blazicons", add.Stdout + add.Stderr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Add_Vendor_Aborts_And_Does_Not_Record_Install_When_A_Satellite_Asset_Cannot_Be_Written()
    {
        // Codex P2 — VendorSatelliteAssetsAsync signals a required-asset write failure via
        // Environment.ExitCode, but the caller never checked it: the command fell through to
        // RecordInstall + the normal "OK Added" summary even though _content/<package> was left
        // incomplete, so the vendored component 404s at runtime while lumeo.json (and the CLI's
        // own exit code) claimed a clean install.
        Assert.True(File.Exists(_lumeoDll),
            "Built CLI (lumeo.dll) not found under tools/Lumeo.Cli/bin — build the solution first.");

        var init = RunCli("init", "--yes", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets");
        Assert.True(init.Exit == 0, $"init failed (exit {init.Exit}). stderr: {init.Stderr}\nstdout: {init.Stdout}");

        // code-editor's ONLY wwwroot asset is wwwroot/js/code-editor.js. Pre-create a FILE at the
        // exact path VendorSatelliteAssetsAsync needs to create as a DIRECTORY
        // (wwwroot/_content/Lumeo.CodeEditor), so Directory.CreateDirectory throws — a
        // deterministic, network-free reproduction of a required-asset vendoring failure.
        var blockerPath = Path.Combine(_proj, "wwwroot", "_content", "Lumeo.CodeEditor");
        Directory.CreateDirectory(Path.GetDirectoryName(blockerPath)!);
        File.WriteAllText(blockerPath, "blocker");

        var add = RunCli("add", "code-editor", "--local", "--yes", "--force", "--vendor");

        Assert.NotEqual(0, add.Exit);
        Assert.DoesNotContain("OK Added", add.Stdout);
        var lumeoJson = File.ReadAllText(Path.Combine(_proj, "lumeo.json"));
        Assert.DoesNotContain("code-editor", lumeoJson);
    }

    [Fact]
    public void Add_Vendor_Reports_Failure_And_Skips_The_Success_Path_When_A_Package_Dependency_Cannot_Be_Installed()
    {
        // Codex P2 — the `false` result from EnsureNuGetPackageAsync (dotnet add package
        // genuinely failed) was discarded entirely: the command had already recorded the
        // install and still printed the normal "OK Added" summary, leaving the project with
        // vendored source that won't compile until the missing package is added manually.
        Assert.True(File.Exists(_lumeoDll),
            "Built CLI (lumeo.dll) not found under tools/Lumeo.Cli/bin — build the solution first.");

        // A syntactically-INVALID .csproj so `dotnet add package` fails deterministically
        // (malformed project XML) — no network/NuGet-resolution timing dependency. Created
        // BEFORE init so FindConsumerCsproj locates THIS file (not something further up the tree).
        File.WriteAllText(Path.Combine(_proj, "App.csproj"), "<Project><Broken");

        var init = RunCli("init", "--yes", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets");
        Assert.True(init.Exit == 0, $"init failed (exit {init.Exit}). stderr: {init.Stderr}\nstdout: {init.Stdout}");

        // `rich-text-editor` references the external Mammoth package as a packageDependency, so
        // --vendor shells out to `dotnet add package` — which fails against the broken csproj.
        var add = RunCli("add", "rich-text-editor", "--local", "--yes", "--force", "--vendor");

        Assert.NotEqual(0, add.Exit);
        Assert.DoesNotContain("OK Added", add.Stdout);
    }

    [Fact]
    public void Update_And_Diff_Of_A_Vendored_Satellite_Resolve_The_Right_Package_Root()
    {
        Assert.True(File.Exists(_lumeoDll),
            "Built CLI (lumeo.dll) not found under tools/Lumeo.Cli/bin — build the solution first.");

        var init = RunCli("init", "--yes", "--namespace", "Acme.Ui", "--path", "Components/Ui", "--no-assets");
        Assert.True(init.Exit == 0, $"init failed (exit {init.Exit}). stderr: {init.Stderr}");

        var add = RunCli("add", "chart", "--local", "--yes", "--force", "--vendor");
        Assert.True(add.Exit == 0, $"add --vendor failed (exit {add.Exit}). stderr: {add.Stderr}");

        // update/diff must fetch the satellite source from src/Lumeo.Charts/, NOT
        // src/Lumeo/ — the latter 404s/crashes on every vendored satellite. With the
        // package threaded, --check sees no drift (vendored == namespace-rewritten
        // upstream) and exits 0; the bug manifested as a FileNotFound/crash.
        var update = RunCli("update", "chart", "--local", "--check");
        Assert.True(update.Exit == 0,
            $"update --check on a vendored satellite failed (exit {update.Exit}) — wrong package root?\nstderr: {update.Stderr}\nstdout: {update.Stdout}");
        Assert.DoesNotContain("not found", update.Stderr, StringComparison.OrdinalIgnoreCase);

        var diff = RunCli("diff", "chart", "--local");
        Assert.True(diff.Exit == 0,
            $"diff on a vendored satellite failed (exit {diff.Exit}) — wrong package root?\nstderr: {diff.Stderr}\nstdout: {diff.Stdout}");
        Assert.DoesNotContain("not found", diff.Stderr, StringComparison.OrdinalIgnoreCase);
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
