using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace Lumeo.Docs.Services;

public sealed class RegistryService(HttpClient http, IJSRuntime js)
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private Registry? _cached;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<Registry> GetAsync()
    {
        if (_cached is not null) return _cached;
        await _gate.WaitAsync();
        try
        {
            if (_cached is not null) return _cached;

            // Prefer registry data inlined into the prerendered catalog HTML.
            // It's available synchronously, so on hydration the catalog renders
            // populated on its first pass instead of flashing a skeleton — which
            // otherwise caused a large layout shift (CLS) when the grid was
            // replaced and re-rendered. Falls back to fetching /registry.json.
            _cached = ReadInline();
            if (_cached is not null) return _cached;

            _cached = await http.GetFromJsonAsync<Registry>("registry.json", JsonOpts)
                ?? throw new InvalidOperationException("registry.json failed to deserialize.");
            Hydrate(_cached);
            return _cached;
        }
        finally { _gate.Release(); }
    }

    private Registry? ReadInline()
    {
        try
        {
            if (js is not IJSInProcessRuntime ip) return null;
            var json = ip.Invoke<string?>("lumeo.readInlineRegistry");
            if (string.IsNullOrWhiteSpace(json)) return null;
            var registry = JsonSerializer.Deserialize<Registry>(json, JsonOpts);
            if (registry is null || registry.Components.Count == 0) return null;
            Hydrate(registry);
            return registry;
        }
        catch
        {
            // Any interop/parse failure: fall back to the HTTP fetch path.
            return null;
        }
    }

    private static void Hydrate(Registry registry)
    {
        foreach (var (key, comp) in registry.Components)
        {
            comp.Slug = key;
        }
    }

    public async Task<Dictionary<string, List<RegistryComponent>>> GroupsByCategoryAsync()
    {
        var registry = await GetAsync();
        // Show every component in the catalog. Undocumented ones (HasDocsPage = false) render
        // with a "Coming soon" badge and no link — handled by CatalogCard. This way the user sees
        // the full library at a glance and never silently loses a component just because its docs
        // page hasn't been written yet.
        return registry.Components.Values
            .GroupBy(c => c.Category)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Name).ToList());
    }

    private readonly Dictionary<string, RegistryComponentDetail?> _detailCache = new();

    /// <summary>
    /// Loads the full per-component facts (api/props, dependencies, a11y, keyboard,
    /// related, css vars, source) from <c>registry/{slug}.json</c>. Cached per slug.
    /// Returns null if the file is missing or fails to parse. This is the single source
    /// of truth that <c>ComponentDocPage</c> / <c>PropsTable</c> / <c>FactsRail</c> render.
    /// </summary>
    public async Task<RegistryComponentDetail?> GetComponentAsync(string slug)
    {
        if (_detailCache.TryGetValue(slug, out var cached)) return cached;
        try
        {
            var detail = await http.GetFromJsonAsync<RegistryComponentDetail>($"registry/{slug}.json", JsonOpts);
            if (detail is not null)
            {
                detail.Slug = slug;
                _detailCache[slug] = detail; // cache successes only, so a transient/prerender failure retries on the client
            }
            return detail;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[registry] detail load failed for '{slug}': {ex.Message}");
            return null;
        }
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
    [JsonPropertyName("testCoverage")] public TestCoverageInfo? TestCoverage { get; set; }
    [JsonIgnore] public string Slug { get; set; } = "";
}

/// <summary>
/// Per-component test-coverage snapshot generated by Lumeo.RegistryGen from the real
/// test sources (bUnit .cs/.razor + the browser E2E suite). Surfaced in the docs so
/// devs can see at a glance what has been battle-tested. See the component page's
/// &lt;TestCoverage&gt; panel and the /test-coverage overview.
/// </summary>
public sealed class TestCoverageInfo
{
    /// <summary>0 = render-smoke only … 4 = real-browser E2E.</summary>
    [JsonPropertyName("tier")] public int Tier { get; set; }
    [JsonPropertyName("files")] public int Files { get; set; }
    [JsonPropertyName("tests")] public int Tests { get; set; }
    [JsonPropertyName("render")] public bool Render { get; set; }
    [JsonPropertyName("behavior")] public bool Behavior { get; set; }
    [JsonPropertyName("a11y")] public bool A11y { get; set; }
    [JsonPropertyName("keyboard")] public bool Keyboard { get; set; }
    [JsonPropertyName("scale")] public bool Scale { get; set; }
    [JsonPropertyName("e2e")] public bool E2e { get; set; }
    /// <summary>"smoke" = covered by the universal render-contract test;
    /// "excluded"/"skipped" = NOT smoke-rendered by it (has its own tests instead).</summary>
    [JsonPropertyName("contract")] public string Contract { get; set; } = "";

