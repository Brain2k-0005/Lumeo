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
}

public sealed class NavConfig
{
    [JsonPropertyName("groups")] public List<NavGroup> Groups { get; set; } = [];
}

public sealed class NavGroup
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("label")] public string Label { get; set; } = "";
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
