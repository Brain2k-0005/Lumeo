using Microsoft.JSInterop;
using System.Text.Json;

namespace Lumeo.Services;

/// <summary>
/// Cookie / tracking consent manager. Persists the user's per-category choices
/// in <c>localStorage</c> so the banner doesn't re-appear on every page load,
/// and exposes a reactive API so consumers can gate scripts / analytics on it.
///
/// Typical wiring:
/// <code>
/// // Program.cs
/// builder.Services.AddLumeo();           // includes ConsentService
///
/// // MainLayout.razor
/// &lt;ConsentBanner PrivacyPolicyUrl="/privacy" /&gt;
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
    /// True once the user has answered the banner (accepted, rejected, or
    /// customized). While false, the banner should be visible.
    /// </summary>
    public bool HasDecided => _state.Count > 0;

    /// <summary>
    /// Has the user granted consent for this category? Unknown categories
    /// return false (fail-closed). "necessary" always returns true.
    /// </summary>
    public bool HasConsent(string category)
    {
        if (string.Equals(category, "necessary", StringComparison.OrdinalIgnoreCase))
            return true;

        return _state.TryGetValue(category, out var v) && v;
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

            var parsed = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
            if (parsed is not null)
            {
                _state = new Dictionary<string, bool>(parsed, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (JSException) { /* localStorage blocked (cookies disabled) — treat as no decision yet */ }
        catch (JsonException) { /* corrupt entry — ignore */ }
        catch (InvalidOperationException) { /* prerendering, IJSRuntime not ready yet */ }
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

        await PersistAsync();
        OnChange?.Invoke();
    }

    /// <summary>Wipe stored consent — user is asked again on next visit. Useful for a "change preferences" link.</summary>
    public async Task ResetAsync()
    {
        _state.Clear();
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
            var json = JsonSerializer.Serialize(_state);
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
            // Mirror the FOUC-guard class so a subsequent runtime change keeps it accurate.
            await _js.InvokeVoidAsync("lumeoConsent.markDecided");
        }
        catch (JSException) { }
        catch (InvalidOperationException) { }
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
