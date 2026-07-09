using Xunit;
using Lumeo.Services;
using Microsoft.JSInterop;

namespace Lumeo.Tests.Components.ConsentBanner;

/// <summary>
/// PR #351 round-4, finding 4: a current-format proof-of-consent record (identified by its
/// "categories" wrapper) whose category map holds a NON-boolean value
/// (<c>{"categories":{"analytics":"true"}}</c>) is malformed. Even with a MATCHING policy
/// version it must not count as decided — <c>ReadBoolMap</c> would silently drop the bad entry
/// and grant/deny consent off a corrupt record. It now fails CLOSED, exactly like the round-2
/// legacy bare-map rule: undecided, no attributable version, and a rejection latch so a later
/// PolicyVersion change can't resurrect it. Tested both ways.
/// </summary>
public class ConsentServiceMalformedProofRecordTests
{
    private const string Key = "lumeo:consent:v1";

    [Fact]
    public async Task Version_matching_proof_record_with_non_boolean_category_is_rejected()
    {
        var store = new RecordingJsRuntime();
        // Current record shape (has "categories"), version MATCHES — but "true" is a STRING.
        store.Set(Key, "{\"categories\":{\"analytics\":\"true\"},\"version\":\"1\",\"timestamp\":\"2024-01-01T00:00:00Z\"}");

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();

        // Fail closed: a matching version must not rescue a corrupt category map.
        Assert.False(svc.HasDecided);
        Assert.False(svc.HasConsent("analytics"));
        Assert.Null(svc.DecisionPolicyVersion);
        // The pre-boot FOUC guard (added for ANY stored entry) is cleared so the banner shows.
        Assert.Equal("lumeoConsent.markUndecided", store.LastFoucCall);
    }

    [Fact]
    public async Task Rejected_proof_record_is_not_resurrected_by_a_version_change()
    {
        var store = new RecordingJsRuntime();
        store.Set(Key, "{\"categories\":{\"analytics\":\"true\"},\"version\":\"1\",\"timestamp\":\"2024-01-01T00:00:00Z\"}");

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();
        Assert.False(svc.HasDecided);

        var changes = 0;
        svc.OnChange += () => changes++;

        // A rejected record has no attributable version; a bump must not flip it to decided.
        svc.PolicyVersion = "2";

        Assert.False(svc.HasDecided);
        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task Version_matching_proof_record_with_boolean_categories_still_decides()
    {
        // The other way: a WELL-FORMED proof record (all-boolean categories) with a matching
        // version is honoured — the new fail-closed gate must not reject valid records.
        var store = new RecordingJsRuntime();
        store.Set(Key, "{\"categories\":{\"analytics\":true},\"version\":\"1\",\"timestamp\":\"2024-01-01T00:00:00Z\"}");

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();

        Assert.True(svc.HasDecided);
        Assert.True(svc.HasConsent("analytics"));
        Assert.Equal("1", svc.DecisionPolicyVersion);
        Assert.Null(store.LastFoucCall); // valid decision → guard untouched, no flash
    }

    // IJSRuntime emulating localStorage plus the lumeoConsent FOUC-guard helpers, recording the
    // last markDecided / markUndecided identifier the service invoked.
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
