using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lumeo.Docs.Services;

public sealed class NavConfigService(HttpClient http)
{
    private NavConfig? _cached;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<NavConfig> GetAsync()
    {
        if (_cached is not null) return _cached;
        await _gate.WaitAsync();
        try
        {
            _cached ??= await http.GetFromJsonAsync<NavConfig>(
                "Layout/nav-config.json",
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, ReadCommentHandling = JsonCommentHandling.Skip })
                ?? throw new InvalidOperationException("nav-config.json failed to deserialize.");
            return _cached;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Returns a NavConfig containing only groups that match the specified section.</summary>
    public async Task<NavConfig> GetForSectionAsync(string section)
    {
        var full = await GetAsync();
        return new NavConfig
        {
            Groups = full.Groups
                .Where(g => string.Equals(g.Section, section, StringComparison.OrdinalIgnoreCase))
                .ToList()
        };
    }

    /// <summary>All component-section groups — the "categories" shown in the header
    /// Components mega-menu (the library overview that lets the per-page sidebar stay
    /// scoped to a single category without losing the big picture).</summary>
    public async Task<List<NavGroup>> GetComponentCategoriesAsync()
    {
        var full = await GetAsync();
        return full.Groups
            .Where(g => string.Equals(g.Section, "components", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>The component category (nav group) that owns <paramref name="relPath"/> —
    /// e.g. "components/button" → the "Form" group, "components/charts/bar" → "Data Display".
    /// Null if no component group contains it. Drives the per-category component sidebar.</summary>
    public async Task<NavGroup?> GetComponentCategoryAsync(string relPath)
    {
        var full = await GetAsync();
        var p = relPath.Trim('/').ToLowerInvariant();
        return full.Groups.FirstOrDefault(g =>
            string.Equals(g.Section, "components", StringComparison.OrdinalIgnoreCase) && GroupOwns(g, p));
    }

    private static bool GroupOwns(NavGroup g, string relPath)
    {
        bool Match(NavItem i) => i.Href.Trim('/').Equals(relPath, StringComparison.OrdinalIgnoreCase);
        if (g.Items is { } items && items.Any(Match)) return true;
        if (g.Subgroups is { } subs && subs.Any(s => s.Items.Any(Match))) return true;
        return false;
    }
}

public sealed class NavConfig
{
    [JsonPropertyName("groups")] public List<NavGroup> Groups { get; set; } = [];
}

public sealed class NavGroup
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    [JsonPropertyName("section")] public string Section { get; set; } = "docs";
    [JsonPropertyName("items")] public List<NavItem>? Items { get; set; }
    [JsonPropertyName("subgroups")] public List<NavSubgroup>? Subgroups { get; set; }
}

public sealed class NavSubgroup
{
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    [JsonPropertyName("items")] public List<NavItem> Items { get; set; } = [];
}

public sealed class NavItem
{
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    [JsonPropertyName("href")] public string Href { get; set; } = "";
}
