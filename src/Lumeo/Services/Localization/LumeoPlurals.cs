using System.Globalization;

namespace Lumeo.Services.Localization;

/// <summary>
/// Public façade for plural-key selection — exposes the rc.44 plural helper from
/// <see cref="LumeoLocalizer"/> (which is internal) so satellite projects like
/// <c>Lumeo.DataGrid</c> can resolve plural-aware translation keys without
/// reflecting through internals.
///
/// Usage:
/// <code>
/// var key = LumeoPlurals.PluralKey(count, "DataGrid.ItemsCount", CultureInfo.CurrentUICulture);
/// var text = L[key, count];
/// </code>
/// </summary>
public static class LumeoPlurals
{
    /// <summary>
    /// Returns the locale-appropriate plural sub-key (e.g. <c>"DataGrid.ItemsCount.One"</c>)
    /// for the given count and culture. See <see cref="LumeoLocalizer.PluralKey"/>
    /// for the rule documentation — this method delegates to it.
    /// </summary>
    public static string PluralKey(int count, string baseKey, CultureInfo culture)
        => LumeoLocalizer.PluralKey(count, baseKey, culture);
}
