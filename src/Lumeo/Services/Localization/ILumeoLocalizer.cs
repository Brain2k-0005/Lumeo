namespace Lumeo.Services.Localization;

/// <summary>
/// Culture-aware string provider for Lumeo component UI text.
/// Resolves keys against <see cref="LumeoLocalizationOptions"/> using
/// <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>, falling back
/// through neutral culture → English → the key itself.
/// </summary>
public interface ILumeoLocalizer
{
    /// <summary>Look up a translation by key.</summary>
    string this[string key] { get; }

    /// <summary>Look up a translation by key and apply string.Format with the given arguments.</summary>
    string this[string key, params object?[] args] { get; }

    /// <summary>Try to resolve a translation; returns false if the key is unknown.</summary>
    bool TryGet(string key, out string value);
}
