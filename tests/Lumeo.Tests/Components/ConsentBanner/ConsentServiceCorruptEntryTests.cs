using Xunit;
using Lumeo.Services;
using Microsoft.JSInterop;

namespace Lumeo.Tests.Components.ConsentBanner;

/// <summary>
/// PR #351 round-5, finding 2: a non-empty stored value that is INVALID JSON, or valid JSON that
/// is NOT an object (null / number / string / array), is not a decision — yet the pre-boot guard
/// hid the banner off its mere presence. Previously LoadFromJson threw (invalid JSON) or returned
/// early (non-object) BEFORE the undecided FOUC sync ran, so the banner stayed invisible although
/// undecided. Both corrupt paths now fail CLOSED and still invoke markUndecided so the banner
/// re-appears, and a later PolicyVersion change can't resurrect them.
/// </summary>
public class ConsentServiceCorruptEntryTests
{
    private const string Key = "lumeo:consent:v1";

    [Theory]
    [InlineData("{ not json")]        // invalid JSON — JsonNode.Parse throws
    [InlineData("{\"categories\":")]  // truncated object — invalid JSON
    [InlineData("123")]               // valid JSON, but a number, not an object
    [InlineData("\"hello\"")]         // valid JSON string
    [InlineData("[1,2,3]")]           // valid JSON array
    [InlineData("null")]              // JSON null
    public async Task Corrupt_entry_is_undecided_and_clears_the_fouc_guard(string stored)
    {
        var store = new RecordingJsRuntime();
        store.Set(Key, stored);

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();

        // Fail closed: garbage is not consent — banner must re-prompt.
        Assert.False(svc.HasDecided);
        Assert.False(svc.HasConsent("analytics"));
        Assert.Null(svc.DecisionPolicyVersion);
        // The pre-boot FOUC guard (added for ANY stored entry) is cleared so the banner shows.
        Assert.Equal("lumeoConsent.markUndecided", store.LastFoucCall);
    }

    [Fact]
    public async Task Corrupt_entry_is_not_resurrected_by_a_version_change()
    {
        var store = new RecordingJsRuntime();
        store.Set(Key, "{ not json"); // invalid JSON

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();
        Assert.False(svc.HasDecided);

        var changes = 0;
        svc.OnChange += () => changes++;

        // A rejected corrupt entry has no attributable version; a bump must not flip it to decided.
        svc.PolicyVersion = "2";

        Assert.False(svc.HasDecided);
        Assert.Equal(0, changes);
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
