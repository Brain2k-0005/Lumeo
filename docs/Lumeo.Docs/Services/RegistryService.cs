using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lumeo.Docs.Services;

public sealed class RegistryService(HttpClient http)
{
    private Registry? _cached;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<Registry> GetAsync()
    {
        if (_cached is not null) return _cached;
        await _gate.WaitAsync();
        try
        {
            _cached ??= await http.GetFromJsonAsync<Registry>(
                "registry.json",
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                ?? throw new InvalidOperationException("registry.json failed to deserialize.");
            foreach (var (key, comp) in _cached.Components)
            {
                comp.Slug = key;
            }
            return _cached;
        }
        finally { _gate.Release(); }
    }

    public async Task<Dictionary<string, List<RegistryComponent>>> GroupsByCategoryAsync()
    {
        var registry = await GetAsync();
        // Skip components without a docs page so the catalog never shows a card that 404s on click.
        // Components in registry without a docs page are real (e.g. AgentMessageList, BorderBeam, BlurFade)
        // but undocumented — they'll appear once their pages are written.
        return registry.Components.Values
            .Where(c => c.HasDocsPage)
            .GroupBy(c => c.Category)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Name).ToList());
    }
}

public sealed class Registry
{
    [JsonPropertyName("components")] public Dictionary<string, RegistryComponent> Components { get; set; } = new();
}

public sealed class RegistryComponent
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("subcategory")] public string? Subcategory { get; set; }
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("thumbnail")] public string? Thumbnail { get; set; }
    [JsonPropertyName("nugetPackage")] public string NugetPackage { get; set; } = "";
    [JsonPropertyName("hasDocsPage")] public bool HasDocsPage { get; set; }
    [JsonIgnore] public string Slug { get; set; } = "";
}
