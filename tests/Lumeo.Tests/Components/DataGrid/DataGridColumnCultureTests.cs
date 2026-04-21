using System.Globalization;
using Xunit;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Unit-level tests for <see cref="DataGridColumn{TItem}.GetFormattedValue(TItem, CultureInfo)"/>.
/// Asserts that numeric and date formatting honours the supplied culture, including the
/// parameterless overload defaulting to <see cref="CultureInfo.CurrentCulture"/>.
/// </summary>
public class DataGridColumnCultureTests
{
    private record Row(decimal Amount, DateTime When, string Note);

    [Fact]
    public void GetFormattedValue_With_German_Culture_Uses_Comma_Decimal()
    {
        var col = new DataGridColumn<Row> { Field = "Amount", Format = "N2" };
        var row = new Row(1234.56m, DateTime.MinValue, "");
        var formatted = col.GetFormattedValue(row, CultureInfo.GetCultureInfo("de-DE"));
        Assert.Equal("1.234,56", formatted);
    }

    [Fact]
    public void GetFormattedValue_With_US_Culture_Uses_Period_Decimal()
    {
        var col = new DataGridColumn<Row> { Field = "Amount", Format = "N2" };
        var row = new Row(1234.56m, DateTime.MinValue, "");
        var formatted = col.GetFormattedValue(row, CultureInfo.GetCultureInfo("en-US"));
        Assert.Equal("1,234.56", formatted);
    }

    [Fact]
    public void GetFormattedValue_Date_With_German_ShortDate_Uses_Dotted_Format()
    {
        var col = new DataGridColumn<Row> { Field = "When", Format = "d" };
        var row = new Row(0, new DateTime(2026, 3, 15), "");
        var formatted = col.GetFormattedValue(row, CultureInfo.GetCultureInfo("de-DE"));
        Assert.Equal("15.03.2026", formatted);
    }

    [Fact]
    public void GetFormattedValue_NonFormattable_Returns_ToString()
    {
        var col = new DataGridColumn<Row> { Field = "Note" };
        var row = new Row(0, DateTime.MinValue, "hello");
        var formatted = col.GetFormattedValue(row, CultureInfo.GetCultureInfo("de-DE"));
        Assert.Equal("hello", formatted);
    }

    [Fact]
    public void GetFormattedValue_Null_Value_Returns_Empty()
    {
        var col = new DataGridColumn<Row> { FieldSelector = _ => null };
        var row = new Row(0, DateTime.MinValue, "");
        var formatted = col.GetFormattedValue(row, CultureInfo.InvariantCulture);
        Assert.Equal("", formatted);
    }

    [Fact]
    public void GetFormattedValue_Parameterless_Overload_Uses_CurrentCulture()
    {
        var col = new DataGridColumn<Row> { Field = "Amount", Format = "N2" };
        var row = new Row(1234.56m, DateTime.MinValue, "");
        var expected = 1234.56m.ToString("N2", CultureInfo.CurrentCulture);
        Assert.Equal(expected, col.GetFormattedValue(row));
    }
}
