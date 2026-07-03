using System.Text.Json;
using Lumeo.Docs.Services;
using Xunit;

namespace Lumeo.Docs.Tests;

/// <summary>
/// Guards the data-driven site-wide icon re-skin (<c>DynamicIcon</c> + <see cref="DynamicIconResolver"/>).
/// For EVERY first-party pack class and EVERY semantic name in the docs vocabulary, walks the SAME
/// candidate ladder the runtime resolver uses (<see cref="IconPackMap.Candidates"/> then
/// <see cref="IconPackMap.FallbackCandidates"/>) against that pack's manifest.json name list, and
/// asserts the chosen property name actually exists in the pack. A name that only resolves through
/// the neutral fallback glyph is counted and reported — the ceiling assertion keeps that honest so a
/// future pack/vocabulary change can't silently degrade coverage.
/// </summary>
public class DynamicIconPackCoverageTests
{
    private static string FindRepoRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Lumeo.slnx"))) return d.FullName;
        throw new InvalidOperationException("Lumeo.slnx not found above " + AppContext.BaseDirectory);
    }

    /// <summary>The manifest name list for a pack, mirrored as (exact set, normalized index).</summary>
    private sealed record PackNames(HashSet<string> Exact, Dictionary<string, string> Normalized)
    {
        public bool Has(string candidate) =>
            Exact.Contains(candidate) || Normalized.ContainsKey(IconPackMap.Normalize(candidate));

        public static PackNames Load(string manifestPath)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var icons = doc.RootElement.GetProperty("icons").EnumerateArray()
                .Select(e => e.GetString()!).ToArray();
            var exact = new HashSet<string>(icons, StringComparer.Ordinal);
            var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var n in icons)
            {
                var key = IconPackMap.Normalize(n);
                if (!normalized.ContainsKey(key)) normalized[key] = n;
            }
            return new PackNames(exact, normalized);
        }
    }

    /// <summary>Resolve like the runtime resolver, returning the chosen name and whether it was a fallback.</summary>
    private static (string? Name, bool Fallback) Resolve(PackNames pack, string packKey, string semantic)
    {
        foreach (var cand in IconPackMap.Candidates(packKey, semantic))
        {
            if (pack.Exact.Contains(cand)) return (cand, false);
            if (pack.Normalized.TryGetValue(IconPackMap.Normalize(cand), out var n)) return (n, false);
        }
        foreach (var fb in IconPackMap.FallbackCandidates)
        {
            if (pack.Exact.Contains(fb)) return (fb, true);
            if (pack.Normalized.TryGetValue(IconPackMap.Normalize(fb), out var n)) return (n, true);
        }
        return (null, true);
    }

    [Fact]
    public void Every_Semantic_Resolves_To_A_Real_Manifest_Name_In_Every_First_Party_Pack()
    {
        var iconsDir = Path.Combine(FindRepoRoot(), "docs", "Lumeo.Docs", "wwwroot", "icons");

        var unresolved = new List<string>();          // resolved to a name NOT in the manifest — a bug
        var perPackFallback = new Dictionary<string, int>();
        var totalFallback = 0;

        foreach (var pack in IconPackCatalog.FirstParty)
        {
            var manifestPath = Path.Combine(iconsDir, pack.ManifestFile);
            Assert.True(File.Exists(manifestPath), $"missing manifest for pack '{pack.Key}': {manifestPath}");

            var names = PackNames.Load(manifestPath);
            Assert.True(names.Exact.Count > 0, $"empty manifest for pack '{pack.Key}'");

            var fallbacks = 0;
            foreach (var semantic in IconPackMap.SemanticNames)
            {
                var (name, isFallback) = Resolve(names, pack.Key, semantic);
                if (name is null || !names.Has(name))
                    unresolved.Add($"  {pack.Key}/{semantic} -> {(name ?? "«null»")}");
                if (isFallback) fallbacks++;
            }
            perPackFallback[pack.Key] = fallbacks;
            totalFallback += fallbacks;
        }

        Assert.True(unresolved.Count == 0,
            "resolution produced a name absent from the pack manifest:\n" + string.Join("\n", unresolved));

        // Regression guard: the default (Lucide) + curated Bootstrap re-skins must be gap-free,
        // and total fallbacks across all 25 packs must not drift above the verified baseline.
        Assert.Equal(0, perPackFallback["lucide"]);
        Assert.Equal(0, perPackFallback["bootstrap"]);
        Assert.True(totalFallback <= 210,
            $"icon-pack fallback count regressed to {totalFallback} (baseline 201). Per pack:\n" +
            string.Join("\n", perPackFallback.OrderByDescending(kv => kv.Value).Select(kv => $"  {kv.Key}: {kv.Value}")));
    }

    [Fact]
    public void Catalog_Lists_All_First_Party_Pack_Classes_With_Unique_Keys()
    {
        // 6 Phosphor weights + 2 Tabler + 4 Heroicons + 2 Remix + 6 Material Symbols + 2 Fluent
        // + Lucide + Bootstrap + Iconoir = 25 selectable first-party pack classes.
        Assert.Equal(25, IconPackCatalog.FirstParty.Count);
        var keys = IconPackCatalog.FirstParty.Select(p => p.Key).ToArray();
        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());

        // The vocabulary is non-trivial and every semantic has a canonical Lucide default.
        Assert.True(IconPackMap.SemanticNames.Length >= 180);
        foreach (var s in IconPackMap.SemanticNames)
            Assert.True(IconPackMap.Default.ContainsKey(s), $"semantic '{s}' has no Lucide default");
    }
}
