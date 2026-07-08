using Xunit;
using Lumeo.Services;
using Microsoft.JSInterop;

namespace Lumeo.Tests.Components.ConsentBanner;

/// <summary>
/// PR #351 hardening: a <see cref="ConsentService.PolicyVersion"/> change applied AFTER a
/// decision has already been hydrated must re-evaluate that decision's validity (finding 1
/// — stale consent must not stay valid against a superseded policy version), and a re-prompt
/// determined by the service must clear the pre-boot FOUC guard class via interop so the
/// CSS-hidden banner actually reappears (finding 2). Both drive the real service through a
/// recording IJSRuntime that captures the localStorage + FOUC-guard interop calls.
/// </summary>
public class ConsentServicePolicyVersionTests
{
    private const string Key = "lumeo:consent:v1";

    // A proof-of-consent record for a given policy version with analytics granted.
    private static string RecordFor(string version)
        => $"{{\"categories\":{{\"analytics\":true}},\"timestamp\":\"2024-01-01T00:00:00.0000000+00:00\",\"version\":\"{version}\"}}";

    [Fact]
    public async Task PolicyVersion_bumped_after_load_invalidates_stale_decision_and_reprompts()
    {
        var store = new RecordingJsRuntime();
        store.Set(Key, RecordFor("1"));

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();

        // Baseline: decision matches the active version, so consent is live.
        Assert.True(svc.HasDecided);
        Assert.True(svc.HasConsent("analytics"));

        var changes = 0;
        svc.OnChange += () => changes++;

        // The app now asks for a newer policy version AFTER the service already hydrated.
        svc.PolicyVersion = "2";

        // The stale decision is invalidated: banner must re-prompt, consent fail-closed.
        Assert.False(svc.HasDecided);
        Assert.False(svc.HasConsent("analytics"));
        Assert.Equal(1, changes);
        // …and the FOUC guard is cleared so the (CSS force-hidden) banner reappears.
        Assert.Equal("lumeoConsent.markUndecided", store.LastFoucCall);
        // Previous choice is retained for prefill.
        Assert.True(svc.Snapshot().TryGetValue("analytics", out var prev) && prev);
    }

    [Fact]
    public async Task PolicyVersion_restored_revalidates_decision_and_reinstates_guard()
    {
        var store = new RecordingJsRuntime();
        store.Set(Key, RecordFor("1"));

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();

        svc.PolicyVersion = "2";          // invalidate
        Assert.False(svc.HasDecided);

        var changes = 0;
        svc.OnChange += () => changes++;

        svc.PolicyVersion = "1";          // matches the stored record again

        Assert.True(svc.HasDecided);
        Assert.True(svc.HasConsent("analytics"));
        Assert.Equal(1, changes);
        Assert.Equal("lumeoConsent.markDecided", store.LastFoucCall);
    }

    [Fact]
    public void PolicyVersion_change_with_no_stored_record_does_not_derive_a_phantom_decision()
    {
        var store = new RecordingJsRuntime();   // empty — nothing ever decided
        var svc = new ConsentService(store) { PolicyVersion = "1" };

        var changes = 0;
        svc.OnChange += () => changes++;

        svc.PolicyVersion = "2";

        // No stored decision, so nothing to (re)validate — the banner still shows.
        Assert.False(svc.HasDecided);
        Assert.Equal(0, changes);
        Assert.Null(store.LastFoucCall);
    }

    [Fact]
    public async Task Unchanged_PolicyVersion_assignment_is_a_noop()
    {
        var store = new RecordingJsRuntime();
        store.Set(Key, RecordFor("1"));

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();

        var changes = 0;
        svc.OnChange += () => changes++;

        svc.PolicyVersion = "1";   // same value — must not re-evaluate or notify

        Assert.Equal(0, changes);
        Assert.True(svc.HasDecided);
    }

    [Fact]
    public async Task Version_mismatch_on_load_clears_FOUC_guard_so_banner_reappears()
    {
        // The pre-boot guard in index.html adds html.lumeo-consent-decided for ANY stored
        // entry; on a version mismatch the service must remove it during hydration.
        var store = new RecordingJsRuntime();
        store.Set(Key, RecordFor("1"));

        var svc = new ConsentService(store) { PolicyVersion = "2" };
        await svc.EnsureLoadedAsync();

        Assert.False(svc.HasDecided);
        Assert.Equal("lumeoConsent.markUndecided", store.LastFoucCall);
    }

    [Fact]
    public async Task Matching_version_on_load_does_not_clear_the_guard()
    {
        var store = new RecordingJsRuntime();
        store.Set(Key, RecordFor("1"));

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();

        // Decision is valid — the guard set by the pre-boot script stays untouched
        // (no interop clearing it), so decided users see no banner flash.
        Assert.True(svc.HasDecided);
        Assert.Null(store.LastFoucCall);
    }

    // IJSRuntime emulating localStorage get/set/remove plus the lumeoConsent FOUC-guard
    // helpers, recording the last markDecided / markUndecided identifier the service called.
    private sealed class RecordingJsRuntime : IJSRuntime
    {
        private readonly Dictionary<string, string> _storage = new();
        public string? LastFoucCall { get; private set; }

        public void Set(string key, string value) => _storage[key] = value;

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
                    break;
                }
                case "localStorage.removeItem":
                {
                    var key = args?[0]?.ToString() ?? "";
                    _storage.Remove(key);
                    break;
                }
                case "lumeoConsent.markDecided":
                case "lumeoConsent.markUndecided":
                    LastFoucCall = identifier;
                    break;
            }
            return new ValueTask<TValue>(default(TValue)!);
        }
    }
}
