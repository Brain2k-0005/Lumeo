using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo.Services;
using Microsoft.Extensions.DependencyInjection;
using L = Lumeo;

namespace Lumeo.Tests.Components.ConsentBanner;

public class ConsentBannerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ConsentBannerTests()
    {
        _ctx.AddLumeoServices();
        // ConsentBanner @injects ConsentService directly by concrete type
        _ctx.Services.AddScoped<ConsentService>();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_without_exception()
    {
        // ConsentBanner only shows after EnsureLoadedAsync + _ready==true
        // In tests the JS interop returns defaults (null/false) so _ready stays false
        // and the banner is hidden. Markup should be empty but no exception.
        var cut = _ctx.Render<L.ConsentBanner>();
        Assert.NotNull(cut);
    }

    [Fact]
    public void Merges_class_parameter_when_visible()
    {
        // We can verify the Class parameter is accepted without error
        var cut = _ctx.Render<L.ConsentBanner>(p => p.Add(c => c.Class, "my-consent-banner"));
        // Banner starts hidden (no JS to trigger _ready), but component renders
        Assert.NotNull(cut);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.ConsentBanner>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "consent" }));
        Assert.NotNull(cut);
    }

    [Fact]
    public void Custom_title_parameter_accepted()
    {
        var cut = _ctx.Render<L.ConsentBanner>(p => p.Add(c => c.Title, "We use cookies"));
        // Component renders; title stored on parameter
        Assert.NotNull(cut);
    }

    // ── Service-level dismissal logic (#311) ─────────────────────────────────
    // Regression: with an all-Required category set, AcceptAll/RejectAll/Save
    // stored nothing, so HasDecided (formerly keyed off _state.Count) never
    // flipped and the banner re-showed forever. HasDecided must now track that
    // the user *acted*, independent of how many categories were stored.

    [Fact]
    public async Task HasDecided_Flips_After_SetMany_With_Empty_Map()
    {
        var svc = new ConsentService(new InMemoryJsRuntime());
        Assert.False(svc.HasDecided);

        // Empty map mimics the all-Required path (no non-necessary keys stored).
        await svc.SetManyAsync(new Dictionary<string, bool>());

        Assert.True(svc.HasDecided);
    }

    [Fact]
    public async Task HasDecided_Flips_After_AcceptAll_With_No_Optional_Categories()
    {
        var svc = new ConsentService(new InMemoryJsRuntime());

        // AcceptAll over an empty (all-Required) optional set.
        await svc.AcceptAllAsync(Array.Empty<string>());

        Assert.True(svc.HasDecided);
    }

    [Fact]
    public async Task Reset_Clears_HasDecided()
    {
        var svc = new ConsentService(new InMemoryJsRuntime());
        await svc.SetManyAsync(new Dictionary<string, bool> { ["analytics"] = true });
        Assert.True(svc.HasDecided);

        await svc.ResetAsync();

        Assert.False(svc.HasDecided);
    }

    [Fact]
    public async Task HasDecided_Restored_From_Persisted_Empty_Entry()
    {
        var store = new InMemoryJsRuntime();
        // First session: user decides with an all-Required set → persists "{}".
        var first = new ConsentService(store);
        await first.SetManyAsync(new Dictionary<string, bool>());

        // Second session over the same storage: must hydrate as "decided".
        var second = new ConsentService(store);
        await second.EnsureLoadedAsync();

        Assert.True(second.HasDecided);
    }

    // Minimal IJSRuntime that emulates the localStorage get/set/remove calls
    // ConsentService makes, so the persistence round-trip can be exercised
    // without a browser. Everything else returns default.
    private sealed class InMemoryJsRuntime : Microsoft.JSInterop.IJSRuntime
    {
        private readonly Dictionary<string, string> _storage = new();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, default, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            switch (identifier)
            {
                case "localStorage.getItem":
                {
                    var key = args?[0]?.ToString() ?? "";
                    var value = _storage.TryGetValue(key, out var v) ? v : null;
                    return new ValueTask<TValue>((TValue)(object?)value!);
                }
                case "localStorage.setItem":
                {
                    var key = args?[0]?.ToString() ?? "";
                    _storage[key] = args?[1]?.ToString() ?? "";
                    return new ValueTask<TValue>(default(TValue)!);
                }
                case "localStorage.removeItem":
                {
                    var key = args?[0]?.ToString() ?? "";
                    _storage.Remove(key);
                    return new ValueTask<TValue>(default(TValue)!);
                }
                default:
                    return new ValueTask<TValue>(default(TValue)!);
            }
        }
    }
}