    public string TierLabel => Tier switch
    {
        4 => "E2E + a11y",
        3 => "A11y + behavior",
        2 => "Behavior",
        1 => "Render + props",
        _ => "Smoke",
    };
}

/// <summary>
/// The full per-component fact set generated by Lumeo.RegistryGen and shipped at
/// <c>registry/{slug}.json</c>. This is what the new data-driven docs pages render —
/// props, a11y, keyboard, dependencies, css vars, related components, source — so the
/// "facts" live in exactly one place instead of being hand-typed per page. JSON is
/// camelCase (RegistryService.JsonOpts applies the policy), so PascalCase props bind.
/// </summary>
public sealed class RegistryComponentDetail
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string? Subcategory { get; set; }
    public string Description { get; set; } = "";
    public string NugetPackage { get; set; } = "";
    public List<string> Dependencies { get; set; } = new();
    public List<string> PackageDependencies { get; set; } = new();
    public List<string> CssVars { get; set; } = new();
    public List<string> Gotchas { get; set; } = new();
    public List<RelatedComponentRef> RelatedComponents { get; set; } = new();
    public List<KeyboardInteractionInfo> KeyboardInteractions { get; set; } = new();
    public List<SourceFileRef> SourceUrl { get; set; } = new();
    public string? DocsUrl { get; set; }
    public TestCoverageInfo? TestCoverage { get; set; }
    public ApiInfo? Api { get; set; }
    [JsonIgnore] public string Slug { get; set; } = "";
}

public sealed class ApiInfo
{
    public string? Namespace { get; set; }
    public string? InheritsFrom { get; set; }
    public List<ApiParameter> Parameters { get; set; } = new();
    public List<ApiEvent> Events { get; set; } = new();
    public A11yInfo? A11y { get; set; }
    /// <summary>
    /// Sub-component APIs for composite components (e.g. Dialog's DialogTrigger,
    /// DialogContent, DialogHeader, ...), keyed by sub-component name. Sourced from
    /// <c>api.subComponents.&lt;Name&gt;</c> in registry/{slug}.json. Empty for leaf
    /// components. Consumed by &lt;PropsTable SubComponent="..."&gt; to render a
    /// sub-component's own parameter/event table instead of the top-level one.
    /// </summary>
    public Dictionary<string, SubComponentInfo> SubComponents { get; set; } = new();
}

/// <summary>
/// One sub-component's API facts (e.g. DialogTrigger, SelectItem), mirroring the shape
/// of <see cref="ApiInfo"/>'s parameters/events but scoped to that sub-component's own
/// members. See <c>ApiInfo.SubComponents</c>.
/// </summary>
public sealed class SubComponentInfo
{
    public string ComponentName { get; set; } = "";
    public List<ApiParameter> Parameters { get; set; } = new();
    public List<ApiEvent> Events { get; set; } = new();
}

public sealed class ApiParameter
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Default { get; set; }
    public string? Description { get; set; }
    public bool IsCascading { get; set; }
    public bool CaptureUnmatched { get; set; }
    public bool IsEditorRequired { get; set; }
}

public sealed class ApiEvent
{
    public string Name { get; set; } = "";
    public string? Type { get; set; }
    public string? Description { get; set; }
}

public sealed class A11yInfo
{
    public List<string> Roles { get; set; } = new();
    public List<string> AriaAttributes { get; set; } = new();
    public List<string> Keys { get; set; } = new();
    public bool KeyboardInteractive { get; set; }
    public bool FocusManaged { get; set; }
}

public sealed class RelatedComponentRef
{
    public string Name { get; set; } = "";
    public string? Reason { get; set; }
}

public sealed class KeyboardInteractionInfo
{
    public string? Key { get; set; }
    public string? Action { get; set; }
}

public sealed class SourceFileRef
{
    public string? File { get; set; }
    public string? Url { get; set; }
}
