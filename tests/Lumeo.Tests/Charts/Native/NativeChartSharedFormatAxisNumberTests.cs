using System.Globalization;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native;

/// <summary>
/// Covers the "thousands separators are absent on native axes" judgment call:
/// axis TICK labels (unlike SVG coordinate/geometry formatting, which stays
/// Invariant on purpose) now go through <c>NativeChartShared.FormatAxisNumber</c>,
/// which uses <c>CultureInfo.CurrentCulture</c> plus a grouping separator
/// (<c>#,##0.##</c>) instead of the previous bare Invariant <c>0.##</c>. The
/// library ships 14 locales, so this is asserted against more than one culture —
/// a fix that "worked" only in en-US (i.e. still secretly hardcoded) would fail
/// the de-DE/fr-FR cases below.
/// </summary>
public class NativeChartSharedFormatAxisNumberTests
{
    [Fact]
    public void EnUS_Uses_Comma_Grouping_And_Dot_Decimal()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal("12,345.6", L.NativeChartShared.FormatAxisNumber(12345.6));
            Assert.Equal("1,000", L.NativeChartShared.FormatAxisNumber(1000));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void DeDE_Uses_Dot_Grouping_And_Comma_Decimal()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal("12.345,6", L.NativeChartShared.FormatAxisNumber(12345.6));
            Assert.Equal("1.000", L.NativeChartShared.FormatAxisNumber(1000));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void FrFR_Uses_A_NonBreaking_Space_Group_Separator()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var formatted = L.NativeChartShared.FormatAxisNumber(12345.6);
            // fr-FR's group separator is U+202F (narrow no-break space) or U+00A0
            // depending on ICU/NLS data version — assert on the digit grouping
            // itself rather than pinning one exact separator codepoint.
            Assert.Contains("12", formatted);
            Assert.Contains("345", formatted);
            Assert.Contains(",6", formatted); // fr-FR decimal separator is a comma
            Assert.NotEqual("12345,6", formatted); // must be GROUPED, not just decimal-localized
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void Small_Values_Under_A_Thousand_Are_Unaffected()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal("42", L.NativeChartShared.FormatAxisNumber(42));
            Assert.Equal("-3.5", L.NativeChartShared.FormatAxisNumber(-3.5));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }
}
