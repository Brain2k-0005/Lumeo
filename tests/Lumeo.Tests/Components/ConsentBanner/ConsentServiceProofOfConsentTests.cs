using System.Text.Json.Nodes;
using Xunit;
using Lumeo.Services;
using Microsoft.JSInterop;

namespace Lumeo.Tests.Components.ConsentBanner;

/// <summary>
/// Proof-of-consent record (GDPR Art. 7(1) accountability) for <see cref="ConsentService"/>:
/// the persisted entry carries the per-category choices plus a UTC timestamp and the
/// policy version; legacy bare-map records are read seamlessly and upgraded on the next
/// write; and a policy-version bump re-prompts while preserving the previous choices for
/// prefill. These drive the REAL persistence round-trip through a recording IJSRuntime.
/// </summary>
public class ConsentServiceProofOfConsentTests
{
    private const string Key = "lumeo:consent:v1";

    [Fact]
    public async Task Persisted_Record_Carries_Categories_Timestamp_And_Version()
    {
        var store = new RecordingJsRuntime();
        var svc = new ConsentService(store) { PolicyVersion = "2024-05" };

        var before = DateTimeOffset.UtcNow.AddSeconds(-2);
        await svc.SetManyAsync(new Dictionary<string, bool> { ["analytics"] = true });
        var after = DateTimeOffset.UtcNow.AddSeconds(2);

        var root = JsonNode.Parse(store.Get(Key)!)!.AsObject();

        // categories object records the actual choice…
        Assert.True(root["categories"]!.AsObject()["analytics"]!.GetValue<bool>());
        // …version is the configured policy version…
        Assert.Equal("2024-05", root["version"]!.GetValue<string>());
        // …and timestamp is a real UTC instant inside the write window.
        var ts = DateTimeOffset.Parse(root["timestamp"]!.GetValue<string>());
        Assert.InRange(ts, before, after);

        // Surfaced on the service too, for a consumer's proof-of-consent UI.
        Assert.Equal("2024-05", svc.DecisionPolicyVersion);
        Assert.NotNull(svc.DecidedAtUtc);
    }

    [Fact]
    public async Task Legacy_Bare_Map_Reads_As_Decided_Without_Reprompt()
    {
        var store = new RecordingJsRuntime();
        // A record written by an older Lumeo: a bare category→bool map, no version.
        store.Set(Key, "{\"analytics\":true}");

        var svc = new ConsentService(store) { PolicyVersion = "2024-05" };
        await svc.EnsureLoadedAsync();

        // The old decision still counts (format change alone must not re-prompt)
        // and the stored choice is honoured…
        Assert.True(svc.HasDecided);
        Assert.True(svc.HasConsent("analytics"));
        // …but no policy version can be attributed to a pre-versioning record.
        Assert.Null(svc.DecisionPolicyVersion);
    }

    [Fact]
    public async Task Legacy_Record_Upgrades_To_Record_Shape_On_Next_Write()
    {
        var store = new RecordingJsRuntime();
        store.Set(Key, "{\"analytics\":false}");

        var svc = new ConsentService(store) { PolicyVersion = "7" };
        await svc.EnsureLoadedAsync();

        // A later change migrates the entry to the current record shape.
        await svc.SetManyAsync(new Dictionary<string, bool> { ["analytics"] = true });

        var root = JsonNode.Parse(store.Get(Key)!)!.AsObject();
        Assert.NotNull(root["categories"]);                          // upgraded shape
        Assert.Equal("7", root["version"]!.GetValue<string>());       // stamped with policy version
        Assert.False(string.IsNullOrEmpty(root["timestamp"]!.GetValue<string>()));
        Assert.True(root["categories"]!.AsObject()["analytics"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Version_Mismatch_Reprompts_But_Preserves_Choices_For_Prefill()
    {
        var store = new RecordingJsRuntime();
        // Session 1: decide against policy version "1".
        var first = new ConsentService(store) { PolicyVersion = "1" };
        await first.SetManyAsync(new Dictionary<string, bool> { ["analytics"] = true });

        // Session 2: the app now asks for policy version "2".
        var second = new ConsentService(store) { PolicyVersion = "2" };
        await second.EnsureLoadedAsync();

        // Re-prompt: banner shows again, and consent is fail-closed until re-confirmed…
        Assert.False(second.HasDecided);
        Assert.False(second.HasConsent("analytics"));
        // …while the previous choice is retained so the dialog can prefill it.
        Assert.True(second.Snapshot().TryGetValue("analytics", out var prev) && prev);
    }

    [Fact]
    public async Task Matching_Version_Does_Not_Reprompt()
    {
        var store = new RecordingJsRuntime();
        var first = new ConsentService(store) { PolicyVersion = "2024-05" };
        await first.SetManyAsync(new Dictionary<string, bool> { ["analytics"] = true });

        var second = new ConsentService(store) { PolicyVersion = "2024-05" };
        await second.EnsureLoadedAsync();

        Assert.True(second.HasDecided);
        Assert.True(second.HasConsent("analytics"));
        Assert.Equal("2024-05", second.DecisionPolicyVersion);
    }

    // Minimal IJSRuntime emulating the localStorage get/set/remove calls ConsentService
    // makes, with accessors to seed and read back the raw stored value.
    private sealed class RecordingJsRuntime : IJSRuntime
    {
        private readonly Dictionary<string, string> _storage = new();

        public string? Get(string key) => _storage.TryGetValue(key, out var v) ? v : null;
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
