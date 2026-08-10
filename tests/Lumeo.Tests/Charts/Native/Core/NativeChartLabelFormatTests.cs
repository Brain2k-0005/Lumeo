using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native.Core;

public class NativeChartLabelFormatTests
{
    [Fact]
    public void Null_Template_Returns_Plain_Number()
    {
        Assert.Equal("42.5", L.NativeChartLabelFormat.Format(42.5, null));
    }

    [Fact]
    public void Template_Substitutes_The_C_Token()
    {
        Assert.Equal("42.5%", L.NativeChartLabelFormat.Format(42.5, "{c}%"));
    }

    [Fact]
    public void Rounds_To_Two_Decimals()
    {
        Assert.Equal("1.23", L.NativeChartLabelFormat.Format(1.2345, null));
    }

    [Fact]
    public void Uses_Invariant_Culture_Decimal_Point_Not_Comma()
    {
        // A concrete disable-check: without InvariantCulture this would render "1,5"
        // on a comma-decimal locale, which is not valid inside an SVG/XML attribute.
        Assert.DoesNotContain(",", L.NativeChartLabelFormat.Format(1.5, null));
        Assert.Contains(".", L.NativeChartLabelFormat.Format(1.5, null));
    }
}
