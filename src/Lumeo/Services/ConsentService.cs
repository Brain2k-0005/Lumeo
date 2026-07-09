using Microsoft.JSInterop;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Lumeo.Services;

/// <summary>
/// Cookie / tracking consent manager. Persists the user's per-category choices
/// in <c>localStorage</c> so the banner doesn't re-appear on every page load,
/// and exposes a reactive API so consumers can gate scripts / analytics on it.
///
/// The persisted entry is a proof-of-consent record —
/// <c>{ "categories": { … }, "timestamp": "&lt;UTC ISO 8601&gt;", "version": "&lt;policy&gt;" }</c>
/// — so you can demonstrate <em>when</em> consent was given and <em>which</em>
/// privacy-policy version it covered (GDPR Art. 7(1) accountability). Records
/// written by older Lumeo versions (a bare <c>{ "analytics": true }</c> map) are
/// read transparently and upgraded to the record shape on the next write — no
/// re-prompt is triggered by the format change alone.
///
/// Typical wiring:
/// <code>
/// // Program.cs
/// builder.Services.AddLumeo();           // includes ConsentService
///
/// // MainLayout.razor
/// &lt;ConsentBanner PrivacyPolicyUrl="/privacy" PolicyVersion="2024-05" /&gt;
///
/// // Anywhere you load a tracker
/// @inject ConsentService Consent
/// @if (Consent.HasConsent("analytics"))
/// {
///     &lt;script defer src="..." data-cf-beacon='{"token":"..."}'&gt;&lt;/script&gt;
/// }
/// </code>
/// </summary>
public sealed class ConsentService
{
    private const string StorageKey = "lumeo:consent:v1";
    private readonly IJSRuntime _js;
    private Dictionary<string, bool> _state = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;
    // Tracks whether the user has explicitly answered the banner, independent
    // of how many non-necessary categories ended up stored. Without this,
    // HasDecided keyed off _state.Count alone, so an all-Required category set
    // (where SetManyAsync stores nothing — required keys are either "necessary"
    // and skipped, or simply not toggleable) left HasDecided false forever and
    // the banner re-showed on every load. Any persisted decision sets this; a
    // present storage entry restores it on load; ResetAsync clears it.
    private bool _decided;

    // Proof-of-consent metadata mirrored from the persisted record. Timestamp is
    // the moment of the last write (UTC); version is the policy version that was
    // in effect. Both are null until a decision is loaded or made. On a legacy
    // (versionless) record the version stays null — we can't attribute a policy
    // version to a decision made before versioning existed.
    private DateTimeOffset? _decidedAtUtc;
    private string? _storedVersion;

    // True once a persisted entry (record OR legacy bare map) has been hydrated.
    // Distinguishes "nothing stored yet, banner should show" from "a decision
    // exists whose validity depends on the policy version" — only the latter is
    // re-evaluated when PolicyVersion changes (see the property setter).
    private bool _hasStoredRecord;

    // A stored entry existed but was MALFORMED (a legacy bare map with a non-boolean
    // property — the pre-4.1.1 deserializer rejected these). We hydrated it far enough
    // to clear the pre-boot FOUC guard and re-prompt, but it is NOT a decision: a later
    // PolicyVersion change must never resurrect it as "decided" (only a fresh write can).
    private bool _decisionRejected;

