using System.Text.Json;
using Xunit;

namespace Lumeo.Docs.Tests;

/// <summary>
/// Guards that the registry's <c>hasDocsPage</c> flag never drifts from reality —
/// the catalog hides a card when the flag is false, so a stale flag silently drops
/// a component from navigation (this is exactly how qr-code disappeared: its page
/// is QrCodePage.razor but the flag was computed case-sensitively against "QRCode").
/// </summary>
public class DocsCoverageTests
{
    private static string FindRepoRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Lumeo.slnx"))) return d.FullName;
        throw new InvalidOperationException("Lumeo.slnx not found above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void HasDocsPage_Flag_Matches_A_Real_Page_File_For_Every_Component()
    {
        var repo = FindRepoRoot();
        using var registry = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(repo, "src", "Lumeo", "registry", "registry.json")));

        var pagesDir = Path.Combine(repo, "docs", "Lumeo.Docs", "Pages", "Components");
        var pageFiles = new HashSet<string>(
            Directory.EnumerateFiles(pagesDir, "*Page.razor", SearchOption.AllDirectories)
                .Select(f => Path.GetFileName(f)!),
            StringComparer.OrdinalIgnoreCase);

        var mismatches = new List<string>();
        foreach (var comp in registry.RootElement.GetProperty("components").EnumerateObject())
        {
            var name = comp.Value.GetProperty("name").GetString()!;
            var flag = comp.Value.GetProperty("hasDocsPage").GetBoolean();
            // Mirror the generator's two filename forms (component page + chart page).
            var fileExists = pageFiles.Contains($"{name}Page.razor")
                          || pageFiles.Contains($"{name}ChartPage.razor");
            if (flag != fileExists)
                mismatches.Add($"  {name}: hasDocsPage={flag} but a page file exists={fileExists}");
        }

        Assert.True(mismatches.Count == 0,
            "registry hasDocsPage flags drifted from the actual page files:\n" + string.Join("\n", mismatches));
    }

    [Fact]
    public void Every_Component_Has_A_TestCoverage_Block_With_A_Valid_Tier()
    {
        // The docs surface per-component test coverage (the <TestCoverage> panel + the
        // /test-coverage overview) from this block; guard that RegistryGen emits it for
        // every component with a sane tier so the docs never render a blank panel.
        var repo = FindRepoRoot();
        using var registry = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(repo, "src", "Lumeo", "registry", "registry.json")));

        var bad = new List<string>();
        foreach (var comp in registry.RootElement.GetProperty("components").EnumerateObject())
        {
            var name = comp.Value.GetProperty("name").GetString()!;
            if (!comp.Value.TryGetProperty("testCoverage", out var cov))
            {
                bad.Add($"  {name}: no testCoverage block");
                continue;
            }
            var tier = cov.GetProperty("tier").GetInt32();
            if (tier is < 0 or > 4) bad.Add($"  {name}: tier {tier} out of range");
            // Every component must at least render (dedicated/shared tests or contract smoke).
            if (!cov.GetProperty("render").GetBoolean())
                bad.Add($"  {name}: render=false — not even smoke-covered");
        }

        Assert.True(bad.Count == 0,
            "testCoverage block invalid for some components:\n" + string.Join("\n", bad));
    }

    [Fact]
    public void Every_Component_Has_A_Per_Component_Registry_Json_That_Parses()
    {
        var repo = FindRepoRoot();
        using var registry = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(repo, "src", "Lumeo", "registry", "registry.json")));
        var perComponentDir = Path.Combine(repo, "docs", "Lumeo.Docs", "wwwroot", "registry");

        var missing = new List<string>();
        foreach (var comp in registry.RootElement.GetProperty("components").EnumerateObject())
        {
            var key = comp.Name; // kebab-case registry key
            var json = Path.Combine(perComponentDir, $"{key}.json");
            if (!File.Exists(json)) { missing.Add($"  {key}: no {key}.json"); continue; }
            try { using var _ = JsonDocument.Parse(File.ReadAllText(json)); }
            catch (Exception ex) { missing.Add($"  {key}: {key}.json failed to parse — {ex.Message}"); }
        }

        Assert.True(missing.Count == 0,
            "per-component registry JSON missing or unparseable:\n" + string.Join("\n", missing));
    }
}
