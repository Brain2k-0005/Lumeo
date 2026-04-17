using System.Globalization;
using Microsoft.Extensions.Options;

namespace Lumeo.Services.Localization;

internal sealed class LumeoLocalizer : ILumeoLocalizer
{
    private readonly LumeoLocalizationOptions _options;

    public LumeoLocalizer(IOptions<LumeoLocalizationOptions> options)
    {
        _options = options.Value;
    }

    public string this[string key] => TryGet(key, out var v) ? v : key;

    public string this[string key, params object?[] args]
    {
        get
        {
            var template = TryGet(key, out var v) ? v : key;
            try
            {
                return string.Format(CultureInfo.CurrentCulture, template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }
    }

    public bool TryGet(string key, out string value)
    {
        var culture = CultureInfo.CurrentUICulture;

        // 1. Exact culture match (e.g. "de-DE")
        if (LookupIn(culture.Name, key, out value)) return true;

        // 2. Neutral / parent culture (e.g. "de")
        var parent = culture.Parent;
        while (!string.IsNullOrEmpty(parent?.Name))
        {
            if (LookupIn(parent.Name, key, out value)) return true;
            parent = parent.Parent;
        }

        // 3. English fallback
        if (!culture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            && LookupIn("en", key, out value)) return true;

        value = key;
        return false;
    }

    private bool LookupIn(string culture, string key, out string value)
    {
        if (_options.Translations.TryGetValue(culture, out var bucket)
            && bucket.TryGetValue(key, out var translated))
        {
            value = translated;
            return true;
        }
        value = string.Empty;
        return false;
    }
}
