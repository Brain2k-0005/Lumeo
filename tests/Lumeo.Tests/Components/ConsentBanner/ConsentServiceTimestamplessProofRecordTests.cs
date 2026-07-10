using Xunit;
using Lumeo.Services;
using Microsoft.JSInterop;

namespace Lumeo.Tests.Components.ConsentBanner;

/// <summary>
/// PR #351 round-6, finding 1: a PROOF-OF-CONSENT record (identified by its <c>categories</c>
/// wrapper) must prove WHEN consent was given. A MISSING, non-string, or unparseable
/// <c>timestamp</c> leaves <see cref="ConsentService.DecidedAtUtc"/> null — the record cannot
/// substantiate the moment of consent (GDPR Art. 7(1)). Previously a matching version still
/// counted such a record as decided. It now fails CLOSED exactly like a bad version / category
/// map: undecided, no attributable version, rejection latch, and the pre-boot FOUC guard cleared
/// so the banner re-prompts.
///
/// Migration guard: records WE write always carry a round-trippable "O" timestamp, and a genuine
/// LEGACY bare map (no <c>categories</c> wrapper) legitimately predates the timestamp field and
/// still reads as decided — so nobody loops.
/// </summary>
public class ConsentServiceTimestamplessProofRecordTests
{
    private const string Key = "lumeo:consent:v1";

    [Fact]
    public async Task Proof_record_missing_timestamp_fails_closed_and_reprompts()
    {
        var store = new RecordingJsRuntime();
        // Current-format record (has "categories"), version MATCHES — but there is NO "timestamp".
        store.Set(Key, "{\"categories\":{\"analytics\":true},\"version\":\"1\"}");

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();

        Assert.False(svc.HasDecided);
        Assert.False(svc.HasConsent("analytics"));
        Assert.Null(svc.DecidedAtUtc);
        Assert.Null(svc.DecisionPolicyVersion);
        Assert.Equal("lumeoConsent.markUndecided", store.LastFoucCall);
    }

    [Fact]
    public async Task Proof_record_non_string_timestamp_fails_closed()
    {
        var store = new RecordingJsRuntime();
        // "timestamp" is a NUMBER, not a string → ParseTimestamp yields null → reject.
        store.Set(Key, "{\"categories\":{\"analytics\":true},\"version\":\"1\",\"timestamp\":123}");

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();

        Assert.False(svc.HasDecided);
        Assert.False(svc.HasConsent("analytics"));
        Assert.Null(svc.DecidedAtUtc);
        Assert.Equal("lumeoConsent.markUndecided", store.LastFoucCall);
    }

    [Fact]
    public async Task Proof_record_garbage_string_timestamp_fails_closed()
    {
        var store = new RecordingJsRuntime();
        // "timestamp" is a string but not a parseable date → reject.
        store.Set(Key, "{\"categories\":{\"analytics\":true},\"version\":\"1\",\"timestamp\":\"not-a-date\"}");

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();

        Assert.False(svc.HasDecided);
        Assert.False(svc.HasConsent("analytics"));
        Assert.Null(svc.DecidedAtUtc);
        Assert.Equal("lumeoConsent.markUndecided", store.LastFoucCall);
    }

    [Fact]
    public async Task Timestampless_proof_record_is_not_resurrected_by_a_later_version_change()
    {
        var store = new RecordingJsRuntime();
        store.Set(Key, "{\"categories\":{\"analytics\":true},\"version\":\"1\",\"timestamp\":\"not-a-date\"}");

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();
        Assert.False(svc.HasDecided);

        var changes = 0;
        svc.OnChange += () => changes++;

        // The rejected record has no attributable version; bumping the policy must not flip it.
        svc.PolicyVersion = "2";

        Assert.False(svc.HasDecided);
        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task Well_formed_record_with_valid_timestamp_still_decides()
    {
        // Regression guard: the new timestamp gate must not reject a valid proof record.
        var store = new RecordingJsRuntime();
        store.Set(Key, "{\"categories\":{\"analytics\":true},\"version\":\"1\",\"timestamp\":\"2024-01-01T00:00:00.0000000+00:00\"}");

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();

        Assert.True(svc.HasDecided);
        Assert.True(svc.HasConsent("analytics"));
        Assert.Equal("1", svc.DecisionPolicyVersion);
        Assert.NotNull(svc.DecidedAtUtc);
        Assert.Null(store.LastFoucCall); // valid decision → guard untouched, no flash
    }

    [Fact]
    public async Task Legacy_bare_map_without_timestamp_still_decides_and_does_not_loop()
    {
        // Migration path: a genuine legacy bare map (no "categories" wrapper) has no timestamp by
        // design. The timestamp gate applies ONLY to the current record shape, so the bare map is
        // still a decision — it must NOT re-prompt (which would loop a returning user forever).
        var store = new RecordingJsRuntime();
        store.Set(Key, "{\"analytics\":true}");

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();

        Assert.True(svc.HasDecided);
        Assert.True(svc.HasConsent("analytics"));
        Assert.Null(svc.DecidedAtUtc);     // no timestamp known for a legacy record
        Assert.Null(store.LastFoucCall);   // decided → guard untouched, banner stays hidden
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
