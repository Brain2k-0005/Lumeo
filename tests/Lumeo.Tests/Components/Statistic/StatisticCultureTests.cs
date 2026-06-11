using System.Globalization;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Statistic;

/// <summary>
/// Regression matrix for the Precision parse path. The old order
/// (invariant FIRST with NumberStyles.Any) read de-DE "1234,5" as 12345 —
/// the ',' was consumed as an invariant *group* separator, rendering a
/// 10×-wrong value. New order: invariant-exact only for single-'.'/no-','
/// strings, then EffectiveCulture, then invariant fallback.
/// </summary>
public class StatisticCultureTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public StatisticCultureTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    private string RenderValue(string value, int precision, CultureInfo? culture)
    {
        var cut = _ctx.Render<Lumeo.Statistic>(p =>
        {
            p.Add(s => s.Value, value);
            p.Add(s => s.Precision, precision);
            if (culture is not null) p.Add(s => s.Culture, culture);
        });
        // The value span is the (only) text-2xl element.
        return cut.Find("span.text-2xl").TextContent;
    }

    [Fact]
    public void German_Comma_Decimal_Parses_As_Decimal_Not_Thousands()
    {
        // Was 12345 → "12.345,0" before the fix.
        Assert.Equal("1.234,5", RenderValue("1234,5", 1, De));
    }

    [Fact]
    public void Invariant_Dot_Decimal_Wins_Under_German_Culture()
    {
        // Documented trade-off: exactly one '.' and no ',' parses invariant-first,
        // so "1234.5" stays 1234.5 under de-DE rather than de-DE's grouping (12345).
        Assert.Equal("1.234,5", RenderValue("1234.5", 1, De));
    }

    [Fact]
    public void German_Grouped_Decimal_Parses_With_Culture()
    {
        Assert.Equal("1.234,56", RenderValue("1.234,56", 2, De));
    }

    [Fact]
    public void EnUs_Dot_Decimal_Parses()
    {
        Assert.Equal("1,234.5", RenderValue("1234.5", 1, Us));
    }

    [Fact]
    public void EnUs_Grouped_Decimal_Parses()
    {
        Assert.Equal("1,234.5", RenderValue("1,234.5", 1, Us));
    }

    [Fact]
    public void German_Comma_Decimal_Parses_Via_CurrentCulture_Fallback()
    {
        // No Culture parameter — EffectiveCulture falls back to CurrentCulture.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = De;
            Assert.Equal("1.234,5", RenderValue("1234,5", 1, culture: null));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Non_Numeric_Value_Renders_Verbatim()
    {
        Assert.Equal("N/A", RenderValue("N/A", 2, De));
    }
}