    public ConsentService(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>Fires whenever any category's consent value changes.</summary>
    public event Action? OnChange;

    /// <summary>Fires when a "Manage cookie preferences" link asks the banner to reopen its dialog.</summary>
    public event Action? OnRequestOpenPreferences;

    /// <summary>Ask any subscribed `ConsentBanner` to pop its preferences dialog. Safe to call even if the banner isn't mounted — it's a no-op then.</summary>
    public void RequestOpenPreferences() => OnRequestOpenPreferences?.Invoke();

    /// <summary>
    /// The privacy-policy version this app is currently asking consent for. When a
    /// stored decision carries a <em>different</em> (explicit) version, the user is
    /// re-prompted so consent is re-confirmed against the new policy — their previous
    /// choices prefill the dialog. Set from <c>&lt;ConsentBanner PolicyVersion="…"/&gt;</c>
    /// (or assign directly). Defaults to <c>"1"</c>; a versionless legacy record never
    /// forces a re-prompt on its own.
    ///
    /// Assigning this AFTER a decision has been hydrated re-evaluates that decision's
    /// validity immediately: if the stored version no longer matches, the banner
    /// re-prompts (and the pre-boot FOUC guard is cleared so the banner is visible).
    /// </summary>
    public string PolicyVersion
    {
        get => _policyVersion;
        set
        {
            // Idempotent: an unchanged assignment (the common re-render case) is a no-op.
            if (string.Equals(_policyVersion, value, StringComparison.Ordinal)) return;
            _policyVersion = value;
            // Before hydration there is no decision to revalidate — LoadFromJson will
            // read the latest _policyVersion when it runs. Once a decision is loaded,
            // a version change here must recompute validity now; otherwise stale
            // consent stays valid against the wrong (superseded) policy version.
            if (_loaded) ReevaluateDecision();
        }
    }
    private string _policyVersion = "1";

    /// <summary>
    /// True once the user has answered the banner (accepted, rejected, or
    /// customized) <em>for the current <see cref="PolicyVersion"/></em>. While false,
    /// the banner should be visible.
    /// </summary>
    public bool HasDecided => _decided;

    /// <summary>UTC timestamp of the last recorded decision, or null if none is stored yet.</summary>
    public DateTimeOffset? DecidedAtUtc => _decidedAtUtc;

    /// <summary>Policy version the last recorded decision was given against. Null for a
    /// versionless legacy record or before any decision.</summary>
    public string? DecisionPolicyVersion => _storedVersion;

    /// <summary>
    /// Has the user granted consent for this category? Unknown categories
    /// return false (fail-closed). "necessary" always returns true. Returns false
    /// while a decision is pending (banner showing) — including a version-mismatch
    /// re-prompt — so stale consent from a superseded policy version never keeps a
    /// tracker running before the user re-confirms.
    /// </summary>
    public bool HasConsent(string category)
    {
        if (string.Equals(category, "necessary", StringComparison.OrdinalIgnoreCase))
            return true;

        return _decided && _state.TryGetValue(category, out var v) && v;
    }

    /// <summary>
    /// Hydrate from <c>localStorage</c>. Safe to call repeatedly; only hits JS
    /// once per service lifetime. Idempotent — no-op if already loaded or if
    /// the storage entry is missing/invalid.
    /// </summary>
    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (string.IsNullOrWhiteSpace(json)) return;
            LoadFromJson(json);

            // A hydrated decision whose stored version no longer matches PolicyVersion
            // must re-prompt — but the pre-boot FOUC guard already added
            // html.lumeo-consent-decided off the mere presence of the stored entry,
            // and CSS force-hides the banner. Clear the class so the re-prompt is
            // actually visible. (Decided/valid records keep the class → no FOUC.)
            if (_hasStoredRecord && !_decided)
                await SyncFoucGuardAsync();
        }
        catch (JSException) { /* localStorage blocked (cookies disabled) — treat as no decision yet */ }
        catch (JsonException) { /* corrupt entry — ignore */ }
        catch (InvalidOperationException) { /* prerendering, IJSRuntime not ready yet */ }
    }

    // Parses either the current proof-of-consent record shape or the legacy bare
    // category map. Populates _state (always, so a version-mismatch re-prompt can
    // prefill the dialog) and decides whether the stored decision still counts.
    private void LoadFromJson(string json)
    {
        // A non-empty entry existed — the pre-boot guard already added html.lumeo-consent-decided
        // (and CSS force-hid the banner) off its mere presence. Mark it stored NOW so EVERY path
        // below — including the corrupt ones — leaves _hasStoredRecord true and EnsureLoadedAsync
        // re-syncs the FOUC guard: a rejected/undecided entry then re-shows the banner
        // (markUndecided) instead of staying invisible.
        _hasStoredRecord = true;

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            // Invalid JSON in storage. Not a decision, but a stored entry the pre-boot guard hid
            // the banner for — fail CLOSED and re-prompt (RejectStoredRecord latches _decisionRejected
            // so a later PolicyVersion change can't resurrect it). Handled here (not just in
            // EnsureLoadedAsync's catch) so the markUndecided FOUC sync still runs.
            RejectStoredRecord();
            return;
        }

        // Parsed, but NOT a JSON object (null / number / string / array). Same as invalid JSON: a
        // stored entry the guard hid the banner for, but no decision — fail closed and re-prompt.
        if (root is null)
        {
            RejectStoredRecord();
            return;
        }

        // New format is uniquely identified by a "categories" object — a legacy
        // map only ever holds string→bool pairs, so it can never have an OBJECT
        // under that key even if a category were literally named "categories".
        if (root["categories"] is JsonObject categories)
        {
            // Fail CLOSED on a malformed category map, exactly like the legacy bare-map rule in
            // the else branch below. A proof-of-consent record whose "categories" holds a
            // NON-boolean value ({"categories":{"analytics":"true"}}) is corrupt: ReadBoolMap
            // would silently drop the bad entry, and a matching "version" would then STILL count
            // the record as decided — granting/denying consent off garbage. Reject it instead
            // (undecided, no attributable version, rejection latch) so a later PolicyVersion
            // change can't resurrect it. _hasStoredRecord stays true, so EnsureLoadedAsync still
            // clears the pre-boot FOUC guard and the banner reappears.
            if (!IsAllBoolean(categories))
            {
                RejectStoredRecord();
                return;
            }

            _state = ReadBoolMap(categories);
            _storedVersion = ReadString(root["version"]);
            _decidedAtUtc = ParseTimestamp(root["timestamp"]);

            // A proof-of-consent (current-format) record — identified by its "categories"
            // wrapper — MUST carry an explicit STRING version. A missing or non-string
            // "version" yields _storedVersion == null; that is NOT a version-agnostic pass.
            // Treating it as one fails OPEN — it would match EVERY configured PolicyVersion
            // off a corrupt or hand-tampered record. Fail CLOSED instead: count it as a
            // version MISMATCH — keep the parsed choices for prefill and re-prompt, and mark
            // the record rejected so a later PolicyVersion change can't resurrect its null
            // version as a "match" (ReevaluateDecision skips rejected records).
            //
            // This is the OPPOSITE of a legacy BARE map (else-if below): that shape has no
            // "categories" wrapper, legitimately predates versioning, and stays decided with
            // a null version. Only the new record shape is held to the version requirement.
            if (_storedVersion is null)
            {
                _decided = false;
                _decisionRejected = true;
            }
            else
            {
                // A versioned record whose version no longer matches means the policy changed
                // since consent was given: keep the old choices (for prefill), re-confirm.
                _decided = string.Equals(_storedVersion, PolicyVersion, StringComparison.Ordinal);
            }
        }
        else if (IsAllBoolean(root))
        {
            // Legacy bare map: a persisted decision existed, so the user HAS decided.
            // The format change alone must never re-prompt. Upgraded to the record
            // shape on the next PersistAsync. No version/timestamp is known.
            // Gate: EVERY property must be a boolean — a bare map is only a legit
            // pre-4.1.1 decision when it is a pure category→bool map (an empty {} counts,
            // vacuously). Anything else falls through to the fail-closed branch below.
            _state = ReadBoolMap(root);
            _storedVersion = null;
            _decidedAtUtc = null;
            _decided = true;
        }
        else
        {
            // Fail CLOSED: a malformed legacy object ({"analytics":"true"}, {"foo":"bar"})
            // has a non-boolean property. The pre-4.1.1 deserializer rejected such entries,
            // so treating them as a completed decision would silently grant/deny consent off
            // garbage. Re-prompt instead: not decided, no attributable version. _hasStoredRecord
            // stays true so EnsureLoadedAsync clears the pre-boot FOUC guard and the banner
            // reappears; _decisionRejected keeps a later version change from resurrecting it.
            RejectStoredRecord();
        }
    }

    // Fail-closed reset shared by every malformed-record branch in LoadFromJson: a corrupt stored
    // entry is NOT a decision (undecided, no attributable version) and latches _decisionRejected
    // so a later PolicyVersion change can't resurrect its null version as a "match"
    // (ReevaluateDecision skips rejected records). _hasStoredRecord is intentionally left as-is —
    // EnsureLoadedAsync still clears the pre-boot FOUC guard so the banner re-appears.
    private void RejectStoredRecord()
    {
        _state = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        _storedVersion = null;
        _decidedAtUtc = null;
        _decided = false;
        _decisionRejected = true;
    }

    // A legacy bare map is a valid decision only if every property is a JSON boolean.
    // A non-boolean value (string "true", number, nested object/array) marks the record
    // malformed → fail closed. Vacuously true for an empty object.
    private static bool IsAllBoolean(JsonObject obj)
    {
        foreach (var (_, value) in obj)
            if (value is not JsonValue jv || !jv.TryGetValue<bool>(out _))
                return false;
        return true;
    }

    // Recompute whether the currently-hydrated decision still counts under the
    // active PolicyVersion. Only meaningful once a stored decision has been loaded
    // (with nothing stored there is no decision to revalidate — the banner shows).
    // Mirrors LoadFromJson's decision rule exactly. Idempotent: a no-op unless
    // _decided actually flips; when it flips we notify subscribers and re-sync the
    // pre-boot FOUC guard so a freshly-invalidated decision re-shows the (CSS-hidden)
    // banner — and a re-validated one hides it again without a flash.
    private void ReevaluateDecision()
    {
        // A rejected (malformed) record is never a decision — a version change must not
        // flip it to "decided" (its null _storedVersion would otherwise read as a match).
        if (!_hasStoredRecord || _decisionRejected) return;
        var versionMatches = _storedVersion is null
            || string.Equals(_storedVersion, _policyVersion, StringComparison.Ordinal);
        if (versionMatches == _decided) return;
        _decided = versionMatches;
        _ = SyncFoucGuardAsync();
        OnChange?.Invoke();
    }

    // Keep the html.lumeo-consent-decided class (set synchronously by index.html's
    // pre-boot guard for ANY stored entry; CSS force-hides .lumeo-consent-banner
    // while present) in sync with the live decision state. Fire-and-forget safe:
    // no-ops during prerender / when cookies are blocked.
    private async Task SyncFoucGuardAsync()
    {
        try
        {
            await _js.InvokeVoidAsync(_decided ? "lumeoConsent.markDecided" : "lumeoConsent.markUndecided");
        }
        catch (JSException) { }
        catch (InvalidOperationException) { }
    }

    private static Dictionary<string, bool> ReadBoolMap(JsonObject obj)
    {
        var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in obj)
        {
            if (value is JsonValue jv && jv.TryGetValue<bool>(out var b))
                map[key] = b;
        }
        return map;
    }

    private static string? ReadString(JsonNode? node)
        => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static DateTimeOffset? ParseTimestamp(JsonNode? node)
    {
        if (node is JsonValue v && v.TryGetValue<string>(out var s)
            && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var dto))
            return dto;
        return null;
    }

    /// <summary>Grant consent for every non-necessary category the consumer supplies, and persist.</summary>
    public Task AcceptAllAsync(IEnumerable<string> categories)
        => SetManyAsync(categories.ToDictionary(c => c, _ => true, StringComparer.OrdinalIgnoreCase));

    /// <summary>Deny consent for every non-necessary category and persist.</summary>
    public Task RejectAllAsync(IEnumerable<string> categories)
        => SetManyAsync(categories.ToDictionary(c => c, _ => false, StringComparer.OrdinalIgnoreCase));

    /// <summary>Commit a per-category consent map (used by the preferences dialog).</summary>
    public async Task SetManyAsync(IReadOnlyDictionary<string, bool> values)
    {
        foreach (var (k, v) in values)
        {
            if (string.Equals(k, "necessary", StringComparison.OrdinalIgnoreCase)) continue;
            _state[k] = v;
        }

        // The user acted — record the decision even if nothing was stored
        // (e.g. every category is Required), so HasDecided flips and the
        // banner dismisses for good.
        _decided = true;
        // A decision now exists (stamped with the current version by PersistAsync),
        // so a later PolicyVersion change re-evaluates it like a loaded one would.
        _hasStoredRecord = true;
        // A fresh write supersedes any rejected/malformed entry we hydrated over.
        _decisionRejected = false;
        await PersistAsync();
        OnChange?.Invoke();
    }

    /// <summary>Wipe stored consent — user is asked again on next visit. Useful for a "change preferences" link.</summary>
    public async Task ResetAsync()
    {
        _state.Clear();
        _decided = false;
        _decidedAtUtc = null;
        _storedVersion = null;
        // No stored decision remains — a later PolicyVersion change must not
        // re-derive a phantom "decided" from the cleared version fields.
        _hasStoredRecord = false;
        _decisionRejected = false;
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
            await _js.InvokeVoidAsync("lumeoConsent.markUndecided");
        }
        catch (JSException) { }
        catch (InvalidOperationException) { }
        OnChange?.Invoke();
    }

    /// <summary>Snapshot of the current consent map (copies are safe to iterate).</summary>
    public IReadOnlyDictionary<string, bool> Snapshot()
        => new Dictionary<string, bool>(_state, StringComparer.OrdinalIgnoreCase);

    private async Task PersistAsync()
    {
        try
        {
            // Stamp the proof-of-consent metadata as of this write and upgrade any
            // legacy record to the current record shape + configured policy version.
            _decidedAtUtc = DateTimeOffset.UtcNow;
            _storedVersion = PolicyVersion;

            var record = new ConsentRecord
            {
                Categories = _state,
                Timestamp = _decidedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture),
                Version = PolicyVersion,
            };
            var json = JsonSerializer.Serialize(record);
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
            // Mirror the FOUC-guard class so a subsequent runtime change keeps it accurate.
            await _js.InvokeVoidAsync("lumeoConsent.markDecided");
        }
        catch (JSException) { }
        catch (InvalidOperationException) { }
    }

    // Persisted proof-of-consent record. Serialized to localStorage under StorageKey.
    private sealed class ConsentRecord
    {
        [JsonPropertyName("categories")] public Dictionary<string, bool> Categories { get; set; } = new();
        [JsonPropertyName("timestamp")] public string Timestamp { get; set; } = "";
        [JsonPropertyName("version")] public string Version { get; set; } = "";
    }
}

/// <summary>
/// One consent bucket the banner presents. <c>Key</c> must be stable (used as
/// a persistence key); <c>Required</c> = true means the toggle is locked on
/// (e.g. strictly necessary cookies for site function).
/// </summary>
/// <param name="Key">Machine-readable identifier, e.g. "analytics", "marketing".</param>
/// <param name="Title">Short label shown in the preferences dialog.</param>
/// <param name="Description">One-sentence explanation for the end user.</param>
/// <param name="Required">Locked on — true for strictly necessary cookies.</param>
public sealed record ConsentCategory(string Key, string Title, string Description, bool Required = false);
