using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native.Core;

/// <summary>Covers <see cref="L.NativeChartAccessibilityJson"/> — proves the
/// generic JSON shape it emits round-trips correctly through the REUSED (not
/// reimplemented) legacy <see cref="L.ChartAccessibility.Build"/>, per the
/// task's explicit instruction to reuse that class as-is.</summary>
public class NativeChartAccessibilityJsonTests
{
    [Fact]
    public void Cartesian_Json_Produces_A_Table_With_One_Row_Per_Category()
    {
        var categories = new List<string> { "Jan", "Feb", "Mar" };
        var series = new List<L.NativeCartesianSeries>
        {
            new() { Name = "Revenue", Kind = L.NativeCartesianSeriesKind.Bar, Values = new double?[] { 10, 20, 30 } },
        };

        var json = L.NativeChartAccessibilityJson.BuildCartesian(categories, series, "bar");
        var table = L.ChartAccessibility.Build(json);

        Assert.NotNull(table);
        Assert.Equal(3, table!.Rows.Count);
        Assert.Equal("Jan", table.Rows[0].Header);
        Assert.Equal("10", table.Rows[0].Cells[0]);
        Assert.Contains("Bar chart", table.Summary);
    }

    [Fact]
    public void Series_With_IncludeInTooltip_False_Is_Excluded()
    {
        // Waterfall's invisible "floor" series must not pollute the SR table.
        var categories = new List<string> { "A" };
        var series = new List<L.NativeCartesianSeries>
        {
            new() { Name = "floor", Kind = L.NativeCartesianSeriesKind.Bar, Values = new double?[] { 5 }, IncludeInTooltip = false },
            new() { Name = "delta", Kind = L.NativeCartesianSeriesKind.Bar, Values = new double?[] { 3 }, IncludeInTooltip = true },
        };

        var json = L.NativeChartAccessibilityJson.BuildCartesian(categories, series, "bar");
        var table = L.ChartAccessibility.Build(json);

        Assert.NotNull(table);
        Assert.Single(table!.ColumnHeaders.Skip(1)); // one data series column, not two
        Assert.DoesNotContain("floor", table.Summary);
    }

    [Fact]
    public void Xy_Json_Produces_A_NameValue_Table_For_A_Single_Series()
    {
        var series = new List<(string Name, IReadOnlyList<(double X, double Y)> Points)>
        {
            ("S1", new List<(double, double)> { (1, 2), (3, 4) }),
        };

        var json = L.NativeChartAccessibilityJson.BuildXy(series, "scatter");
        var table = L.ChartAccessibility.Build(json);

        Assert.NotNull(table);
        Assert.Equal(2, table!.Rows.Count);
        Assert.Contains("Scatter chart", table.Summary);
    }
}
