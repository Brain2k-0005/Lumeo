using Xunit;
using Lumeo.Services;
using Microsoft.JSInterop;

namespace Lumeo.Tests.Components.ConsentBanner;

/// <summary>
/// PR #351 round-3, finding 2: a PROOF-OF-CONSENT record (identified by its <c>categories</c>
/// wrapper — the current format) whose <c>version</c> is MISSING or not a JSON string must
/// fail CLOSED. Previously that left the stored version null, which matched EVERY configured
/// PolicyVersion (fail-open) off a corrupt / hand-tampered record. It now counts as a version
/// MISMATCH: <see cref="ConsentService.HasDecided"/> is false, the banner re-prompts, prior
/// choices are kept for prefill, and a later PolicyVersion change must not resurrect the null
/// version as a "match".
///
/// A genuine LEGACY bare map (no <c>categories</c> wrapper) is the distinct case that
/// legitimately predates versioning and still reads as decided — re-pinned here so the
/// fail-closed rule doesn't over-reach into it.
/// </summary>
public class ConsentServiceVersionlessProofRecordTests
{
    private const string Key = "lumeo:consent:v1";

    private const string Ts = "2024-01-01T00:00:00.0000000+00:00";

    [Fact]
    public async Task Proof_record_missing_version_fails_closed_and_reprompts()
    {
        var store = new RecordingJsRuntime();
        // Current-format record (has "categories") but NO "version" key at all.
        store.Set(Key, $"{{\"categories\":{{\"analytics\":true}},\"timestamp\":\"{Ts}\"}}");

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();

        // Fail closed: not a decision, consent denied until re-confirmed…
        Assert.False(svc.HasDecided);
        Assert.False(svc.HasConsent("analytics"));
        // …the pre-boot FOUC guard is cleared so the CSS-hidden banner actually reappears…
        Assert.Equal("lumeoConsent.markUndecided", store.LastFoucCall);
        // …and the prior choice is retained so the re-prompt dialog can prefill it.
        Assert.True(svc.Snapshot().TryGetValue("analytics", out var prev) && prev);
    }

    [Fact]
    public async Task Proof_record_non_string_version_fails_closed()
    {
        var store = new RecordingJsRuntime();
        // "version" is a NUMBER, not a string — ReadString yields null → treat as a mismatch,
        // even though the numeric 5 "looks like" the configured policy version.
        store.Set(Key, $"{{\"categories\":{{\"analytics\":true}},\"timestamp\":\"{Ts}\",\"version\":5}}");

        var svc = new ConsentService(store) { PolicyVersion = "5" };
        await svc.EnsureLoadedAsync();

        Assert.False(svc.HasDecided);
        Assert.False(svc.HasConsent("analytics"));
        // No policy version can be attributed to a tampered/typeless-version record.
        Assert.Null(svc.DecisionPolicyVersion);
        Assert.Equal("lumeoConsent.markUndecided", store.LastFoucCall);
    }

    [Fact]
    public async Task Versionless_proof_record_is_not_resurrected_by_a_later_version_change()
    {
        var store = new RecordingJsRuntime();
        store.Set(Key, $"{{\"categories\":{{\"analytics\":true}},\"timestamp\":\"{Ts}\"}}");

        var svc = new ConsentService(store) { PolicyVersion = "1" };
        await svc.EnsureLoadedAsync();
        Assert.False(svc.HasDecided);

        var changes = 0;
        svc.OnChange += () => changes++;

        // The rejected record has no attributable version; bumping the policy must not let its
        // null stored version read as a "match" and flip it back to decided.
        svc.PolicyVersion = "2";

        Assert.False(svc.HasDecided);
        Assert.False(svc.HasConsent("analytics"));
        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task Well_formed_versioned_proof_record_still_matches()
    {
        // Regression guard: the fail-closed rule must not break a valid record — a proof record
        // whose string version matches PolicyVersion is still decided, with no re-prompt.
        var store = new RecordingJsRuntime();
        store.Set(Key, $"{{\"categories\":{{\"analytics\":true}},\"timestamp\":\"{Ts}\",\"version\":\"1\"}}");

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
