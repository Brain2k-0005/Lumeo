using System.Text.Json;
using Lumeo.Docs.Services;
using Xunit;

namespace Lumeo.Docs.Tests;

/// <summary>
/// Guards that every per-component registry file (registry/{slug}.json) binds to
/// <see cref="RegistryComponentDetail"/> — the model the data-driven docs pages
/// (ComponentDocPage / PropsTable / FactsRail) render. A silent shape drift here would
/// blank the facts rail and props table on a component page with no build error, so this
/// is the regression guard for the docs-redesign data layer.
/// </summary>
public class RegistryDetailTests
{
    // Same options RegistryService uses.
    private static readonly JsonSerializerOptions Opts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static string FindRepoRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Lumeo.slnx"))) return d.FullName;
        throw new InvalidOperationException("Lumeo.slnx not found above " + AppContext.BaseDirectory);
    }

    private static string RegistryDir() =>
        Path.Combine(FindRepoRoot(), "docs", "Lumeo.Docs", "wwwroot", "registry");

    [Fact]
    public void Button_Json_Binds_With_Its_Facts()
    {
        var json = File.ReadAllText(Path.Combine(RegistryDir(), "button.json"));
        var detail = JsonSerializer.Deserialize<RegistryComponentDetail>(json, Opts);

        Assert.NotNull(detail);
        Assert.Equal("Button", detail!.Name);
        Assert.Equal("Forms", detail.Category);
        Assert.False(string.IsNullOrEmpty(detail.Description));
        Assert.Equal("Lumeo", detail.NugetPackage);
        Assert.NotNull(detail.Api);
        Assert.NotEmpty(detail.Api!.Parameters);
        Assert.Contains(detail.Api.Parameters, p => p.Name == "Variant");
        Assert.NotNull(detail.Api.A11y);
        Assert.NotNull(detail.TestCoverage);
    }

    [Fact]
    public void Every_Per_Component_File_Binds()
    {
        var dir = RegistryDir();
        var failures = new List<string>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            var name = Path.GetFileName(file);
            if (name is "cdn-deps.json") continue; // not a component
            try
            {
                var detail = JsonSerializer.Deserialize<RegistryComponentDetail>(File.ReadAllText(file), Opts);
                if (detail is null || string.IsNullOrEmpty(detail.Name))
                    failures.Add($"  {name}: deserialized null/empty");
            }
            catch (Exception ex)
            {
                failures.Add($"  {name}: {ex.GetType().Name} — {ex.Message}");
            }
        }
        Assert.True(failures.Count == 0,
            "per-component registry files failed to bind to RegistryComponentDetail:\n" + string.Join("\n", failures));
    }
}
