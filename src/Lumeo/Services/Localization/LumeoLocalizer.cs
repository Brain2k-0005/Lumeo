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

    /// <summary>
    /// Map an integer count to the appropriate plural-form sub-key for the given
    /// culture. Returns one of <c>{baseKey}.One</c>, <c>{baseKey}.Few</c>,
    /// <c>{baseKey}.Many</c> or <c>{baseKey}.Other</c>.
    /// </summary>
    /// <remarks>
    /// This is a pragmatic CLDR-lite implementation — full CLDR plural rules are
    /// not available in .NET without an extra dependency. We cover the three
    /// language families that actually differ from the EN/DE binary model:
    /// <list type="bullet">
    ///   <item>EN, DE, ES, FR, IT, PT, NL, TR, JA, ZH, KO and most other cultures:
    ///         <c>count == 1 ? One : Other</c>.</item>
    ///   <item>RU (and other East-Slavic): <c>1, 21, 31…</c> → One;
    ///         <c>2–4, 22–24…</c> → Few; <c>0, 5–20, 25–30…</c> → Many.</item>
    ///   <item>PL: <c>1</c> → One; <c>2–4 (excl. 12–14)</c> → Few;
    ///         <c>0, 5+, 12–14</c> → Many.</item>
    ///   <item>AR: <c>0</c> → Zero (collapsed to Many here); <c>1</c> → One;
    ///         <c>2</c> → Two (collapsed to Few); <c>3–10</c> → Few; rest → Many.</item>
    /// </list>
    /// Callers should provide <c>.One</c> and <c>.Other</c> in every locale and
    /// <c>.Few</c> / <c>.Many</c> for RU/PL/AR. If a specific sub-key is missing
    /// the regular fallback chain in <see cref="TryGet"/> will land on <c>.Other</c>
    /// or the EN dictionary.
    /// </remarks>
    public static string PluralKey(int count, string baseKey, CultureInfo culture)
    {
        var n = Math.Abs(count);
        var lang = culture.TwoLetterISOLanguageName;

        // East-Slavic family — Russian / Ukrainian / Belarusian / Serbian / Croatian etc.
        // Use RU rules as the conservative default for the rest of the family.
        if (lang is "ru" or "uk" or "be" or "sr" or "hr" or "bs")
        {
            var mod10 = n % 10;
            var mod100 = n % 100;
            if (mod10 == 1 && mod100 != 11) return $"{baseKey}.One";
            if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return $"{baseKey}.Few";
            return $"{baseKey}.Many";
        }

        // Polish — similar to RU but "1" is its own form (One), 2-4 is Few, rest Many.
        if (lang == "pl")
        {
            if (n == 1) return $"{baseKey}.One";
            var mod10 = n % 10;
            var mod100 = n % 100;
            if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return $"{baseKey}.Few";
            return $"{baseKey}.Many";
        }

        // Arabic — six CLDR forms (zero/one/two/few/many/other). We collapse zero→Many
        // and two→Few because the four-key surface keeps consumer dicts manageable.
        if (lang == "ar")
        {
            if (n == 0) return $"{baseKey}.Many";
            if (n == 1) return $"{baseKey}.One";
            if (n == 2) return $"{baseKey}.Few";
            var mod100 = n % 100;
            if (mod100 >= 3 && mod100 <= 10) return $"{baseKey}.Few";
            if (mod100 >= 11 && mod100 <= 99) return $"{baseKey}.Many";
            return $"{baseKey}.Other";
        }

        // Default (EN/DE/ES/FR/IT/PT/NL/TR/JA/ZH/KO/…): binary One / Other.
        return n == 1 ? $"{baseKey}.One" : $"{baseKey}.Other";
    }
}
