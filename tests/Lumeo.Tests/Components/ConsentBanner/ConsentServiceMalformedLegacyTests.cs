using Xunit;
using Lumeo.Services;
using Microsoft.JSInterop;

namespace Lumeo.Tests.Components.ConsentBanner;

/// <summary>
/// PR #351 round-2, finding (f): a MALFORMED legacy record (a bare map with any
/// non-boolean property — <c>{"analytics":"true"}</c>, <c>{"foo":"bar"}</c>) must NOT be
/// read as a completed decision. The pre-4.1.1 deserializer rejected such entries; the
/// service now fails CLOSED — a legacy object counts as decided only when EVERY property
/// is a boolean, otherwise the banner re-prompts. Malformed entries also must not be
/// resurrected as "decided" by a later PolicyVersion change.
/// </summary>
public class ConsentServiceMalformedLegacyTests
{
    private const string Key = "lumeo:consent:v1";

    [Fact]
    public async Task Malformed_legacy_string_value_is_not_a_decision_and_reprompts()
    {
        var store = new RecordingJsRuntime();
        store.Set(Key, "{\"analytics\":\"true\"}"); // "true" is a STRING, not a boolean

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();

        // Fail closed: garbage is not consent — re-prompt, and analytics stays denied.
        Assert.False(svc.HasDecided);
        Assert.False(svc.HasConsent("analytics"));
        // The pre-boot FOUC guard (added for ANY stored entry) is cleared so the banner shows.
        Assert.Equal("lumeoConsent.markUndecided", store.LastFoucCall);
    }

    [Fact]
    public async Task Malformed_legacy_non_bool_map_is_not_a_decision()
    {
        var store = new RecordingJsRuntime();
        store.Set(Key, "{\"foo\":\"bar\"}");

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();

        Assert.False(svc.HasDecided);
        Assert.Equal("lumeoConsent.markUndecided", store.LastFoucCall);
    }

    [Fact]
    public async Task Legacy_map_with_one_non_bool_property_is_rejected_whole()
    {
        // A single non-boolean value taints the whole record — it can't be a pre-4.1.1
        // decision, so we don't cherry-pick the valid pairs and call it decided.
        var store = new RecordingJsRuntime();
        store.Set(Key, "{\"analytics\":true,\"marketing\":\"yes\"}");

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();

        Assert.False(svc.HasDecided);
        Assert.False(svc.HasConsent("analytics"));
    }

    [Fact]
    public async Task Valid_legacy_bool_map_still_reads_as_decided()
    {
        // The other way: a genuine pre-4.1.1 pure bool map is still honoured (no re-prompt).
        var store = new RecordingJsRuntime();
        store.Set(Key, "{\"analytics\":true}");

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();

        Assert.True(svc.HasDecided);
        Assert.True(svc.HasConsent("analytics"));
        Assert.Null(store.LastFoucCall); // valid decision → guard untouched, no flash
    }

    [Fact]
    public async Task Rejected_malformed_record_is_not_resurrected_by_a_version_change()
    {
        var store = new RecordingJsRuntime();
        store.Set(Key, "{\"analytics\":\"true\"}");

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();
        Assert.False(svc.HasDecided);

        var changes = 0;
        svc.OnChange += () => changes++;

        // A rejected record has no attributable version; a version bump must not let its
        // null stored version read as a "match" and flip it to decided.
        svc.PolicyVersion = "2";

        Assert.False(svc.HasDecided);
        Assert.Equal(0, changes);
    }

    // IJSRuntime emulating localStorage plus the lumeoConsent FOUC-guard helpers, recording
    // the last markDecided / markUndecided identifier the service invoked.
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
