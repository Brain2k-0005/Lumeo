namespace Lumeo.Services.Localization;

/// <summary>
/// Holds translation strings for Lumeo components, keyed by culture name
/// (e.g. "en", "en-US", "de", "de-DE") and then by string key.
/// </summary>
public sealed class LumeoLocalizationOptions
{
    /// <summary>
    /// Culture name → (string key → translated text).
    /// Lookup order at runtime: current UI culture (e.g. "de-DE") → neutral ("de") → "en" → key.
    /// </summary>
    public IDictionary<string, IDictionary<string, string>> Translations { get; } =
        new Dictionary<string, IDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Convenience: add or update a single translation.
    /// </summary>
    public LumeoLocalizationOptions Add(string culture, string key, string value)
    {
        if (!Translations.TryGetValue(culture, out var bucket))
        {
            bucket = new Dictionary<string, string>(StringComparer.Ordinal);
            Translations[culture] = bucket;
        }
        bucket[key] = value;
        return this;
    }

    /// <summary>
    /// Convenience: add or update many keys for one culture in a single call.
    /// </summary>
    public LumeoLocalizationOptions AddMany(string culture, IDictionary<string, string> entries)
    {
        foreach (var kv in entries) Add(culture, kv.Key, kv.Value);
        return this;
    }
}
