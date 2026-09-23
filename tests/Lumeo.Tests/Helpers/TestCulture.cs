using System.Globalization;
using System.Runtime.CompilerServices;

namespace Lumeo.Tests.Helpers;

/// <summary>
/// Runs the suite in the culture CI runs it in (en-US), whatever the developer's machine says.
/// Hundreds of tests assert a localized default string or a culture-formatted date ("No results",
/// "Increase", the day abbreviations in Calendar); on an nl_NL or de_DE machine
/// <c>LumeoLocalizer</c> and <c>DateTime.ToString</c> answer in that language and 242 tests fail
/// without any defect in a component. Tests that deliberately switch culture
/// (<c>CultureSwitchReRenderTests</c>, the Skeleton tests) set and restore it themselves and
/// are unaffected.
/// </summary>
internal static class TestCulture
{
    [ModuleInitializer]
    internal static void PinToEnglish()
    {
        var en = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = en;
        CultureInfo.DefaultThreadCurrentUICulture = en;
        CultureInfo.CurrentCulture = en;
        CultureInfo.CurrentUICulture = en;
    }
}
